using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Tests.Fakes;

namespace EquipmentBorrowing.Tests;

// Service-level tests for the multi-item checkout (UC-06) and the single checkout (UC-03).
// They use a fake in-memory database, so no PostgreSQL is needed.
public class CheckoutRuleTests
{
    // ---------- Success ----------

    [Fact]
    public async Task Multi_TwoAvailableItems_SavesBothBorrowingsAndMarksBothUnavailable()
    {
        var h = Harness.WithStandardData();

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 2 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Success, result.Outcome);
        Assert.Equal(2, h.Db.ActiveCountFor(1));
        Assert.False(h.Db.Item(1).IsAvailable);
        Assert.False(h.Db.Item(2).IsAvailable);
        Assert.Contains("LAP-001", result.Message);
        Assert.Contains("PRJ-001", result.Message);
    }

    [Fact]
    public async Task Multi_ThreeItems_ExactlyAtTheLimit_IsAllowed()
    {
        var h = Harness.WithStandardData();

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 2, 3 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Success, result.Outcome);
        Assert.Equal(3, h.Db.ActiveCountFor(1));
    }

    [Fact]
    public async Task Multi_StudentWithOneActiveItem_TwoMoreReachTheLimitExactly_IsAllowed()
    {
        var h = Harness.WithStandardData();
        h.Db.AddActiveBorrowing(h.Alice, h.Db.Item(4));

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 2 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Success, result.Outcome);
        Assert.Equal(3, h.Db.ActiveCountFor(1));
    }

    // ---------- Rejections: nothing may change (BR-08 to BR-12) ----------

    [Fact]
    public async Task Multi_OnlyOneItem_IsRejected_BR12()
    {
        var h = Harness.WithStandardData();
        var before = h.Db.Snapshot();

        var result = await h.Multi.ExecuteAsync(1, new[] { 1 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Contains("at least two", result.Message);
        Assert.Equal(before, h.Db.Snapshot());
    }

    [Fact]
    public async Task Multi_NoItems_IsRejected_BR12()
    {
        var h = Harness.WithStandardData();
        var before = h.Db.Snapshot();

        var result = await h.Multi.ExecuteAsync(1, Array.Empty<int>(), Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Equal(before, h.Db.Snapshot());
    }

    [Fact]
    public async Task Multi_DuplicateItem_IsRejectedAndNeverSilentlyFixed_BR12()
    {
        var h = Harness.WithStandardData();
        var before = h.Db.Snapshot();

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 1, 2 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Contains("more than once", result.Message);
        Assert.Equal(before, h.Db.Snapshot());
    }

    [Fact]
    public async Task Multi_FourItemsAtOnce_IsRejected_BR09()
    {
        var h = Harness.WithStandardData();
        var before = h.Db.Snapshot();

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 2, 3, 4 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Equal(before, h.Db.Snapshot());
    }

    [Fact]
    public async Task Multi_ActiveItemsPlusRequestedItemsExceedTheLimit_IsRejected_BR09()
    {
        var h = Harness.WithStandardData();
        h.Db.AddActiveBorrowing(h.Alice, h.Db.Item(4));
        h.Db.AddActiveBorrowing(h.Alice, h.Db.Item(5));
        var before = h.Db.Snapshot();

        // 2 active + 2 requested = 4, over the limit of 3
        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 2 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Contains("limit", result.Message);
        Assert.Equal(before, h.Db.Snapshot());
    }

    [Fact]
    public async Task Multi_RestrictedStudent_IsRejected_BR04()
    {
        var h = Harness.WithStandardData();
        var before = h.Db.Snapshot();

        var result = await h.Multi.ExecuteAsync(2, new[] { 1, 2 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Contains("not allowed to borrow", result.Message);
        Assert.Equal(before, h.Db.Snapshot());
    }

    [Fact]
    public async Task Multi_OneUnavailableItem_RejectsTheWholeCheckout_AllOrNothing()
    {
        var h = Harness.WithStandardData();
        h.Db.Item(2).MarkAsBorrowed(); // PRJ-001 is on loan
        var before = h.Db.Snapshot();

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 2 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Contains("PRJ-001", result.Message);
        Assert.True(h.Db.Item(1).IsAvailable); // the free item was NOT checked out either
        Assert.Equal(before, h.Db.Snapshot());
    }

    [Fact]
    public async Task Multi_ItemWithAnActiveBorrowing_IsRejectedEvenIfItsAvailableFlagIsTrue_BR10()
    {
        var h = Harness.WithStandardData();
        h.Db.AddActiveBorrowing(h.Carla, h.Db.Item(2), markItemUnavailable: false);
        var before = h.Db.Snapshot();

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 2 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Equal(before, h.Db.Snapshot());
    }

    [Theory]
    [InlineData(0)]   // today
    [InlineData(-3)]  // in the past
    public async Task Multi_ExpectedReturnDateNotAfterToday_IsRejected_BR11(int daysFromToday)
    {
        var h = Harness.WithStandardData();
        var before = h.Db.Snapshot();

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 2 }, DateTime.Today.AddDays(daysFromToday));

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Equal(before, h.Db.Snapshot());
    }

    [Fact]
    public async Task Multi_StudentThatNoLongerExists_IsRejected_BR08()
    {
        var h = Harness.WithStandardData();
        var before = h.Db.Snapshot();

        var result = await h.Multi.ExecuteAsync(99, new[] { 1, 2 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Equal(before, h.Db.Snapshot());
    }

    [Fact]
    public async Task Multi_ItemThatNoLongerExists_IsRejected_BR08()
    {
        var h = Harness.WithStandardData();
        var before = h.Db.Snapshot();

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 99 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Equal(before, h.Db.Snapshot());
    }

    // ---------- Failures and rollback (A06, BR-15) ----------

    [Fact]
    public async Task Multi_WriteFailsAfterTheFirstItem_RollsBackEverything()
    {
        var h = Harness.WithStandardData();
        var before = h.Db.Snapshot();
        h.Faults.ThrowAfterWriteNumber = 1; // fail after the first item was written, before commit

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 2 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Failed, result.Outcome);
        Assert.Equal(1, h.Faults.WritesSeen);           // a write really was attempted
        Assert.Equal(before, h.Db.Snapshot());          // but nothing was kept
        Assert.Equal(0, h.Db.ActiveCountFor(1));
        Assert.True(h.Db.Item(1).IsAvailable);
        Assert.True(h.Db.Item(2).IsAvailable);
    }

    [Fact]
    public async Task Multi_DatabaseCannotBeReached_FailsAndChangesNothing()
    {
        var h = Harness.WithStandardData();
        var before = h.Db.Snapshot();
        h.Db.FailOnBegin = true;

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 2 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Failed, result.Outcome);
        Assert.Equal(before, h.Db.Snapshot());
    }

    [Fact]
    public async Task Multi_WriteFails_MessageDoesNotLeakTechnicalDetails_BR18()
    {
        var h = Harness.WithStandardData();
        h.Db.FailOnSave = true;

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 2 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Failed, result.Outcome);
        Assert.DoesNotContain("Simulated", result.Message);
        Assert.DoesNotContain("IOException", result.Message);
    }

    [Fact]
    public async Task Multi_DatabaseRefusesTheWrite_ConcurrentChange_IsRejectedAndNothingIsKept()
    {
        var h = Harness.WithStandardData();
        var before = h.Db.Snapshot();
        h.Db.ConflictOnSave = true; // imitates the unique index catching a second staff member

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 2 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Equal(before, h.Db.Snapshot());
    }

    // ---------- Unknown outcome (A18, BR-18) ----------

    [Fact]
    public async Task Multi_CommitFails_OutcomeIsUnknown_NotSuccess()
    {
        var h = Harness.WithStandardData();
        h.Db.FailOnCommit = true;

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 2 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Unknown, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Contains("not known", result.Message);
    }

    [Fact]
    public async Task Multi_ConnectionLostAfterCommitWasAccepted_ReportsUnknown_ButTheDataIsSaved()
    {
        var h = Harness.WithStandardData();
        h.Faults.ThrowAfterCommit = true;

        var result = await h.Multi.ExecuteAsync(1, new[] { 1, 2 }, Harness.NextWeek);

        // The application cannot know the commit worked, so it must say "unknown"...
        Assert.Equal(OperationOutcome.Unknown, result.Outcome);
        Assert.DoesNotContain("checked out", result.Message);
        Assert.DoesNotContain("rolled back", result.Message);

        // ...but the database really did keep the checkout.
        Assert.Equal(2, h.Db.ActiveCountFor(1));
        Assert.False(h.Db.Item(1).IsAvailable);
    }

    [Fact]
    public async Task Multi_RetryAfterAnUnknownOutcome_DoesNotCreateDuplicates()
    {
        var h = Harness.WithStandardData();
        h.Faults.ThrowAfterCommit = true;
        await h.Multi.ExecuteAsync(1, new[] { 1, 2 }, Harness.NextWeek);

        h.Faults.ThrowAfterCommit = false;
        var retry = await h.Multi.ExecuteAsync(1, new[] { 1, 2 }, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, retry.Outcome); // the items are already on loan
        Assert.Equal(2, h.Db.ActiveCountFor(1));                // still exactly two, not four
    }

    // ---------- Single checkout (UC-03) shares the same rules ----------

    [Fact]
    public async Task Single_AvailableItem_IsCheckedOut()
    {
        var h = Harness.WithStandardData();

        var result = await h.Single.ExecuteAsync(1, 1, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Success, result.Outcome);
        Assert.Equal(1, h.Db.ActiveCountFor(1));
        Assert.False(h.Db.Item(1).IsAvailable);
    }

    [Fact]
    public async Task Single_UnavailableItem_IsRejected()
    {
        var h = Harness.WithStandardData();
        h.Db.Item(1).MarkAsBorrowed();
        var before = h.Db.Snapshot();

        var result = await h.Single.ExecuteAsync(1, 1, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Equal(before, h.Db.Snapshot());
    }

    [Fact]
    public async Task Single_RestrictedStudent_IsRejected()
    {
        var h = Harness.WithStandardData();
        var before = h.Db.Snapshot();

        var result = await h.Single.ExecuteAsync(2, 1, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Equal(before, h.Db.Snapshot());
    }

    [Fact]
    public async Task Single_StudentAlreadyAtTheLimit_IsRejected_BR09()
    {
        var h = Harness.WithStandardData();
        h.Db.AddActiveBorrowing(h.Alice, h.Db.Item(2));
        h.Db.AddActiveBorrowing(h.Alice, h.Db.Item(3));
        h.Db.AddActiveBorrowing(h.Alice, h.Db.Item(4));
        var before = h.Db.Snapshot();

        var result = await h.Single.ExecuteAsync(1, 1, Harness.NextWeek);

        Assert.Equal(OperationOutcome.Rejected, result.Outcome);
        Assert.Equal(3, h.Db.ActiveCountFor(1));
        Assert.Equal(before, h.Db.Snapshot());
    }
}
