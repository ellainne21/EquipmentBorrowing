using EquipmentBorrowing.Application.Models;

namespace EquipmentBorrowing.Application.Interfaces;

// Read-only borrowing queries. Writes (checkout and return) go through the unit of work,
// so that they run inside one transaction.
public interface IBorrowingRepository
{
    // UC-04: active borrowings only, optionally narrowed by search text.
    Task<IEnumerable<ActiveBorrowingDetails>> GetActiveBorrowingsAsync(string? searchText, CancellationToken cancellationToken = default);

    // UC-07, UC-08, UC-09: active and returned records with search, status filter and sort (BR-17).
    // Filtering and sorting are done by the database.
    Task<IReadOnlyList<BorrowingRecord>> SearchAsync(BorrowingSearchCriteria criteria, CancellationToken cancellationToken = default);
}
