using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class EfBorrowingRepository : IBorrowingRepository
{
    private readonly AppDbContext _context;

    public EfBorrowingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetActiveBorrowingsCountByStudentIdAsync(int studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Borrowings
            .CountAsync(b => b.StudentId == studentId && b.Status == BorrowingStatus.Active, cancellationToken);
    }

    public async Task<IEnumerable<ActiveBorrowingDetails>> GetActiveBorrowingsAsync(string? searchText, CancellationToken cancellationToken = default)
    {
        // 1. Base query with all required fields (Read-only, so AsNoTracking)
        var query = (
            from b in _context.Borrowings.AsNoTracking()
            join s in _context.Students.AsNoTracking() on b.StudentId equals s.Id
            join e in _context.Equipment.AsNoTracking() on b.EquipmentId equals e.Id
            where b.Status == BorrowingStatus.Active
            select new ActiveBorrowingDetails
            {
                BorrowingId = b.Id,
                StudentNumber = s.StudentNumber,
                StudentName = s.Name,
                EquipmentIdentifier = e.Identifier,
                EquipmentName = e.Name,
                DateBorrowed = b.DateBorrowed,
                ExpectedReturnDate = b.ExpectedReturnDate
            }
        );

        // 2. Database-side filtering (if searchText is provided)
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim().ToLowerInvariant();
            query = query.Where(b =>
                b.StudentNumber.ToLower().Contains(term) ||
                b.StudentName.ToLower().Contains(term) ||
                b.EquipmentIdentifier.ToLower().Contains(term) ||
                b.EquipmentName.ToLower().Contains(term)
            );
        }

        // 3. Database-side sorting (BR-17: Id tie-breaker for stable order)
        return await query
            .OrderBy(b => b.ExpectedReturnDate)
            .ThenBy(b => b.BorrowingId)
            .ToListAsync(cancellationToken);
    }
}