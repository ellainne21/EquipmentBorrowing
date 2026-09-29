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

    public async Task AddAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        await _context.Borrowings.AddAsync(borrowing, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetActiveBorrowingsCountByStudentIdAsync(int studentId, CancellationToken cancellationToken = default)
    {
        // Query 3: aggregate count of active borrowings for one student.
        return await _context.Borrowings
            .CountAsync(b => b.StudentId == studentId && b.Status == BorrowingStatus.Active, cancellationToken);
    }

    public async Task<IEnumerable<ActiveBorrowingDetails>> GetActiveBorrowingsAsync(CancellationToken cancellationToken = default)
    {
        // Query 2: joins Borrowings with Students and Equipment so real
        // names are shown instead of raw IDs. Read-only, so AsNoTracking().
        return await (
            from b in _context.Borrowings.AsNoTracking()
            join s in _context.Students.AsNoTracking() on b.StudentId equals s.Id
            join e in _context.Equipment.AsNoTracking() on b.EquipmentId equals e.Id
            where b.Status == BorrowingStatus.Active
            select new ActiveBorrowingDetails
            {
                BorrowingId = b.Id,
                StudentName = s.Name,
                EquipmentName = e.Name,
                DateBorrowed = b.DateBorrowed,
                ExpectedReturnDate = b.ExpectedReturnDate
            }
        ).ToListAsync(cancellationToken);
    }

    public async Task<Borrowing?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // Tracking ON: the return workflow changes this row and saves it.
        return await _context.Borrowings
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        _context.Borrowings.Update(borrowing);
        await _context.SaveChangesAsync(cancellationToken);
    }
}