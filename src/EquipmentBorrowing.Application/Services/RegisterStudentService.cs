using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

public class RegisterStudentService
{
    private readonly IStudentRepository _students;

    public RegisterStudentService(IStudentRepository students)
    {
        _students = students;
    }

    public async Task<OperationResult> ExecuteAsync(
        string? studentNumber, string? fullName, string? contact,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studentNumber))
            return OperationResult.Rejected("Student number is required.");
        if (string.IsNullOrWhiteSpace(fullName))
            return OperationResult.Rejected("Full name is required.");

        try
        {
            if (await _students.StudentNumberExistsAsync(studentNumber, null, cancellationToken))
                return OperationResult.Rejected($"Student number '{studentNumber.Trim()}' is already registered.");

            var student = new Student(studentNumber, fullName, contact); // eligible by default (BR-02)
            await _students.AddAsync(student, cancellationToken);

            return OperationResult.Success($"Student '{student.Name}' was registered.", student.Id);
        }
        catch (DuplicateKeyException)
        {
            // The database unique index caught a duplicate the pre-check could not see (BR-16).
            return OperationResult.Rejected($"Student number '{studentNumber.Trim()}' is already registered.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return OperationResult.DatabaseFailure(); }
    }
}