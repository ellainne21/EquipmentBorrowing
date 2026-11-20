namespace EquipmentBorrowing.Application.Interfaces;

// Lab 5: the "today" used for checkout and return dates (BR-11, BR-13).
// The real clock is used normally. A fixed date is allowed only with the disposable test database,
// so the acceptance tests can run on the date 2026-11-10 (see the Api project, Program.cs).
public interface IClock
{
    // Local calendar date (the time part is always 00:00).
    DateTime Today { get; }
}
