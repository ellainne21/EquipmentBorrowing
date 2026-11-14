using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Interfaces;

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Student>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Student>> SearchAsync(string? text, CancellationToken cancellationToken = default);
    Task<bool> StudentNumberExistsAsync(string studentNumber, int? excludeStudentId, CancellationToken cancellationToken = default);
    Task AddAsync(Student student, CancellationToken cancellationToken = default);
    Task UpdateDetailsAsync(Student student, CancellationToken cancellationToken = default);
    Task UpdateEligibilityAsync(Student student, CancellationToken cancellationToken = default);
}