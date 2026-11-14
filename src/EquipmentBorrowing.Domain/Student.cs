namespace EquipmentBorrowing.Domain;

public class Student
{
    public int Id { get; private set; }
    public string StudentNumber { get; private set; } = string.Empty;
    // Trimmed + uppercase copy of StudentNumber. A unique index on this column
    // makes the number unique ignoring case and surrounding spaces (BR-01).
    public string StudentNumberKey { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Contact { get; private set; }
    public bool IsAllowedToBorrow { get; private set; }

    // Used by EF Core when it reads rows from the database.
    private Student() { }

    public Student(string studentNumber, string name, string? contact = null)
    {
        UpdateDetails(studentNumber, name, contact);
        IsAllowedToBorrow = true; // BR-02: new students are eligible
    }

    public void UpdateDetails(string studentNumber, string name, string? contact)
    {
        var number = studentNumber?.Trim();
        var fullName = name?.Trim();

        if (string.IsNullOrEmpty(number))
            throw new ArgumentException("Student number is required.");
        if (string.IsNullOrEmpty(fullName))
            throw new ArgumentException("Full name is required.");

        StudentNumber = number;
        StudentNumberKey = number.ToUpperInvariant();
        Name = fullName;
        Contact = string.IsNullOrWhiteSpace(contact) ? null : contact.Trim(); // blank clears it (BR-03)
    }

    public void SetEligibility(bool isAllowedToBorrow)
    {
        IsAllowedToBorrow = isAllowedToBorrow; // BR-04
    }
}