using System.Net;
using System.Text;
using System.Text.Json;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Infrastructure.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EquipmentBorrowing.Tests.Lab6;

// ---------------------------------------------------------------------------------------------
// Shared: "safe error body" = exactly { code, message }, the expected code, and nothing that leaks internals.
// ---------------------------------------------------------------------------------------------
internal static class ErrorBody
{
    private static readonly string[] Leaks =
    {
        "Exception", "StackTrace", "   at ", "EquipmentBorrowing.", "Npgsql", "Microsoft.", "System.",
        "SELECT ", "INSERT ", "UPDATE ", "DELETE ", "Host=", "Server=", "Password", "Username", "SqlState"
    };

    public static void AssertSafe(ApiResult result, string expectedCode)
    {
        using var doc = JsonDocument.Parse(result.Body); // the body must be JSON, not an HTML or plain-text error page
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(new[] { "code", "message" }, root.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal(expectedCode, root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
        foreach (var leak in Leaks)
            Assert.DoesNotContain(leak, result.Body, StringComparison.OrdinalIgnoreCase);
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-E03 Protocol errors (UC-03,06,09,12,15; BR-08-12,17-18; API-04,08,10,11,14)
// Each row: status, safe error body, and a whole-database snapshot identical before and after (fresh connection).
// Tokens in url/body are resolved at run time from the API: {S1}..{S4}, {E1}..{E7}, {BE1} {BE3} (borrowing IDs of
// E1 and E3), {D} {D-1} {D+2} (dates relative to D).
// ---------------------------------------------------------------------------------------------
public class TC6_E03_ProtocolErrorTests : ApiTestBase
{
    public TC6_E03_ProtocolErrorTests(ApiFixture api) : base(api) { }

    private async Task<string> ExpandAsync(string text)
    {
        var borrowings = await Client.BorrowingIdsByEquipmentAsync();
        var map = new Dictionary<string, string>
        {
            ["{BE1}"] = borrowings["EQ-001"].ToString(),
            ["{BE3}"] = borrowings["EQ-003"].ToString(),
            ["{D}"] = Date(0),
            ["{D-1}"] = Date(-1),
            ["{D+2}"] = Date(+2),
        };
        for (var n = 1; n <= 4; n++) map[$"{{S{n}}}"] = Ids.S(n).ToString();
        for (var n = 1; n <= 7; n++) map[$"{{E{n}}}"] = Ids.E(n).ToString();
        foreach (var (token, value) in map) text = text.Replace(token, value);
        return text;
    }

    private async Task<ApiResult> SendRawAsync(string method, string url, string? body)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await Client.SendAsync(request);
        return new ApiResult(response.StatusCode, await response.Content.ReadAsStringAsync(), response.Headers.Location);
    }

    [Theory]
    // ---- 400: invalid JSON, unknown or wrong-typed fields, blank required values, bad dates (API-04) ----
    [InlineData("POST", "/api/v1/students", "{bad json", 400, "validation_error")]
    [InlineData("POST", "/api/v1/students", """{"studentNumber":"ST-700","fullName":"X","unknownField":1}""", 400, "validation_error")]
    [InlineData("POST", "/api/v1/students", """{"studentNumber":"ST-700","fullName":"   "}""", 400, "validation_error")]
    [InlineData("POST", "/api/v1/students", """{"studentNumber":"   ","fullName":"Name"}""", 400, "validation_error")]
    [InlineData("POST", "/api/v1/students", """{"studentNumber":123,"fullName":"Name"}""", 400, "validation_error")]
    // ---- 400 (API-08) ----
    [InlineData("POST", "/api/v1/equipment", "{bad json", 400, "validation_error")]
    [InlineData("POST", "/api/v1/equipment", """{"equipmentCode":"EQ-900","name":"  "}""", 400, "validation_error")]
    [InlineData("POST", "/api/v1/equipment", """{"equipmentCode":"EQ-900","name":"X","isAvailable":false}""", 400, "validation_error")]
    // ---- 400 (API-10) ----
    [InlineData("POST", "/api/v1/borrowings", """{"studentId":{S4},"equipmentId":{E6},"expectedReturnDate":"2026-13-45"}""", 400, "validation_error")]
    [InlineData("POST", "/api/v1/borrowings", """{"studentId":{S4},"equipmentId":{E6},"expectedReturnDate":"{D}"}""", 400, "validation_error")]
    [InlineData("POST", "/api/v1/borrowings", """{"studentId":{S4},"equipmentId":{E6},"expectedReturnDate":"{D-1}"}""", 400, "validation_error")]
    [InlineData("POST", "/api/v1/borrowings", """{"studentId":"abc","equipmentId":{E6},"expectedReturnDate":"{D+2}"}""", 400, "validation_error")]
    [InlineData("POST", "/api/v1/borrowings", """{"studentId":{S4},"equipmentId":{E6}}""", 400, "validation_error")]
    [InlineData("POST", "/api/v1/borrowings", "", 400, "validation_error")]
    // ---- 400 (API-11) ----
    [InlineData("POST", "/api/v1/checkout-requests", "{bad json", 400, "validation_error")]
    [InlineData("POST", "/api/v1/checkout-requests", """{"studentId":{S4},"equipmentIds":[{E2},{E2}],"expectedReturnDate":"{D+2}"}""", 400, "validation_error")]
    [InlineData("POST", "/api/v1/checkout-requests", """{"studentId":{S4},"equipmentIds":[{E2}],"expectedReturnDate":"{D+2}"}""", 400, "validation_error")]
    [InlineData("POST", "/api/v1/checkout-requests", """{"studentId":{S4},"equipmentIds":[{E2},{E6}],"expectedReturnDate":"2026-13-45"}""", 400, "validation_error")]
    [InlineData("POST", "/api/v1/checkout-requests", """{"studentId":{S4},"equipmentIds":"x","expectedReturnDate":"{D+2}"}""", 400, "validation_error")]
    // ---- 400: bad query values (API-14, API-01) ----
    [InlineData("GET", "/api/v1/borrowings?status=Bogus", null, 400, "validation_error")]
    [InlineData("GET", "/api/v1/borrowings?sort=bogus", null, 400, "validation_error")]
    [InlineData("GET", "/api/v1/borrowings?direction=sideways", null, 400, "validation_error")]
    [InlineData("GET", "/api/v1/equipment?available=maybe", null, 400, "validation_error")]
    // ---- 404: missing resource and unknown route ----
    [InlineData("POST", "/api/v1/borrowings", """{"studentId":999999,"equipmentId":{E6},"expectedReturnDate":"{D+2}"}""", 404, "not_found")]
    [InlineData("POST", "/api/v1/borrowings", """{"studentId":{S4},"equipmentId":999999,"expectedReturnDate":"{D+2}"}""", 404, "not_found")]
    [InlineData("POST", "/api/v1/checkout-requests", """{"studentId":{S4},"equipmentIds":[{E2},999999],"expectedReturnDate":"{D+2}"}""", 404, "not_found")]
    [InlineData("GET", "/api/v1/borrowings/999999", null, 404, "not_found")]
    [InlineData("POST", "/api/v1/borrowings/999999/return", "{}", 404, "not_found")]
    [InlineData("GET", "/api/v1/no-such-route", null, 404, "not_found")]
    // ---- 405: known route, unsupported method ----
    [InlineData("DELETE", "/api/v1/students/{S1}", null, 405, "method_not_allowed")]
    [InlineData("DELETE", "/api/v1/equipment/{E1}", null, 405, "method_not_allowed")]
    [InlineData("PUT", "/api/v1/borrowings", "{}", 405, "method_not_allowed")]
    [InlineData("DELETE", "/api/v1/borrowings/{BE1}", null, 405, "method_not_allowed")]
    [InlineData("GET", "/api/v1/checkout-requests", null, 405, "method_not_allowed")]
    [InlineData("POST", "/api/v1/equipment/{E1}/borrowings", "{}", 405, "method_not_allowed")]
    // ---- 409: business conflicts ----
    [InlineData("POST", "/api/v1/borrowings", """{"studentId":{S2},"equipmentId":{E6},"expectedReturnDate":"{D+2}"}""", 409, "conflict")]
    [InlineData("POST", "/api/v1/borrowings", """{"studentId":{S4},"equipmentId":{E1},"expectedReturnDate":"{D+2}"}""", 409, "conflict")]
    [InlineData("POST", "/api/v1/checkout-requests", """{"studentId":{S4},"equipmentIds":[{E1},{E2}],"expectedReturnDate":"{D+2}"}""", 409, "conflict")]
    [InlineData("POST", "/api/v1/borrowings/{BE3}/return", "{}", 409, "conflict")]
    [InlineData("POST", "/api/v1/students", """{"studentNumber":"ST-100","fullName":"Duplicate Person"}""", 409, "conflict")]
    [InlineData("POST", "/api/v1/equipment", """{"equipmentCode":"EQ-001","name":"Another Camera"}""", 409, "conflict")]
    public async Task Request_gets_the_documented_status_a_safe_error_body_and_changes_nothing(
        string method, string url, string? body, int expectedStatus, string expectedCode)
    {
        var resolvedUrl = await ExpandAsync(url);
        var resolvedBody = body is null ? null : await ExpandAsync(body);
        var before = await DbSnapshot.TakeAsync();

        var result = await SendRawAsync(method, resolvedUrl, resolvedBody);

        Assert.Equal(expectedStatus, (int)result.Status);
        ErrorBody.AssertSafe(result, expectedCode);
        Assert.Equal(before, await DbSnapshot.TakeAsync()); // rejected requests leave state unchanged
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-E04 PostgreSQL rollback (UC-06; BR-12,15,16,18; API-11)
// A second API host is started with the existing "fail after the first item write" fault injected through DI.
// The recording wrapper proves the fault fired AFTER a real write inside the transaction (1 of 2 items written).
// What this proves: a failure after an actual PostgreSQL write attempt leaves no rows and no changed items. What it
// cannot prove: other failure kinds (a dropped connection during COMMIT is TC6-E07, process crash is TC6-E08).
// ---------------------------------------------------------------------------------------------
public sealed class RecordingInjector : ICheckoutFaultInjector
{
    private readonly ICheckoutFaultInjector _inner;
    public List<(int Written, int Total)> Writes { get; } = new();
    public int FaultsThrown { get; private set; }

    public RecordingInjector(ICheckoutFaultInjector inner) => _inner = inner;

    public async Task AfterWriteAsync(int writtenCount, int totalCount, CancellationToken cancellationToken = default)
    {
        Writes.Add((writtenCount, totalCount));
        try
        {
            await _inner.AfterWriteAsync(writtenCount, totalCount, cancellationToken);
        }
        catch
        {
            FaultsThrown++;
            throw;
        }
    }

    public Task AfterCommitAsync(CancellationToken cancellationToken = default) => _inner.AfterCommitAsync(cancellationToken);
}

// A separate real API host (same pipeline, same disposable test database) with only the fault injector replaced.
public sealed class FaultApiFactory : WebApplicationFactory<Program>
{
    public RecordingInjector Injector { get; }

    public FaultApiFactory(ICheckoutFaultInjector fault)
    {
        Injector = new RecordingInjector(fault);
        _ = Lab6Settings.ConnectionString; // refuses a non-test database
        Environment.SetEnvironmentVariable("EQUIPMENT_BORROWING_DB", "Test");
        Environment.SetEnvironmentVariable("EQUIPMENT_BORROWING_FIXED_DATE", Lab6Settings.D.ToString("yyyy-MM-dd"));
        Environment.SetEnvironmentVariable("EQUIPMENT_BORROWING_FAULT", null);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICheckoutFaultInjector>();
            services.AddSingleton<ICheckoutFaultInjector>(Injector);
        });
}

public class TC6_E04_PostgresRollbackTests : ApiTestBase
{
    public TC6_E04_PostgresRollbackTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task Failure_after_the_first_item_write_returns_a_safe_500_and_leaves_E2_E6_and_all_rows_as_before()
    {
        // 1. Baseline from a fresh connection.
        var baseline = await DbSnapshot.TakeAsync();
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(2)));
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(6)));
        Assert.Equal(4, await Pg.CountAsync("Borrowings"));

        // 2. Induce the failure after an actual PostgreSQL write attempt inside the transaction.
        var faulty = new FaultApiFactory(new FailAfterFirstWriteInjector());
        ApiResult result;
        string whileHostAlive;
        try
        {
            using var client = faulty.CreateClient();
            result = await client.SendJsonAsync(HttpMethod.Post, "/api/v1/checkout-requests",
                new { studentId = Ids.S(4), equipmentIds = new[] { Ids.E(2), Ids.E(6) }, expectedReturnDate = Date(+2) });
            whileHostAlive = await DbSnapshot.TakeAsync();
        }
        finally
        {
            await faulty.DisposeAsync(); // the host and its database contexts are gone before the final readback
        }

        // 3. The fault was reached AFTER a real write (1 of 2 items), exactly once.
        Assert.Equal((1, 2), Assert.Single(faulty.Injector.Writes));
        Assert.Equal(1, faulty.Injector.FaultsThrown);

        // 4. Known, reachable failure: safe 500 (not "outcome unknown").
        Assert.Equal(HttpStatusCode.InternalServerError, result.Status);
        ErrorBody.AssertSafe(result, "server_error");

        // 5. Independent fresh state is identical to the baseline, with the host alive and after it was disposed.
        Assert.Equal(baseline, whileHostAlive);
        Assert.Equal(baseline, await DbSnapshot.TakeAsync());
        Assert.Equal(4, await Pg.CountAsync("Borrowings"));
        Assert.Equal(0, await Pg.ScalarAsync<long>("SELECT COUNT(*) FROM \"Borrowings\" WHERE \"StudentId\" = @s", ("s", Ids.S(4))));
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(2)));
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(6)));

        // 6. Control: the same request succeeds on the normal host, so nothing was left half-borrowed.
        var retry = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/checkout-requests",
            new { studentId = Ids.S(4), equipmentIds = new[] { Ids.E(2), Ids.E(6) }, expectedReturnDate = Date(+2) });
        Assert.Equal(HttpStatusCode.Created, retry.Status);
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(2)));
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(6)));
        Assert.Equal(6, await Pg.CountAsync("Borrowings"));
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-E06 Optional and protected fields (UC-13,16; BR-02,03,06,07,14,16; API-05,09)
// ---------------------------------------------------------------------------------------------
public class TC6_E06_OptionalAndProtectedFieldTests : ApiTestBase
{
    public TC6_E06_OptionalAndProtectedFieldTests(ApiFixture api) : base(api) { }

    // ---- students (API-05) ---------------------------------------------------------------------
    [Fact]
    public async Task Clearing_the_student_contact_returns_200_and_stores_NULL_without_touching_anything_else()
    {
        var put1 = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/students/{Ids.S(1)}",
            new { studentNumber = "ST-100", fullName = "Ada Cruz", contact = "09170000001" });
        Assert.Equal(HttpStatusCode.OK, put1.Status);
        var withContact = Assert.Single(await Pg.RowsAsync("SELECT * FROM \"Students\" WHERE \"Id\" = @i", ("i", Ids.S(1))));
        Assert.Equal("09170000001", (string)withContact["Contact"]!);
        var borrowingsBefore = await DbSnapshot.TableAsync("Borrowings");
        var othersBefore = await DbSnapshot.TableAsync("Students", Ids.S(1));

        var cleared = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/students/{Ids.S(1)}",
            new { studentNumber = "ST-100", fullName = "Ada Cruz", contact = (string?)null });

        Assert.Equal(HttpStatusCode.OK, cleared.Status);
        Assert.Null(cleared.Json<StudentRow>().Contact);
        var db = Assert.Single(await Pg.RowsAsync("SELECT * FROM \"Students\" WHERE \"Id\" = @i", ("i", Ids.S(1))));
        Assert.Null(db["Contact"]); // a real NULL, not an empty string
        Assert.Equal("Ada Cruz", (string)db["Name"]!);
        Assert.True((bool)db["IsAllowedToBorrow"]!);
        Assert.Equal(borrowingsBefore, await DbSnapshot.TableAsync("Borrowings"));
        Assert.Equal(othersBefore, await DbSnapshot.TableAsync("Students", Ids.S(1)));
        Assert.Null((await Client.FetchAsync($"/api/v1/students/{Ids.S(1)}")).Json<StudentRow>().Contact);
    }

    [Fact]
    public async Task Blank_or_null_required_student_fields_are_rejected_with_400_and_the_row_is_unchanged()
    {
        var before = await DbSnapshot.TakeAsync();
        var bodies = new object[]
        {
            new { studentNumber = "ST-100", fullName = "   ", contact = (string?)null },
            new { studentNumber = "   ", fullName = "Ada Cruz", contact = (string?)null },
            new { studentNumber = "ST-100", fullName = (string?)null, contact = (string?)null },
        };

        foreach (var body in bodies)
        {
            var result = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/students/{Ids.S(1)}", body);
            Assert.Equal(HttpStatusCode.BadRequest, result.Status);
            ErrorBody.AssertSafe(result, "validation_error");
            Assert.Equal(before, await DbSnapshot.TakeAsync());
        }
    }

    [Fact]
    public async Task A_duplicate_student_number_on_update_returns_409_and_both_rows_are_unchanged()
    {
        var before = await DbSnapshot.TakeAsync();

        var result = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/students/{Ids.S(4)}",
            new { studentNumber = "ST-100", fullName = "Dani Ong", contact = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, result.Status);
        ErrorBody.AssertSafe(result, "conflict");
        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task Eligibility_cannot_be_changed_through_the_student_update_and_the_student_stays_ineligible()
    {
        Assert.False(await Fresh.IsAllowedAsync(Ids.S(2)));
        var before = await DbSnapshot.TakeAsync();

        var result = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/students/{Ids.S(2)}",
            new { studentNumber = "ST-200", fullName = "Ben Diaz", contact = (string?)null, isEligible = true });

        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
        ErrorBody.AssertSafe(result, "validation_error");
        Assert.False(await Fresh.IsAllowedAsync(Ids.S(2)));
        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    // ---- equipment (API-09) ---------------------------------------------------------------------
    [Fact]
    public async Task Clearing_the_equipment_description_returns_200_stores_NULL_and_keeps_availability_and_history()
    {
        var put1 = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/equipment/{Ids.E(1)}",
            new { equipmentCode = "EQ-001", name = "Camera", description = "Black body, 50mm lens" });
        Assert.Equal(HttpStatusCode.OK, put1.Status);
        var borrowingsBefore = await DbSnapshot.TableAsync("Borrowings");
        var othersBefore = await DbSnapshot.TableAsync("Equipment", Ids.E(1));

        var cleared = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/equipment/{Ids.E(1)}",
            new { equipmentCode = "EQ-001", name = "Camera", description = (string?)null });

        Assert.Equal(HttpStatusCode.OK, cleared.Status);
        var json = cleared.Json<EquipmentRow>();
        Assert.Null(json.Description);
        Assert.False(json.IsAvailable);
        var db = Assert.Single(await Pg.RowsAsync("SELECT * FROM \"Equipment\" WHERE \"Id\" = @i", ("i", Ids.E(1))));
        Assert.Null(db["Description"]);
        Assert.False((bool)db["IsAvailable"]!);
        Assert.Equal(borrowingsBefore, await DbSnapshot.TableAsync("Borrowings"));
        Assert.Equal(othersBefore, await DbSnapshot.TableAsync("Equipment", Ids.E(1)));
    }

    [Fact]
    public async Task Blank_required_equipment_fields_are_rejected_with_400_and_the_row_is_unchanged()
    {
        var before = await DbSnapshot.TakeAsync();
        var bodies = new object[]
        {
            new { equipmentCode = "EQ-001", name = "   ", description = (string?)null },
            new { equipmentCode = "   ", name = "Camera", description = (string?)null },
        };

        foreach (var body in bodies)
        {
            var result = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/equipment/{Ids.E(1)}", body);
            Assert.Equal(HttpStatusCode.BadRequest, result.Status);
            ErrorBody.AssertSafe(result, "validation_error");
            Assert.Equal(before, await DbSnapshot.TakeAsync());
        }
    }

    [Fact]
    public async Task A_duplicate_equipment_code_on_update_returns_409_and_both_rows_are_unchanged()
    {
        var before = await DbSnapshot.TakeAsync();

        var result = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/equipment/{Ids.E(2)}",
            new { equipmentCode = "EQ-001", name = "Laptop", description = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, result.Status);
        ErrorBody.AssertSafe(result, "conflict");
        Assert.Equal(before, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task Availability_cannot_be_changed_through_the_equipment_update_in_either_direction()
    {
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(1)));
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(2)));
        var before = await DbSnapshot.TakeAsync();

        // E1 is borrowed: try to force it available.
        var forceAvailable = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/equipment/{Ids.E(1)}",
            new { equipmentCode = "EQ-001", name = "Camera", description = (string?)null, isAvailable = true });
        Assert.Equal(HttpStatusCode.BadRequest, forceAvailable.Status);
        ErrorBody.AssertSafe(forceAvailable, "validation_error");

        // E2 is free: try to force it unavailable.
        var forceUnavailable = await Client.SendJsonAsync(HttpMethod.Put, $"/api/v1/equipment/{Ids.E(2)}",
            new { equipmentCode = "EQ-002", name = "Laptop", description = (string?)null, isAvailable = false });
        Assert.Equal(HttpStatusCode.BadRequest, forceUnavailable.Status);
        ErrorBody.AssertSafe(forceUnavailable, "validation_error");

        Assert.False(await Fresh.IsAvailableAsync(Ids.E(1)));
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(2)));
        Assert.Equal(before, await DbSnapshot.TakeAsync()); // availability and the whole history intact
    }
}
