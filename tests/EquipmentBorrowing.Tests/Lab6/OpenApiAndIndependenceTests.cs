using System.Net;
using System.Reflection;
using System.Text.Json;

namespace EquipmentBorrowing.Tests.Lab6;

// ---------------------------------------------------------------------------------------------
// OpenAPI helpers. The description is served by the API itself at /openapi/v1.json.
// "Consistent" here means: the served description agrees with what the API really does (operations, query
// parameters, success status codes, response shapes, documented error statuses) and with the C# contract types.
// ---------------------------------------------------------------------------------------------
internal static class Spec
{
    // API-01..16 (numbering as in the Lab 5 reference).
    public static readonly (int Api, string Method, string Path)[] Operations =
    {
        (1, "GET", "/api/v1/equipment"),
        (2, "GET", "/api/v1/students"),
        (3, "GET", "/api/v1/students/{id}"),
        (4, "POST", "/api/v1/students"),
        (5, "PUT", "/api/v1/students/{id}"),
        (6, "PATCH", "/api/v1/students/{id}/eligibility"),
        (7, "GET", "/api/v1/equipment/{id}"),
        (8, "POST", "/api/v1/equipment"),
        (9, "PUT", "/api/v1/equipment/{id}"),
        (10, "POST", "/api/v1/borrowings"),
        (11, "POST", "/api/v1/checkout-requests"),
        (12, "POST", "/api/v1/borrowings/{id}/return"),
        (13, "GET", "/api/v1/borrowings/{id}"),
        (14, "GET", "/api/v1/borrowings"),
        (15, "GET", "/api/v1/students/{id}/borrowings"),
        (16, "GET", "/api/v1/equipment/{id}/borrowings"),
    };

    private static readonly string[] HttpMethods = { "get", "put", "post", "delete", "options", "head", "patch", "trace" };

    public static async Task<JsonDocument> LoadAsync(HttpClient client)
    {
        var result = await client.FetchAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, result.Status);
        return JsonDocument.Parse(result.Body);
    }

    public static List<string> OperationsIn(JsonDocument doc) =>
        doc.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateObject()
                .Where(o => HttpMethods.Contains(o.Name))
                .Select(o => $"{o.Name.ToUpperInvariant()} {p.Name}"))
            .Order().ToList();

    public static List<string> ExpectedOperations() =>
        Operations.Select(o => $"{o.Method} {o.Path}").Order().ToList();

    public static JsonElement Operation(JsonDocument doc, string method, string path) =>
        doc.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method.ToLowerInvariant());

    private static string? RefName(JsonElement schema) =>
        schema.TryGetProperty("$ref", out var r) ? r.GetString()!.Split('/').Last() : null;

    public static (string Code, string Schema, bool IsArray) SuccessResponse(JsonElement operation)
    {
        var success = operation.GetProperty("responses").EnumerateObject().Single(p => p.Name.StartsWith('2'));
        var schema = success.Value.GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var name = RefName(schema);
        if (name is not null) return (success.Name, name, false);
        Assert.Equal("array", schema.GetProperty("type").GetString());
        return (success.Name, RefName(schema.GetProperty("items"))!, true);
    }

    public static HashSet<string> ResponseCodes(JsonElement operation) =>
        operation.GetProperty("responses").EnumerateObject().Select(p => p.Name).ToHashSet();

    private static bool IsNullable(JsonElement s)
    {
        if (s.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.Array)
            return t.EnumerateArray().Any(e => e.GetString() == "null");
        foreach (var key in new[] { "oneOf", "anyOf" })
            if (s.TryGetProperty(key, out var list) &&
                list.EnumerateArray().Any(e => e.TryGetProperty("type", out var tt) && tt.ValueKind == JsonValueKind.String && tt.GetString() == "null"))
                return true;
        return false;
    }

    // property name -> is it nullable (so it may legitimately be absent or null)
    public static Dictionary<string, bool> Properties(JsonDocument doc, string schemaName)
    {
        var schema = doc.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schemaName);
        var result = new Dictionary<string, bool>();
        if (schema.TryGetProperty("properties", out var props))
            foreach (var p in props.EnumerateObject()) result[p.Name] = IsNullable(p.Value);
        return result;
    }

    public static void AssertShape(string label, JsonElement actual, Dictionary<string, bool> documented)
    {
        Assert.Equal(JsonValueKind.Object, actual.ValueKind);
        var actualNames = actual.EnumerateObject().Select(p => p.Name).ToHashSet();
        var undocumented = actualNames.Except(documented.Keys).ToList();
        var missing = documented.Where(d => !d.Value).Select(d => d.Key).Except(actualNames).ToList();
        Assert.True(undocumented.Count == 0, $"{label}: in the response but not in the OpenAPI schema: {string.Join(", ", undocumented)}");
        Assert.True(missing.Count == 0, $"{label}: required by the OpenAPI schema but missing from the response: {string.Join(", ", missing)}");
    }
}

// ---------------------------------------------------------------------------------------------
// R2 OpenAPI consistency
// ---------------------------------------------------------------------------------------------
public class TC6_OpenApi_ConsistencyTests : ApiTestBase
{
    public TC6_OpenApi_ConsistencyTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task The_served_description_lists_exactly_the_sixteen_API_operations()
    {
        using var spec = await Spec.LoadAsync(Client);

        Assert.Equal(Spec.ExpectedOperations(), Spec.OperationsIn(spec));
    }

    [Fact]
    public async Task Documented_query_and_path_parameters_match_the_real_routes()
    {
        using var spec = await Spec.LoadAsync(Client);
        var expectedQuery = new Dictionary<string, string[]>
        {
            ["GET /api/v1/equipment"] = new[] { "available", "search" },
            ["GET /api/v1/students"] = new[] { "search" },
            ["GET /api/v1/borrowings"] = new[] { "direction", "search", "sort", "status" },
        };

        foreach (var (api, method, path) in Spec.Operations)
        {
            var op = Spec.Operation(spec, method, path);
            var parameters = op.TryGetProperty("parameters", out var p) ? p.EnumerateArray().ToList() : new List<JsonElement>();
            var query = parameters.Where(x => x.GetProperty("in").GetString() == "query").Select(x => x.GetProperty("name").GetString()!).Order().ToArray();
            var route = parameters.Where(x => x.GetProperty("in").GetString() == "path").Select(x => x.GetProperty("name").GetString()!).ToArray();

            Assert.Equal(expectedQuery.GetValueOrDefault($"{method} {path}", Array.Empty<string>()), query);
            Assert.Equal(path.Contains("{id}") ? new[] { "id" } : Array.Empty<string>(), route);
            _ = api;
        }
    }

    [Fact]
    public async Task Documented_schemas_have_exactly_the_properties_of_the_contract_types()
    {
        using var spec = await Spec.LoadAsync(Client);
        var pairs = new (string Schema, Type Contract)[]
        {
            ("CreateStudentRequest", typeof(EquipmentBorrowing.Contracts.CreateStudentRequest)),
            ("UpdateStudentRequest", typeof(EquipmentBorrowing.Contracts.UpdateStudentRequest)),
            ("SetEligibilityRequest", typeof(EquipmentBorrowing.Contracts.SetEligibilityRequest)),
            ("CreateEquipmentRequest", typeof(EquipmentBorrowing.Contracts.CreateEquipmentRequest)),
            ("UpdateEquipmentRequest", typeof(EquipmentBorrowing.Contracts.UpdateEquipmentRequest)),
            ("CreateBorrowingRequest", typeof(EquipmentBorrowing.Contracts.CreateBorrowingRequest)),
            ("CheckoutRequest", typeof(EquipmentBorrowing.Contracts.CheckoutRequest)),
            ("ReturnBorrowingRequest", typeof(EquipmentBorrowing.Contracts.ReturnBorrowingRequest)),
            ("CheckoutResponse", typeof(EquipmentBorrowing.Contracts.CheckoutResponse)),
            ("StudentDto", typeof(EquipmentBorrowing.Contracts.StudentDto)),
            ("EquipmentDto", typeof(EquipmentBorrowing.Contracts.EquipmentDto)),
            ("BorrowingDto", typeof(EquipmentBorrowing.Contracts.BorrowingDto)),
        };

        foreach (var (schema, contract) in pairs)
        {
            var fromContract = contract.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name)).Order().ToArray();
            var documented = Spec.Properties(spec, schema).Keys.Order().ToArray();
            Assert.True(fromContract.SequenceEqual(documented),
                $"{schema}: contract has [{string.Join(", ", fromContract)}] but OpenAPI documents [{string.Join(", ", documented)}]");
        }
    }

    [Fact]
    public async Task Protected_fields_are_not_part_of_the_documented_update_requests()
    {
        using var spec = await Spec.LoadAsync(Client);

        Assert.DoesNotContain("isAvailable", Spec.Properties(spec, "UpdateEquipmentRequest").Keys); // BR-06/07
        Assert.DoesNotContain("isAvailable", Spec.Properties(spec, "CreateEquipmentRequest").Keys);
        Assert.DoesNotContain("isEligible", Spec.Properties(spec, "UpdateStudentRequest").Keys); // BR-02/03: only the PATCH changes it
        Assert.DoesNotContain("isEligible", Spec.Properties(spec, "CreateStudentRequest").Keys);
    }

    [Fact]
    public async Task Every_API_returns_its_documented_success_status_and_a_body_that_matches_the_documented_schema()
    {
        using var spec = await Spec.LoadAsync(Client);
        var be = await Client.BorrowingIdsByEquipmentAsync();

        // One happy-path call per API, ordered so the calls do not interfere with each other.
        var calls = new (int Api, string Method, string Template, string Path, object? Body)[]
        {
            (1, "GET", "/api/v1/equipment", "/api/v1/equipment", null),
            (2, "GET", "/api/v1/students", "/api/v1/students", null),
            (3, "GET", "/api/v1/students/{id}", $"/api/v1/students/{Ids.S(1)}", null),
            (4, "POST", "/api/v1/students", "/api/v1/students", new { studentNumber = "ST-500", fullName = "Eli Tan", contact = "09171234567" }),
            (5, "PUT", "/api/v1/students/{id}", $"/api/v1/students/{Ids.S(4)}", new { studentNumber = "ST-400", fullName = "Dani Ong", contact = "09170000004" }),
            (10, "POST", "/api/v1/borrowings", "/api/v1/borrowings", new { studentId = Ids.S(1), equipmentId = Ids.E(2), expectedReturnDate = Date(+2) }),
            (11, "POST", "/api/v1/checkout-requests", "/api/v1/checkout-requests", new { studentId = Ids.S(4), equipmentIds = new[] { Ids.E(6), Ids.E(7) }, expectedReturnDate = Date(+2) }),
            (12, "POST", "/api/v1/borrowings/{id}/return", $"/api/v1/borrowings/{be["EQ-001"]}/return", new { }),
            (13, "GET", "/api/v1/borrowings/{id}", $"/api/v1/borrowings/{be["EQ-001"]}", null),
            (14, "GET", "/api/v1/borrowings", "/api/v1/borrowings", null),
            (15, "GET", "/api/v1/students/{id}/borrowings", $"/api/v1/students/{Ids.S(1)}/borrowings", null),
            (16, "GET", "/api/v1/equipment/{id}/borrowings", $"/api/v1/equipment/{Ids.E(1)}/borrowings", null),
            (6, "PATCH", "/api/v1/students/{id}/eligibility", $"/api/v1/students/{Ids.S(4)}/eligibility", new { isEligible = false }),
            (7, "GET", "/api/v1/equipment/{id}", $"/api/v1/equipment/{Ids.E(1)}", null),
            (8, "POST", "/api/v1/equipment", "/api/v1/equipment", new { equipmentCode = "EQ-008", name = "Charger", description = "USB-C" }),
            (9, "PUT", "/api/v1/equipment/{id}", $"/api/v1/equipment/{Ids.E(2)}", new { equipmentCode = "EQ-002", name = "Laptop", description = "13 inch" }),
        };
        Assert.Equal(16, calls.Select(c => c.Api).Distinct().Count());

        foreach (var c in calls)
        {
            var label = $"API-{c.Api:00} {c.Method} {c.Template}";
            var (code, schemaName, isArray) = Spec.SuccessResponse(Spec.Operation(spec, c.Method, c.Template));

            var result = c.Body is null
                ? await Client.FetchAsync(c.Path)
                : await Client.SendJsonAsync(new HttpMethod(c.Method), c.Path, c.Body);

            Assert.True(((int)result.Status).ToString() == code,
                $"{label}: OpenAPI documents {code} but the API returned {(int)result.Status}. Body: {result.Body}");

            using var body = JsonDocument.Parse(result.Body);
            var documented = Spec.Properties(spec, schemaName);
            if (isArray)
            {
                var first = Assert.Single(body.RootElement.EnumerateArray().Take(1));
                Spec.AssertShape(label, first, documented);
            }
            else
            {
                Spec.AssertShape(label, body.RootElement, documented);
                if (schemaName == "CheckoutResponse")
                    foreach (var item in body.RootElement.GetProperty("borrowings").EnumerateArray())
                        Spec.AssertShape(label + " (borrowings item)", item, Spec.Properties(spec, "BorrowingDto"));
            }
        }
    }

    [Fact]
    public async Task Every_error_status_the_API_really_returns_is_documented_for_that_operation_with_the_documented_error_shape()
    {
        using var spec = await Spec.LoadAsync(Client);
        var be = await Client.BorrowingIdsByEquipmentAsync();
        var errorShape = Spec.Properties(spec, "ErrorResponse");
        Assert.Equal(new[] { "code", "message" }, errorShape.Keys.Order().ToArray());

        var probes = new (string Method, string Template, string Path, object? Body, int Expected)[]
        {
            ("POST", "/api/v1/students", "/api/v1/students", new { studentNumber = "ST-100", fullName = "Duplicate Person" }, 409),
            ("POST", "/api/v1/students", "/api/v1/students", new { studentNumber = "   ", fullName = "Name" }, 400),
            ("GET", "/api/v1/borrowings", "/api/v1/borrowings?status=Bogus", null, 400),
            ("GET", "/api/v1/equipment", "/api/v1/equipment?available=maybe", null, 400),
            ("POST", "/api/v1/borrowings", "/api/v1/borrowings", new { studentId = 999_999, equipmentId = Ids.E(6), expectedReturnDate = Date(+2) }, 404),
            ("POST", "/api/v1/checkout-requests", "/api/v1/checkout-requests", new { studentId = Ids.S(3), equipmentIds = new[] { Ids.E(2), Ids.E(6) }, expectedReturnDate = Date(+2) }, 409),
            ("GET", "/api/v1/borrowings/{id}", "/api/v1/borrowings/999999", null, 404),
            ("POST", "/api/v1/borrowings/{id}/return", $"/api/v1/borrowings/{be["EQ-003"]}/return", new { }, 409),
            ("PUT", "/api/v1/equipment/{id}", $"/api/v1/equipment/{Ids.E(2)}", new { equipmentCode = "EQ-001", name = "Laptop" }, 409),
            ("PATCH", "/api/v1/students/{id}/eligibility", "/api/v1/students/999999/eligibility", new { isEligible = false }, 404),
        };

        foreach (var p in probes)
        {
            var label = $"{p.Method} {p.Template}";
            var result = p.Body is null
                ? await Client.FetchAsync(p.Path)
                : await Client.SendJsonAsync(new HttpMethod(p.Method), p.Path, p.Body);

            Assert.Equal(p.Expected, (int)result.Status);
            var documentedCodes = Spec.ResponseCodes(Spec.Operation(spec, p.Method, p.Template));
            Assert.True(documentedCodes.Contains(p.Expected.ToString()),
                $"{label}: the API returns {p.Expected} but OpenAPI documents only [{string.Join(", ", documentedCodes.Order())}]");

            using var body = JsonDocument.Parse(result.Body);
            Spec.AssertShape(label + " (error body)", body.RootElement, errorShape);
        }
    }
}

// ---------------------------------------------------------------------------------------------
// MA6-02 automated parts: the API is independent of the desktop, and one persisted change is seen by two
// independent clients. (The UI refresh and the screenshots are the manual part of MA6-02.)
// ---------------------------------------------------------------------------------------------
public class MA6_02_IndependentApiAndSynchronizationTests : ApiTestBase
{
    public MA6_02_IndependentApiAndSynchronizationTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task The_API_runs_as_its_own_process_without_the_desktop_and_serves_a_consistent_OpenAPI_description()
    {
        await using var api = await ApiProcess.StartAsync(); // a separate OS process; no Avalonia, no desktop

        var spec = await api.Http.FetchAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, spec.Status);
        using var doc = JsonDocument.Parse(spec.Body);
        Assert.Equal(Spec.ExpectedOperations(), Spec.OperationsIn(doc));

        var equipment = await api.Http.FetchAsync("/api/v1/equipment");
        Assert.Equal(HttpStatusCode.OK, equipment.Status);
        Assert.Equal(7, equipment.Json<List<EquipmentRow>>().Count);
    }

    [Fact]
    public async Task One_persisted_change_made_through_one_client_is_seen_by_an_independent_second_client_and_by_PostgreSQL()
    {
        var baseline = await DbSnapshot.TableAsync("Borrowings");
        await using var external = await ApiProcess.StartAsync(); // client A: an independent API process

        // Change through A.
        var created = await external.Http.SendJsonAsync(HttpMethod.Post, "/api/v1/borrowings",
            new { studentId = Ids.S(4), equipmentId = Ids.E(2), expectedReturnDate = Date(+2) });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var loan = created.Json<BorrowingRow>();

        // Seen by B (the in-process API), identical record.
        var seenByB = await Client.FetchAsync($"/api/v1/borrowings/{loan.Id}");
        Assert.Equal(HttpStatusCode.OK, seenByB.Status);
        Assert.Equal(loan, seenByB.Json<BorrowingRow>());
        Assert.False((await Client.FetchAsync($"/api/v1/equipment/{Ids.E(2)}")).Json<EquipmentRow>().IsAvailable);

        // Seen by PostgreSQL: one change, nothing unrelated touched.
        Assert.Equal(5, await Pg.CountAsync("Borrowings"));
        Assert.True(Fresh.IsActive(await Fresh.BorrowingAsync(loan.Id)));
        Assert.Equal(baseline, await DbSnapshot.TableAsync("Borrowings", loan.Id));

        // Change through B, seen by A.
        var returned = await Client.SendJsonAsync(HttpMethod.Post, $"/api/v1/borrowings/{loan.Id}/return", new { });
        Assert.Equal(HttpStatusCode.OK, returned.Status);
        var seenByA = await external.Http.FetchAsync($"/api/v1/borrowings/{loan.Id}");
        Assert.Equal(returned.Json<BorrowingRow>(), seenByA.Json<BorrowingRow>());
        Assert.Equal("Returned", seenByA.Json<BorrowingRow>().Status);
        Assert.True((await external.Http.FetchAsync($"/api/v1/equipment/{Ids.E(2)}")).Json<EquipmentRow>().IsAvailable);
        Assert.True(await Fresh.IsAvailableAsync(Ids.E(2)));
        Assert.Equal(5, await Pg.CountAsync("Borrowings")); // history retained
    }
}
