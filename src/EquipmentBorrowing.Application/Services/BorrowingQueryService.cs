using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;

namespace EquipmentBorrowing.Application.Services;

// Read-only lookup of borrowings for the Active Borrowings screen (UC-04).
public class BorrowingQueryService
{
    private readonly IBorrowingRepository _borrowings;

    public BorrowingQueryService(IBorrowingRepository borrowings)
    {
        _borrowings = borrowings;
    }

    public async Task<QueryResult<ActiveBorrowingDetails>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _borrowings.GetActiveBorrowingsAsync(cancellationToken);

            // Stable order: earliest due first, borrowing id as the tie-breaker
            var sorted = items
                .OrderBy(b => b.ExpectedReturnDate)
                .ThenBy(b => b.BorrowingId);

            return QueryResult<ActiveBorrowingDetails>.Success(sorted);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return QueryResult<ActiveBorrowingDetails>.Failure(
                "Could not load active borrowings from the database. Check the connection and try again.");
        }
    }
}
