using EquipmentBorrowing.Application.Interfaces;

namespace EquipmentBorrowing.Infrastructure.Testing;

// TEST SEAM for Lab 5: a fixed local date (for example 2026-11-10) so the acceptance tests repeat.
// It is only used with the disposable test database. See Program.cs in the Api project.
public class FixedClock : IClock
{
    public FixedClock(DateTime date)
    {
        Today = date.Date;
    }

    public DateTime Today { get; }
}
