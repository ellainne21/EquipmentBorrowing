using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Models;

// One borrowing record together with its student and equipment (active or returned).
// Read-only data for screens. It is filled by a database query, so screens never touch EF Core.
public class BorrowingRecord
{
    public int BorrowingId { get; set; }

    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;

    public int EquipmentId { get; set; }
    public string EquipmentIdentifier { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;

    // Calendar dates only (no time)
    public DateTime DateBorrowed { get; set; }
    public DateTime ExpectedReturnDate { get; set; }
    public DateTime? ReturnedDate { get; set; }

    public BorrowingStatus Status { get; set; }
}
