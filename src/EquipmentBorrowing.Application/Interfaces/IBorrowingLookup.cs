using EquipmentBorrowing.Application.Models;

namespace EquipmentBorrowing.Application.Interfaces;

// Lab 5: read ONE borrowing record (with its student and equipment) by id.
// It is a separate small interface so IBorrowingRepository and its test fakes stay unchanged.
public interface IBorrowingLookup
{
    Task<BorrowingRecord?> GetRecordAsync(int borrowingId, CancellationToken cancellationToken = default);
}
