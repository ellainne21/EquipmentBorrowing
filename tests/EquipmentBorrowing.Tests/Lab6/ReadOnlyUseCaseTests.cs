using System.Net;
using System.Text.Json;

namespace EquipmentBorrowing.Tests.Lab6;

// Read-only use cases. Every test: real ASP.NET Core pipeline, real PostgreSQL, fixed date D = 2026-11-10,
// fixture reset before each test (ApiTestBase). Where the suite says "No changes", the test takes a
// whole-database snapshot through a fresh connection before and after and compares them.

// ---------------------------------------------------------------------------------------------
// TC6-U01 Catalog (UC-01; BR-05-07,10,19; API-01)
// ---------------------------------------------------------------------------------------------
public class TC6_U01_CatalogTests : ApiTestBase
{
    public TC6_U01_CatalogTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task Catalog_returns_exactly_seven_items_with_ids_names_and_availability()
    {
        var before = await DbSnapshot.TakeAsync();

        var result = await Client.FetchAsync("/api/v1/equipment");

        Assert.Equal(HttpStatusCode.OK, result.Status);
        var items = result.Json<List<EquipmentRow>>();
        Assert.Equal(7, items.Count);
        Assert.Equal(7, items.Select(i => i.Id).Distinct().Count());

        var expected = new (int N, string Code, string Name, bool Available)[]
        {
            (1, "EQ-001", "Camera", false),
            (2, "EQ-002", "Laptop", true),
            (3, "EQ-003", "Projector", true),
            (4, "EQ-004", "Tablet", false),
            (5, "EQ-005", "Microphone", false),
            (6, "EQ-006", "Tripod", true),
            (7, "EQ-007", "Speaker", true),
        };
        foreach (var e in expected)
        {
            var item = Assert.Single(items, i => i.EquipmentCode == e.Code);
            Assert.Equal(Ids.E(e.N), item.Id);
            Assert.Equal(e.Name, item.Name);
            Assert.Equal(e.Available, item.IsAvailable);
        }

        // The API's availability must equal what PostgreSQL stores (fresh connection).
        var db = (await Pg.RowsAsync("SELECT \"Id\", \"IsAvailable\" FROM \"Equipment\""))
            .ToDictionary(r => Convert.ToInt32(r["Id"]), r => (bool)r["IsAvailable"]!);
        foreach (var item in items)
            Assert.Equal(db[item.Id], item.IsAvailable);

        Assert.Equal(before, await DbSnapshot.TakeAsync()); // Expected database state: no changes
    }

    [Fact]
    public async Task Empty_catalog_returns_200_and_an_empty_json_array()
    {
        // Precondition created directly in the disposable test database (Borrowings depends on Equipment).
        await Pg.ExecAsync("TRUNCATE TABLE \"Borrowings\", \"Equipment\" RESTART IDENTITY CASCADE");
        Assert.Equal(0, await Pg.CountAsync("Equipment"));

        var result = await Client.FetchAsync("/api/v1/equipment");

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.StartsWith("[", result.Body.TrimStart());
        Assert.Empty(result.Json<List<EquipmentRow>>());
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-U02 Available filtering (UC-02; BR-10,19; API-01)
// ---------------------------------------------------------------------------------------------
public class TC6_U02_AvailableFilteringTests : ApiTestBase
{
    public TC6_U02_AvailableFilteringTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task Available_true_returns_only_E2_E3_E6_E7_and_excludes_E1_E4_E5()
    {
        var before = await DbSnapshot.TakeAsync();

        var result = await Client.FetchAsync("/api/v1/equipment?available=true");

        Assert.Equal(HttpStatusCode.OK, result.Status);
        var items = result.Json<List<EquipmentRow>>();
        Assert.Equal(new[] { "EQ-002", "EQ-003", "EQ-006", "EQ-007" },
            items.Select(i => i.EquipmentCode).Order().ToArray());
        Assert.All(items, i => Assert.True(i.IsAvailable));
        Assert.Equal(
            new[] { Ids.E(2), Ids.E(3), Ids.E(6), Ids.E(7) }.Order().ToArray(),
            items.Select(i => i.Id).Order().ToArray());

        // Exclusion: the three borrowed items must not appear.
        Assert.DoesNotContain(items, i => i.Id == Ids.E(1));
        Assert.DoesNotContain(items, i => i.Id == Ids.E(4));
        Assert.DoesNotContain(items, i => i.Id == Ids.E(5));

        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    // CHARACTERIZATION TEST (not a TC6-U02 requirement). API-01 defines one filter: ?available=true returns only
    // available items; otherwise all items are returned (EquipmentController: "all equipment, or only the available
    // ones with ?available=true"). available=false is not a filter in the published design, so this test records the
    // observed behavior. Defect log L6-01: first written as "available=false returns only borrowed items", which
    // invented a requirement and failed; reclassified as a test-design error, not an application defect.
    [Fact]
    public async Task Available_false_is_not_a_filter_and_returns_the_whole_catalog()
    {
        var result = await Client.FetchAsync("/api/v1/equipment?available=false");

        Assert.Equal(HttpStatusCode.OK, result.Status);
        var items = result.Json<List<EquipmentRow>>();
        Assert.Equal(7, items.Count);
        Assert.Contains(items, i => i.IsAvailable);
        Assert.Contains(items, i => !i.IsAvailable);
    }

    [Fact]
    public async Task Available_true_with_no_available_items_returns_200_and_an_empty_array()
    {
        // Precondition set directly in the disposable test database: no item is available.
        await Pg.ExecAsync("UPDATE \"Equipment\" SET \"IsAvailable\" = false");
        Assert.Equal(0, await Pg.ScalarAsync<long>("SELECT COUNT(*) FROM \"Equipment\" WHERE \"IsAvailable\""));

        var result = await Client.FetchAsync("/api/v1/equipment?available=true");

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.StartsWith("[", result.Body.TrimStart());
        Assert.Empty(result.Json<List<EquipmentRow>>());
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-U04 Active borrowings (UC-04; BR-14,19; API-14)
// ---------------------------------------------------------------------------------------------
public class TC6_U04_ActiveBorrowingsTests : ApiTestBase
{
    public TC6_U04_ActiveBorrowingsTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task Active_status_returns_exactly_E1_E4_E5_with_correct_students_and_excludes_returned_E3()
    {
        var before = await DbSnapshot.TakeAsync();

        var result = await Client.FetchAsync("/api/v1/borrowings?status=Active");

        Assert.Equal(HttpStatusCode.OK, result.Status);
        var rows = result.Json<List<BorrowingRow>>();
        Assert.Equal(3, rows.Count);

        var expected = new (int E, string Code, string Item, int S, string StudentNumber, string StudentName)[]
        {
            (1, "EQ-001", "Camera", 1, "ST-100", "Ada Cruz"),
            (4, "EQ-004", "Tablet", 3, "ST-300", "Cara Lim"),
            (5, "EQ-005", "Microphone", 3, "ST-300", "Cara Lim"),
        };
        foreach (var e in expected)
        {
            var row = Assert.Single(rows, r => r.EquipmentCode == e.Code);
            Assert.Equal(Ids.E(e.E), row.EquipmentId);
            Assert.Equal(e.Item, row.EquipmentName);
            Assert.Equal(Ids.S(e.S), row.StudentId);
            Assert.Equal(e.StudentNumber, row.StudentNumber);
            Assert.Equal(e.StudentName, row.StudentName);
            Assert.Equal("Active", row.Status);
            Assert.Null(row.ReturnedDate);
        }

        // The returned loan (E3) must not be present.
        Assert.DoesNotContain(rows, r => r.EquipmentId == Ids.E(3));
        Assert.DoesNotContain(rows, r => r.Status == "Returned");

        // Same set of borrowing IDs as PostgreSQL holds for Status = Active.
        var dbActiveIds = (await Pg.RowsAsync("SELECT \"Id\" FROM \"Borrowings\" WHERE \"Status\" = 0 ORDER BY \"Id\""))
            .Select(r => Convert.ToInt32(r["Id"])).ToList();
        Assert.Equal(dbActiveIds, rows.Select(r => r.Id).Order().ToList());

        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-U07 Student history (UC-07; BR-14,19; API-03,15)
// ---------------------------------------------------------------------------------------------
public class TC6_U07_StudentHistoryTests : ApiTestBase
{
    public TC6_U07_StudentHistoryTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task S1_history_shows_active_E1_and_returned_E3_with_associations_dates_and_status()
    {
        var before = await DbSnapshot.TakeAsync();

        var student = await Client.FetchAsync($"/api/v1/students/{Ids.S(1)}"); // API-03
        Assert.Equal(HttpStatusCode.OK, student.Status);
        var s1 = student.Json<StudentRow>();
        Assert.Equal(Ids.S(1), s1.Id);
        Assert.Equal("ST-100", s1.StudentNumber);
        Assert.Equal("Ada Cruz", s1.FullName);
        Assert.True(s1.IsEligible);

        var result = await Client.FetchAsync($"/api/v1/students/{Ids.S(1)}/borrowings"); // API-15
        Assert.Equal(HttpStatusCode.OK, result.Status);
        var rows = result.Json<List<BorrowingRow>>();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal(Ids.S(1), r.StudentId);
            Assert.Equal("ST-100", r.StudentNumber);
            Assert.Equal("Ada Cruz", r.StudentName);
        });

        var active = Assert.Single(rows, r => r.EquipmentId == Ids.E(1));
        Assert.Equal("Active", active.Status);
        Assert.Equal("EQ-001", active.EquipmentCode);
        Assert.Equal(Date(-9), active.BorrowedDate);
        Assert.Equal(Date(-1), active.ExpectedReturnDate);
        Assert.Null(active.ReturnedDate);

        var returned = Assert.Single(rows, r => r.EquipmentId == Ids.E(3));
        Assert.Equal("Returned", returned.Status);
        Assert.Equal("EQ-003", returned.EquipmentCode);
        Assert.Equal(Date(-8), returned.BorrowedDate);
        Assert.Equal(Date(+2), returned.ExpectedReturnDate);
        Assert.Equal(Date(-2), returned.ReturnedDate);

        // Same borrowing IDs PostgreSQL holds for S1 (nothing missing, nothing from another student).
        var dbIds = (await Pg.RowsAsync(
                "SELECT \"Id\" FROM \"Borrowings\" WHERE \"StudentId\" = @s ORDER BY \"Id\"", ("s", Ids.S(1))))
            .Select(r => Convert.ToInt32(r["Id"])).ToList();
        Assert.Equal(dbIds, rows.Select(r => r.Id).Order().ToList());

        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task S3_history_contains_only_S3_records_for_E4_and_E5()
    {
        var result = await Client.FetchAsync($"/api/v1/students/{Ids.S(3)}/borrowings");

        Assert.Equal(HttpStatusCode.OK, result.Status);
        var rows = result.Json<List<BorrowingRow>>();
        Assert.Equal(new[] { Ids.E(4), Ids.E(5) }.Order().ToArray(), rows.Select(r => r.EquipmentId).Order().ToArray());
        Assert.All(rows, r =>
        {
            Assert.Equal(Ids.S(3), r.StudentId);
            Assert.Equal("Active", r.Status);
            Assert.Equal(Date(-7), r.BorrowedDate);
            Assert.Equal(Date(+2), r.ExpectedReturnDate);
        });
    }

    [Fact]
    public async Task Existing_student_without_borrowings_returns_200_and_an_empty_array()
    {
        Assert.Equal(HttpStatusCode.OK, (await Client.FetchAsync($"/api/v1/students/{Ids.S(4)}")).Status);

        var result = await Client.FetchAsync($"/api/v1/students/{Ids.S(4)}/borrowings");

        Assert.Equal(HttpStatusCode.OK, result.Status); // empty is not "missing"
        Assert.StartsWith("[", result.Body.TrimStart());
        Assert.Empty(result.Json<List<BorrowingRow>>());
    }

    [Fact]
    public async Task Missing_student_returns_404_not_found_for_both_the_record_and_the_history()
    {
        var missing = 999_999;

        var student = await Client.FetchAsync($"/api/v1/students/{missing}");
        Assert.Equal(HttpStatusCode.NotFound, student.Status);
        Assert.Equal("not_found", student.ErrorCode);

        var history = await Client.FetchAsync($"/api/v1/students/{missing}/borrowings");
        Assert.Equal(HttpStatusCode.NotFound, history.Status);
        Assert.Equal("not_found", history.ErrorCode);
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-U08 Equipment history (UC-08; BR-14,19; API-07,16)
// ---------------------------------------------------------------------------------------------
public class TC6_U08_EquipmentHistoryTests : ApiTestBase
{
    public TC6_U08_EquipmentHistoryTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task E1_history_is_one_active_record_for_S1()
    {
        var before = await DbSnapshot.TakeAsync();

        var item = await Client.FetchAsync($"/api/v1/equipment/{Ids.E(1)}"); // API-07
        Assert.Equal(HttpStatusCode.OK, item.Status);
        var e1 = item.Json<EquipmentRow>();
        Assert.Equal("EQ-001", e1.EquipmentCode);
        Assert.False(e1.IsAvailable);

        var result = await Client.FetchAsync($"/api/v1/equipment/{Ids.E(1)}/borrowings"); // API-16
        Assert.Equal(HttpStatusCode.OK, result.Status);
        var row = Assert.Single(result.Json<List<BorrowingRow>>());
        Assert.Equal(Ids.E(1), row.EquipmentId);
        Assert.Equal("EQ-001", row.EquipmentCode);
        Assert.Equal("Camera", row.EquipmentName);
        Assert.Equal(Ids.S(1), row.StudentId);
        Assert.Equal("ST-100", row.StudentNumber);
        Assert.Equal("Active", row.Status);
        Assert.Null(row.ReturnedDate);

        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task E3_history_is_one_returned_record_for_S1_with_its_return_date()
    {
        var result = await Client.FetchAsync($"/api/v1/equipment/{Ids.E(3)}/borrowings");

        Assert.Equal(HttpStatusCode.OK, result.Status);
        var row = Assert.Single(result.Json<List<BorrowingRow>>());
        Assert.Equal(Ids.E(3), row.EquipmentId);
        Assert.Equal(Ids.S(1), row.StudentId);
        Assert.Equal("Returned", row.Status);
        Assert.Equal(Date(-2), row.ReturnedDate);
    }

    [Fact]
    public async Task E4_history_belongs_to_S3_not_to_another_student_or_item()
    {
        var result = await Client.FetchAsync($"/api/v1/equipment/{Ids.E(4)}/borrowings");

        Assert.Equal(HttpStatusCode.OK, result.Status);
        var row = Assert.Single(result.Json<List<BorrowingRow>>());
        Assert.Equal(Ids.E(4), row.EquipmentId);
        Assert.Equal(Ids.S(3), row.StudentId);
        Assert.Equal("ST-300", row.StudentNumber);
    }

    [Fact]
    public async Task Existing_item_without_borrowings_returns_200_and_an_empty_array()
    {
        Assert.Equal(HttpStatusCode.OK, (await Client.FetchAsync($"/api/v1/equipment/{Ids.E(2)}")).Status);

        var result = await Client.FetchAsync($"/api/v1/equipment/{Ids.E(2)}/borrowings");

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.StartsWith("[", result.Body.TrimStart());
        Assert.Empty(result.Json<List<BorrowingRow>>());
    }

    [Fact]
    public async Task Missing_item_returns_404_not_found_for_both_the_record_and_the_history()
    {
        var missing = 999_999;

        var item = await Client.FetchAsync($"/api/v1/equipment/{missing}");
        Assert.Equal(HttpStatusCode.NotFound, item.Status);
        Assert.Equal("not_found", item.ErrorCode);

        var history = await Client.FetchAsync($"/api/v1/equipment/{missing}/borrowings");
        Assert.Equal(HttpStatusCode.NotFound, history.Status);
        Assert.Equal("not_found", history.ErrorCode);
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-U09 Search and ordering (UC-09; BR-17,19; API-14)
//
// Fixture borrowings (IDs resolved from the API): E1 S1 active, E3 S1 returned, E4 and E5 S3 active.
// Equal-date ties: borrowedDate D-7 (E4, E5); expectedReturnDate D+2 (E3, E4, E5).
//
// SPEC CHECK NEEDED: the expected tie-break below follows the Lab 5 observed behavior (borrowing ID in the
// SAME direction as the requested sort). Confirm against the Lab 4 UC-09 / BR-17 wording. If the published
// text says "ascending ID" regardless of direction, keep this test failing and record it as a defect;
// if the text is unclear, document the ambiguity (the handout asks for this, not a silent change).
// ---------------------------------------------------------------------------------------------
public class TC6_U09_SearchAndOrderingTests : ApiTestBase
{
    public TC6_U09_SearchAndOrderingTests(ApiFixture api) : base(api) { }

    private static string[] Split(string codes) => codes.Length == 0 ? [] : codes.Split(',');

    private async Task<List<BorrowingRow>> QueryAsync(string query)
    {
        var result = await Client.FetchAsync($"/api/v1/borrowings{query}");
        Assert.Equal(HttpStatusCode.OK, result.Status);
        return result.Json<List<BorrowingRow>>();
    }

    private static List<string> Codes(IEnumerable<BorrowingRow> rows) => rows.Select(r => r.EquipmentCode!).ToList();

    // ---- case-insensitive partial matches, one theory row per searched field -----------------
    [Theory]
    // student name (case-insensitive, partial, surrounding spaces ignored)
    [InlineData("ada", "EQ-001,EQ-003")]
    [InlineData(" aDa ", "EQ-001,EQ-003")]
    [InlineData("CRUZ", "EQ-001,EQ-003")]
    [InlineData("lim", "EQ-004,EQ-005")]
    // student number (full and partial)
    [InlineData("st-100", "EQ-001,EQ-003")]
    [InlineData("ST-3", "EQ-004,EQ-005")]
    // equipment code (UC-09 says each field; confirm the field list in the Lab 4 text)
    [InlineData("eq-003", "EQ-003")]
    [InlineData("eq-00", "EQ-001,EQ-003,EQ-004,EQ-005")]
    // equipment name (case-insensitive partial)
    [InlineData("cam", "EQ-001")]
    [InlineData("PROJEC", "EQ-003")]
    [InlineData("tablet", "EQ-004")]
    [InlineData("phone", "EQ-005")]
    // no match: unknown text, and an item that exists but has no borrowings (E2 Laptop)
    [InlineData("zzzz", "")]
    [InlineData("laptop", "")]
    public async Task Search_matches_case_insensitive_partial_text_in_each_field(string search, string expectedCodes)
    {
        var rows = await QueryAsync($"?search={Uri.EscapeDataString(search)}");

        Assert.Equal(Split(expectedCodes).Order().ToArray(), Codes(rows).Order().ToArray());
    }

    // ---- status filters ------------------------------------------------------------------------
    [Theory]
    [InlineData("Active", "EQ-001,EQ-004,EQ-005")]
    [InlineData("Returned", "EQ-003")]
    [InlineData("All", "EQ-001,EQ-003,EQ-004,EQ-005")]
    public async Task Status_filter_returns_the_correct_set(string status, string expectedCodes)
    {
        var rows = await QueryAsync($"?status={status}");

        Assert.Equal(Split(expectedCodes).Order().ToArray(), Codes(rows).Order().ToArray());
    }

    [Fact]
    public async Task No_criteria_returns_all_four_borrowings_and_a_search_does_not_stick_to_the_next_request()
    {
        Assert.Empty(await QueryAsync("?search=zzzz"));

        var all = await QueryAsync(""); // "reset": no criteria

        Assert.Equal(new[] { "EQ-001", "EQ-003", "EQ-004", "EQ-005" }, Codes(all).Order().ToArray());
    }

    // ---- both sort fields, both directions, equal-date ties broken by borrowing ID ---------------
    [Theory]
    [InlineData("borrowedDate", "asc", "EQ-001,EQ-003,EQ-004,EQ-005")]
    [InlineData("borrowedDate", "desc", "EQ-005,EQ-004,EQ-003,EQ-001")]
    [InlineData("expectedReturnDate", "asc", "EQ-001,EQ-003,EQ-004,EQ-005")]
    [InlineData("expectedReturnDate", "desc", "EQ-005,EQ-004,EQ-003,EQ-001")]
    public async Task Sort_orders_by_the_chosen_date_then_by_borrowing_id(string sort, string direction, string expectedOrder)
    {
        var rows = await QueryAsync($"?sort={sort}&direction={direction}");

        // Exact order, ties included (E4/E5 share borrowedDate; E3/E4/E5 share expectedReturnDate).
        Assert.Equal(Split(expectedOrder), Codes(rows));

        // The primary key really is the requested date (not only the ID).
        var dates = rows.Select(r => sort == "borrowedDate" ? r.BorrowedDate : r.ExpectedReturnDate).ToList();
        var sortedDates = direction == "asc" ? dates.Order().ToList() : dates.OrderDescending().ToList();
        Assert.Equal(sortedDates, dates);
    }

    [Fact]
    public async Task Equal_date_ties_come_back_in_the_same_order_on_every_request()
    {
        var first = Codes(await QueryAsync("?sort=expectedReturnDate&direction=asc"));
        var second = Codes(await QueryAsync("?sort=expectedReturnDate&direction=asc"));
        var third = Codes(await QueryAsync("?sort=expectedReturnDate&direction=asc"));

        Assert.Equal(first, second);
        Assert.Equal(first, third);

        // The tie group (E3, E4, E5) is ordered by the borrowing ID resolved from the API.
        var ids = await Client.BorrowingIdsByEquipmentAsync();
        var rows = await QueryAsync("?sort=expectedReturnDate&direction=asc");
        var tie = rows.Where(r => r.ExpectedReturnDate == Date(+2)).Select(r => r.Id).ToList();
        Assert.Equal(new[] { ids["EQ-003"], ids["EQ-004"], ids["EQ-005"] }.Order().ToList(), tie);
    }

    // ---- combined criteria -------------------------------------------------------------------------
    [Theory]
    [InlineData("?search=cruz&status=Active&sort=borrowedDate&direction=desc", "EQ-001")]
    [InlineData("?search=cruz&status=Returned", "EQ-003")]
    [InlineData("?search=st-300&status=Active&sort=expectedReturnDate&direction=asc", "EQ-004,EQ-005")]
    [InlineData("?search=st-300&status=Active&sort=expectedReturnDate&direction=desc", "EQ-005,EQ-004")]
    [InlineData("?search=lim&status=Returned", "")] // Cara has no returned loan: [] on no matches
    public async Task Combined_search_status_and_sort_apply_together(string query, string expectedOrder)
    {
        var rows = await QueryAsync(query);

        Assert.Equal(Split(expectedOrder), Codes(rows));
    }

    [Fact]
    public async Task Searching_filtering_and_sorting_never_change_the_database()
    {
        var before = await DbSnapshot.TakeAsync();

        await QueryAsync("?search=ada");
        await QueryAsync("?status=Returned");
        await QueryAsync("?sort=borrowedDate&direction=desc");
        await QueryAsync("?search=st-300&status=Active&sort=expectedReturnDate&direction=asc");
        await QueryAsync("?search=zzzz");

        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }
}
