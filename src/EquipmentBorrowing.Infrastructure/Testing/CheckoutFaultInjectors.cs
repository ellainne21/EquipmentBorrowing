using System.Diagnostics;
using EquipmentBorrowing.Application.Interfaces;

namespace EquipmentBorrowing.Infrastructure.Testing;

// TEST SEAMS for Lab 4 (A06 and A18). They are only switched on when the disposable
// test database is selected, never for real data. See App.axaml.cs.

// The desktop app has no console window, so each seam also writes its message to a small
// log file. The log file is the written evidence that a write was attempted before the failure.
// Location: %TEMP%\EquipmentBorrowing_test_seam.log
internal static class TestSeamLog
{
    public static string FilePath { get; } =
        Path.Combine(Path.GetTempPath(), "EquipmentBorrowing_test_seam.log");

    public static void Write(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [test seam] {message}";

        Console.WriteLine(line);
        Debug.WriteLine(line);

        try
        {
            File.AppendAllText(FilePath, line + Environment.NewLine);
        }
        catch
        {
            // The log is only evidence. A logging problem must never change the test result.
        }
    }
}

// Fails right after the FIRST item's write was sent inside the transaction.
// Expected result: nothing is saved, because the transaction is never committed.
public class FailAfterFirstWriteInjector : ICheckoutFaultInjector
{
    public Task AfterWriteAsync(int writtenCount, int totalCount, CancellationToken cancellationToken = default)
    {
        if (writtenCount == 1)
        {
            TestSeamLog.Write(
                $"{writtenCount} of {totalCount} item write(s) were sent inside the transaction. Failing now, before commit.");
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
        TestSeamLog.Write("COMMIT was accepted by PostgreSQL. Imitating a lost connection now.");
        throw new IOException("Simulated connection loss after COMMIT (test seam).");
    }
}
