using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;

namespace EquipmentBorrowing.Application.Services;

public class UpdateStudentService
{
    private readonly IStudentRepository _students;

    public UpdateStudentService(IStudentRepository students)
    {
        _students = students;
    }

    public async Task<OperationResult> ExecuteAsync(
        int studentId, string? studentNumber, string? fullName, string? contact,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studentNumber))
            return OperationResult.Rejected("Student number is required.");
        if (string.IsNullOrWhiteSpace(fullName))
            return OperationResult.Rejected("Full name is required.");

        try
        {
            var student = await _students.GetByIdAsync(studentId, cancellationToken);
            if (student == null)
                return OperationResult.Rejected("This student no longer exists. Refresh the list and try again.");

            if (await _students.StudentNumberExistsAsync(studentNumber, studentId, cancellationToken))
                return OperationResult.Rejected($"Student number '{studentNumber.Trim()}' belongs to another student.");

            // Same internal Id, so all borrowing history stays linked (BR-03, BR-14).
            student.UpdateDetails(studentNumber, fullName, contact);
            await _students.UpdateDetailsAsync(student, cancellationToken);

            return OperationResult.Success($"Student '{student.Name}' was updated.", student.Id);
        }
        catch (DuplicateKeyException)
        {
            return OperationResult.Rejected($"Student number '{studentNumber.Trim()}' belongs to another student.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return OperationResult.DatabaseFailure(); }
    }
}