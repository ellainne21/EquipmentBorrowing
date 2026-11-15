using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

// Read-only equipment lookup. Also covers "find available equipment" (UC-02).
public class EquipmentQueryService
{
    private readonly IEquipmentRepository _equipment;

    public EquipmentQueryService(IEquipmentRepository equipment)
    {
        _equipment = equipment;
    }

    public async Task<QueryResult<Equipment>> SearchAsync(
        string? text, bool availableOnly, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _equipment.SearchAsync(text, availableOnly, cancellationToken);
            return QueryResult<Equipment>.Success(items);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return QueryResult<Equipment>.Failure(
                "Could not load equipment from the database. Check the connection and try again.");
        }
    }
}
