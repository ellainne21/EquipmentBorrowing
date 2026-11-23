using System.Net;
using System.Net.Http.Json;

namespace EquipmentBorrowing.Tests.Lab6;

// Infrastructure checks only. These are NOT the TC6 scenarios; they prove the harness is trustworthy
// (real pipeline, real PostgreSQL, fixed date D, independent fresh-connection reads, per-test reset)
// before the TC6 tests are built on top of it.
public class InfrastructureSmokeTests : ApiTestBase
{
    public InfrastructureSmokeTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task Real_pipeline_returns_the_seven_seeded_items()
    {
        var response = await Client.GetAsync("/api/v1/equipment");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        var items = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(7, items.Count);

        var unavailable = items
            .Where(i => !i.GetProperty("isAvailable").GetBoolean())
            .Select(i => i.GetProperty("equipmentCode").GetString())
            .Order()
            .ToList();
        Assert.Equal(new[] { "EQ-001", "EQ-004", "EQ-005" }, unavailable); // E1, E4, E5
    }

    [Fact]
    public async Task Fresh_connection_sees_the_S1_to_S4_E1_to_E7_fixture_relative_to_D()
    {
        Assert.Equal(4, await Pg.CountAsync("Students"));
        Assert.Equal(7, await Pg.CountAsync("Equipment"));
        Assert.Equal(4, await Pg.CountAsync("Borrowings"));
        Assert.Equal(3, await Pg.ActiveBorrowingCountAsync());

        // E1 active loan: borrowed D-9, expected D-1 (overdue), no return date.
        var e1 = (await Pg.RowsAsync(
            "SELECT \"DateBorrowed\", \"ExpectedReturnDate\", \"ReturnedDate\" FROM \"Borrowings\" WHERE \"EquipmentId\" = @e",
            ("e", Ids.E(1)))).Single();
        Assert.Equal(DateOnly.FromDateTime(D.AddDays(-9)), (DateOnly)e1["DateBorrowed"]!);
        Assert.Equal(DateOnly.FromDateTime(D.AddDays(-1)), (DateOnly)e1["ExpectedReturnDate"]!);
        Assert.Null(e1["ReturnedDate"]);

        // E3 returned loan: borrowed D-8, expected D+2, returned D-2.
        var e3 = (await Pg.RowsAsync(
            "SELECT \"DateBorrowed\", \"ExpectedReturnDate\", \"ReturnedDate\" FROM \"Borrowings\" WHERE \"EquipmentId\" = @e",
            ("e", Ids.E(3)))).Single();
        Assert.Equal(DateOnly.FromDateTime(D.AddDays(-8)), (DateOnly)e3["DateBorrowed"]!);
        Assert.Equal(DateOnly.FromDateTime(D.AddDays(2)), (DateOnly)e3["ExpectedReturnDate"]!);
        Assert.Equal(DateOnly.FromDateTime(D.AddDays(-2)), (DateOnly)e3["ReturnedDate"]!);
    }

    [Fact]
    public async Task Api_uses_the_fixed_date_D_and_a_write_is_visible_on_a_fresh_connection()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/borrowings", new
        {
            studentId = Ids.S(4),
            equipmentId = Ids.E(2),
            expectedReturnDate = Date(+2)
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = await ReadJsonAsync(response);
        Assert.Equal(Date(0), doc.RootElement.GetProperty("borrowedDate").GetString()); // API clock == D

        Assert.Equal(5, await Pg.CountAsync("Borrowings"));
        Assert.False(await Pg.ScalarAsync<bool>(
            "SELECT \"IsAvailable\" FROM \"Equipment\" WHERE \"Id\" = @e", ("e", Ids.E(2))));
    }

    // The next two tests both change data. Each starts from a clean fixture, so they pass in ANY order.
    [Fact]
    public async Task Isolation_A_creates_a_borrowing_and_still_sees_exactly_five()
    {
        await Client.PostAsJsonAsync("/api/v1/borrowings", new { studentId = Ids.S(4), equipmentId = Ids.E(6), expectedReturnDate = Date(+2) });
        Assert.Equal(5, await Pg.CountAsync("Borrowings"));
    }

    [Fact]
    public async Task Isolation_B_creates_a_borrowing_and_still_sees_exactly_five()
    {
        await Client.PostAsJsonAsync("/api/v1/borrowings", new { studentId = Ids.S(4), equipmentId = Ids.E(7), expectedReturnDate = Date(+2) });
        Assert.Equal(5, await Pg.CountAsync("Borrowings"));
    }
}

