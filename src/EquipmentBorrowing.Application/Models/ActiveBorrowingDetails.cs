namespace EquipmentBorrowing.Application.Models;

public class ActiveBorrowingDetails
{
    public int BorrowingId { get; set; }

    public string StudentNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;

    public string EquipmentIdentifier { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;

    // Calendar dates only (no time). They stay DateTime because the Borrowing
    // entity, the services and the screens all use DateTime. The Borrowing class
    // removes the time part with .Date, and PostgreSQL stores them as "date".
    public DateTime DateBorrowed { get; set; }
    public DateTime ExpectedReturnDate { get; set; }
}