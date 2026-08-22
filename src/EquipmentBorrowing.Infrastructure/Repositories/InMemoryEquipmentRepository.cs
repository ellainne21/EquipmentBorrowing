using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class InMemoryEquipmentRepository : IEquipmentRepository
{
    private readonly List<Equipment> _equipments = new();

    // Helper method to add test data
    public void Seed(Equipment equipment) => _equipments.Add(equipment);

    public Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var equipment = _equipments.FirstOrDefault(e => e.Id == id);
        return Task.FromResult(equipment);
    }

    public Task UpdateAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        // In-memory: object is already updated, nothing to do
        return Task.CompletedTask;
    }
}