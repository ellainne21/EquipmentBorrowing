using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Tests;

// Rules that live inside the domain classes.
public class DomainTests
{
    // ---------- Student ----------

    [Fact]
    public void NewStudent_IsEligible_AndTheNumberIsTrimmedWithAnUppercaseKey_BR01_BR02()
    {
        var student = new Student("  eng-2026-01  ", "  Ella Cabasag ");

        Assert.Equal("eng-2026-01", student.StudentNumber);
        Assert.Equal("ENG-2026-01", student.StudentNumberKey);
        Assert.Equal("Ella Cabasag", student.Name);
        Assert.True(student.IsAllowedToBorrow);
    }

    [Theory]
    [InlineData("", "Ella")]
    [InlineData("   ", "Ella")]
    [InlineData("2026-0001", "")]
    [InlineData("2026-0001", "   ")]
    public void Student_BlankNumberOrName_IsRejected(string number, string name)
    {
        Assert.Throws<ArgumentException>(() => new Student(number, name));
    }

    [Fact]
    public void Student_BlankContactClearsTheContact_BR03()
    {
        var student = new Student("2026-0001", "Alice", "0917 000 0001");

        student.UpdateDetails("2026-0001", "Alice", "   ");

        Assert.Null(student.Contact);
    }

    [Fact]
    public void Student_EligibilityCanBeChangedBothWays_BR04()
    {
        var student = new Student("2026-0001", "Alice");

        student.SetEligibility(false);
        Assert.False(student.IsAllowedToBorrow);

        student.SetEligibility(true);
        Assert.True(student.IsAllowedToBorrow);
    }

    // ---------- Equipment ----------

    [Fact]
    public void NewEquipment_IsAvailable_AndTheIdentifierHasAnUppercaseKey_BR05_BR06()
    {
        var item = new Equipment(" lap-001 ", " Laptop ");

        Assert.Equal("lap-001", item.Identifier);
        Assert.Equal("LAP-001", item.IdentifierKey);
        Assert.True(item.IsAvailable);
    }

    [Theory]
    [InlineData("", "Laptop")]
    [InlineData("LAP-001", "  ")]
    public void Equipment_BlankIdentifierOrName_IsRejected(string identifier, string name)
    {
        Assert.Throws<ArgumentException>(() => new Equipment(identifier, name));
    }

    [Fact]
    public void Equipment_UpdatingDetails_DoesNotChangeAvailability_BR07()
    {
        var item = new Equipment("LAP-001", "Laptop");
        item.MarkAsBorrowed();

        item.UpdateDetails("LAP-001", "Laptop Dell", "15-inch");

        Assert.False(item.IsAvailable); // only borrow and return change availability
    }

    [Fact]
    public void Equipment_BorrowAndReturn_ChangeAvailability()
    {
        var item = new Equipment("LAP-001", "Laptop");

        item.MarkAsBorrowed();
        Assert.False(item.IsAvailable);

        item.MarkAsReturned();
        Assert.True(item.IsAvailable);
    }

    // ---------- Borrowing ----------

    [Fact]
    public void NewBorrowing_IsActive_AndStoresCalendarDatesOnly()
    {
        var borrowing = new Borrowing(1, 2, new DateTime(2026, 11, 16, 14, 30, 0), new DateTime(2026, 11, 23, 9, 0, 0));

        Assert.Equal(BorrowingStatus.Active, borrowing.Status);
        Assert.Equal(new DateTime(2026, 11, 16), borrowing.DateBorrowed);
        Assert.Equal(new DateTime(2026, 11, 23), borrowing.ExpectedReturnDate);
        Assert.Null(borrowing.ReturnedDate);
    }

    [Theory]
    [InlineData(0)]   // same day
    [InlineData(-1)]  // earlier
    public void Borrowing_ExpectedReturnDateMustBeAfterCheckout_BR11(int daysAfterCheckout)
    {
        var checkout = new DateTime(2026, 11, 16);

        Assert.Throws<ArgumentException>(() => new Borrowing(1, 2, checkout, checkout.AddDays(daysAfterCheckout)));
    }

    [Fact]
    public void Borrowing_MarkAsReturned_StoresTheReturnDate_BR13()
    {
        var borrowing = new Borrowing(1, 2, new DateTime(2026, 11, 16), new DateTime(2026, 11, 23));

        borrowing.MarkAsReturned(new DateTime(2026, 11, 20, 10, 0, 0));

        Assert.Equal(BorrowingStatus.Returned, borrowing.Status);
        Assert.Equal(new DateTime(2026, 11, 20), borrowing.ReturnedDate);
    }

    [Fact]
    public void Borrowing_CannotBeReturnedTwice()
    {
        var borrowing = new Borrowing(1, 2, new DateTime(2026, 11, 16), new DateTime(2026, 11, 23));
        borrowing.MarkAsReturned(new DateTime(2026, 11, 20));

        Assert.Throws<InvalidOperationException>(() => borrowing.MarkAsReturned(new DateTime(2026, 11, 21)));
    }

    [Fact]
    public void Borrowing_CannotBeReturnedBeforeItWasCheckedOut()
    {
        var borrowing = new Borrowing(1, 2, new DateTime(2026, 11, 16), new DateTime(2026, 11, 23));

        Assert.Throws<ArgumentException>(() => borrowing.MarkAsReturned(new DateTime(2026, 11, 15)));
    }
}
