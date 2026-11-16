using EquipmentBorrowing.Application.Interfaces;

namespace EquipmentBorrowing.Application.Services;

// Normal behaviour: nothing is injected.
public class NoCheckoutFaults : ICheckoutFaultInjector
{
    public Task AfterWriteAsync(int writtenCount, int totalCount, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task AfterCommitAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
