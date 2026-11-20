using System.Diagnostics;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

// Shared by UC-03 (one item) and UC-06 (several items).
// The rules are checked and the writes are made inside ONE database transaction,
// so a checkout either saves every borrowing and availability change, or none (BR-15).
public class CheckoutTransaction
{
    public const int MaxActiveBorrowings = 3; // BR-09

    private readonly IBorrowingUnitOfWorkFactory _factory;
    private readonly ICheckoutFaultInjector _faults;
    private readonly IClock _clock;

    // Lab 5: the clock is optional so existing callers still work. Without one, the real local date is used.
    public CheckoutTransaction(IBorrowingUnitOfWorkFactory factory, ICheckoutFaultInjector faults, IClock? clock = null)
    {
        _factory = factory;
        _faults = faults;
        _clock = clock ?? new SystemClock();
    }

    public async Task<OperationResult> ExecuteAsync(
        int studentId,
        IReadOnlyList<int> equipmentIds,
        DateTime expectedReturnDate,
        CancellationToken cancellationToken = default)
    {
        var checkoutDate = _clock.Today; // local calendar date (BR-11)

        // ---- Checks that need no database ----
        if (equipmentIds.Count == 0)
            return OperationResult.Rejected("Select at least one item to check out.");
        if (equipmentIds.Distinct().Count() != equipmentIds.Count)
            return OperationResult.Rejected("The same item was selected more than once. Nothing was checked out.");
        if (expectedReturnDate.Date <= checkoutDate)
            return OperationResult.Rejected("The expected return date must be later than today's checkout date.");
        if (equipmentIds.Count > MaxActiveBorrowings)
            return OperationResult.Rejected(
                $"A student can have at most {MaxActiveBorrowings} active items, so no more than {MaxActiveBorrowings} items can be checked out at once.");

        await using var uow = _factory.Create();
        Student student;
        IReadOnlyList<Equipment> items;

        try
        {
            await uow.BeginAsync(cancellationToken);

            // ---- Revalidate CURRENT data inside the transaction (BR-08, BR-15) ----
            var foundStudent = await uow.GetStudentForUpdateAsync(studentId, cancellationToken);
            if (foundStudent is null)
                return OperationResult.Rejected("This student no longer exists. Refresh the list and try again.");
            student = foundStudent;

            if (!student.IsAllowedToBorrow)
                return OperationResult.Rejected($"{student.Name} is not allowed to borrow right now. Nothing was checked out.");

            items = await uow.GetEquipmentForUpdateAsync(equipmentIds, cancellationToken);
            if (items.Count != equipmentIds.Count)
                return OperationResult.Rejected("One or more selected items no longer exist. Refresh the list and try again. Nothing was checked out.");

            // BR-10: an item is borrowable only if available AND without an active borrowing
            var busyIds = (await uow.GetEquipmentIdsWithActiveBorrowingAsync(equipmentIds, cancellationToken)).ToHashSet();
            var notBorrowable = items
                .Where(i => !i.IsAvailable || busyIds.Contains(i.Id))
                .Select(i => $"{i.Identifier} ({i.Name})")
                .ToList();
            if (notBorrowable.Count > 0)
                return OperationResult.Rejected(
                    $"Not available: {string.Join(", ", notBorrowable)}. Nothing was checked out.");

            // BR-09: existing active records + requested items must not exceed the limit
            var activeCount = await uow.CountActiveBorrowingsAsync(studentId, cancellationToken);
            if (activeCount + items.Count > MaxActiveBorrowings)
                return OperationResult.Rejected(
                    $"{student.Name} already has {activeCount} active item(s) and the limit is {MaxActiveBorrowings}. " +
                    $"Checking out {items.Count} more would exceed it. Nothing was checked out.");

            // ---- Writes: one active borrowing per item, plus the availability change ----
            var written = 0;
            foreach (var item in items)
            {
                uow.AddBorrowing(new Borrowing(studentId, item.Id, checkoutDate, expectedReturnDate));
                item.MarkAsBorrowed();

                await uow.SaveChangesAsync(cancellationToken); // sent inside the open transaction
                written++;

                await _faults.AfterWriteAsync(written, items.Count, cancellationToken); // test seam
            }
        }
        catch (ConflictException ex)
        {
            // Someone else changed the data after our checks (the database caught it).
            return OperationResult.Rejected($"{ex.Message} Nothing was checked out. Refresh the lists and try again.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // A known failure BEFORE commit: the transaction is rolled back when it is disposed.
            Debug.WriteLine($"Checkout failed before commit: {ex.GetType().Name}");
            return OperationResult.Failed(
                "The checkout could not be saved, so no changes were made. " +
                "Check the database connection, refresh the lists, then try again.");
        }

        // ---- Commit. A failure HERE means the outcome is unknown (BR-18). ----
        try
        {
            await uow.CommitAsync(cancellationToken);
            await _faults.AfterCommitAsync(cancellationToken); // test seam: imitates a lost connection
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Checkout commit outcome unknown: {ex.GetType().Name}");
            return OperationResult.UnknownOutcome();
        }

        var names = string.Join(", ", items.Select(i => i.Identifier));
        return OperationResult.Success(
            $"{student.Name} checked out {items.Count} item(s) ({names}), due {expectedReturnDate.Date:MM/dd/yyyy}.");
    }
}
