using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace EquipmentBorrowing.Infrastructure.Data;

// Creates one short-lived AppDbContext per operation (checkout or return).
// Registered as a singleton because it only holds the context factory.
public class EfBorrowingUnitOfWorkFactory : IBorrowingUnitOfWorkFactory
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public EfBorrowingUnitOfWorkFactory(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IBorrowingUnitOfWork Create()
        => new EfBorrowingUnitOfWork(_contextFactory.CreateDbContext());
}

// One DbContext + one PostgreSQL transaction. Nothing is shared with other operations,
// so it never works with stale tracked data left over from an earlier operation.
public class EfBorrowingUnitOfWork : IBorrowingUnitOfWork
{
    // Name of the partial unique index that allows only ONE active borrowing per item (BR-10, BR-16)
    private const string OneActivePerItemIndex = "UX_Borrowings_OneActivePerEquipment";

    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    public EfBorrowingUnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task<Student?> GetStudentForUpdateAsync(int studentId, CancellationToken cancellationToken = default)
    {
        // FOR UPDATE locks the row until the transaction ends, so a second checkout for the
        // same student waits here and then sees the first checkout's active count.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Students\" WHERE \"Id\" = {studentId} FOR UPDATE", cancellationToken);

        return await _context.Students.FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Equipment>> GetEquipmentForUpdateAsync(
        IReadOnlyCollection<int> equipmentIds, CancellationToken cancellationToken = default)
    {
        // Lock in a fixed order (lowest id first) so two checkouts can never wait on each other forever.
        var ids = equipmentIds.Distinct().OrderBy(id => id).ToList();

        foreach (var id in ids)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Equipment\" WHERE \"Id\" = {id} FOR UPDATE", cancellationToken);
        }

        return await _context.Equipment
            .Where(e => ids.Contains(e.Id))
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Borrowing?> GetBorrowingForUpdateAsync(int borrowingId, CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Borrowings\" WHERE \"Id\" = {borrowingId} FOR UPDATE", cancellationToken);

        return await _context.Borrowings.FirstOrDefaultAsync(b => b.Id == borrowingId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<int>> GetEquipmentIdsWithActiveBorrowingAsync(
        IReadOnlyCollection<int> equipmentIds, CancellationToken cancellationToken = default)
    {
        var ids = equipmentIds.ToList();

        return await _context.Borrowings
            .AsNoTracking()
            .Where(b => ids.Contains(b.EquipmentId) && b.Status == BorrowingStatus.Active)
            .Select(b => b.EquipmentId)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountActiveBorrowingsAsync(int studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Borrowings
            .CountAsync(b => b.StudentId == studentId && b.Status == BorrowingStatus.Active, cancellationToken);
    }

    public void AddBorrowing(Borrowing borrowing)
    {
        _context.Borrowings.Add(borrowing);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
        {
            // Translate database errors into application language (no Npgsql types leave Infrastructure).
            if (pg.SqlState == PostgresErrorCodes.UniqueViolation && pg.ConstraintName == OneActivePerItemIndex)
                throw new ConflictException("One of the selected items was checked out by someone else just now.", ex);

            if (pg.SqlState == PostgresErrorCodes.ForeignKeyViolation)
                throw new ConflictException("The student or one of the items no longer exists.", ex);

            throw;
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("The transaction was not started.");

        await _transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        // Disposing an uncommitted transaction rolls it back. If the connection is already gone,
        // PostgreSQL discards the uncommitted work by itself, so errors here are safe to ignore.
        try
        {
            if (_transaction is not null) await _transaction.DisposeAsync();
        }
        catch { }

        try
        {
            await _context.DisposeAsync();
        }
        catch { }
    }
}
