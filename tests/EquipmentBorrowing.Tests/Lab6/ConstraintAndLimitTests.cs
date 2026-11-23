using System.Net;
using Npgsql;

namespace EquipmentBorrowing.Tests.Lab6;

// PostgreSQL integrity scenarios (E01, E02, E05). Real HTTP pipeline + real PostgreSQL. Direct SQL goes to the
// disposable test database only (Lab6Settings refuses any database whose name lacks "test").
//
// SqlState codes: 23505 unique_violation, 23514 check_violation, 23503 foreign_key_violation.

internal static class DbReject
{
    // Runs a statement that PostgreSQL must refuse and returns the server's exception for inspection.
    public static async Task<PostgresException> ExpectAsync(string sql, params (string Name, object? Value)[] p) =>
        await Assert.ThrowsAsync<PostgresException>(() => Pg.ExecAsync(sql, p));
}

// ---------------------------------------------------------------------------------------------
// TC6-E01 Normalized uniqueness (UC-12,13,15,16; BR-01,03,05,07,16; API-04,05,08,09)
// API level: case and whitespace variants are the SAME identifier (409). Database level: the unique indexes reject
// a direct duplicate. Note: PostgreSQL cannot normalize text by itself; the application computes the normalized
// key and the index guarantees that key is unique. The API tests prove the normalization, the direct inserts
// prove the constraint exists.
// ---------------------------------------------------------------------------------------------
public class TC6_E01_NormalizedUniquenessTests : ApiTestBase
{
    public TC6_E01_NormalizedUniquenessTests(ApiFixture api) : base(api) { }

    // ---- students (API-04, API-05) ------------------------------------------------------------
    [Theory]
    [InlineData(" st-100 ")]
    [InlineData("ST-100")]
    [InlineData("St-100")]
    [InlineData("  ST-100")]
    public async Task Registering_a_variant_of_an_existing_student_number_returns_409_and_changes_nothing(string number)
    {
        var before = await DbSnapshot.TakeAsync();

        var result = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/students",
            new { studentNumber = number, fullName = "Duplicate Person" });

        Assert.Equal(HttpStatusCode.Conflict, result.Status);
        Assert.Equal("conflict", result.ErrorCode);
        Assert.Equal(4, await Pg.CountAsync("Students"));
        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task Updating_another_student_to_a_variant_of_ST_100_returns_409_and_leaves_both_rows_unchanged()
    {
        var before = await DbSnapshot.TakeAsync();

        var result = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/students/{Ids.S(4)}",
            new { studentNumber = " st-100 ", fullName = "Dani Ong", contact = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, result.Status);
        Assert.Equal("conflict", result.ErrorCode);
        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task A_student_may_resubmit_their_own_number_in_a_different_case_and_spacing()
    {
        var result = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/students/{Ids.S(1)}",
            new { studentNumber = " st-100 ", fullName = "Ada Cruz", contact = (string?)null });

        Assert.Equal(HttpStatusCode.OK, result.Status); // not a duplicate of ANOTHER student
        Assert.Equal(Ids.S(1), result.Json<StudentRow>().Id);
        Assert.Equal(4, await Pg.CountAsync("Students"));
    }

    [Fact]
    public async Task A_padded_new_student_number_is_stored_normalized_and_then_blocks_its_own_case_variant()
    {
        var created = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/students",
            new { studentNumber = "  ST-500  ", fullName = "Eli Tan" });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var id = created.Json<StudentRow>().Id;

        var db = Assert.Single(await Pg.RowsAsync(
            "SELECT \"StudentNumber\", \"StudentNumberKey\" FROM \"Students\" WHERE \"Id\" = @i", ("i", id)));
        var number = (string)db["StudentNumber"]!;
        var key = (string)db["StudentNumberKey"]!;
        Assert.Equal(number.Trim(), number); // no stored surrounding spaces
        Assert.Equal(key.Trim(), key);
        Assert.Equal("ST-500", key, ignoreCase: true); // the key is the case-insensitive form

        var variant = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/students",
            new { studentNumber = "st-500", fullName = "Someone Else" });
        Assert.Equal(HttpStatusCode.Conflict, variant.Status);
        Assert.Equal(5, await Pg.CountAsync("Students"));
    }

    // ---- equipment (API-08, API-09) -----------------------------------------------------------
    [Theory]
    [InlineData(" eq-001 ")]
    [InlineData("EQ-001")]
    [InlineData("Eq-001")]
    [InlineData("  EQ-001")]
    public async Task Registering_a_variant_of_an_existing_equipment_code_returns_409_and_changes_nothing(string code)
    {
        var before = await DbSnapshot.TakeAsync();

        var result = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/equipment",
            new { equipmentCode = code, name = "Another Camera", description = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, result.Status);
        Assert.Equal("conflict", result.ErrorCode);
        Assert.Equal(7, await Pg.CountAsync("Equipment"));
        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task Updating_another_item_to_a_variant_of_EQ_001_returns_409_and_leaves_both_rows_unchanged()
    {
        var before = await DbSnapshot.TakeAsync();

        var result = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/equipment/{Ids.E(2)}",
            new { equipmentCode = " eq-001 ", name = "Laptop", description = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, result.Status);
        Assert.Equal("conflict", result.ErrorCode);
        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task An_item_may_resubmit_its_own_code_in_a_different_case_and_spacing()
    {
        var result = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/equipment/{Ids.E(1)}",
            new { equipmentCode = " eq-001 ", name = "Camera", description = (string?)null });

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.Equal(Ids.E(1), result.Json<EquipmentRow>().Id);
        Assert.False(result.Json<EquipmentRow>().IsAvailable);
        Assert.Equal(7, await Pg.CountAsync("Equipment"));
    }

    [Fact]
    public async Task A_padded_new_equipment_code_is_stored_normalized_and_then_blocks_its_own_case_variant()
    {
        var created = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/equipment",
            new { equipmentCode = "  EQ-008  ", name = "Charger", description = (string?)null });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var id = created.Json<EquipmentRow>().Id;

        var db = Assert.Single(await Pg.RowsAsync(
            "SELECT \"Identifier\", \"IdentifierKey\" FROM \"Equipment\" WHERE \"Id\" = @i", ("i", id)));
        var code = (string)db["Identifier"]!;
        var key = (string)db["IdentifierKey"]!;
        Assert.Equal(code.Trim(), code);
        Assert.Equal(key.Trim(), key);
        Assert.Equal("EQ-008", key, ignoreCase: true);

        var variant = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/equipment",
            new { equipmentCode = "eq-008", name = "Other", description = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, variant.Status);
        Assert.Equal(8, await Pg.CountAsync("Equipment"));
    }

    // ---- the database itself rejects bypass (direct SQL, no API) ---------------------------------
    [Fact]
    public async Task PostgreSQL_rejects_a_direct_duplicate_student_key_with_a_unique_violation()
    {
        var before = await DbSnapshot.TakeAsync();

        var ex = await DbReject.ExpectAsync(
            "INSERT INTO \"Students\" (\"StudentNumber\", \"StudentNumberKey\", \"Name\", \"Contact\", \"IsAllowedToBorrow\") " +
            "SELECT 'st-100', \"StudentNumberKey\", 'Duplicate Person', NULL, true FROM \"Students\" WHERE \"Id\" = @id",
            ("id", Ids.S(1)));

        Assert.Equal("23505", ex.SqlState);
        Assert.Equal("UX_Students_StudentNumberKey", ex.ConstraintName);
        Assert.Equal(4, await Pg.CountAsync("Students"));
        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task PostgreSQL_rejects_a_direct_duplicate_equipment_key_with_a_unique_violation()
    {
        var before = await DbSnapshot.TakeAsync();

        var ex = await DbReject.ExpectAsync(
            "INSERT INTO \"Equipment\" (\"Identifier\", \"IdentifierKey\", \"Name\", \"Description\", \"IsAvailable\") " +
            "SELECT 'eq-001', \"IdentifierKey\", 'Another Camera', NULL, true FROM \"Equipment\" WHERE \"Id\" = @id",
            ("id", Ids.E(1)));

        Assert.Equal("23505", ex.SqlState);
        Assert.Equal("UX_Equipment_IdentifierKey", ex.ConstraintName);
        Assert.Equal(7, await Pg.CountAsync("Equipment"));
        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }
}

// ---------------------------------------------------------------------------------------------
// Relational integrity at the database (supports the R3 "limits and relational integrity" criterion and the
// "no invalid change" part of TC6-E01): CHECK constraints and foreign keys refuse bad rows even if the API is bypassed.
// ---------------------------------------------------------------------------------------------
public class TC6_E01_DatabaseIntegrityTests : ApiTestBase
{
    public TC6_E01_DatabaseIntegrityTests(ApiFixture api) : base(api) { }

    private const string InsertBorrowing =
        "INSERT INTO \"Borrowings\" (\"StudentId\", \"EquipmentId\", \"DateBorrowed\", \"ExpectedReturnDate\", \"ReturnedDate\", \"Status\") " +
        "VALUES (@s, @e, @b, @x, @r, 1)"; // Status 1 (not Active) so only the constraint under test can fire

    [Fact]
    public async Task Expected_return_date_not_after_the_checkout_date_is_refused_by_a_CHECK_constraint()
    {
        var before = await DbSnapshot.TakeAsync();

        var ex = await DbReject.ExpectAsync(InsertBorrowing,
            ("s", Ids.S(4)), ("e", Ids.E(2)), ("b", Dates.On(0)), ("x", Dates.On(0)), ("r", null));

        Assert.Equal("23514", ex.SqlState);
        Assert.Equal("CK_Borrowings_ExpectedAfterCheckout", ex.ConstraintName);
        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task A_return_date_before_the_checkout_date_is_refused_by_a_CHECK_constraint()
    {
        var before = await DbSnapshot.TakeAsync();

        var ex = await DbReject.ExpectAsync(InsertBorrowing,
            ("s", Ids.S(4)), ("e", Ids.E(2)), ("b", Dates.On(0)), ("x", Dates.On(+2)), ("r", Dates.On(-1)));

        Assert.Equal("23514", ex.SqlState);
        Assert.Equal("CK_Borrowings_ReturnNotBeforeCheckout", ex.ConstraintName);
        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task A_borrowing_for_a_student_or_item_that_does_not_exist_is_refused_by_a_foreign_key()
    {
        var before = await DbSnapshot.TakeAsync();

        var noStudent = await DbReject.ExpectAsync(InsertBorrowing,
            ("s", 999_999), ("e", Ids.E(2)), ("b", Dates.On(-3)), ("x", Dates.On(+2)), ("r", null));
        Assert.Equal("23503", noStudent.SqlState);

        var noItem = await DbReject.ExpectAsync(InsertBorrowing,
            ("s", Ids.S(4)), ("e", 999_999), ("b", Dates.On(-3)), ("x", Dates.On(+2)), ("r", null));
        Assert.Equal("23503", noItem.SqlState);

        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task A_student_or_item_that_has_borrowing_history_cannot_be_deleted()
    {
        var before = await DbSnapshot.TakeAsync();

        // The schema uses ON DELETE RESTRICT (AppDbContext: DeleteBehavior.Restrict). PostgreSQL reports that rule as
        // 23001 restrict_violation (23503 foreign_key_violation is what the default NO ACTION rule reports).
        var student = await DbReject.ExpectAsync("DELETE FROM \"Students\" WHERE \"Id\" = @i", ("i", Ids.S(1)));
        Assert.Equal("23001", student.SqlState); // history protected (ON DELETE RESTRICT)

        var item = await DbReject.ExpectAsync("DELETE FROM \"Equipment\" WHERE \"Id\" = @i", ("i", Ids.E(3)));
        Assert.Equal("23001", item.SqlState); // E3 has a returned loan: history still protected

        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-E02 Limit boundaries (UC-03,06; BR-08-12,15; API-10,11). Each subcase is its own test, so each
// starts from a freshly reset fixture. The limit is three active items per student.
// ---------------------------------------------------------------------------------------------
public class TC6_E02_LimitBoundaryTests : ApiTestBase
{
    public TC6_E02_LimitBoundaryTests(ApiFixture api) : base(api) { }

    private static Task<long> ActiveCountAsync(int studentId) =>
        Pg.ScalarAsync<long>("SELECT COUNT(*) FROM \"Borrowings\" WHERE \"StudentId\" = @s AND \"Status\" = 0", ("s", studentId));

    private Task<ApiResult> IssueAsync(int student, int item) =>
        Client.SendJsonAsync(HttpMethod.Post, "/api/v1/borrowings",
            new { studentId = student, equipmentId = item, expectedReturnDate = Date(+2) });

    private Task<ApiResult> CheckoutAsync(int student, params int[] items) =>
        Client.SendJsonAsync(HttpMethod.Post, "/api/v1/checkout-requests",
            new { studentId = student, equipmentIds = items, expectedReturnDate = Date(+2) });

    [Fact]
    public async Task Subcase_a_S3_with_two_active_items_can_borrow_E2_as_the_third_single_item()
    {
        Assert.Equal(2, await ActiveCountAsync(Ids.S(3)));

        var result = await IssueAsync(Ids.S(3), Ids.E(2)); // API-10

        Assert.Equal(HttpStatusCode.Created, result.Status);
        Assert.Equal(3, await ActiveCountAsync(Ids.S(3))); // exactly three
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(2)));
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(6))); // other item untouched
        Assert.Equal(5, await Pg.CountAsync("Borrowings"));
    }

    [Fact]
    public async Task Subcase_b_S1_with_one_active_item_can_check_out_E2_and_E6_together_to_reach_exactly_three()
    {
        Assert.Equal(1, await ActiveCountAsync(Ids.S(1)));

        var result = await CheckoutAsync(Ids.S(1), Ids.E(2), Ids.E(6)); // API-11

        Assert.Equal(HttpStatusCode.Created, result.Status);
        Assert.Equal(2, result.Json<CheckoutResponse>().Borrowings.Count);
        Assert.Equal(3, await ActiveCountAsync(Ids.S(1)));
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(2)));
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(6)));
        Assert.Equal(6, await Pg.CountAsync("Borrowings"));
    }

    [Fact]
    public async Task Subcase_c_S3_with_two_active_items_cannot_check_out_two_more_and_nothing_is_written()
    {
        var before = await DbSnapshot.TakeAsync();
        Assert.Equal(2, await ActiveCountAsync(Ids.S(3)));

        var result = await CheckoutAsync(Ids.S(3), Ids.E(2), Ids.E(6));

        Assert.Equal(HttpStatusCode.Conflict, result.Status);
        Assert.Equal("conflict", result.ErrorCode);
        Assert.Equal(2, await ActiveCountAsync(Ids.S(3))); // still two
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(2))); // no partial write for E2
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(6)));
        Assert.Equal(4, await Pg.CountAsync("Borrowings"));
        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task A_fourth_single_loan_is_rejected_after_the_third_was_accepted()
    {
        Assert.Equal(HttpStatusCode.Created, (await IssueAsync(Ids.S(3), Ids.E(2))).Status);
        var afterThird = await DbSnapshot.TakeAsync();

        var fourth = await IssueAsync(Ids.S(3), Ids.E(6));

        Assert.Equal(HttpStatusCode.Conflict, fourth.Status);
        Assert.Equal("conflict", fourth.ErrorCode);
        Assert.Equal(3, await ActiveCountAsync(Ids.S(3)));
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(6)));
        Assert.Equal(afterThird, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task A_student_with_no_loans_can_check_out_exactly_three_items_in_one_request()
    {
        var result = await CheckoutAsync(Ids.S(4), Ids.E(2), Ids.E(6), Ids.E(7));

        Assert.Equal(HttpStatusCode.Created, result.Status);
        Assert.Equal(3, result.Json<CheckoutResponse>().Borrowings.Count);
        Assert.Equal(3, await ActiveCountAsync(Ids.S(4)));
    }

    [Fact]
    public async Task A_four_item_request_is_rejected_as_a_whole_with_no_partial_writes()
    {
        var before = await DbSnapshot.TakeAsync();

        var result = await CheckoutAsync(Ids.S(4), Ids.E(2), Ids.E(3), Ids.E(6), Ids.E(7));

        Assert.Equal(HttpStatusCode.Conflict, result.Status);
        Assert.Equal("conflict", result.ErrorCode);
        Assert.Equal(0, await ActiveCountAsync(Ids.S(4)));
        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-E05 Stale and conflicting state (UC-03,06; BR-10,15,16; API-10,11)
// ---------------------------------------------------------------------------------------------
public class TC6_E05_StaleAndConflictingStateTests : ApiTestBase
{
    public TC6_E05_StaleAndConflictingStateTests(ApiFixture api) : base(api) { }

    private static Task<long> ActiveForItemAsync(int equipmentId) =>
        Pg.ScalarAsync<long>("SELECT COUNT(*) FROM \"Borrowings\" WHERE \"EquipmentId\" = @e AND \"Status\" = 0", ("e", equipmentId));

    [Fact]
    public async Task A_stale_single_request_for_E2_is_rejected_after_another_client_issued_it()
    {
        // Client A observes E2 as available (this is the stale view).
        var observed = (await Client.FetchAsync($"/api/v1/equipment/{Ids.E(2)}")).Json<EquipmentRow>();
        Assert.True(observed.IsAvailable);

        // Another context (client B) issues E2 to S4.
        using var other = Api.CreateClient();
        var first = await other.SendJsonAsync(HttpMethod.Post, "/api/v1/borrowings",
            new { studentId = Ids.S(4), equipmentId = Ids.E(2), expectedReturnDate = Date(+2) });
        Assert.Equal(HttpStatusCode.Created, first.Status);
        var afterFirst = await DbSnapshot.TakeAsync();

        // Client A submits its now-stale request.
        var stale = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/borrowings",
            new { studentId = Ids.S(1), equipmentId = Ids.E(2), expectedReturnDate = Date(+2) });

        Assert.Equal(HttpStatusCode.Conflict, stale.Status);
        Assert.Equal("conflict", stale.ErrorCode);
        Assert.Equal(1, await ActiveForItemAsync(Ids.E(2))); // one active E2 record, S4's
        var holder = Assert.Single(await Pg.RowsAsync(
            "SELECT \"StudentId\" FROM \"Borrowings\" WHERE \"EquipmentId\" = @e AND \"Status\" = 0", ("e", Ids.E(2))));
        Assert.Equal(Ids.S(4), Convert.ToInt32(holder["StudentId"]));
        Assert.Equal(afterFirst, await DbSnapshot.TakeAsync()); // the stale request changed nothing
    }

    [Fact]
    public async Task A_stale_multi_item_request_fails_as_a_whole_and_the_other_item_stays_available()
    {
        using var other = Api.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await other.SendJsonAsync(HttpMethod.Post, "/api/v1/borrowings",
            new { studentId = Ids.S(4), equipmentId = Ids.E(2), expectedReturnDate = Date(+2) })).Status);
        var afterFirst = await DbSnapshot.TakeAsync();

        var stale = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/checkout-requests",
            new { studentId = Ids.S(1), equipmentIds = new[] { Ids.E(2), Ids.E(6) }, expectedReturnDate = Date(+2) });

        Assert.Equal(HttpStatusCode.Conflict, stale.Status);
        Assert.Equal("conflict", stale.ErrorCode);
        Assert.Contains("EQ-002", stale.Body);
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(6))); // the other item: no partial write
        Assert.Equal(1, await ActiveForItemAsync(Ids.E(2)));
        Assert.Equal(afterFirst, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task PostgreSQL_itself_refuses_a_second_active_row_for_the_same_item()
    {
        Assert.Equal(HttpStatusCode.Created, (await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/borrowings",
            new { studentId = Ids.S(4), equipmentId = Ids.E(2), expectedReturnDate = Date(+2) })).Status);
        var before = await DbSnapshot.TakeAsync();

        // Bypass the API: attempt a second ACTIVE (Status = 0) record for E2 directly.
        var ex = await DbReject.ExpectAsync(
            "INSERT INTO \"Borrowings\" (\"StudentId\", \"EquipmentId\", \"DateBorrowed\", \"ExpectedReturnDate\", \"ReturnedDate\", \"Status\") " +
            "VALUES (@s, @e, @b, @x, NULL, 0)",
            ("s", Ids.S(1)), ("e", Ids.E(2)), ("b", Dates.On(0)), ("x", Dates.On(+2)));

        Assert.Equal("23505", ex.SqlState);
        Assert.Equal("UX_Borrowings_OneActivePerEquipment", ex.ConstraintName);
        Assert.Equal(1, await ActiveForItemAsync(Ids.E(2)));
        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    // Two requests race for the same item. The timing is not deterministic, but the invariant is: exactly one
    // wins (201), the other is a 409 conflict (not a 500), and the database holds exactly one active record.
    // Repeated for five fresh rounds. A 500 here would be a defect: the unique index protects the data, but the
    // API should translate the conflict.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Two_simultaneous_requests_for_E2_produce_exactly_one_active_record(int round)
    {
        _ = round; // each round starts from a freshly reset fixture
        using var clientA = Api.CreateClient();
        using var clientB = Api.CreateClient();

        var a = clientA.SendJsonAsync(HttpMethod.Post, "/api/v1/borrowings",
            new { studentId = Ids.S(1), equipmentId = Ids.E(2), expectedReturnDate = Date(+2) });
        var b = clientB.SendJsonAsync(HttpMethod.Post, "/api/v1/borrowings",
            new { studentId = Ids.S(4), equipmentId = Ids.E(2), expectedReturnDate = Date(+2) });
        var results = await Task.WhenAll(a, b);

        var winner = Assert.Single(results, r => r.Status == HttpStatusCode.Created);
        var loser = Assert.Single(results, r => r.Status != HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, loser.Status);
        Assert.Equal("conflict", loser.ErrorCode);

        Assert.Equal(1, await ActiveForItemAsync(Ids.E(2)));
        Assert.Equal(5, await Pg.CountAsync("Borrowings"));
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(2)));
        var holder = Assert.Single(await Pg.RowsAsync(
            "SELECT \"Id\" FROM \"Borrowings\" WHERE \"EquipmentId\" = @e AND \"Status\" = 0", ("e", Ids.E(2))));
        Assert.Equal(winner.Json<BorrowingRow>().Id, Convert.ToInt32(holder["Id"]));
    }
}
