using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EquipmentBorrowing.Desktop.Api;
using EquipmentBorrowing.Infrastructure.Testing;

namespace EquipmentBorrowing.Tests.Lab6;

// ---------------------------------------------------------------------------------------------
// Test doubles for the desktop client's HTTP transport.
// A double can simulate "no response", a slow server, a lost connection or a canned status. It CANNOT prove that the
// server persisted anything or rolled anything back; those claims below use the real API and genuine PostgreSQL.
// ---------------------------------------------------------------------------------------------
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _respond;
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) => _respond = respond;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _count);
        return _respond(request);
    }

    public static StubHandler Throwing(Exception exception) =>
        new(_ => Task.FromException<HttpResponseMessage>(exception));

    public static StubHandler Responding(HttpStatusCode status, string? body, string mediaType = "application/json") =>
        new(_ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = body is null ? null : new StringContent(body, Encoding.UTF8, mediaType)
        }));

    public static HttpClient ClientFor(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("http://localhost/") };
}

// Counts every request that really leaves the client (used to prove "no automatic retry").
internal sealed class CountingHandler : DelegatingHandler
{
    private int _count;
    public int Count => Volatile.Read(ref _count);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _count);
        return base.SendAsync(request, cancellationToken);
    }
}

internal static class Flat
{
    public static (ApiOutcome Outcome, string Message, bool HasValue) Of<T>(ApiResult<T> result) =>
        (result.Outcome, result.Message, result.Value is not null);
}

// ---------------------------------------------------------------------------------------------
// TC6-E07a / E07b / E07c, client side, with a controlled HTTP test double.
// (UC-03,06; BR-18,19; API-10,11)
// ---------------------------------------------------------------------------------------------
public class TC6_E07_ClientDoubleTests
{
    private static Task<(ApiOutcome Outcome, string Message, bool HasValue)> SendAsync(HttpMessageHandler handler, bool isChange)
    {
        var client = new ApiClient(StubHandler.ClientFor(handler));
        return isChange
            ? Task.Run(async () => Flat.Of(await new BorrowingsApi(client).CheckoutAsync(1, new List<int> { 2, 6 }, Dates.On(+2))))
            : Task.Run(async () => Flat.Of(await new EquipmentApi(client).SearchAsync(null, false)));
    }

    // ---- E07a: no HTTP response ----------------------------------------------------------------
    [Theory]
    [InlineData(SocketError.ConnectionRefused, false)]
    [InlineData(SocketError.ConnectionRefused, true)]
    [InlineData(SocketError.HostNotFound, false)]
    [InlineData(SocketError.HostNotFound, true)]
    [InlineData(SocketError.NetworkUnreachable, false)]
    [InlineData(SocketError.NetworkUnreachable, true)]
    public async Task E07a_no_HTTP_response_is_reported_as_unreachable_with_no_value_and_no_retry(SocketError error, bool isChange)
    {
        var handler = StubHandler.Throwing(new HttpRequestException("no response", new SocketException((int)error)));

        var (outcome, message, hasValue) = await SendAsync(handler, isChange);

        Assert.Equal(ApiOutcome.Unreachable, outcome); // a transport failure, not an HTTP status
        Assert.NotEqual(ApiOutcome.ServerError, outcome);
        Assert.False(hasValue);
        Assert.Contains("Cannot reach the server", message);
        Assert.DoesNotContain("saved", message, StringComparison.OrdinalIgnoreCase); // nothing was sent: no "maybe saved" claim
        Assert.Equal(1, handler.Count); // one attempt, no automatic retry
    }

    // ---- the connection is lost AFTER the request may have been sent ----------------------------
    [Fact]
    public async Task A_lost_connection_during_a_change_is_an_unknown_outcome_but_during_a_read_it_is_just_unreachable()
    {
        var write = StubHandler.Throwing(new HttpRequestException("connection reset"));
        var (writeOutcome, writeMessage, _) = await SendAsync(write, isChange: true);
        Assert.Equal(ApiOutcome.UnknownOutcome, writeOutcome);
        Assert.Contains("may or may not have been saved", writeMessage);
        Assert.Contains("check before trying again", writeMessage);
        Assert.Equal(1, write.Count);

        var read = StubHandler.Throwing(new HttpRequestException("connection reset"));
        var (readOutcome, _, _) = await SendAsync(read, isChange: false);
        Assert.Equal(ApiOutcome.Unreachable, readOutcome);
        Assert.Equal(1, read.Count);
    }

    [Fact]
    public async Task A_timeout_during_a_change_is_an_unknown_outcome_but_during_a_read_it_is_unreachable()
    {
        var write = StubHandler.Throwing(new TaskCanceledException("timed out"));
        var (writeOutcome, writeMessage, _) = await SendAsync(write, isChange: true);
        Assert.Equal(ApiOutcome.UnknownOutcome, writeOutcome);
        Assert.Contains("may or may not have been saved", writeMessage);
        Assert.Equal(1, write.Count);

        var read = StubHandler.Throwing(new TaskCanceledException("timed out"));
        var (readOutcome, readMessage, _) = await SendAsync(read, isChange: false);
        Assert.Equal(ApiOutcome.Unreachable, readOutcome);
        Assert.Contains("did not answer in time", readMessage);
        Assert.Equal(1, read.Count);
    }

    // ---- E07b: the API answered with an error status (a response WAS received) -------------------
    [Theory]
    [InlineData(400, "validation_error", ApiOutcome.ValidationError)]
    [InlineData(404, "not_found", ApiOutcome.NotFound)]
    [InlineData(409, "conflict", ApiOutcome.Conflict)]
    [InlineData(500, "server_error", ApiOutcome.ServerError)]
    [InlineData(405, "method_not_allowed", ApiOutcome.ServerError)]
    public async Task E07b_an_error_status_keeps_its_own_outcome_and_shows_the_servers_safe_message(
        int status, string code, ApiOutcome expected)
    {
        var handler = StubHandler.Responding((HttpStatusCode)status, $"{{\"code\":\"{code}\",\"message\":\"Server message.\"}}");

        var (outcome, message, hasValue) = await SendAsync(handler, isChange: true);

        Assert.Equal(expected, outcome);
        Assert.NotEqual(ApiOutcome.Unreachable, outcome); // an answered request is never "unreachable"
        Assert.Equal("Server message.", message);
        Assert.False(hasValue);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task E07b_a_500_without_a_readable_body_still_becomes_a_safe_server_error_message()
    {
        var handler = StubHandler.Responding(HttpStatusCode.InternalServerError, "<html>oops</html>", "text/html");

        var (outcome, message, _) = await SendAsync(handler, isChange: true);

        Assert.Equal(ApiOutcome.ServerError, outcome);
        Assert.DoesNotContain("<html>", message);
        Assert.Contains("Refresh the lists", message);
        Assert.Equal(1, handler.Count);
    }

    // ---- E07c: the server reports that the commit result is not known ------------------------------
    [Fact]
    public async Task E07c_outcome_unknown_from_the_server_is_an_unknown_outcome_and_is_never_retried_in_the_background()
    {
        var handler = StubHandler.Responding(HttpStatusCode.InternalServerError,
            """{"code":"outcome_unknown","message":"The result of the checkout is not known. Refresh before trying again."}""");

        var (outcome, message, hasValue) = await SendAsync(handler, isChange: true);
        await Task.Delay(400); // give any (unwanted) automatic retry time to show up

        Assert.Equal(ApiOutcome.UnknownOutcome, outcome);
        Assert.NotEqual(ApiOutcome.ServerError, outcome); // distinct from a known failure
        Assert.Contains("Refresh", message);
        Assert.False(hasValue);
        Assert.Equal(1, handler.Count); // still exactly one request
    }
}

// ---------------------------------------------------------------------------------------------
// The same client against the REAL API (genuine HTTP, genuine PostgreSQL).
// ---------------------------------------------------------------------------------------------
public class TC6_E07_ClientAgainstRealApiTests : ApiTestBase
{
    public TC6_E07_ClientAgainstRealApiTests(ApiFixture api) : base(api) { }

    private static string ServerMessage(ApiResult result)
    {
        using var doc = JsonDocument.Parse(result.Body);
        return doc.RootElement.GetProperty("message").GetString() ?? "";
    }

    private static int ClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task E07a_real_transport_to_a_closed_port_gives_no_response_and_the_client_reports_unreachable()
    {
        // No double at all: the operating system refuses the connection, so this checks the client's real exception mapping.
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{ClosedPort()}/"), Timeout = TimeSpan.FromSeconds(10) };
        var client = new ApiClient(http);

        var read = Flat.Of(await new EquipmentApi(client).SearchAsync(null, false));
        var write = Flat.Of(await new BorrowingsApi(client).CheckoutAsync(Ids.S(4), new List<int> { Ids.E(2), Ids.E(6) }, Dates.On(+2)));

        Assert.Equal(ApiOutcome.Unreachable, read.Outcome);
        Assert.Equal(ApiOutcome.Unreachable, write.Outcome); // the request never reached a server: not "maybe saved"
        Assert.False(read.HasValue);
        Assert.False(write.HasValue);
        Assert.Contains("Cannot reach the server", read.Message);
        Assert.Contains("Cannot reach the server", write.Message);
    }

    [Fact]
    public async Task E07b_a_reachable_API_with_an_unreachable_database_gives_the_client_a_server_error_not_unreachable()
    {
        var baseline = await DbSnapshot.TakeAsync();
        var counting = new CountingHandler();
        var broken = new BrokenConnectionFactory();
        (ApiOutcome Outcome, string Message, bool HasValue) write, read;
        ApiResult rawWrite, rawRead;
        try
        {
            var client = new ApiClient(broken.CreateDefaultClient(counting));
            write = Flat.Of(await new BorrowingsApi(client).CheckoutAsync(Ids.S(4), new List<int> { Ids.E(2), Ids.E(6) }, Dates.On(+2)));
            read = Flat.Of(await new EquipmentApi(client).SearchAsync(null, false));

            // The SAME requests with a plain HTTP client (not counted): what the server itself says.
            using var plain = broken.CreateClient();
            rawWrite = await plain.SendJsonAsync(HttpMethod.Post, "/api/v1/checkout-requests",
                new { studentId = Ids.S(4), equipmentIds = new[] { Ids.E(2), Ids.E(6) }, expectedReturnDate = Date(+2) });
            rawRead = await plain.FetchAsync("/api/v1/equipment");
        }
        finally
        {
            await broken.DisposeAsync();
            BrokenConnectionFactory.ClearFault();
        }

        Assert.Equal(ApiOutcome.ServerError, write.Outcome); // a response was received, so it is not Unreachable
        Assert.Equal(ApiOutcome.ServerError, read.Outcome);
        Assert.DoesNotContain("Cannot reach", write.Message);

        // The client must show the server's own safe message, not a generic fallback.
        Assert.True(ServerMessage(rawWrite) == write.Message,
            $"Client showed: \"{write.Message}\" but the server said: \"{ServerMessage(rawWrite)}\"");
        Assert.True(ServerMessage(rawRead) == read.Message,
            $"Client showed: \"{read.Message}\" but the server said: \"{ServerMessage(rawRead)}\"");
        Assert.False(write.HasValue);
        Assert.Equal(2, counting.Count); // one request per call, no retries
        Assert.Equal(baseline, await DbSnapshot.TakeAsync()); // known failure: no partial writes
    }

    [Fact]
    public async Task E07c_a_lost_commit_acknowledgement_makes_the_client_report_an_unknown_outcome_without_retrying_and_a_refresh_shows_the_truth()
    {
        var counting = new CountingHandler();
        var lost = new FaultApiFactory(new LostCommitResponseInjector());
        (ApiOutcome Outcome, string Message, bool HasValue) result;
        try
        {
            var client = new ApiClient(lost.CreateDefaultClient(counting));
            result = Flat.Of(await new BorrowingsApi(client).CheckoutAsync(Ids.S(4), new List<int> { Ids.E(2), Ids.E(6) }, Dates.On(+2)));
            await Task.Delay(400); // an automatic retry, if one existed, would have happened by now
        }
        finally
        {
            await lost.DisposeAsync();
        }

        // The client's feedback: unknown outcome, not a known failure and not a success.
        Assert.Equal(ApiOutcome.UnknownOutcome, result.Outcome);
        Assert.False(result.HasValue);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Equal(1, counting.Count); // no automatic retry
        Assert.Equal(1, lost.Injector.AfterCommitCalls); // and none inside the server either

        // Fresh independent PostgreSQL read: the commit really happened, all or none (both items, once).
        var rows = await Pg.RowsAsync("SELECT \"Id\", \"EquipmentId\" FROM \"Borrowings\" WHERE \"StudentId\" = @s ORDER BY \"EquipmentId\"", ("s", Ids.S(4)));
        Assert.Equal(2, rows.Count);
        Assert.Equal(6, await Pg.CountAsync("Borrowings"));
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(2)));
        Assert.False(await Fresh.IsAvailableAsync(Ids.E(6)));

        // "Refresh before retrying": the client's own read path (against a healthy API) shows the committed records.
        var history = await new BorrowingsApi(new ApiClient(Client)).StudentHistoryAsync(Ids.S(4));
        Assert.True(history.IsSuccess);
        Assert.Equal(rows.Select(r => Convert.ToInt32(r["Id"])).Order().ToArray(), history.Value!.Select(b => b.Id).Order().ToArray());
    }
}

// ---------------------------------------------------------------------------------------------
// Desktop architecture boundary (automated evidence for MA6-02): the desktop reaches data only through HTTP.
// ---------------------------------------------------------------------------------------------
internal static class RepoRoot
{
    public static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EquipmentBorrowing.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}

public class TC6_Architecture_DesktopBoundaryTests
{
    private static string DesktopDir() => Path.Combine(RepoRoot.Find(), "src", "EquipmentBorrowing.Desktop");

    [Fact]
    public void The_desktop_project_references_only_Contracts_and_no_database_package()
    {
        var csproj = XDocument.Load(Path.Combine(DesktopDir(), "EquipmentBorrowing.Desktop.csproj"));

        var projects = csproj.Descendants("ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(((string)e.Attribute("Include")!).Replace('\\', '/')))
            .ToArray();
        Assert.Equal(new[] { "EquipmentBorrowing.Contracts" }, projects);

        var packages = csproj.Descendants("PackageReference").Select(e => (string)e.Attribute("Include")!).ToList();
        Assert.NotEmpty(packages);
        Assert.DoesNotContain(packages, p => Regex.IsMatch(p, "Npgsql|EntityFramework|Dapper|SqlClient|Sqlite", RegexOptions.IgnoreCase));
    }

    [Fact]
    public void No_desktop_source_file_uses_the_database_or_the_server_side_layers()
    {
        var forbidden = new Regex(
            @"^\s*(global\s+)?using\s+(static\s+)?(Npgsql|Microsoft\.EntityFrameworkCore|EquipmentBorrowing\.(Infrastructure|Domain|Application))\b",
            RegexOptions.Multiline);

        var offenders = Directory.EnumerateFiles(DesktopDir(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => forbidden.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(DesktopDir(), f))
            .ToList();

        Assert.True(offenders.Count == 0, "Desktop files that bypass the API: " + string.Join(", ", offenders));
    }
}
