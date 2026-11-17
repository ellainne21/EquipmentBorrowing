using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Tests.Fakes;

namespace EquipmentBorrowing.Tests;

// Service-level tests for returning equipment (UC-05).
public class ReturnTests
{
    [Fact]
    public async Task Return_ActiveBorrowing_KeepsTheRecordAndStoresTheActualReturnDate_BR13()
    {
        var h = Harness.WithStandardData();
        var loan = h.Db.AddActiveBorrowing(h.Alice, h.Db.Item(1));
        var recordsBefore = h.Db.Borrowings.Count;

        var result = await h.Returns.ExecuteAsync(loan.Id);

        Assert.Equal(OperationOutcome.Success, result.Outcome);
        Assert.Equal(recordsBefore, h.Db.Borrowings.Count);          // history is kept (BR-14)
        var saved = h.Db.Loan(loan.Id);
        Assert.Equal(BorrowingStatus.Returned, saved.Status);
        Assert.Equal(DateTime.Today, saved.ReturnedDate!.Value);
        Assert.True(h.Db.Item(1).IsAvailable);
    }

    [Fact]
    public async Task Return_SameBorrowingTwice_SecondAttemptIsRejectedAndChangesNothing()
    {
        var h = Harness.WithStandardData();
        var loan = h.Db.AddActiveBorrowing(h.Alice, h.Db.Item(1));

        await h.Returns.ExecuteAsync(loan.Id);
        var afterFirst = h.Db.Snapshot();

        var second = await h.Returns.ExecuteAsync(loan.Id); // a stale list on another screen

        Assert.Equal(OperationOutcome.Rejected, second.Outcome);
        Assert.Contains("already returned", second.Message);
        Assert.Equal(afterFirst, h.Db.Snapshot());
    }

    [Fact]
    public async Task Return_BorrowingThatDoesNotExist_IsRejected()
    {
        var h = Harness.WithStandardData();
        var before = h.Db.Snapshot();

        var result = await h.Returns.ExecuteAsync(999);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Equal(before, h.Db.Snapshot());
    }

    [Fact]
    public async Task Return_ByARestrictedStudent_IsStillAllowed_BR04()
    {
        var h = Harness.WithStandardData();
        var loan = h.Db.AddActiveBorrowing(h.Bob, h.Db.Item(1)); // Bob is restricted

        var result = await h.Returns.ExecuteAsync(loan.Id);

        Assert.Equal(OperationOutcome.Success, result.Outcome);
        Assert.True(h.Db.Item(1).IsAvailable);
    }

    [Fact]
    public async Task Return_WriteFails_RollsBackBothTheBorrowingAndTheEquipment()
    {
        var h = Harness.WithStandardData();
        var loan = h.Db.AddActiveBorrowing(h.Alice, h.Db.Item(1));
        var before = h.Db.Snapshot();
        h.Faults.ThrowAfterWriteNumber = 1;

        var result = await h.Returns.ExecuteAsync(loan.Id);

        Assert.Equal(OperationOutcome.Failed, result.Outcome);
        Assert.Equal(before, h.Db.Snapshot());
        Assert.Equal(BorrowingStatus.Active, h.Db.Loan(loan.Id).Status);
        Assert.False(h.Db.Item(1).IsAvailable);
    }

    [Fact]
    public async Task Return_ConnectionLostAfterCommit_ReportsUnknown_ButTheReturnIsSaved()
    {
        var h = Harness.WithStandardData();
        var loan = h.Db.AddActiveBorrowing(h.Alice, h.Db.Item(1));
        h.Faults.ThrowAfterCommit = true;

        var result = await h.Returns.ExecuteAsync(loan.Id);

        Assert.Equal(OperationOutcome.Unknown, result.Outcome);
        Assert.Equal(BorrowingStatus.Returned, h.Db.Loan(loan.Id).Status);
    }

    [Fact]
    public async Task Return_BorrowingCheckedOutAfterToday_IsRejected()
    {
        var h = Harness.WithStandardData();
        var futureLoan = h.Db.AddBorrowingRaw(
            new Borrowing(1, 1, DateTime.Today.AddDays(2), DateTime.Today.AddDays(5)));
        h.Db.Item(1).MarkAsBorrowed();
        var before = h.Db.Snapshot();

        var result = await h.Returns.ExecuteAsync(futureLoan.Id);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Equal(before, h.Db.Snapshot());
    }
}
