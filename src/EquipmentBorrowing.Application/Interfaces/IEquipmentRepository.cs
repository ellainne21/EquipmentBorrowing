using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Interfaces;

public interface IEquipmentRepository
{
    Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Equipment>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Equipment>> GetAvailableAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Equipment>> SearchAsync(string? text, CancellationToken cancellationToken = default);
    Task<bool> IdentifierExistsAsync(string identifier, int? excludeEquipmentId, CancellationToken cancellationToken = default);
    Task AddAsync(Equipment equipment, CancellationToken cancellationToken = default);
    Task UpdateDetailsAsync(Equipment equipment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Equipment equipment, CancellationToken cancellationToken = default);
}