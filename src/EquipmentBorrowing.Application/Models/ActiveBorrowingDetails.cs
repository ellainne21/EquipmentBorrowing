namespace EquipmentBorrowing.Application.Models;

public class ActiveBorrowingDetails
{
    public int BorrowingId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public DateTime DateBorrowed { get; set; }
    public DateTime ExpectedReturnDate { get; set; }
}