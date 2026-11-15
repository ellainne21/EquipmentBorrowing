namespace EquipmentBorrowing.Application.Models;

// Result of a read-only query. It lets screens tell apart three different situations
// (BR-19): data was found, nothing matched (empty list), or the database failed.
public class QueryResult<T>
{
    public bool IsSuccess { get; }
    public IReadOnlyList<T> Items { get; }
    public string? ErrorMessage { get; }

    private QueryResult(bool isSuccess, IReadOnlyList<T> items, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Items = items;
        ErrorMessage = errorMessage;
    }

    public static QueryResult<T> Success(IEnumerable<T> items)
        => new(true, items.ToList(), null);

    public static QueryResult<T> Failure(string message)
        => new(false, Array.Empty<T>(), message);
}
