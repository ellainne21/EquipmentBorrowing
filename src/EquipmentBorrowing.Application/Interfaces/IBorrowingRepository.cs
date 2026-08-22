using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Interfaces;

public interface IBorrowingRepository
{
    Task AddAsync(Borrowing borrowing, CancellationToken cancellationToken = default);
    Task<int> GetActiveBorrowingsCountByStudentIdAsync(int studentId, CancellationToken cancellationToken = default);
}