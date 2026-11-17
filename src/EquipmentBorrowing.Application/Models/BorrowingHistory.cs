namespace EquipmentBorrowing.Application.Models;

// The history of ONE student or ONE equipment item (UC-07, UC-08).
// SubjectFound tells "that student/item no longer exists" apart from "it exists but has no records",
// so the screen can show a missing-selection message or an empty state (BR-19).
public class BorrowingHistory
{
    public bool SubjectFound { get; }
    public IReadOnlyList<BorrowingRecord> Records { get; }

    private BorrowingHistory(bool subjectFound, IReadOnlyList<BorrowingRecord> records)
    {
        SubjectFound = subjectFound;
        Records = records;
    }

    public static BorrowingHistory Found(IReadOnlyList<BorrowingRecord> records)
        => new(true, records);

    public static BorrowingHistory NotFound()
        => new(false, Array.Empty<BorrowingRecord>());
}
