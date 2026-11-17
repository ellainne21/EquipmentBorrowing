using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;

namespace EquipmentBorrowing.Application.Services;

public class BorrowingQueryService
{
    private readonly IBorrowingRepository _borrowings;

    public BorrowingQueryService(IBorrowingRepository borrowings)
    {
        _borrowings = borrowings;
    }

    public async Task<QueryResult<ActiveBorrowingDetails>> GetActiveAsync(string? searchText, CancellationToken cancellationToken = default)
    {
        try
        {
            // Filtering and sorting are now done in the database by the repository
            var items = await _borrowings.GetActiveBorrowingsAsync(searchText, cancellationToken);
            return QueryResult<ActiveBorrowingDetails>.Success(items);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return QueryResult<ActiveBorrowingDetails>.Failure(
                "Could not load active borrowings from the database. Check the connection and try again.");
        }
    }
}