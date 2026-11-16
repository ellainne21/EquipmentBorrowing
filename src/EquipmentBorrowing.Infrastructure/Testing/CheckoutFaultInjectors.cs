using EquipmentBorrowing.Application.Interfaces;

namespace EquipmentBorrowing.Infrastructure.Testing;

// TEST SEAMS for Lab 4 (A06 and A18). They are only switched on when the disposable
// test database is selected, never for real data. See App.axaml.cs.

// Fails right after the FIRST item's write was sent inside the transaction.
// Expected result: nothing is saved, because the transaction is never committed.
public class FailAfterFirstWriteInjector : ICheckoutFaultInjector
{
    public Task AfterWriteAsync(int writtenCount, int totalCount, CancellationToken cancellationToken = default)
    {
        if (writtenCount == 1)
        {
            Console.WriteLine(
                $"[test seam] {writtenCount} of {totalCount} item write(s) were sent inside the transaction. Failing now, before commit.");
            throw new InvalidOperationException("Simulated failure after the first write (test seam).");
        }

        return Task.CompletedTask;
    }

    public Task AfterCommitAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

// Imitates a connection that drops right AFTER the database accepted COMMIT.
// The change IS saved, but the application cannot know that, so it must report an unknown outcome.
public class LostCommitResponseInjector : ICheckoutFaultInjector
{
    public Task AfterWriteAsync(int writtenCount, int totalCount, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task AfterCommitAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[test seam] COMMIT was accepted by PostgreSQL. Imitating a lost connection now.");
        throw new IOException("Simulated connection loss after COMMIT (test seam).");
    }
}
