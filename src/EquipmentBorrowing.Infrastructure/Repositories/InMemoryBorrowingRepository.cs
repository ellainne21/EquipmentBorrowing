using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class InMemoryBorrowingRepository : IBorrowingRepository
{
    private readonly List<Borrowing> _borrowings = new();

    public Task AddAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        _borrowings.Add(borrowing);
        return Task.CompletedTask;
    }

    public Task<int> GetActiveBorrowingsCountByStudentIdAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var count = _borrowings.Count(b => b.StudentId == studentId && b.Status == BorrowingStatus.Active);
        return Task.FromResult(count);
    }

    public Task<IEnumerable<ActiveBorrowingDetails>> GetActiveBorrowingsAsync(CancellationToken cancellationToken = default)
    {
        var active = _borrowings
            .Where(b => b.Status == BorrowingStatus.Active)
            .Select(b => new ActiveBorrowingDetails
            {
                BorrowingId = b.Id,
                StudentName = $"Student #{b.StudentId}",
                EquipmentName = $"Equipment #{b.EquipmentId}",
                DateBorrowed = b.DateBorrowed,
                ExpectedReturnDate = b.ExpectedReturnDate
            });
        return Task.FromResult<IEnumerable<ActiveBorrowingDetails>>(active);
    }

    public Task<Borrowing?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var borrowing = _borrowings.FirstOrDefault(b => b.Id == id);
        return Task.FromResult(borrowing);
    }

    public Task UpdateAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        // In-memory: object is already updated, nothing to do
        return Task.CompletedTask;
    }
}