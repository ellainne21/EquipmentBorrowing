using EquipmentBorrowing.Application.Models;

namespace EquipmentBorrowing.Application.Services;

// UC-03: issue ONE item to a student.
public class BorrowEquipmentService
{
    private readonly CheckoutTransaction _checkout;

    public BorrowEquipmentService(CheckoutTransaction checkout)
    {
        _checkout = checkout;
    }

    public Task<OperationResult> ExecuteAsync(
        int studentId,
        int equipmentId,
        DateTime expectedReturnDate,
        CancellationToken cancellationToken = default)
    {
        return _checkout.ExecuteAsync(studentId, new[] { equipmentId }, expectedReturnDate, cancellationToken);
    }
}
