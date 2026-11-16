using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Application.Interfaces;

// One unit of work = one short-lived database context + one transaction.
// Checkout and return use it so every read, rule check and write happens in the same transaction.
public interface IBorrowingUnitOfWork : IAsyncDisposable
{
    Task BeginAsync(CancellationToken cancellationToken = default);

    // These read the CURRENT data and lock the rows until the transaction ends,
    // so two staff members cannot pass the same checks at the same time (BR-15).
    Task<Student?> GetStudentForUpdateAsync(int studentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Equipment>> GetEquipmentForUpdateAsync(IReadOnlyCollection<int> equipmentIds, CancellationToken cancellationToken = default);
    Task<Borrowing?> GetBorrowingForUpdateAsync(int borrowingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<int>> GetEquipmentIdsWithActiveBorrowingAsync(IReadOnlyCollection<int> equipmentIds, CancellationToken cancellationToken = default);
    Task<int> CountActiveBorrowingsAsync(int studentId, CancellationToken cancellationToken = default);

    void AddBorrowing(Borrowing borrowing);

    // Sends pending changes to the database INSIDE the open transaction (not yet committed).
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    // Makes everything permanent. Disposing without committing rolls everything back.
    Task CommitAsync(CancellationToken cancellationToken = default);
}

public interface IBorrowingUnitOfWorkFactory
{
    IBorrowingUnitOfWork Create();
}
