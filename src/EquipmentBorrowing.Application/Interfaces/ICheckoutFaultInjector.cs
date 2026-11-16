namespace EquipmentBorrowing.Application.Interfaces;

// A controlled test seam (Lab 4 A06 and A18). In normal use the do-nothing version is registered.
// The test versions are only switched on for the disposable test database.
public interface ICheckoutFaultInjector
{
    // Called inside the transaction after an item's write was sent, before commit.
    Task AfterWriteAsync(int writtenCount, int totalCount, CancellationToken cancellationToken = default);

    // Called right after COMMIT succeeded. A test version throws to imitate a lost connection,
    // so the application cannot know that the commit actually worked.
    Task AfterCommitAsync(CancellationToken cancellationToken = default);
}
