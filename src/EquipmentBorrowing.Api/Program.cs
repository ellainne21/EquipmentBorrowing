using System.Globalization;
using System.Text.Json.Serialization;
using EquipmentBorrowing.Api;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Contracts;
using EquipmentBorrowing.Infrastructure.Data;
using EquipmentBorrowing.Infrastructure.Repositories;
using EquipmentBorrowing.Infrastructure.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// ---------------------------------------------------------------------------------------------
// Campus Equipment Borrowing API (Lab 5). Start it with:
//   dotnet run --project src/EquipmentBorrowing.Api
// Test mode (disposable database, test seams allowed):
//   $env:EQUIPMENT_BORROWING_DB="Test"
// ---------------------------------------------------------------------------------------------

// Set EQUIPMENT_BORROWING_DB=Test to use the disposable test database (same switch as Lab 4).
var useTestDatabase = string.Equals(
    Environment.GetEnvironmentVariable("EQUIPMENT_BORROWING_DB"), "Test", StringComparison.OrdinalIgnoreCase);

// Test seams are honoured ONLY with the test database, never with real data.
var faultMode = useTestDatabase
    ? Environment.GetEnvironmentVariable("EQUIPMENT_BORROWING_FAULT")?.Trim().ToLowerInvariant()
    : null;

var builder = WebApplication.CreateBuilder(args);

// The connection string comes from dotnet user-secrets (never from a file in Git).
var connectionString = DatabaseSettings.GetConnectionString(useTestDatabase ? "Test" : "Main");

// T08 test seam: "BrokenConnection" points the API at a port where nothing listens,
// so the API starts but every database call fails. The real connection string is not changed or shown.
if (faultMode == "brokenconnection")
{
    var broken = new NpgsqlConnectionStringBuilder(connectionString) { Port = 1, Timeout = 3, CommandTimeout = 3 };
    connectionString = broken.ConnectionString;
}

// Clock: the real local date, or a fixed date for repeatable tests (test database only).
IClock clock = new SystemClock();
var fixedDateText = useTestDatabase ? Environment.GetEnvironmentVariable("EQUIPMENT_BORROWING_FIXED_DATE") : null;
if (!string.IsNullOrWhiteSpace(fixedDateText))
{
    if (DateTime.TryParseExact(fixedDateText.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var fixedDate))
    {
        clock = new FixedClock(fixedDate);
    }
    else
    {
        Console.Error.WriteLine("EQUIPMENT_BORROWING_FIXED_DATE must look like 2026-11-10. The real date is used.");
    }
}

// CONTEXT LIFETIME: contexts are short-lived and never shared between operations.
// - The factory creates one new context per checkout/return (unit of work).
// - Read-only repositories each get their own context (AsNoTracking reads), disposed at the end of the request.
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddTransient<AppDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

builder.Services.AddSingleton(clock);

// Repositories (EF Core / PostgreSQL)
builder.Services.AddTransient<IStudentRepository, EfStudentRepository>();
builder.Services.AddTransient<IEquipmentRepository, EfEquipmentRepository>();
builder.Services.AddTransient<IBorrowingRepository, EfBorrowingRepository>();
builder.Services.AddTransient<IBorrowingLookup, EfBorrowingRepository>();

// TRANSACTION BOUNDARY: checkout and return run inside one unit of work (one transaction)
builder.Services.AddSingleton<IBorrowingUnitOfWorkFactory, EfBorrowingUnitOfWorkFactory>();
builder.Services.AddSingleton<ICheckoutFaultInjector>(_ => faultMode switch
{
    "afterfirstwrite" => new FailAfterFirstWriteInjector(),
    "lostcommit" => new LostCommitResponseInjector(),
    _ => new NoCheckoutFaults()
});

// Application services (the same Lab 4 business rules, unchanged)
builder.Services.AddTransient<CheckoutTransaction>();
builder.Services.AddTransient<BorrowEquipmentService>();
builder.Services.AddTransient<CheckOutMultipleEquipmentService>();
builder.Services.AddTransient<ReturnEquipmentService>();
builder.Services.AddTransient<RegisterStudentService>();
builder.Services.AddTransient<UpdateStudentService>();
builder.Services.AddTransient<ChangeStudentEligibilityService>();
builder.Services.AddTransient<RegisterEquipmentService>();
builder.Services.AddTransient<UpdateEquipmentService>();
builder.Services.AddTransient<StudentQueryService>();
builder.Services.AddTransient<EquipmentQueryService>();
builder.Services.AddTransient<BorrowingQueryService>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // A field the contract does not define (for example isAvailable on an equipment update) is a 400.
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Bad JSON, a wrong type, a bad query value: 400 with the same { code, message } body.
        options.InvalidModelStateResponseFactory = _ => new ObjectResult(new ErrorResponse(
            ErrorCodes.ValidationError,
            "The request is not valid. Check the JSON field names, the value types and the date format (YYYY-MM-DD)."))
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    });

builder.Services.AddOpenApi(); // machine-readable description at /openapi/v1.json

var app = builder.Build();

// ---- Optional command: load the controlled dataset into the TEST database, then exit ----
//   $env:EQUIPMENT_BORROWING_DB="Test"
//   dotnet run --project src/EquipmentBorrowing.Api -- --reset-test-data
if (args.Contains("--reset-test-data"))
{
    if (!useTestDatabase)
    {
        Console.Error.WriteLine("Refused: set $env:EQUIPMENT_BORROWING_DB=\"Test\" first. The reset only works on the test database.");
        return 2;
    }

    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    ControlledDataset.Reset(context, clock.Today);
    Console.WriteLine($"Controlled dataset loaded (S1-S4, E1-E7). Local date used: {clock.Today:yyyy-MM-dd}.");
    return 0;
}

// ---- Apply migrations at startup. If the database is unreachable the API still starts (needed for T08). ----
try
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    app.Logger.LogInformation("Database migrations are up to date.");
}
catch (Exception ex)
{
    app.Logger.LogWarning(
        "Could not apply migrations at startup ({Type}). The API keeps running; database routes return 500 until the database is reachable.",
        ex.GetType().Name);
}

if (useTestDatabase)
{
    app.Logger.LogWarning("[TEST DATABASE] Disposable test database in use. Local date used by the API: {Date:yyyy-MM-dd}. Fault mode: {Fault}",
        clock.Today, faultMode ?? "none");
}

app.UseMiddleware<ApiErrorMiddleware>();
app.MapOpenApi();
app.MapControllers();

app.Run();
return 0;

// Lab 6 (disclosed change, no behaviour change): makes the entry point visible to
// WebApplicationFactory<Program> so the xUnit integration tests can start the real API pipeline.
public partial class Program { }