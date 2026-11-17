using System.Reflection;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Tests.Fakes;

// Small helpers for the domain classes, whose Id has a private setter (EF Core sets it normally).
public static class TestData
{
    public static T WithId<T>(this T entity, int id) where T : class
    {
        typeof(T).GetProperty("Id")!.GetSetMethod(nonPublic: true)!.Invoke(entity, new object[] { id });
        return entity;
    }

    // A copy of the entity. The fake unit of work hands out copies, so changes are only
    // kept when the transaction is committed, exactly like a real database transaction.
    public static T Clone<T>(this T entity) where T : class
    {
        var method = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (T)method.Invoke(entity, null)!;
    }
}

// The committed data of the fake database, plus switches that imitate database problems.
public class FakeDatabase
{
    private int _nextBorrowingId = 1;

    public List<Student> Students { get; } = new();
    public List<Equipment> Equipment { get; } = new();
    public List<Borrowing> Borrowings { get; } = new();

    // Switches that imitate problems
    public bool FailOnBegin { get; set; }        // the database cannot be reached
    public bool FailOnSave { get; set; }         // a write fails inside the transaction
    public bool ConflictOnSave { get; set; }     // the database refuses a write (for example a unique index)
    public bool FailOnCommit { get; set; }       // the commit itself fails

    public Student AddStudent(int id, string number, string name, bool allowedToBorrow = true)
    {
        var student = new Student(number, name).WithId(id);
        student.SetEligibility(allowedToBorrow);
        Students.Add(student);
        return student;
    }

    public Equipment AddEquipment(int id, string identifier, string name, bool available = true)
    {
        var item = new Equipment(identifier, name).WithId(id);
        if (!available) item.MarkAsBorrowed();
        Equipment.Add(item);
        return item;
    }

    // An existing active borrowing, like one made by an earlier checkout.
    public Borrowing AddActiveBorrowing(Student student, Equipment item, bool markItemUnavailable = true)
    {
        var borrowing = new Borrowing(student.Id, item.Id, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(5))
            .WithId(NextBorrowingId());
        if (markItemUnavailable) item.MarkAsBorrowed();
        Borrowings.Add(borrowing);
        return borrowing;
    }

    // An existing borrowing that is in the list as it is (for special cases).
    public Borrowing AddBorrowingRaw(Borrowing borrowing)
    {
        borrowing.WithId(NextBorrowingId());
        Borrowings.Add(borrowing);
        return borrowing;
    }

    public int NextBorrowingId() => _nextBorrowingId++;

    public Equipment Item(int id) => Equipment.Single(e => e.Id == id);

    public Borrowing Loan(int id) => Borrowings.Single(b => b.Id == id);

    public int ActiveCountFor(int studentId)
        => Borrowings.Count(b => b.StudentId == studentId && b.Status == BorrowingStatus.Active);

    // A text picture of all committed data. Two equal snapshots mean "nothing changed".
    public string Snapshot()
    {
        var items = string.Join(";", Equipment.OrderBy(e => e.Id).Select(e => $"E{e.Id}:{e.IsAvailable}"));
        var loans = string.Join(";", Borrowings.OrderBy(b => b.Id)
            .Select(b => $"B{b.Id}:{b.StudentId}:{b.EquipmentId}:{b.Status}:{b.ReturnedDate:yyyy-MM-dd}"));
        return items + "|" + loans;
    }
}

public class FakeUnitOfWorkFactory : IBorrowingUnitOfWorkFactory
{
    private readonly FakeDatabase _db;

    public FakeUnitOfWorkFactory(FakeDatabase db)
    {
        _db = db;
    }

    public IBorrowingUnitOfWork Create() => new FakeBorrowingUnitOfWork(_db);
}

// One fake transaction. Reads give copies. Writes are only "sent" (staged) until CommitAsync.
// If the unit of work is disposed without a commit, everything is thrown away (a rollback).
public class FakeBorrowingUnitOfWork : IBorrowingUnitOfWork
{
    private readonly FakeDatabase _db;

    private readonly Dictionary<int, Student> _students = new();
    private readonly Dictionary<int, Equipment> _equipment = new();
    private readonly Dictionary<int, Borrowing> _borrowings = new();
    private readonly List<Borrowing> _newBorrowings = new();

    private List<Equipment> _stagedEquipment = new();
    private List<Borrowing> _stagedBorrowings = new();

    public FakeBorrowingUnitOfWork(FakeDatabase db)
    {
        _db = db;
    }

    public Task BeginAsync(CancellationToken cancellationToken = default)
    {
        if (_db.FailOnBegin) throw new IOException("Simulated: the database cannot be reached.");
        return Task.CompletedTask;
    }

    public Task<Student?> GetStudentForUpdateAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var row = _db.Students.SingleOrDefault(s => s.Id == studentId);
        if (row is null) return Task.FromResult<Student?>(null);

        if (!_students.TryGetValue(studentId, out var tracked))
        {
            tracked = row.Clone();
            _students[studentId] = tracked;
        }
        return Task.FromResult<Student?>(tracked);
    }

    public Task<IReadOnlyList<Equipment>> GetEquipmentForUpdateAsync(IReadOnlyCollection<int> equipmentIds, CancellationToken cancellationToken = default)
    {
        var result = new List<Equipment>();
        foreach (var row in _db.Equipment.Where(e => equipmentIds.Contains(e.Id)))
        {
            if (!_equipment.TryGetValue(row.Id, out var tracked))
            {
                tracked = row.Clone();
                _equipment[row.Id] = tracked;
            }
            result.Add(tracked);
        }
        return Task.FromResult<IReadOnlyList<Equipment>>(result);
    }

    public Task<Borrowing?> GetBorrowingForUpdateAsync(int borrowingId, CancellationToken cancellationToken = default)
    {
        var row = _db.Borrowings.SingleOrDefault(b => b.Id == borrowingId);
        if (row is null) return Task.FromResult<Borrowing?>(null);

        if (!_borrowings.TryGetValue(borrowingId, out var tracked))
        {
            tracked = row.Clone();
            _borrowings[borrowingId] = tracked;
        }
        return Task.FromResult<Borrowing?>(tracked);
    }

    public Task<IReadOnlyCollection<int>> GetEquipmentIdsWithActiveBorrowingAsync(IReadOnlyCollection<int> equipmentIds, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<int> ids = _db.Borrowings
            .Where(b => b.Status == BorrowingStatus.Active && equipmentIds.Contains(b.EquipmentId))
            .Select(b => b.EquipmentId)
            .Distinct()
            .ToList();
        return Task.FromResult(ids);
    }

    public Task<int> CountActiveBorrowingsAsync(int studentId, CancellationToken cancellationToken = default)
        => Task.FromResult(_db.ActiveCountFor(studentId));

    public void AddBorrowing(Borrowing borrowing) => _newBorrowings.Add(borrowing);

    // "Sends" the changes inside the open transaction. Nothing is permanent yet.
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_db.ConflictOnSave)
            throw new ConflictException("Simulated: the database refused the change.", new Exception("unique index"));
        if (_db.FailOnSave)
            throw new IOException("Simulated: a write failed inside the transaction.");

        foreach (var borrowing in _newBorrowings.Where(b => b.Id == 0))
        {
            borrowing.WithId(_db.NextBorrowingId());
        }

        _stagedEquipment = _equipment.Values.Select(e => e.Clone()).ToList();
        _stagedBorrowings = _borrowings.Values.Concat(_newBorrowings).Select(b => b.Clone()).ToList();
        return Task.CompletedTask;
    }

    // Makes the staged changes permanent in the fake database.
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_db.FailOnCommit) throw new IOException("Simulated: the commit failed.");

        foreach (var item in _stagedEquipment)
        {
            var index = _db.Equipment.FindIndex(e => e.Id == item.Id);
            if (index >= 0) _db.Equipment[index] = item;
        }

        foreach (var borrowing in _stagedBorrowings)
        {
            var index = _db.Borrowings.FindIndex(b => b.Id == borrowing.Id);
            if (index >= 0) _db.Borrowings[index] = borrowing;
            else _db.Borrowings.Add(borrowing);
        }

        return Task.CompletedTask;
    }

    // Disposing without a commit throws everything away, like a rollback.
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
