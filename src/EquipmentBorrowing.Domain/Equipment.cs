namespace EquipmentBorrowing.Domain;

public class Equipment
{
    public int Id { get; private set; }
    public string Name { get; private set; }
    public bool IsAvailable { get; private set; }

    public Equipment(int id, string name)
    {
        Id = id;
        Name = name;
        IsAvailable = true;
    }

    public void MarkAsBorrowed() { IsAvailable = false; }
    public void MarkAsReturned() { IsAvailable = true; }
}