using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public class RegisterEquipmentService
{
    private readonly IEquipmentRepository _equipment;

    public RegisterEquipmentService(IEquipmentRepository equipment)
    {
        _equipment = equipment;
    }

    public async Task<OperationResult> ExecuteAsync(
        string? identifier, string? name, string? description,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return OperationResult.Rejected("Equipment identifier is required.");
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Rejected("Equipment name is required.");

        try
        {
            if (await _equipment.IdentifierExistsAsync(identifier, null, cancellationToken))
                return OperationResult.Rejected($"Equipment identifier '{identifier.Trim()}' is already registered.");

            var item = new Equipment(identifier, name, description); // available by default (BR-06)
            await _equipment.AddAsync(item, cancellationToken);

            return OperationResult.Success($"Equipment '{item.Name}' was registered and is available.", item.Id);
        }
        catch (DuplicateKeyException)
        {
            return OperationResult.Rejected($"Equipment identifier '{identifier.Trim()}' is already registered.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return OperationResult.DatabaseFailure(); }
    }
}