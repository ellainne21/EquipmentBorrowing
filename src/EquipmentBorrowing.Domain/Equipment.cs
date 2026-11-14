namespace EquipmentBorrowing.Domain;

public class Equipment
{
    public int Id { get; private set; }
    public string Identifier { get; private set; } = string.Empty;
    // Trimmed + uppercase copy of Identifier, used by the unique index (BR-05).
    public string IdentifierKey { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsAvailable { get; private set; }

    // Used by EF Core when it reads rows from the database.
    private Equipment() { }

    public Equipment(string identifier, string name, string? description = null)
    {
        UpdateDetails(identifier, name, description);
        IsAvailable = true; // BR-06: new equipment is available
    }

    public void UpdateDetails(string identifier, string name, string? description)
    {
        var id = identifier?.Trim();
        var itemName = name?.Trim();

        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Equipment identifier is required.");
        if (string.IsNullOrEmpty(itemName))
            throw new ArgumentException("Equipment name is required.");

        Identifier = id;
        IdentifierKey = id.ToUpperInvariant();
        Name = itemName;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    // BR-07: availability is NOT a descriptive field. It only changes through borrow/return.
    public void MarkAsBorrowed() { IsAvailable = false; }
    public void MarkAsReturned() { IsAvailable = true; }
}