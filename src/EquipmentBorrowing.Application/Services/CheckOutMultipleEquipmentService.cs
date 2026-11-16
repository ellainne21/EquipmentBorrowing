using EquipmentBorrowing.Application.Models;

namespace EquipmentBorrowing.Application.Services;

// UC-06: issue SEVERAL distinct items to one student with one expected return date,
// all in one transaction (all items are checked out, or none).
public class CheckOutMultipleEquipmentService
{
    private readonly CheckoutTransaction _checkout;

    public CheckOutMultipleEquipmentService(CheckoutTransaction checkout)
    {
        _checkout = checkout;
    }

    public Task<OperationResult> ExecuteAsync(
        int studentId,
        IReadOnlyList<int> equipmentIds,
        DateTime expectedReturnDate,
        CancellationToken cancellationToken = default)
    {
        // BR-12: reject, never silently fix, a request that is not a real multi-item checkout
        if (equipmentIds.Count < 2)
            return Task.FromResult(OperationResult.Rejected(
                "A multi-item checkout needs at least two different items. Nothing was checked out."));

        if (equipmentIds.Distinct().Count() != equipmentIds.Count)
            return Task.FromResult(OperationResult.Rejected(
                "The same item was selected more than once. Remove the duplicate and try again. Nothing was checked out."));

        return _checkout.ExecuteAsync(studentId, equipmentIds, expectedReturnDate, cancellationToken);
    }
}
