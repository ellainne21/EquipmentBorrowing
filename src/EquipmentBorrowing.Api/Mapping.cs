using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Contracts;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Api;

// Turns Lab 4 objects into the published JSON shapes (Contracts project).
// Contract names differ from the Lab 4 names on purpose:
//   Name -> fullName, IsAllowedToBorrow -> isEligible, Identifier -> equipmentCode, DateBorrowed -> borrowedDate.
internal static class Mapping
{
    public static StudentDto ToDto(this Student s) => new()
    {
        Id = s.Id,
        StudentNumber = s.StudentNumber,
        FullName = s.Name,
        Contact = s.Contact,
        IsEligible = s.IsAllowedToBorrow
    };

    public static EquipmentDto ToDto(this Equipment e) => new()
    {
        Id = e.Id,
        EquipmentCode = e.Identifier,
        Name = e.Name,
        Description = e.Description,
        IsAvailable = e.IsAvailable
    };

    public static BorrowingDto ToDto(this BorrowingRecord r) => new()
    {
        Id = r.BorrowingId,
        StudentId = r.StudentId,
        EquipmentId = r.EquipmentId,
        BorrowedDate = DateOnly.FromDateTime(r.DateBorrowed),
        ExpectedReturnDate = DateOnly.FromDateTime(r.ExpectedReturnDate),
        ReturnedDate = r.ReturnedDate.HasValue ? DateOnly.FromDateTime(r.ReturnedDate.Value) : null,
        Status = r.Status == BorrowingStatus.Active ? BorrowingStatusNames.Active : BorrowingStatusNames.Returned,
        StudentNumber = r.StudentNumber,
        StudentName = r.StudentName,
        EquipmentCode = r.EquipmentIdentifier,
        EquipmentName = r.EquipmentName
    };
}
