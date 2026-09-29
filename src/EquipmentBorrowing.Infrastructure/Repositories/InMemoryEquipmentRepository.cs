using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class InMemoryEquipmentRepository : IEquipmentRepository
{
    private readonly List<Equipment> _equipments = new();

    public void Seed(Equipment equipment) => _equipments.Add(equipment);

    public Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var equipment = _equipments.FirstOrDefault(e => e.Id == id);
        return Task.FromResult(equipment);
    }

    public Task<IEnumerable<Equipment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<Equipment>>(_equipments);
    }

    public Task<IEnumerable<Equipment>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        var available = _equipments.Where(e => e.IsAvailable);
        return Task.FromResult<IEnumerable<Equipment>>(available);
    }

    public Task UpdateAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}