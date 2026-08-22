namespace EquipmentBorrowing.Domain;

public class Student
{
    public int Id { get; private set; }
    public string Name { get; private set; }
    public bool IsAllowedToBorrow { get; private set; }

    public Student(int id, string name, bool isAllowedToBorrow)
    {
        Id = id;
        Name = name;
        IsAllowedToBorrow = isAllowedToBorrow;
    }
}