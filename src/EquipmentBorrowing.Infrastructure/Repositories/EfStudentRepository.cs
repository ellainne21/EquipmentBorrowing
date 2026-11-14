using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class EfStudentRepository : IStudentRepository
{
    private readonly AppDbContext _context;

    public EfStudentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Student>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Students
            .AsNoTracking()
            .OrderBy(s => s.Name).ThenBy(s => s.Id)   // stable order
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Student>> SearchAsync(string? text, CancellationToken cancellationToken = default)
    {
        var query = _context.Students.AsNoTracking();

        var term = text?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(s => s.Name.ToLower().Contains(term)
                                  || s.StudentNumber.ToLower().Contains(term));
        }

        return await query
            .OrderBy(s => s.Name).ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> StudentNumberExistsAsync(string studentNumber, int? excludeStudentId, CancellationToken cancellationToken = default)
    {
        var key = studentNumber.Trim().ToUpperInvariant(); // same normalization as Student.StudentNumberKey
        return await _context.Students.AnyAsync(
            s => s.StudentNumberKey == key
                 && (!excludeStudentId.HasValue || s.Id != excludeStudentId.Value),
            cancellationToken);
    }

    public async Task AddAsync(Student student, CancellationToken cancellationToken = default)
    {
        _context.Students.Add(student);
        await SaveAsync(cancellationToken);
    }

    public async Task UpdateDetailsAsync(Student student, CancellationToken cancellationToken = default)
    {
        // Writes ONLY the descriptive columns, so a stale edit can never overwrite eligibility.
        var entry = _context.Attach(student);
        entry.Property(s => s.StudentNumber).IsModified = true;
        entry.Property(s => s.StudentNumberKey).IsModified = true;
        entry.Property(s => s.Name).IsModified = true;
        entry.Property(s => s.Contact).IsModified = true;
        await SaveAsync(cancellationToken);
    }

    public async Task UpdateEligibilityAsync(Student student, CancellationToken cancellationToken = default)
    {
        // Writes ONLY the eligibility column.
        var entry = _context.Attach(student);
        entry.Property(s => s.IsAllowedToBorrow).IsModified = true;
        await SaveAsync(cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DuplicateKeyException("That student number is already registered.", ex);
        }
    }
}