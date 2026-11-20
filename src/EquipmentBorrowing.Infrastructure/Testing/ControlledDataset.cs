using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Testing;

// Loads the Lab 5 controlled dataset (students S1-S4, equipment E1-E7) into the DISPOSABLE test database.
// It deletes everything first, so it refuses to run unless the database name contains "test".
// All dates are offsets from "today". With the fixed date 2026-11-10 they match the reference exactly:
//   E1 borrowed 2026-11-01 (today-9), expected 2026-11-09 (today-1)
//   E3 borrowed 2026-11-02 (today-8), expected 2026-11-12 (today+2), returned 2026-11-08 (today-2)
//   E4/E5 borrowed 2026-11-03 (today-7), expected 2026-11-12 (today+2)
public static class ControlledDataset
{
    public static void Reset(AppDbContext context, DateTime today)
    {
        var databaseName = context.Database.GetDbConnection().Database;
        if (string.IsNullOrEmpty(databaseName) || !databaseName.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Refused: the database name does not contain 'test'. The controlled dataset is only loaded into the disposable test database.");
        }

        context.Database.Migrate();

        // Empty the three tables and restart the id numbers. The migration history table is not touched.
        context.Database.ExecuteSqlRaw(
            "TRUNCATE TABLE \"Borrowings\", \"Equipment\", \"Students\" RESTART IDENTITY CASCADE");

        var s1 = new Student("ST-100", "Ada Cruz");
        var s2 = new Student("ST-200", "Ben Diaz");
        s2.SetEligibility(false); // ineligible
        var s3 = new Student("ST-300", "Cara Lim");
        var s4 = new Student("ST-400", "Dani Ong");

        var e1 = new Equipment("EQ-001", "Camera");
        var e2 = new Equipment("EQ-002", "Laptop");
        var e3 = new Equipment("EQ-003", "Projector");
        var e4 = new Equipment("EQ-004", "Tablet");
        var e5 = new Equipment("EQ-005", "Microphone");
        var e6 = new Equipment("EQ-006", "Tripod");
        var e7 = new Equipment("EQ-007", "Speaker");

        // Items with an active borrowing are unavailable. E3 was returned, so it is available.
        e1.MarkAsBorrowed();
        e4.MarkAsBorrowed();
        e5.MarkAsBorrowed();

        context.Students.AddRange(s1, s2, s3, s4);
        context.Equipment.AddRange(e1, e2, e3, e4, e5, e6, e7);
        context.SaveChanges(); // ids are generated here

        var d = today.Date;

        var s1Active = new Borrowing(s1.Id, e1.Id, d.AddDays(-9), d.AddDays(-1));

        var s1Returned = new Borrowing(s1.Id, e3.Id, d.AddDays(-8), d.AddDays(2));
        s1Returned.MarkAsReturned(d.AddDays(-2));

        var s3ActiveA = new Borrowing(s3.Id, e4.Id, d.AddDays(-7), d.AddDays(2));
        var s3ActiveB = new Borrowing(s3.Id, e5.Id, d.AddDays(-7), d.AddDays(2));

        context.Borrowings.AddRange(s1Active, s1Returned, s3ActiveA, s3ActiveB);
        context.SaveChanges();
    }
}
