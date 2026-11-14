using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;

namespace EquipmentBorrowing.Application.Services;

public class UpdateEquipmentService
{
    private readonly IEquipmentRepository _equipment;

    public UpdateEquipmentService(IEquipmentRepository equipment)
    {
        _equipment = equipment;
    }

    public async Task<OperationResult> ExecuteAsync(
        int equipmentId, string? identifier, string? name, string? description,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return OperationResult.Rejected("Equipment identifier is required.");
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Rejected("Equipment name is required.");

        try
        {
            var item = await _equipment.GetByIdAsync(equipmentId, cancellationToken);
            if (item == null)
                return OperationResult.Rejected("This equipment no longer exists. Refresh the list and try again.");

            if (await _equipment.IdentifierExistsAsync(identifier, equipmentId, cancellationToken))
                return OperationResult.Rejected($"Equipment identifier '{identifier.Trim()}' belongs to another item.");

            // Availability is not a descriptive field; it only changes through borrow/return (BR-07).
            item.UpdateDetails(identifier, name, description);
            await _equipment.UpdateDetailsAsync(item, cancellationToken);

            return OperationResult.Success($"Equipment '{item.Name}' was updated.", item.Id);
        }
        catch (DuplicateKeyException)
        {
            return OperationResult.Rejected($"Equipment identifier '{identifier.Trim()}' belongs to another item.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return OperationResult.DatabaseFailure(); }
    }
}