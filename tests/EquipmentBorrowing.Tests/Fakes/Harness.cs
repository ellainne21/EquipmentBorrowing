using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Tests.Fakes;

// A test fault injector: it can fail after a chosen write, or imitate a lost connection after the commit.
public class TestFaultInjector : ICheckoutFaultInjector
{
    public int? ThrowAfterWriteNumber { get; set; }
    public bool ThrowAfterCommit { get; set; }
    public int WritesSeen { get; private set; }

    public Task AfterWriteAsync(int writtenCount, int totalCount, CancellationToken cancellationToken = default)
    {
        WritesSeen = writtenCount;
        if (ThrowAfterWriteNumber == writtenCount)
            throw new InvalidOperationException("Simulated failure after a write.");
        return Task.CompletedTask;
    }

    public Task AfterCommitAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowAfterCommit)
            throw new IOException("Simulated lost connection after the commit was accepted.");
        return Task.CompletedTask;
    }
}

// Builds the real services on top of the fake database, with a standard small data set.
public class Harness
{
    public FakeDatabase Db { get; } = new();
    public TestFaultInjector Faults { get; } = new();

    public CheckOutMultipleEquipmentService Multi { get; }
    public BorrowEquipmentService Single { get; }
    public ReturnEquipmentService Returns { get; }

    public Student Alice { get; private set; } = null!;     // id 1, eligible
    public Student Bob { get; private set; } = null!;       // id 2, restricted
    public Student Carla { get; private set; } = null!;     // id 3, eligible

    private Harness()
    {
        var factory = new FakeUnitOfWorkFactory(Db);
        var checkout = new CheckoutTransaction(factory, Faults);

        Multi = new CheckOutMultipleEquipmentService(checkout);
        Single = new BorrowEquipmentService(checkout);
        Returns = new ReturnEquipmentService(factory, Faults);
    }

    // Students 1 to 3 and equipment 1 to 5 (all available), no borrowings.
    public static Harness WithStandardData()
    {
        var h = new Harness();

        h.Alice = h.Db.AddStudent(1, "2026-0001", "Alice Santos");
        h.Bob = h.Db.AddStudent(2, "2026-0002", "Bob Reyes", allowedToBorrow: false);
        h.Carla = h.Db.AddStudent(3, "2026-0003", "Carla Mendoza");

        h.Db.AddEquipment(1, "LAP-001", "Laptop Dell XPS 15");
        h.Db.AddEquipment(2, "PRJ-001", "Projector Epson");
        h.Db.AddEquipment(3, "ARD-001", "Arduino Kit");
        h.Db.AddEquipment(4, "CAM-001", "Canon Camera");
        h.Db.AddEquipment(5, "MIC-001", "USB Microphone");

        return h;
    }

    public static DateTime NextWeek => DateTime.Today.AddDays(7);
}
