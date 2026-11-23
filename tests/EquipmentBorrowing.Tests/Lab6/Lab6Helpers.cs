using System.Net;
using System.Text;
using System.Text.Json;

namespace EquipmentBorrowing.Tests.Lab6;

// Shapes of the API-01..16 JSON responses (property names match the Lab 5 OpenAPI file; web defaults = camelCase).
public sealed record StudentRow(int Id, string StudentNumber, string FullName, string? Contact, bool IsEligible);

public sealed record EquipmentRow(int Id, string EquipmentCode, string Name, string? Description, bool IsAvailable);

public sealed record BorrowingRow(
    int Id, int StudentId, int EquipmentId,
    string BorrowedDate, string ExpectedReturnDate, string? ReturnedDate, string Status,
    string? StudentNumber, string? StudentName, string? EquipmentCode, string? EquipmentName);

// One HTTP response: status code plus raw body, so a test can assert both the status and the error code.
public sealed record ApiResult(HttpStatusCode Status, string Body)
{
    public T Json<T>() => JsonSerializer.Deserialize<T>(Body, JsonSerializerOptions.Web)!;

    // Error bodies look like { "code": "not_found", "message": "..." }. Returns null for non-error bodies.
    public string? ErrorCode
    {
        get
        {
            try
            {
                using var doc = JsonDocument.Parse(Body);
                return doc.RootElement.ValueKind == JsonValueKind.Object
                       && doc.RootElement.TryGetProperty("code", out var code)
                    ? code.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}

public static class HttpClientExtensions
{
    public static async Task<ApiResult> FetchAsync(this HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        return new ApiResult(response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    // Borrowing IDs keyed by equipment code (EQ-001 ...), resolved from the API, never hard-coded.
    public static async Task<Dictionary<string, int>> BorrowingIdsByEquipmentAsync(this HttpClient client)
    {
        var result = await client.FetchAsync("/api/v1/borrowings");
        return result.Json<List<BorrowingRow>>().ToDictionary(b => b.EquipmentCode!, b => b.Id);
    }
}

// Whole-database snapshot read through a fresh connection. Read-only scenarios compare before/after
// snapshots to prove "No changes" in the database, including unrelated rows.
public static class DbSnapshot
{
    public static async Task<string> TakeAsync()
    {
        var sb = new StringBuilder();
        foreach (var table in new[] { "Students", "Equipment", "Borrowings" })
        {
            sb.AppendLine($"[{table}]");
            foreach (var row in await Pg.RowsAsync($"SELECT * FROM \"{table}\" ORDER BY \"Id\""))
                sb.AppendLine(string.Join("|", row.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        return sb.ToString();
    }
}
