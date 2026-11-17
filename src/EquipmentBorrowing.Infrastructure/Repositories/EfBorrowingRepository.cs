using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Repositories;

// Read-only borrowing queries. Every query uses AsNoTracking, so no stale tracked data can be shown.
public class EfBorrowingRepository : IBorrowingRepository
{
    private readonly AppDbContext _context;

    public EfBorrowingRepository(AppDbContext context)
    {
        _context = context;
    }

    // UC-04: the active list is the same query as the search, fixed to "active" and sorted by due date.
    public async Task<IEnumerable<ActiveBorrowingDetails>> GetActiveBorrowingsAsync(string? searchText, CancellationToken cancellationToken = default)
    {
        var criteria = new BorrowingSearchCriteria(
            SearchText: searchText,
            Status: BorrowingStatusFilter.Active,
            SortField: BorrowingSortField.ExpectedReturnDate,
            Descending: false);

        var records = await SearchAsync(criteria, cancellationToken);

        return records
            .Select(r => new ActiveBorrowingDetails
            {
                BorrowingId = r.BorrowingId,
                StudentNumber = r.StudentNumber,
                StudentName = r.StudentName,
                EquipmentIdentifier = r.EquipmentIdentifier,
                EquipmentName = r.EquipmentName,
                DateBorrowed = r.DateBorrowed,
                ExpectedReturnDate = r.ExpectedReturnDate
            })
            .ToList();
    }

    // UC-07: the full history of one student (active and returned), newest checkout first.
    public async Task<BorrowingHistory> GetStudentHistoryAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var exists = await _context.Students.AsNoTracking()
            .AnyAsync(s => s.Id == studentId, cancellationToken);

        if (!exists)
        {
            return BorrowingHistory.NotFound();
        }

        var records = await SearchAsync(
            new BorrowingSearchCriteria(
                Status: BorrowingStatusFilter.All,
                SortField: BorrowingSortField.CheckoutDate,
                Descending: true,
                StudentId: studentId),
            cancellationToken);

        return BorrowingHistory.Found(records);
    }

    // UC-08: the full history of one equipment item (active and returned), newest checkout first.
    public async Task<BorrowingHistory> GetEquipmentHistoryAsync(int equipmentId, CancellationToken cancellationToken = default)
    {
        var exists = await _context.Equipment.AsNoTracking()
            .AnyAsync(e => e.Id == equipmentId, cancellationToken);

        if (!exists)
        {
            return BorrowingHistory.NotFound();
        }

        var records = await SearchAsync(
            new BorrowingSearchCriteria(
                Status: BorrowingStatusFilter.All,
                SortField: BorrowingSortField.CheckoutDate,
                Descending: true,
                EquipmentId: equipmentId),
            cancellationToken);

        return BorrowingHistory.Found(records);
    }

    // UC-07, UC-08, UC-09 (BR-17): search, status filter and sort. All of it runs in PostgreSQL.
    public async Task<IReadOnlyList<BorrowingRecord>> SearchAsync(BorrowingSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        // 1. Join the borrowing with its student and equipment (read-only)
        var records =
            from b in _context.Borrowings.AsNoTracking()
            join s in _context.Students.AsNoTracking() on b.StudentId equals s.Id
            join e in _context.Equipment.AsNoTracking() on b.EquipmentId equals e.Id
            select new BorrowingRecord
            {
                BorrowingId = b.Id,
                StudentId = s.Id,
                StudentNumber = s.StudentNumber,
                StudentName = s.Name,
                EquipmentId = e.Id,
                EquipmentIdentifier = e.Identifier,
                EquipmentName = e.Name,
                DateBorrowed = b.DateBorrowed,
                ExpectedReturnDate = b.ExpectedReturnDate,
                ReturnedDate = b.ReturnedDate,
                Status = b.Status
            };

        // 2. Optional history filters (UC-07 one student, UC-08 one item)
        if (criteria.StudentId.HasValue)
        {
            var studentId = criteria.StudentId.Value;
            records = records.Where(r => r.StudentId == studentId);
        }

        if (criteria.EquipmentId.HasValue)
        {
            var equipmentId = criteria.EquipmentId.Value;
            records = records.Where(r => r.EquipmentId == equipmentId);
        }

        // 3. Status filter: active, returned or all
        if (criteria.Status == BorrowingStatusFilter.Active)
        {
            records = records.Where(r => r.Status == BorrowingStatus.Active);
        }
        else if (criteria.Status == BorrowingStatusFilter.Returned)
        {
            records = records.Where(r => r.Status == BorrowingStatus.Returned);
        }

        // 4. Search text: trimmed, case-insensitive, partial match
        var term = criteria.SearchText?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term))
        {
            records = records.Where(r =>
                r.StudentName.ToLower().Contains(term) ||
                r.StudentNumber.ToLower().Contains(term) ||
                r.EquipmentName.ToLower().Contains(term) ||
                r.EquipmentIdentifier.ToLower().Contains(term));
        }

        // 5. Sort by the chosen date. The borrowing id is the tie-breaker, in the same direction,
        //    so the order is always stable and "descending" is exactly the reverse of "ascending".
        IOrderedQueryable<BorrowingRecord> ordered = (criteria.SortField, criteria.Descending) switch
        {
            (BorrowingSortField.ExpectedReturnDate, false) =>
                records.OrderBy(r => r.ExpectedReturnDate).ThenBy(r => r.BorrowingId),
            (BorrowingSortField.ExpectedReturnDate, true) =>
                records.OrderByDescending(r => r.ExpectedReturnDate).ThenByDescending(r => r.BorrowingId),
            (_, false) =>
                records.OrderBy(r => r.DateBorrowed).ThenBy(r => r.BorrowingId),
            _ =>
                records.OrderByDescending(r => r.DateBorrowed).ThenByDescending(r => r.BorrowingId)
        };

        return await ordered.ToListAsync(cancellationToken);
    }
}

//$env:EQUIPMENT_BORROWING_DB="Test"
//dotnet run --project src/EquipmentBorrowing.Desktop