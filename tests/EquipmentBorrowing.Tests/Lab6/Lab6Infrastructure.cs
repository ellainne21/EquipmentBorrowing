using System.Net.Http.Json;
using System.Text.Json;
using EquipmentBorrowing.Infrastructure.Data;
using EquipmentBorrowing.Infrastructure.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EquipmentBorrowing.Tests.Lab6;

// ---------------------------------------------------------------------------------------------
// Lab 6 test infrastructure.
//  - D (the documented fixed local date) is 2026-11-10, the same date as the Lab 5 controlled dataset.
//  - The connection string comes from dotnet user-secrets ("ConnectionStrings:Test"), never from a file.
//  - Every scenario starts from ControlledDataset.Reset (S1-S4, E1-E7), so no test depends on another.
//  - Persisted state is read through brand-new, non-pooled connections (Pg helper), not through the API.
// ---------------------------------------------------------------------------------------------
public static class Lab6Settings
{
    // D: documented fixed date for every Lab 6 scenario (matches EQUIPMENT_BORROWING_FIXED_DATE below).
    public static readonly DateTime D = new(2026, 11, 10);

    public static string ConnectionString { get; } = Resolve();

    private static string Resolve()
    {
        var cs = DatabaseSettings.GetConnectionString("Test");
        var database = new NpgsqlConnectionStringBuilder(cs).Database ?? "";
        if (!database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Refused: the 'Test' connection string must point at a database whose name contains 'test'.");
        }
        return cs;
    }
}

// Starts the REAL ASP.NET Core pipeline (routing, controllers, middleware, EF Core, PostgreSQL) in-process.
public sealed class ApiFixture : WebApplicationFactory<Program>
{
    public ApiFixture()
    {
        _ = Lab6Settings.ConnectionString; // guard first: fails before anything touches a database

        // Program.cs reads these at startup (test database only, fixed clock, no fault mode).
        Environment.SetEnvironmentVariable("EQUIPMENT_BORROWING_DB", "Test");
        Environment.SetEnvironmentVariable("EQUIPMENT_BORROWING_FIXED_DATE", Lab6Settings.D.ToString("yyyy-MM-dd"));
        Environment.SetEnvironmentVariable("EQUIPMENT_BORROWING_FAULT", null);
    }
}

[CollectionDefinition(Name)]
public class Lab6Collection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Lab6Db";
}

// Independent PostgreSQL access: a NEW connection per call, pooling off, so a read never reuses
// the API's session or a previous test's connection.
public static class Pg
{
    private static string FreshConnectionString()
    {
        var b = new NpgsqlConnectionStringBuilder(Lab6Settings.ConnectionString) { Pooling = false };
        return b.ConnectionString;
    }

    private static async Task<NpgsqlConnection> OpenAsync()
    {
        var c = new NpgsqlConnection(FreshConnectionString());
        await c.OpenAsync();
        return c;
    }

    private static NpgsqlCommand Cmd(NpgsqlConnection c, string sql, (string Name, object? Value)[] p)
    {
        var cmd = new NpgsqlCommand(sql, c);
        foreach (var (name, value) in p) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    public static async Task<T> ScalarAsync<T>(string sql, params (string Name, object? Value)[] p)
    {
        await using var c = await OpenAsync();
        await using var cmd = Cmd(c, sql, p);
        var result = await cmd.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T));
    }

    public static async Task<List<Dictionary<string, object?>>> RowsAsync(string sql, params (string Name, object? Value)[] p)
    {
        await using var c = await OpenAsync();
        await using var cmd = Cmd(c, sql, p);
        await using var r = await cmd.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();
        while (await r.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < r.FieldCount; i++)
                row[r.GetName(i)] = await r.IsDBNullAsync(i) ? null : r.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    public static async Task<int> ExecAsync(string sql, params (string Name, object? Value)[] p)
    {
        await using var c = await OpenAsync();
        await using var cmd = Cmd(c, sql, p);
        return await cmd.ExecuteNonQueryAsync();
    }

    // Convenience counts (always a fresh connection).
    public static Task<long> CountAsync(string table) =>
        ScalarAsync<long>($"SELECT COUNT(*) FROM \"{table}\"");

    public static Task<long> ActiveBorrowingCountAsync() =>
        ScalarAsync<long>("SELECT COUNT(*) FROM \"Borrowings\" WHERE \"Status\" = 0");
}

// IDs are resolved from the API (by student number / equipment code), never hard-coded.
public sealed class Ids
{
    private readonly Dictionary<string, int> _students;
    private readonly Dictionary<string, int> _equipment;

    private Ids(Dictionary<string, int> students, Dictionary<string, int> equipment)
    {
        _students = students;
        _equipment = equipment;
    }

    public int S(int n) => _students[$"ST-{n}00"]; // S1 = ST-100 ... S4 = ST-400
    public int E(int n) => _equipment[$"EQ-{n:000}"]; // E1 = EQ-001 ... E7 = EQ-007

    public static async Task<Ids> ResolveAsync(HttpClient client)
    {
        var students = new Dictionary<string, int>();
        using (var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/students")))
            foreach (var s in doc.RootElement.EnumerateArray())
                students[s.GetProperty("studentNumber").GetString()!] = s.GetProperty("id").GetInt32();

        var equipment = new Dictionary<string, int>();
        using (var doc = JsonDocument.Parse(await client.GetStringAsync("/api/v1/equipment")))
            foreach (var e in doc.RootElement.EnumerateArray())
                equipment[e.GetProperty("equipmentCode").GetString()!] = e.GetProperty("id").GetInt32();

        return new Ids(students, equipment);
    }
}

// Every Lab 6 test class inherits this. Before EACH test the disposable database is reset to S1-S4/E1-E7,
// so there is no execution-order dependency.
[Collection(Lab6Collection.Name)]
public abstract class ApiTestBase : IAsyncLifetime
{
    protected ApiFixture Api { get; }
    protected HttpClient Client { get; private set; } = null!;
    protected Ids Ids { get; private set; } = null!;

    protected static DateTime D => Lab6Settings.D;
    protected static string Date(int offsetDays) => D.AddDays(offsetDays).ToString("yyyy-MM-dd");

    protected ApiTestBase(ApiFixture api) => Api = api;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Lab6Settings.ConnectionString)
            .Options;
        await using (var context = new AppDbContext(options))
        {
            ControlledDataset.Reset(context, D);
        }

        Client = Api.CreateClient();
        Ids = await Ids.ResolveAsync(Client);
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    protected static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
