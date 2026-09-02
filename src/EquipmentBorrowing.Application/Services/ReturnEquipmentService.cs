using EquipmentBorrowing.Application.Interfaces;

namespace EquipmentBorrowing.Application.Services;

public class ReturnEquipmentService
{
    private readonly IBorrowingRepository _borrowingRepository;
    private readonly IEquipmentRepository _equipmentRepository;

    public ReturnEquipmentService(
        IBorrowingRepository borrowingRepository,
        IEquipmentRepository equipmentRepository)
    {
        _borrowingRepository = borrowingRepository;
        _equipmentRepository = equipmentRepository;
    }

    public async Task<string> ExecuteAsync(int borrowingId, CancellationToken cancellationToken = default)
    {
        var borrowing = await _borrowingRepository.GetByIdAsync(borrowingId, cancellationToken);
        if (borrowing == null)
        {
            return "Failure: Borrowing record not found.";
        }

        if (borrowing.Status == Domain.BorrowingStatus.Returned)
        {
            return "Failure: This borrowing has already been returned.";
        }

        var equipment = await _equipmentRepository.GetByIdAsync(borrowing.EquipmentId, cancellationToken);
        if (equipment == null)
        {
            return "Failure: Associated equipment not found.";
        }

        borrowing.MarkAsReturned();
        await _borrowingRepository.UpdateAsync(borrowing, cancellationToken);

        equipment.MarkAsReturned();
        await _equipmentRepository.UpdateAsync(equipment, cancellationToken);

        return $"Success: Equipment '{equipment.Name}' has been returned.";
    }
}