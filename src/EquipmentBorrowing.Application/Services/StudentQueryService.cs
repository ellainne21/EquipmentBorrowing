using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Services;

// Read-only student lookup used by the Students screen.
public class StudentQueryService
{
    private readonly IStudentRepository _students;

    public StudentQueryService(IStudentRepository students)
    {
        _students = students;
    }

    public async Task<QueryResult<Student>> SearchAsync(string? text, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _students.SearchAsync(text, cancellationToken);
            return QueryResult<Student>.Success(items);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            // No exception text is passed on, so connection details never reach the screen (BR-18).
            return QueryResult<Student>.Failure(
                "Could not load students from the database. Check the connection and try again.");
        }
    }
}
