namespace EquipmentBorrowing.Application.Models;

// Which borrowing records to show (BR-17).
public enum BorrowingStatusFilter
{
    All,
    Active,
    Returned
}

// Which date the result list is sorted by (BR-17).
public enum BorrowingSortField
{
    CheckoutDate,
    ExpectedReturnDate
}

// All search, filter and sort choices in one object. Every choice can be combined with the others.
// StudentId and EquipmentId let the same query also serve the student history (UC-07)
// and the equipment history (UC-08).
public record BorrowingSearchCriteria(
    string? SearchText = null,
    BorrowingStatusFilter Status = BorrowingStatusFilter.All,
    BorrowingSortField SortField = BorrowingSortField.CheckoutDate,
    bool Descending = false,
    int? StudentId = null,
    int? EquipmentId = null);
