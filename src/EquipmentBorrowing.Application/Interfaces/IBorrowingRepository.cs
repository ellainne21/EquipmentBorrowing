using EquipmentBorrowing.Application.Models;

namespace EquipmentBorrowing.Application.Interfaces;

public interface IBorrowingRepository
{
    Task<int> GetActiveBorrowingsCountByStudentIdAsync(int studentId, CancellationToken cancellationToken = default);

    Task<IEnumerable<ActiveBorrowingDetails>> GetActiveBorrowingsAsync(string? searchText, CancellationToken cancellationToken = default);
}