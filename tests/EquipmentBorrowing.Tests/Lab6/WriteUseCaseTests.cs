using System.Net;

namespace EquipmentBorrowing.Tests.Lab6;

// Write use cases. Every test: real ASP.NET Core pipeline, real PostgreSQL, fixed date D = 2026-11-10, fixture reset
// before the test (ApiTestBase). Each HTTP result is confirmed through a brand-new, non-pooled PostgreSQL connection
// (Pg / Fresh), and rows that should NOT change are compared before and after (DbSnapshot).

public sealed record CheckoutResponse(List<BorrowingRow> Borrowings);

internal static class Dates
{
    public static DateOnly On(int offsetFromD) => DateOnly.FromDateTime(Lab6Settings.D.AddDays(offsetFromD));
}

// Small readers over fresh connections. "Status" 0 = Active (see UX_Borrowings_OneActivePerEquipment).
internal static class Fresh
{
    public static async Task<Dictionary<string, object?>> BorrowingAsync(int id) =>
        Assert.Single(await Pg.RowsAsync("SELECT * FROM \"Borrowings\" WHERE \"Id\" = @i", ("i", id)));

    public static Task<bool> IsAvailableAsync(int equipmentId) =>
        Pg.ScalarAsync<bool>("SELECT \"IsAvailable\" FROM \"Equipment\" WHERE \"Id\" = @i", ("i", equipmentId));

    public static Task<bool> IsAllowedAsync(int studentId) =>
        Pg.ScalarAsync<bool>("SELECT \"IsAllowedToBorrow\" FROM \"Students\" WHERE \"Id\" = @i", ("i", studentId));

    public static bool IsActive(Dictionary<string, object?> borrowing) => Convert.ToInt32(borrowing["Status"]) == 0;
}

// ---------------------------------------------------------------------------------------------
// TC6-U03 Single borrowing (UC-03; BR-08-11,14-16,19; API-10,13)
// ---------------------------------------------------------------------------------------------
public class TC6_U03_SingleBorrowingTests : ApiTestBase
{
    public TC6_U03_SingleBorrowingTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task Issue_E2_to_S4_returns_201_with_location_and_persists_one_active_record_and_an_unavailable_item()
    {
        var originalIds = (await Client.BorrowingIdsByEquipmentAsync()).Values.ToList();
        var studentsBefore = await DbSnapshot.TableAsync("Students");
        var equipmentBeforeExceptE2 = await DbSnapshot.TableAsync("Equipment", Ids.E(2));

        var created = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/borrowings",
            new { studentId = Ids.S(4), equipmentId = Ids.E(2), expectedReturnDate = Date(+2) });

        Assert.Equal(HttpStatusCode.Created, created.Status);
        var row = created.Json<BorrowingRow>();
        Assert.True(row.Id > 0);
        Assert.DoesNotContain(row.Id, originalIds); // a new generated ID
        Assert.Equal(Ids.S(4), row.StudentId);
        Assert.Equal("ST-400", row.StudentNumber);
        Assert.Equal(Ids.E(2), row.EquipmentId);
        Assert.Equal("EQ-002", row.EquipmentCode);
        Assert.Equal(Date(0), row.BorrowedDate); // borrowed on D
        Assert.Equal(Date(+2), row.ExpectedReturnDate);
        Assert.Null(row.ReturnedDate);
        Assert.Equal("Active", row.Status);

        // Location header points at the new record, and following it returns the same active record.
        Assert.NotNull(created.Location);
        Assert.Equal($"/api/v1/borrowings/{row.Id}", created.Location!.AbsolutePath);
        var followed = await Client.FetchAsync(created.Location.ToString());
        Assert.Equal(HttpStatusCode.OK, followed.Status);
        Assert.Equal(row, followed.Json<BorrowingRow>());

        // Fresh PostgreSQL connection: exactly one S4/E2 record, active, with the right dates; E2 unavailable.
        var db = Assert.Single(await Pg.RowsAsync(
            "SELECT * FROM \"Borrowings\" WHERE \"StudentId\" = @s AND \"EquipmentId\" = @e",
            ("s", Ids.S(4)), ("e", Ids.E(2))));
        Assert.Equal(row.Id, Convert.ToInt32(db["Id"]));
        Assert.Equal(Dates.On(0), (DateOnly)db["DateBorrowed"]!);
        Assert.Equal(Dates.On(+2), (DateOnly)db["ExpectedReturnDate"]!);
        Assert.Null(db["ReturnedDate"]);
        Assert.True(Fresh.IsActive(db));
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(2)));

        // Nothing unrelated changed.
        Assert.Equal(5, await Pg.CountAsync("Borrowings"));
        Assert.Equal(studentsBefore, await DbSnapshot.TableAsync("Students"));
        Assert.Equal(equipmentBeforeExceptE2, await DbSnapshot.TableAsync("Equipment", Ids.E(2)));
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-U05 Return and history (UC-05; BR-10,13-14,19; API-12,13)
// ---------------------------------------------------------------------------------------------
public class TC6_U05_ReturnAndHistoryTests : ApiTestBase
{
    public TC6_U05_ReturnAndHistoryTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task Return_S1_E1_marks_it_Returned_dated_D_keeps_the_same_record_and_a_repeat_returns_409_unchanged()
    {
        var b1 = (await Client.BorrowingIdsByEquipmentAsync())["EQ-001"];

        var first = await Client.SendJsonAsync(HttpMethod.Post, $"/api/v1/borrowings/{b1}/return", new { });

        Assert.Equal(HttpStatusCode.OK, first.Status);
        var returned = first.Json<BorrowingRow>();
        Assert.Equal(b1, returned.Id); // original identity
        Assert.Equal(Ids.S(1), returned.StudentId);
        Assert.Equal(Ids.E(1), returned.EquipmentId);
        Assert.Equal(Date(-9), returned.BorrowedDate);
        Assert.Equal(Date(-1), returned.ExpectedReturnDate);
        Assert.Equal("Returned", returned.Status);
        Assert.Equal(Date(0), returned.ReturnedDate); // returned on D

        var read = await Client.FetchAsync($"/api/v1/borrowings/{b1}"); // API-13
        Assert.Equal(HttpStatusCode.OK, read.Status);
        Assert.Equal(returned, read.Json<BorrowingRow>());

        // Fresh connection: the same record is retained (not deleted), returned on D; E1 is available again.
        var db = await Fresh.BorrowingAsync(b1);
        Assert.False(Fresh.IsActive(db));
        Assert.Equal(Dates.On(0), (DateOnly)db["ReturnedDate"]!);
        Assert.Equal(Dates.On(-9), (DateOnly)db["DateBorrowed"]!);
        Assert.Equal(4, await Pg.CountAsync("Borrowings"));
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(1)));
        Assert.Equal(2, await Pg.ActiveBorrowingCountAsync()); // only E4 and E5 remain active

        // History still lists the returned loan next to the earlier returned E3 loan.
        var history = (await Client.FetchAsync($"/api/v1/students/{Ids.S(1)}/borrowings")).Json<List<BorrowingRow>>();
        Assert.Equal(2, history.Count);
        Assert.All(history, h => Assert.Equal("Returned", h.Status));

        // Repeat return: 409 conflict and nothing changes.
        var afterFirst = await DbSnapshot.TakeAsync();
        var repeat = await Client.SendJsonAsync(HttpMethod.Post, $"/api/v1/borrowings/{b1}/return", new { });
        Assert.Equal(HttpStatusCode.Conflict, repeat.Status);
        Assert.Equal("conflict", repeat.ErrorCode);
        Assert.Equal(afterFirst, await DbSnapshot.TakeAsync());

        var stillReturned = (await Client.FetchAsync($"/api/v1/borrowings/{b1}")).Json<BorrowingRow>();
        Assert.Equal(Date(0), stillReturned.ReturnedDate); // the original return date was not overwritten
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-U06 Atomic multi-item success (UC-06; BR-08-12,14-16,19; API-11)
// ---------------------------------------------------------------------------------------------
public class TC6_U06_MultiItemCheckoutTests : ApiTestBase
{
    public TC6_U06_MultiItemCheckoutTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task Checkout_E2_and_E6_for_S4_creates_two_active_records_and_makes_both_items_unavailable()
    {
        var originalIds = (await Client.BorrowingIdsByEquipmentAsync()).Values.ToList();
        var studentsBefore = await DbSnapshot.TableAsync("Students");
        var originalBorrowingsBefore = await DbSnapshot.TableAsync("Borrowings");

        var result = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/checkout-requests",
            new { studentId = Ids.S(4), equipmentIds = new[] { Ids.E(2), Ids.E(6) }, expectedReturnDate = Date(+2) });

        Assert.Equal(HttpStatusCode.Created, result.Status);
        var rows = result.Json<CheckoutResponse>().Borrowings;
        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows.Select(r => r.Id).Distinct().Count());
        Assert.All(rows.Select(r => r.Id), id => Assert.DoesNotContain(id, originalIds));
        Assert.Equal(new[] { Ids.E(2), Ids.E(6) }.Order().ToArray(), rows.Select(r => r.EquipmentId).Order().ToArray());
        Assert.All(rows, r =>
        {
            Assert.Equal(Ids.S(4), r.StudentId);
            Assert.Equal("ST-400", r.StudentNumber);
            Assert.Equal(Date(0), r.BorrowedDate);
            Assert.Equal(Date(+2), r.ExpectedReturnDate);
            Assert.Null(r.ReturnedDate);
            Assert.Equal("Active", r.Status);
        });
        Assert.Contains(rows, r => r.EquipmentCode == "EQ-002");
        Assert.Contains(rows, r => r.EquipmentCode == "EQ-006");

        // Each record can be read back through API-13.
        foreach (var r in rows)
        {
            var read = await Client.FetchAsync($"/api/v1/borrowings/{r.Id}");
            Assert.Equal(HttpStatusCode.OK, read.Status);
            Assert.Equal(r, read.Json<BorrowingRow>());
        }

        // Fresh connection: two active S4 records with the same IDs; both items unavailable; others as before.
        var dbRows = await Pg.RowsAsync("SELECT * FROM \"Borrowings\" WHERE \"StudentId\" = @s ORDER BY \"Id\"", ("s", Ids.S(4)));
        Assert.Equal(2, dbRows.Count);
        Assert.All(dbRows, d => Assert.True(Fresh.IsActive(d)));
        Assert.Equal(rows.Select(r => r.Id).Order().ToArray(), dbRows.Select(d => Convert.ToInt32(d["Id"])).ToArray());
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(2)));
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(6)));
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(3)));
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(7)));

        Assert.Equal(6, await Pg.CountAsync("Borrowings"));
        Assert.Equal(studentsBefore, await DbSnapshot.TableAsync("Students"));
        // The four original borrowing rows are unchanged (leave out the two new ones and compare).
        Assert.Equal(originalBorrowingsBefore, await DbSnapshot.TableAsync("Borrowings", rows.Select(r => r.Id).ToArray()));
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-U12 Register student (UC-12; BR-01-02,16,19; API-03,04)
// ---------------------------------------------------------------------------------------------
public class TC6_U12_RegisterStudentTests : ApiTestBase
{
    public TC6_U12_RegisterStudentTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task Register_ST_500_returns_201_with_id_location_and_eligible_default_and_persists_one_row()
    {
        var existingIds = new[] { Ids.S(1), Ids.S(2), Ids.S(3), Ids.S(4) };
        var borrowingsBefore = await DbSnapshot.TableAsync("Borrowings");

        var created = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/students",
            new { studentNumber = "ST-500", fullName = "Eli Tan", contact = "09171234567" });

        Assert.Equal(HttpStatusCode.Created, created.Status);
        var student = created.Json<StudentRow>();
        Assert.True(student.Id > 0);
        Assert.DoesNotContain(student.Id, existingIds);
        Assert.Equal("ST-500", student.StudentNumber);
        Assert.Equal("Eli Tan", student.FullName);
        Assert.Equal("09171234567", student.Contact);
        Assert.True(student.IsEligible); // default (BR-02)

        Assert.NotNull(created.Location);
        Assert.Equal($"/api/v1/students/{student.Id}", created.Location!.AbsolutePath);
        var followed = await Client.FetchAsync(created.Location.ToString());
        Assert.Equal(HttpStatusCode.OK, followed.Status);
        Assert.Equal(student, followed.Json<StudentRow>());

        // Fresh connection: one durable student row with the same values.
        var db = Assert.Single(await Pg.RowsAsync(
            "SELECT * FROM \"Students\" WHERE \"StudentNumber\" = @n", ("n", "ST-500")));
        Assert.Equal(student.Id, Convert.ToInt32(db["Id"]));
        Assert.Equal("Eli Tan", (string)db["Name"]!);
        Assert.Equal("09171234567", (string)db["Contact"]!);
        Assert.True((bool)db["IsAllowedToBorrow"]!);
        Assert.Equal(5, await Pg.CountAsync("Students"));
        Assert.Equal(borrowingsBefore, await DbSnapshot.TableAsync("Borrowings"));
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-U13 Update student (UC-13; BR-01-03,14,16,19; API-02,03,05,15)
// ---------------------------------------------------------------------------------------------
public class TC6_U13_UpdateStudentTests : ApiTestBase
{
    public TC6_U13_UpdateStudentTests(ApiFixture api) : base(api) { }

    private static object[] Keys(IEnumerable<BorrowingRow> rows) =>
        rows.OrderBy(r => r.Id)
            .Select(r => (object)(r.Id, r.StudentId, r.EquipmentId, r.BorrowedDate, r.ExpectedReturnDate, r.ReturnedDate, r.Status))
            .ToArray();

    [Fact]
    public async Task Search_then_edit_S1_changes_name_and_contact_but_not_identity_or_borrowing_relationships()
    {
        // API-02: matching and non-matching search.
        var match = await Client.FetchAsync("/api/v1/students?search=ST-100");
        Assert.Equal(HttpStatusCode.OK, match.Status);
        var found = Assert.Single(match.Json<List<StudentRow>>());
        Assert.Equal(Ids.S(1), found.Id);

        var none = await Client.FetchAsync("/api/v1/students?search=ST-999");
        Assert.Equal(HttpStatusCode.OK, none.Status);
        Assert.StartsWith("[", none.Body.TrimStart());
        Assert.Empty(none.Json<List<StudentRow>>());

        // Baseline before the edit.
        var before = (await Client.FetchAsync($"/api/v1/students/{Ids.S(1)}")).Json<StudentRow>(); // API-03
        var historyBefore = (await Client.FetchAsync($"/api/v1/students/{Ids.S(1)}/borrowings")).Json<List<BorrowingRow>>();
        var borrowingsBefore = await DbSnapshot.TableAsync("Borrowings");
        var equipmentBefore = await DbSnapshot.TableAsync("Equipment");
        var otherStudentsBefore = await DbSnapshot.TableAsync("Students", Ids.S(1));

        // API-05: edit name and contact.
        var put = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/students/{Ids.S(1)}",
            new { studentNumber = "ST-100", fullName = "Ada M. Cruz", contact = "09170000001" });

        Assert.Equal(HttpStatusCode.OK, put.Status);
        var updated = put.Json<StudentRow>();
        Assert.Equal(Ids.S(1), updated.Id); // stable identity
        Assert.Equal("ST-100", updated.StudentNumber);
        Assert.Equal("Ada M. Cruz", updated.FullName);
        Assert.Equal("09170000001", updated.Contact);
        Assert.Equal(before.IsEligible, updated.IsEligible);

        var read = (await Client.FetchAsync($"/api/v1/students/{Ids.S(1)}")).Json<StudentRow>();
        Assert.Equal(updated, read);

        // API-15: history unchanged (same IDs, student/equipment foreign keys, dates and status).
        var historyAfter = await Client.FetchAsync($"/api/v1/students/{Ids.S(1)}/borrowings");
        Assert.Equal(HttpStatusCode.OK, historyAfter.Status);
        Assert.Equal(Keys(historyBefore), Keys(historyAfter.Json<List<BorrowingRow>>()));

        // Fresh connection.
        var db = Assert.Single(await Pg.RowsAsync("SELECT * FROM \"Students\" WHERE \"Id\" = @i", ("i", Ids.S(1))));
        Assert.Equal("Ada M. Cruz", (string)db["Name"]!);
        Assert.Equal("09170000001", (string)db["Contact"]!);
        Assert.Equal(borrowingsBefore, await DbSnapshot.TableAsync("Borrowings")); // active/returned foreign keys intact
        Assert.Equal(equipmentBefore, await DbSnapshot.TableAsync("Equipment"));
        Assert.Equal(otherStudentsBefore, await DbSnapshot.TableAsync("Students", Ids.S(1)));
        Assert.Equal(4, await Pg.CountAsync("Students"));
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-U14 Eligibility transitions (UC-14; BR-04,08,13-14,19; API-06,10,12)
// ---------------------------------------------------------------------------------------------
public class TC6_U14_EligibilityTransitionsTests : ApiTestBase
{
    public TC6_U14_EligibilityTransitionsTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task Restriction_blocks_a_new_loan_not_the_return_and_permitting_restores_issuing()
    {
        var b1 = (await Client.BorrowingIdsByEquipmentAsync())["EQ-001"];

        // 1. Restrict S1 (API-06). The original loan survives.
        var restrict = await Client.SendJsonAsync(HttpMethod.Patch, $"/api/v1/students/{Ids.S(1)}/eligibility", new { isEligible = false });
        Assert.Equal(HttpStatusCode.OK, restrict.Status);
        Assert.False(restrict.Json<StudentRow>().IsEligible);
        Assert.False(await Fresh.IsAllowedAsync(Ids.S(1)));
        var loan = await Fresh.BorrowingAsync(b1);
        Assert.True(Fresh.IsActive(loan));
        Assert.Null(loan["ReturnedDate"]);
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(1)));

        // 2. A new issue is rejected with 409 and writes nothing (API-10).
        var rejected = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/borrowings",
            new { studentId = Ids.S(1), equipmentId = Ids.E(2), expectedReturnDate = Date(+2) });
        Assert.Equal(HttpStatusCode.Conflict, rejected.Status);
        Assert.Equal("conflict", rejected.ErrorCode);
        Assert.Equal(4, await Pg.CountAsync("Borrowings"));
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(2)));

        // 3. The return still works while restricted (API-12); history survives.
        var returned = await Client.SendJsonAsync(HttpMethod.Post, $"/api/v1/borrowings/{b1}/return", new { });
        Assert.Equal(HttpStatusCode.OK, returned.Status);
        var returnedRow = returned.Json<BorrowingRow>();
        Assert.Equal("Returned", returnedRow.Status);
        Assert.Equal(Date(0), returnedRow.ReturnedDate);
        var afterReturn = await Fresh.BorrowingAsync(b1);
        Assert.False(Fresh.IsActive(afterReturn));
        Assert.Equal(Dates.On(0), (DateOnly)afterReturn["ReturnedDate"]!);
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(1)));
        Assert.Equal(4, await Pg.CountAsync("Borrowings"));
        Assert.False(await Fresh.IsAllowedAsync(Ids.S(1))); // still restricted

        // 4. Permit S1 again.
        var permit = await Client.SendJsonAsync(HttpMethod.Patch, $"/api/v1/students/{Ids.S(1)}/eligibility", new { isEligible = true });
        Assert.Equal(HttpStatusCode.OK, permit.Status);
        Assert.True(permit.Json<StudentRow>().IsEligible);
        Assert.True(await Fresh.IsAllowedAsync(Ids.S(1)));

        // 5. Issuing works again (201); the earlier history is still there.
        var issued = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/borrowings",
            new { studentId = Ids.S(1), equipmentId = Ids.E(2), expectedReturnDate = Date(+2) });
        Assert.Equal(HttpStatusCode.Created, issued.Status);
        var issuedRow = issued.Json<BorrowingRow>();
        Assert.Equal(Ids.S(1), issuedRow.StudentId);
        Assert.Equal(Ids.E(2), issuedRow.EquipmentId);
        Assert.Equal("Active", issuedRow.Status);
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(2)));
        Assert.Equal(5, await Pg.CountAsync("Borrowings"));

        var history = (await Client.FetchAsync($"/api/v1/students/{Ids.S(1)}/borrowings")).Json<List<BorrowingRow>>();
        Assert.Equal(3, history.Count);
        Assert.Contains(history, h => h.Id == b1 && h.Status == "Returned");
        Assert.Contains(history, h => h.EquipmentId == Ids.E(3) && h.Status == "Returned");
        Assert.Contains(history, h => h.Id == issuedRow.Id && h.Status == "Active");
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-U15 Register equipment (UC-15; BR-05-06,10,16,19; API-07,08)
// ---------------------------------------------------------------------------------------------
public class TC6_U15_RegisterEquipmentTests : ApiTestBase
{
    public TC6_U15_RegisterEquipmentTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task Register_EQ_008_returns_201_available_and_a_duplicate_EQ_001_is_rejected_with_409()
    {
        var existing = Enumerable.Range(1, 7).Select(n => Ids.E(n)).ToList();
        var equipmentBefore = await DbSnapshot.TableAsync("Equipment");

        var created = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/equipment",
            new { equipmentCode = "EQ-008", name = "Charger", description = (string?)null });

        Assert.Equal(HttpStatusCode.Created, created.Status);
        var item = created.Json<EquipmentRow>();
        Assert.True(item.Id > 0);
        Assert.DoesNotContain(item.Id, existing);
        Assert.Equal("EQ-008", item.EquipmentCode);
        Assert.Equal("Charger", item.Name);
        Assert.Null(item.Description);
        Assert.True(item.IsAvailable); // default (BR-06)

        Assert.NotNull(created.Location);
        Assert.Equal($"/api/v1/equipment/{item.Id}", created.Location!.AbsolutePath);
        var followed = await Client.FetchAsync(created.Location.ToString()); // API-07
        Assert.Equal(HttpStatusCode.OK, followed.Status);
        Assert.Equal(item, followed.Json<EquipmentRow>());

        // Fresh connection: one new available row.
        var db = Assert.Single(await Pg.RowsAsync("SELECT * FROM \"Equipment\" WHERE \"Identifier\" = @c", ("c", "EQ-008")));
        Assert.Equal(item.Id, Convert.ToInt32(db["Id"]));
        Assert.True((bool)db["IsAvailable"]!);
        Assert.Equal(8, await Pg.CountAsync("Equipment"));
        Assert.Equal(equipmentBefore, await DbSnapshot.TableAsync("Equipment", item.Id)); // the seven others unchanged

        // Duplicate identifier: 409 conflict, and still exactly one EQ-001.
        var duplicate = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/equipment",
            new { equipmentCode = "EQ-001", name = "Another Camera", description = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.Status);
        Assert.Equal("conflict", duplicate.ErrorCode);
        Assert.Equal(8, await Pg.CountAsync("Equipment"));
        Assert.Equal(1, await Pg.ScalarAsync<long>(
            "SELECT COUNT(*) FROM \"Equipment\" WHERE LOWER(TRIM(\"Identifier\")) = 'eq-001'"));
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-U16 Update equipment (UC-16; BR-05-07,10,14,16,19; API-07,09,16)
// ---------------------------------------------------------------------------------------------
public class TC6_U16_UpdateEquipmentTests : ApiTestBase
{
    public TC6_U16_UpdateEquipmentTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task Editing_the_E1_description_keeps_its_identity_unavailable_state_and_active_history()
    {
        var b1 = (await Client.BorrowingIdsByEquipmentAsync())["EQ-001"];
        var borrowingsBefore = await DbSnapshot.TableAsync("Borrowings");
        var studentsBefore = await DbSnapshot.TableAsync("Students");
        var otherEquipmentBefore = await DbSnapshot.TableAsync("Equipment", Ids.E(1));

        var put = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/equipment/{Ids.E(1)}",
            new { equipmentCode = "EQ-001", name = "Camera", description = "Black body, 50mm lens" });

        Assert.Equal(HttpStatusCode.OK, put.Status);
        var updated = put.Json<EquipmentRow>();
        Assert.Equal(Ids.E(1), updated.Id); // stable identity
        Assert.Equal("EQ-001", updated.EquipmentCode);
        Assert.Equal("Camera", updated.Name);
        Assert.Equal("Black body, 50mm lens", updated.Description);
        Assert.False(updated.IsAvailable); // availability untouched by a descriptive edit

        var read = (await Client.FetchAsync($"/api/v1/equipment/{Ids.E(1)}")).Json<EquipmentRow>(); // API-07
        Assert.Equal(updated, read);

        // API-16: the original active record is still there.
        var history = await Client.FetchAsync($"/api/v1/equipment/{Ids.E(1)}/borrowings");
        Assert.Equal(HttpStatusCode.OK, history.Status);
        var rec = Assert.Single(history.Json<List<BorrowingRow>>());
        Assert.Equal(b1, rec.Id);
        Assert.Equal(Ids.S(1), rec.StudentId);
        Assert.Equal(Ids.E(1), rec.EquipmentId);
        Assert.Equal("Active", rec.Status);
        Assert.Equal(Date(-9), rec.BorrowedDate);

        // Fresh connection.
        var db = Assert.Single(await Pg.RowsAsync("SELECT * FROM \"Equipment\" WHERE \"Id\" = @i", ("i", Ids.E(1))));
        Assert.Equal("Black body, 50mm lens", (string)db["Description"]!);
        Assert.False((bool)db["IsAvailable"]!);
        Assert.Equal(borrowingsBefore, await DbSnapshot.TableAsync("Borrowings")); // relationships and history intact
        Assert.Equal(studentsBefore, await DbSnapshot.TableAsync("Students"));
        Assert.Equal(otherEquipmentBefore, await DbSnapshot.TableAsync("Equipment", Ids.E(1)));
        Assert.Equal(7, await Pg.CountAsync("Equipment"));
    }
}
