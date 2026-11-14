using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;

namespace EquipmentBorrowing.Application.Services;

public class ChangeStudentEligibilityService
{
    private readonly IStudentRepository _students;

    public ChangeStudentEligibilityService(IStudentRepository students)
    {
        _students = students;
    }

    public async Task<OperationResult> ExecuteAsync(
        int studentId, bool isAllowedToBorrow,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var student = await _students.GetByIdAsync(studentId, cancellationToken);
            if (student == null)
                return OperationResult.Rejected("This student no longer exists. Refresh the list and try again.");

            if (student.IsAllowedToBorrow == isAllowedToBorrow)
            {
                var state = isAllowedToBorrow ? "allowed to borrow" : "restricted";
                return OperationResult.Rejected($"{student.Name} is already {state}. No change was made.");
            }

            // Only the eligibility column changes. Existing active borrowings are not cancelled,
            // and returning them stays possible (BR-04).
            student.SetEligibility(isAllowedToBorrow);
            await _students.UpdateEligibilityAsync(student, cancellationToken);

            return OperationResult.Success(isAllowedToBorrow
                ? $"{student.Name} is now allowed to borrow."
                : $"{student.Name} is now restricted from new borrowing. Existing borrowings stay active and can still be returned.",
                student.Id);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return OperationResult.DatabaseFailure(); }
    }
}