using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Data;
using EquipmentBorrowing.Infrastructure.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace EquipmentBorrowing.Tests.Lab6;

// ---------------------------------------------------------------------------------------------
// Hosts used by E07 and E08. All of them are the REAL API pipeline against the disposable PostgreSQL test database.
// ---------------------------------------------------------------------------------------------

// A plain second (or third) API application instance: no fault, same database, same fixed date D.
public sealed class PlainApiFactory : WebApplicationFactory<Program>
{
    public PlainApiFactory()
    {
        _ = Lab6Settings.ConnectionString; // refuses a non-test database
        Environment.SetEnvironmentVariable("EQUIPMENT_BORROWING_DB", "Test");
        Environment.SetEnvironmentVariable("EQUIPMENT_BORROWING_FIXED_DATE", Lab6Settings.D.ToString("yyyy-MM-dd"));
        Environment.SetEnvironmentVariable("EQUIPMENT_BORROWING_FAULT", null);
    }
}

// The API's own T08 test seam: the API starts but every database call fails (a port where nothing listens).
// The seam is read once at start-up, so the variable is set here and cleared again by the test (finally block).
public sealed class BrokenConnectionFactory : WebApplicationFactory<Program>
{
    public BrokenConnectionFactory()
    {
        _ = Lab6Settings.ConnectionString;
        Environment.SetEnvironmentVariable("EQUIPMENT_BORROWING_DB", "Test");
        Environment.SetEnvironmentVariable("EQUIPMENT_BORROWING_FIXED_DATE", Lab6Settings.D.ToString("yyyy-MM-dd"));
        Environment.SetEnvironmentVariable("EQUIPMENT_BORROWING_FAULT", "BrokenConnection");
    }

    public static void ClearFault() => Environment.SetEnvironmentVariable("EQUIPMENT_BORROWING_FAULT", null);
}

// ---------------------------------------------------------------------------------------------
// TC6-E07 Transport and uncertain outcome (UC-03,06; BR-18,19; API-10,11)
//
//   E07a (no HTTP response, client handling)  : needs the desktop client class, added separately.
//   E07b (reachable API, controlled safe 500)  : below, server side.
//   E07c (lost commit acknowledgement)         : below, server side with genuine PostgreSQL.
//
// What these prove: the API distinguishes a KNOWN failure (server_error, nothing written) from an UNKNOWN outcome
// (outcome_unknown, the commit actually happened), performs no automatic retry, and PostgreSQL really holds
// all-or-none state. What they do NOT prove: how a client reacts (that is E07a/E07c on the client class), and the
// simulated acknowledgement loss alone is not a real network failure.
// ---------------------------------------------------------------------------------------------
public class TC6_E07_TransportAndUncertainOutcomeTests : ApiTestBase
{
    public TC6_E07_TransportAndUncertainOutcomeTests(ApiFixture api) : base(api) { }

    [Fact]
    public async Task E07b_A_reachable_API_whose_database_is_unreachable_returns_a_safe_500_and_writes_nothing()
    {
        var baseline = await DbSnapshot.TakeAsync();

        var broken = new BrokenConnectionFactory();
        ApiResult write, read;
        try
        {
            using var client = broken.CreateClient();
            write = await client.SendJsonAsync(HttpMethod.Post, "/api/v1/checkout-requests",
                new { studentId = Ids.S(4), equipmentIds = new[] { Ids.E(2), Ids.E(6) }, expectedReturnDate = Date(+2) });
            read = await client.FetchAsync("/api/v1/equipment");
        }
        finally
        {
            await broken.DisposeAsync();
            BrokenConnectionFactory.ClearFault();
        }

        // A response WAS received (this is not a transport failure) and it is a safe server error.
        Assert.Equal(HttpStatusCode.InternalServerError, write.Status);
        ErrorBody.AssertSafe(write, "server_error");
        Assert.Equal(HttpStatusCode.InternalServerError, read.Status);
        ErrorBody.AssertSafe(read, "server_error");
        Assert.DoesNotContain("Port=", write.Body, StringComparison.OrdinalIgnoreCase); // no connection details leak
        Assert.DoesNotContain("Port=", read.Body, StringComparison.OrdinalIgnoreCase);

        // Known failure: no partial writes (checked on a fresh connection to the real database).
        Assert.Equal(baseline, await DbSnapshot.TakeAsync());
    }

    [Fact]
    public async Task E07c_A_lost_commit_acknowledgement_is_reported_as_outcome_unknown_and_PostgreSQL_holds_all_or_none_state()
    {
        var baseline = await DbSnapshot.TakeAsync();
        Assert.Equal(4, await Pg.CountAsync("Borrowings"));

        var lost = new FaultApiFactory(new LostCommitResponseInjector());
        ApiResult result;
        try
        {
            using var client = lost.CreateClient();
            result = await client.SendJsonAsync(HttpMethod.Post, "/api/v1/checkout-requests",
                new { studentId = Ids.S(4), equipmentIds = new[] { Ids.E(2), Ids.E(6) }, expectedReturnDate = Date(+2) });
        }
        finally
        {
            await lost.DisposeAsync();
        }

        // Distinct handling: not "server_error" (known failure) but "outcome_unknown".
        Assert.Equal(HttpStatusCode.InternalServerError, result.Status);
        ErrorBody.AssertSafe(result, "outcome_unknown");

        // No automatic retry by the server: one acknowledgement point, each item written exactly once.
        Assert.Equal(1, lost.Injector.AfterCommitCalls);
        Assert.Equal(1, lost.Injector.AfterCommitFaults);
        Assert.Equal(new[] { 1, 2 }, lost.Injector.Writes.Select(w => w.Written).ToArray());
        Assert.All(lost.Injector.Writes, w => Assert.Equal(2, w.Total));

        // Independent fresh PostgreSQL read: the commit DID happen, and it is all-or-none (both items, no duplicates).
        Assert.Equal(6, await Pg.CountAsync("Borrowings"));
        var s4 = await Pg.RowsAsync("SELECT * FROM \"Borrowings\" WHERE \"StudentId\" = @s ORDER BY \"EquipmentId\"", ("s", Ids.S(4)));
        Assert.Equal(2, s4.Count);
        Assert.All(s4, r => Assert.True(Fresh.IsActive(r)));
        Assert.Equal(new[] { Ids.E(2), Ids.E(6) }.Order().ToArray(), s4.Select(r => Convert.ToInt32(r["EquipmentId"])).ToArray());
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(2)));
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(6)));
        Assert.NotEqual(baseline, await DbSnapshot.TakeAsync()); // the outcome was a real change, unknown to the caller

        // After state was verified, a blind re-submit is correctly refused (this is what a premature retry would hit).
        var afterCheck = await DbSnapshot.TakeAsync();
        var resubmit = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/checkout-requests",
            new { studentId = Ids.S(4), equipmentIds = new[] { Ids.E(2), Ids.E(6) }, expectedReturnDate = Date(+2) });
        Assert.Equal(HttpStatusCode.Conflict, resubmit.Status);
        Assert.Equal(afterCheck, await DbSnapshot.TakeAsync());
    }
}

// ---------------------------------------------------------------------------------------------
// Harness: starts the REAL API as a separate operating-system process (dotnet EquipmentBorrowing.Api.dll) against
// the disposable test database, so a stop/start is a genuine process restart, not a new in-memory host.
// ---------------------------------------------------------------------------------------------
public sealed class ApiProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly List<string> _output = new();

    public HttpClient Http { get; }
    public int Port { get; }
    public int Pid { get; }
    public bool HasExited => _process.HasExited;
    public string OutputTail { get { lock (_output) return string.Join(Environment.NewLine, _output.TakeLast(25)); } }

    private ApiProcess(Process process, int port)
    {
        _process = process;
        Port = port;
        Pid = process.Id;
        Http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(10) };
    }

    private void Capture(string? line)
    {
        if (line is null) return;
        lock (_output) _output.Add(line);
    }

    // The test project builds the API (project reference), so the API's own build output exists beside the repo root:
    // <root>\src\EquipmentBorrowing.Api\bin\<Configuration>\<tfm>\EquipmentBorrowing.Api.dll
    public static string FindApiDll()
    {
        var tfm = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var configuration = tfm.Parent!.Name;
        var root = tfm;
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "EquipmentBorrowing.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var dll = Path.Combine(root!.FullName, "src", "EquipmentBorrowing.Api", "bin", configuration, tfm.Name, "EquipmentBorrowing.Api.dll");
        Assert.True(File.Exists(dll), $"API build output not found: {dll} (run 'dotnet build' first).");
        return dll;
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static async Task<ApiProcess> StartAsync()
    {
        _ = Lab6Settings.ConnectionString; // refuses a non-test database (the child reads the same user-secret)
        var dll = FindApiDll();
        var port = FreePort();

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(dll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(dll);
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add($"http://127.0.0.1:{port}");
        startInfo.Environment["EQUIPMENT_BORROWING_DB"] = "Test";
        startInfo.Environment["EQUIPMENT_BORROWING_FIXED_DATE"] = Lab6Settings.D.ToString("yyyy-MM-dd");
        startInfo.Environment.Remove("EQUIPMENT_BORROWING_FAULT");
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        var process = new Process { StartInfo = startInfo };
        // Handlers are attached before Start so no start-up line is lost. Output is kept only as failure evidence.
        ApiProcess? instance = null;
        process.OutputDataReceived += (_, e) => instance?.Capture(e.Data);
        process.ErrorDataReceived += (_, e) => instance?.Capture(e.Data);
        process.Start();
        instance = new ApiProcess(process, port);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await instance.WaitUntilReadyAsync();
        return instance;
    }

    private async Task WaitUntilReadyAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
                throw new InvalidOperationException($"The API process exited early (exit code {_process.ExitCode}).{Environment.NewLine}{OutputTail}");
            try
            {
                using var response = await Http.GetAsync("/api/v1/equipment");
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { /* not listening yet */ }
            catch (TaskCanceledException) { /* slow start */ }
            await Task.Delay(250);
        }
        throw new TimeoutException($"The API process did not become ready on port {Port}.{Environment.NewLine}{OutputTail}");
    }

    public async Task StopAsync()
    {
        if (_process.HasExited) return;
        _process.Kill(entireProcessTree: true);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await _process.WaitForExitAsync(cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        Http.Dispose();
        _process.Dispose();
    }
}

// ---------------------------------------------------------------------------------------------
// TC6-E08 Fresh connections and restart (UC-03,05,12,15; BR-13,14,19; API-03,07,10,12,13)
//   Part 1 (automated): every API application instance is disposed, then all state is read back through a brand-new
//           PostgreSQL connection AND a brand-new EF Core context; a NEW instance then serves the same data.
//   Part 2 (harness):   the real API process is killed and a new process started; records are retrieved and compared
//           with fresh PostgreSQL state. Method used: DOCUMENTED INTEGRATION HARNESS (ApiProcess above).
// ---------------------------------------------------------------------------------------------
public class TC6_E08_FreshConnectionsAndRestartTests : ApiTestBase
{
    private readonly ITestOutputHelper _output;

    public TC6_E08_FreshConnectionsAndRestartTests(ApiFixture api, ITestOutputHelper output) : base(api) => _output = output;

    private static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Lab6Settings.ConnectionString).Options);

    [Fact]
    public async Task Part1_state_survives_disposed_hosts_and_is_read_back_through_fresh_connections_and_contexts()
    {
        int studentId, equipmentId, borrowingId;

        // ---- original application instance: create student and equipment, issue a borrowing ----
        await using (var first = new PlainApiFactory())
        {
            using var client = first.CreateClient();

            var student = await client.SendJsonAsync(HttpMethod.Post, "/api/v1/students",
                new { studentNumber = "ST-500", fullName = "Eli Tan", contact = "09171234567" });
            Assert.Equal(HttpStatusCode.Created, student.Status);
            studentId = student.Json<StudentRow>().Id;

            var item = await client.SendJsonAsync(HttpMethod.Post, "/api/v1/equipment",
                new { equipmentCode = "EQ-008", name = "Charger", description = "USB-C" });
            Assert.Equal(HttpStatusCode.Created, item.Status);
            equipmentId = item.Json<EquipmentRow>().Id;

            var loan = await client.SendJsonAsync(HttpMethod.Post, "/api/v1/borrowings",
                new { studentId, equipmentId, expectedReturnDate = Date(+2) });
            Assert.Equal(HttpStatusCode.Created, loan.Status);
            borrowingId = loan.Json<BorrowingRow>().Id;
        } // the original application instance and all its database contexts are disposed here

        // ---- fresh PostgreSQL connection ----
        var dbStudent = Assert.Single(await Pg.RowsAsync("SELECT * FROM \"Students\" WHERE \"Id\" = @i", ("i", studentId)));
        Assert.Equal("ST-500", (string)dbStudent["StudentNumber"]!);
        Assert.Equal("Eli Tan", (string)dbStudent["Name"]!);
        Assert.Equal("09171234567", (string)dbStudent["Contact"]!);
        Assert.True((bool)dbStudent["IsAllowedToBorrow"]!);
        var dbItem = Assert.Single(await Pg.RowsAsync("SELECT * FROM \"Equipment\" WHERE \"Id\" = @i", ("i", equipmentId)));
        Assert.Equal("EQ-008", (string)dbItem["Identifier"]!);
        Assert.False((bool)dbItem["IsAvailable"]!); // borrowed
        var dbLoan = await Fresh.BorrowingAsync(borrowingId);
        Assert.Equal(studentId, Convert.ToInt32(dbLoan["StudentId"]));
        Assert.Equal(equipmentId, Convert.ToInt32(dbLoan["EquipmentId"]));
        Assert.Equal(Dates.On(0), (DateOnly)dbLoan["DateBorrowed"]!);
        Assert.Equal(Dates.On(+2), (DateOnly)dbLoan["ExpectedReturnDate"]!);
        Assert.Null(dbLoan["ReturnedDate"]);
        Assert.True(Fresh.IsActive(dbLoan));

        // ---- fresh EF Core context (a different path to the same truth: mapping and relationships) ----
        await using (var context = NewContext())
        {
            var s = await context.Students.AsNoTracking().SingleAsync(x => x.Id == studentId);
            var e = await context.Equipment.AsNoTracking().SingleAsync(x => x.Id == equipmentId);
            var b = await context.Borrowings.AsNoTracking().SingleAsync(x => x.Id == borrowingId);
            Assert.Equal("Eli Tan", s.Name);
            Assert.False(e.IsAvailable);
            Assert.Equal(s.Id, b.StudentId);
            Assert.Equal(e.Id, b.EquipmentId);
            Assert.Equal(0, (int)b.Status);
            Assert.Equal(Lab6Settings.D.Date, b.DateBorrowed.Date);
        }

        // ---- a NEW application instance serves the persisted records (API-03, 07, 13) ----
        await using (var second = new PlainApiFactory())
        {
            using var client = second.CreateClient();
            Assert.Equal("Eli Tan", (await client.FetchAsync($"/api/v1/students/{studentId}")).Json<StudentRow>().FullName);
            Assert.False((await client.FetchAsync($"/api/v1/equipment/{equipmentId}")).Json<EquipmentRow>().IsAvailable);
            var read = await client.FetchAsync($"/api/v1/borrowings/{borrowingId}");
            Assert.Equal(HttpStatusCode.OK, read.Status);
            Assert.Equal("Active", read.Json<BorrowingRow>().Status);

            // ---- return the item (API-12) ----
            var returned = await client.SendJsonAsync(HttpMethod.Post, $"/api/v1/borrowings/{borrowingId}/return", new { });
            Assert.Equal(HttpStatusCode.OK, returned.Status);
            Assert.Equal("Returned", returned.Json<BorrowingRow>().Status);
        }

        // ---- another fresh connection and context: retained history and availability ----
        var afterReturn = await Fresh.BorrowingAsync(borrowingId);
        Assert.False(Fresh.IsActive(afterReturn));
        Assert.Equal(Dates.On(0), (DateOnly)afterReturn["ReturnedDate"]!);
        Assert.True(await Fresh.IsAvailableAsync(equipmentId));
        Assert.Equal(5, await Pg.CountAsync("Borrowings")); // history retained: the 4 fixture rows plus this one
        Assert.Equal(5, await Pg.CountAsync("Students"));
        Assert.Equal(8, await Pg.CountAsync("Equipment"));
        await using (var context = NewContext())
        {
            var b = await context.Borrowings.AsNoTracking().SingleAsync(x => x.Id == borrowingId);
            Assert.NotEqual(0, (int)b.Status); // no longer Active (0)
            Assert.NotNull(b.ReturnedDate);
            Assert.True((await context.Equipment.AsNoTracking().SingleAsync(x => x.Id == equipmentId)).IsAvailable);
        }

        // ---- a third instance still shows the returned loan in the student's history ----
        await using (var third = new PlainApiFactory())
        {
            using var client = third.CreateClient();
            var history = await client.FetchAsync($"/api/v1/students/{studentId}/borrowings");
            Assert.Equal(HttpStatusCode.OK, history.Status);
            var record = Assert.Single(history.Json<List<BorrowingRow>>());
            Assert.Equal(borrowingId, record.Id);
            Assert.Equal("Returned", record.Status);
            Assert.Equal(Date(0), record.ReturnedDate);
        }
    }

    [Fact]
    public async Task Part2_harness_a_real_API_process_restart_keeps_created_and_returned_records()
    {
        StudentRow student;
        EquipmentRow item;
        BorrowingRow loan;
        int firstPid, firstPort;

        // ---- process 1: create, issue, return, read everything back ----
        var first = await ApiProcess.StartAsync();
        try
        {
            firstPid = first.Pid;
            firstPort = first.Port;
            _output.WriteLine($"Process 1: pid {firstPid}, port {firstPort}");

            var s = await first.Http.SendJsonAsync(HttpMethod.Post, "/api/v1/students",
                new { studentNumber = "ST-500", fullName = "Eli Tan", contact = "09171234567" });
            Assert.Equal(HttpStatusCode.Created, s.Status);
            var sid = s.Json<StudentRow>().Id;

            var e = await first.Http.SendJsonAsync(HttpMethod.Post, "/api/v1/equipment",
                new { equipmentCode = "EQ-008", name = "Charger", description = "USB-C" });
            Assert.Equal(HttpStatusCode.Created, e.Status);
            var eid = e.Json<EquipmentRow>().Id;

            var b = await first.Http.SendJsonAsync(HttpMethod.Post, "/api/v1/borrowings",
                new { studentId = sid, equipmentId = eid, expectedReturnDate = Date(+2) });
            Assert.Equal(HttpStatusCode.Created, b.Status);
            var bid = b.Json<BorrowingRow>().Id;

            var r = await first.Http.SendJsonAsync(HttpMethod.Post, $"/api/v1/borrowings/{bid}/return", new { });
            Assert.Equal(HttpStatusCode.OK, r.Status);

            // The state exactly as process 1 reports it (the "before restart" evidence).
            student = (await first.Http.FetchAsync($"/api/v1/students/{sid}")).Json<StudentRow>();
            item = (await first.Http.FetchAsync($"/api/v1/equipment/{eid}")).Json<EquipmentRow>();
            loan = (await first.Http.FetchAsync($"/api/v1/borrowings/{bid}")).Json<BorrowingRow>();
            Assert.Equal("Returned", loan.Status);
            Assert.True(item.IsAvailable);
        }
        finally
        {
            await first.StopAsync();
        }

        // ---- the process is really gone ----
        Assert.True(first.HasExited);
        await Assert.ThrowsAsync<HttpRequestException>(() => first.Http.GetAsync("/api/v1/equipment"));
        _output.WriteLine($"Process 1 stopped; requests to port {firstPort} now fail (no HTTP response).");
        await first.DisposeAsync();

        // ---- process 2: a brand-new operating-system process ----
        await using var second = await ApiProcess.StartAsync();
        _output.WriteLine($"Process 2: pid {second.Pid}, port {second.Port}");
        Assert.NotEqual(firstPid, second.Pid); // a different process: this was a genuine restart

        var studentAfter = await second.Http.FetchAsync($"/api/v1/students/{student.Id}");
        var itemAfter = await second.Http.FetchAsync($"/api/v1/equipment/{item.Id}");
        var loanAfter = await second.Http.FetchAsync($"/api/v1/borrowings/{loan.Id}");
        Assert.Equal(HttpStatusCode.OK, studentAfter.Status);
        Assert.Equal(HttpStatusCode.OK, itemAfter.Status);
        Assert.Equal(HttpStatusCode.OK, loanAfter.Status);

        // Same persisted state before and after the restart.
        Assert.Equal(student, studentAfter.Json<StudentRow>());
        Assert.Equal(item, itemAfter.Json<EquipmentRow>());
        Assert.Equal(loan, loanAfter.Json<BorrowingRow>());
        var history = (await second.Http.FetchAsync($"/api/v1/students/{student.Id}/borrowings")).Json<List<BorrowingRow>>();
        Assert.Equal(loan, Assert.Single(history));

        // ...and equal to what a fresh PostgreSQL connection holds (the restart did not reseed or reset anything).
        var db = await Fresh.BorrowingAsync(loan.Id);
        Assert.False(Fresh.IsActive(db));
        Assert.Equal(Dates.On(0), (DateOnly)db["ReturnedDate"]!);
        Assert.Equal(student.StudentNumber, await Pg.ScalarAsync<string>(
            "SELECT \"StudentNumber\" FROM \"Students\" WHERE \"Id\" = @i", ("i", student.Id)));
        Assert.True(await Fresh.IsAvailableAsync(item.Id));
        Assert.Equal(5, await Pg.CountAsync("Borrowings"));
        Assert.Equal(5, await Pg.CountAsync("Students"));
        Assert.Equal(8, await Pg.CountAsync("Equipment"));
    }
}
