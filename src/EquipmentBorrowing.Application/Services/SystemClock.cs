using EquipmentBorrowing.Application.Interfaces;

namespace EquipmentBorrowing.Application.Services;

// Normal behaviour: the real local calendar date.
public class SystemClock : IClock
{
    public DateTime Today => DateTime.Today;
}
