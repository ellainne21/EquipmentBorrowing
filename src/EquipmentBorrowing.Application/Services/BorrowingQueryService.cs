using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;

namespace EquipmentBorrowing.Application.Services;

// Read-only lookups of borrowings: the active list (UC-04), the histories (UC-07, UC-08)
// and the search, filter and sort page (UC-09).
// It tells "found", "no matches" and "database problem" apart (BR-19).
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
            // Filtering and sorting are done in the database by the repository
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

    public async Task<QueryResult<BorrowingRecord>> SearchAsync(BorrowingSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _borrowings.SearchAsync(criteria, cancellationToken);
            return QueryResult<BorrowingRecord>.Success(items);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return QueryResult<BorrowingRecord>.Failure(
                "Could not load borrowing records from the database. Check the connection and try again.");
        }
    }

    // UC-07
    public async Task<QueryResult<BorrowingRecord>> GetStudentHistoryAsync(int studentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var history = await _borrowings.GetStudentHistoryAsync(studentId, cancellationToken);

            // A stale selection: the student is gone. Explain it and let the user choose again (UC-07 2e).
            if (!history.SubjectFound)
            {
                return QueryResult<BorrowingRecord>.Failure(
                    "The selected student could not be found. Please select a student again.");
            }

            return QueryResult<BorrowingRecord>.Success(history.Records);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return QueryResult<BorrowingRecord>.Failure(
                "Could not load the student's borrowing history from the database. Check the connection and try again.");
        }
    }

    // UC-08
    public async Task<QueryResult<BorrowingRecord>> GetEquipmentHistoryAsync(int equipmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var history = await _borrowings.GetEquipmentHistoryAsync(equipmentId, cancellationToken);

            // A stale selection: the item is gone. Explain it and let the user choose again (UC-08 2e).
            if (!history.SubjectFound)
            {
                return QueryResult<BorrowingRecord>.Failure(
                    "The selected equipment could not be found. Please select an item again.");
            }

            return QueryResult<BorrowingRecord>.Success(history.Records);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return QueryResult<BorrowingRecord>.Failure(
                "Could not load the equipment's borrowing history from the database. Check the connection and try again.");
        }
    }
}
