namespace EquipmentBorrowing.Contracts;

public static class BorrowingStatusNames
{
    public const string Active = "Active";
    public const string Returned = "Returned";
}

// API-10 to API-16. Dates are calendar dates (YYYY-MM-DD).
// The student and equipment display fields are filled in list and history responses.
public sealed record BorrowingDto
{
    public int Id { get; init; }
    public int StudentId { get; init; }
    public int EquipmentId { get; init; }
    public DateOnly BorrowedDate { get; init; }
    public DateOnly ExpectedReturnDate { get; init; }
    public DateOnly? ReturnedDate { get; init; }
    public string Status { get; init; } = BorrowingStatusNames.Active;

    public string? StudentNumber { get; init; }
    public string? StudentName { get; init; }
    public string? EquipmentCode { get; init; }
    public string? EquipmentName { get; init; }
}

// API-10. The server decides the checkout date.
// Nullable types so that a missing field becomes a 400, not a silent 0.
public sealed record CreateBorrowingRequest
{
    public int? StudentId { get; init; }
    public int? EquipmentId { get; init; }
    public DateOnly? ExpectedReturnDate { get; init; }
}

// API-11: multi-item checkout (two or three items, all or nothing).
public sealed record CheckoutRequest
{
    public int? StudentId { get; init; }
    public List<int>? EquipmentIds { get; init; }
    public DateOnly? ExpectedReturnDate { get; init; }
}

public sealed record CheckoutResponse
{
    public List<BorrowingDto> Borrowings { get; init; } = new();
}

// API-12: the body is an empty JSON object {}.
public sealed record ReturnBorrowingRequest;
