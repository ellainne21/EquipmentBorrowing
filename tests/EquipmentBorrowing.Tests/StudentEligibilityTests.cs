using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Tests.Fakes;

namespace EquipmentBorrowing.Tests;

// A small fake of the student repository, built on the same fake database as the other tests.
internal class FakeStudentRepository : IStudentRepository
{
    private readonly FakeDatabase _db;

    public FakeStudentRepository(FakeDatabase db)
    {
        _db = db;
    }

    public Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(_db.Students.SingleOrDefault(s => s.Id == id));

    public Task<IEnumerable<Student>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Student>>(_db.Students.ToList());

    public Task<IEnumerable<Student>> SearchAsync(string? text, CancellationToken cancellationToken = default)
        => Task.FromResult<IEnumerable<Student>>(_db.Students.ToList());

    public Task<bool> StudentNumberExistsAsync(string studentNumber, int? excludeStudentId, CancellationToken cancellationToken = default)
        => Task.FromResult(_db.Students.Any(s => s.StudentNumber == studentNumber && s.Id != excludeStudentId));

    public Task AddAsync(Student student, CancellationToken cancellationToken = default)
    {
        _db.Students.Add(student);
        return Task.CompletedTask;
    }

    public Task UpdateDetailsAsync(Student student, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    // The student object is changed in place by SetEligibility, so there is nothing more to save here.
    public Task UpdateEligibilityAsync(Student student, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

// Service-level tests for changing borrowing eligibility (UC-14, BR-04).
// They also retest defect F-01: the success message once contained pasted PowerShell text.
public class StudentEligibilityTests
{
    private const string RestrictedMessage =
        "Alice Santos is now restricted from new borrowing. Existing borrowings stay active and can still be returned.";

    [Fact]
    public async Task Restrict_Student_ReturnsTheExactCleanMessage_F01()
    {
        var h = Harness.WithStandardData();
        var service = new ChangeStudentEligibilityService(new FakeStudentRepository(h.Db));

        var result = await service.ExecuteAsync(h.Alice.Id, false);

        Assert.True(result.IsSuccess);
        Assert.Equal(RestrictedMessage, result.Message);
        Assert.False(h.Alice.IsAllowedToBorrow);
    }

    [Fact]
    public async Task Restrict_Student_MessageContainsNoShellText_F01()
    {
        var h = Harness.WithStandardData();
        var service = new ChangeStudentEligibilityService(new FakeStudentRepository(h.Db));

        var result = await service.ExecuteAsync(h.Alice.Id, false);

        Assert.DoesNotContain("cd C:", result.Message);
        Assert.DoesNotContain("git ", result.Message);
        Assert.DoesNotContain("PowerShell", result.Message);
    }

    [Fact]
    public async Task Restrict_Student_KeepsExistingBorrowingsActive_BR04()
    {
        var h = Harness.WithStandardData();
        h.Db.AddActiveBorrowing(h.Alice, h.Db.Item(1));
        var before = h.Db.Snapshot();
        var service = new ChangeStudentEligibilityService(new FakeStudentRepository(h.Db));

        var result = await service.ExecuteAsync(h.Alice.Id, false);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, h.Db.ActiveCountFor(h.Alice.Id));   // the active borrowing stays active
        Assert.Equal(before, h.Db.Snapshot());              // no borrowing or equipment was changed
    }

    [Fact]
    public async Task Permit_RestrictedStudent_Works_AndRestrictingTwiceIsRejected()
    {
        var h = Harness.WithStandardData();
        var service = new ChangeStudentEligibilityService(new FakeStudentRepository(h.Db));

        var again = await service.ExecuteAsync(h.Bob.Id, false);   // Bob is already restricted
        Assert.False(again.IsSuccess);
        Assert.Equal("Bob Reyes is already restricted. No change was made.", again.Message);

        var permit = await service.ExecuteAsync(h.Bob.Id, true);
        Assert.True(permit.IsSuccess);
        Assert.Equal("Bob Reyes is now allowed to borrow.", permit.Message);
        Assert.True(h.Bob.IsAllowedToBorrow);
    }
}