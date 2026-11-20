namespace EquipmentBorrowing.Contracts;

// API-01, API-07, API-08, API-09
public sealed record EquipmentDto
{
    public int Id { get; init; }
    public string EquipmentCode { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public bool IsAvailable { get; init; }
}

// API-08: create. New items start available.
public sealed record CreateEquipmentRequest
{
    public string? EquipmentCode { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
}

// API-09: update. There is no IsAvailable here on purpose. Sending it is a 400 (protected field).
public sealed record UpdateEquipmentRequest
{
    public string? EquipmentCode { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
}
