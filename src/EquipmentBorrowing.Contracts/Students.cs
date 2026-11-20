namespace EquipmentBorrowing.Contracts;

// API-02, API-03, API-04, API-05, API-06
public sealed record StudentDto
{
    public int Id { get; init; }
    public string StudentNumber { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? Contact { get; init; }
    public bool IsEligible { get; init; }
}

// API-04: create. Contact can be left out (absent). New students start eligible.
public sealed record CreateStudentRequest
{
    public string? StudentNumber { get; init; }
    public string? FullName { get; init; }
    public string? Contact { get; init; }
}

// API-05: update sends all editable fields. A null contact clears it.
public sealed record UpdateStudentRequest
{
    public string? StudentNumber { get; init; }
    public string? FullName { get; init; }
    public string? Contact { get; init; }
}

// API-06: bool? so that a missing field becomes a 400 instead of a silent "false".
public sealed record SetEligibilityRequest
{
    public bool? IsEligible { get; init; }
}
