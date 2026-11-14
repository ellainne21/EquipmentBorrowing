using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class EfEquipmentRepository : IEquipmentRepository
{
    private readonly AppDbContext _context;

    public EfEquipmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        // Tracking stays ON: borrow/return services change this entity and save it.
        return await _context.Equipment
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Equipment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Equipment
            .AsNoTracking()
            .OrderBy(e => e.Identifier).ThenBy(e => e.Id)   // stable order
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Equipment>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Equipment
            .AsNoTracking()
            .Where(e => e.IsAvailable)
            .OrderBy(e => e.Identifier).ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Equipment>> SearchAsync(string? text, CancellationToken cancellationToken = default)
    {
        var query = _context.Equipment.AsNoTracking();

        var term = text?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(e => e.Name.ToLower().Contains(term)
                                  || e.Identifier.ToLower().Contains(term));
        }

        return await query
            .OrderBy(e => e.Identifier).ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IdentifierExistsAsync(string identifier, int? excludeEquipmentId, CancellationToken cancellationToken = default)
    {
        var key = identifier.Trim().ToUpperInvariant(); // same normalization as Equipment.IdentifierKey
        return await _context.Equipment.AnyAsync(
            e => e.IdentifierKey == key
                 && (!excludeEquipmentId.HasValue || e.Id != excludeEquipmentId.Value),
            cancellationToken);
    }

    public async Task AddAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        _context.Equipment.Add(equipment);
        await SaveAsync(cancellationToken);
    }

    public async Task UpdateDetailsAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        // Writes ONLY the descriptive columns. IsAvailable is never touched (BR-07).
        var entry = _context.Attach(equipment);
        entry.Property(e => e.Identifier).IsModified = true;
        entry.Property(e => e.IdentifierKey).IsModified = true;
        entry.Property(e => e.Name).IsModified = true;
        entry.Property(e => e.Description).IsModified = true;
        await SaveAsync(cancellationToken);
    }

    public async Task UpdateAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        _context.Equipment.Update(equipment);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DuplicateKeyException("That equipment identifier is already registered.", ex);
        }
    }
}