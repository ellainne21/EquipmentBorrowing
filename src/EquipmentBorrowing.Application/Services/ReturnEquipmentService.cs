using System.Diagnostics;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

// UC-05: return an active borrowing. The borrowing and the equipment change together in one transaction.
public class ReturnEquipmentService
{
    private readonly IBorrowingUnitOfWorkFactory _factory;
    private readonly ICheckoutFaultInjector _faults;

    public ReturnEquipmentService(IBorrowingUnitOfWorkFactory factory, ICheckoutFaultInjector faults)
    {
        _factory = factory;
        _faults = faults;
    }

    public async Task<OperationResult> ExecuteAsync(int borrowingId, CancellationToken cancellationToken = default)
    {
        var returnDate = DateTime.Today; // local calendar date (BR-13)

        await using var uow = _factory.Create();
        Equipment equipment;

        try
        {
            await uow.BeginAsync(cancellationToken);

            // Lock and re-read the CURRENT row, so two staff cannot return the same borrowing twice.
            var borrowing = await uow.GetBorrowingForUpdateAsync(borrowingId, cancellationToken);
            if (borrowing is null)
                return OperationResult.Rejected("This borrowing no longer exists. Refresh the list and try again.");

            if (borrowing.Status == BorrowingStatus.Returned)
                return OperationResult.Rejected("This borrowing was already returned. No change was made.");

            if (returnDate < borrowing.DateBorrowed.Date)
                return OperationResult.Rejected("The return date cannot be earlier than the checkout date. No change was made.");

            var items = await uow.GetEquipmentForUpdateAsync(new[] { borrowing.EquipmentId }, cancellationToken);
            if (items.Count == 0)
                return OperationResult.Rejected("The equipment for this borrowing could not be found. No change was made.");
            equipment = items[0];

            borrowing.MarkAsReturned(returnDate); // keeps the record and stores the actual return date
            equipment.MarkAsReturned();

            await uow.SaveChangesAsync(cancellationToken);
            await _faults.AfterWriteAsync(1, 1, cancellationToken); // test seam
        }
        catch (ConflictException ex)
        {
            return OperationResult.Rejected($"{ex.Message} No change was made. Refresh the list and try again.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Debug.WriteLine($"Return failed before commit: {ex.GetType().Name}");
            return OperationResult.Failed(
                "The return could not be saved, so no changes were made. " +
                "Check the database connection, refresh the list, then try again.");
        }

        try
        {
            await uow.CommitAsync(cancellationToken);
            await _faults.AfterCommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Return commit outcome unknown: {ex.GetType().Name}");
            return OperationResult.UnknownOutcome();
        }

        return OperationResult.Success(
            $"'{equipment.Name}' ({equipment.Identifier}) was returned on {returnDate:MM/dd/yyyy}.",
            borrowingId);
    }
}
