using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EquipmentBorrowing.Desktop.Api;

public enum ApiOutcome
{
    Success,
    ValidationError,  // 400: the API answered
    NotFound,         // 404: the API answered
    Conflict,         // 409: the API answered
    ServerError,      // 500 or other: the API answered, but failed safely
    Unreachable,      // no HTTP response was received at all
    UnknownOutcome    // a change was sent, but its result is not known: refresh before retrying
}

public sealed class ApiResult<T>
{
    public ApiOutcome Outcome { get; init; }
    public T? Value { get; init; }
    public string Message { get; init; } = "";
    public bool IsSuccess => Outcome == ApiOutcome.Success;
}

public sealed class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public ApiClient(HttpClient http) => _http = http;

    public Task<ApiResult<T>> GetAsync<T>(string url, CancellationToken ct = default)
        => SendAsync<T>(HttpMethod.Get, url, null, false, ct);

    public Task<ApiResult<T>> PostAsync<T>(string url, object body, CancellationToken ct = default)
        => SendAsync<T>(HttpMethod.Post, url, body, true, ct);

    public Task<ApiResult<T>> PutAsync<T>(string url, object body, CancellationToken ct = default)
        => SendAsync<T>(HttpMethod.Put, url, body, true, ct);

    public Task<ApiResult<T>> PatchAsync<T>(string url, object body, CancellationToken ct = default)
        => SendAsync<T>(HttpMethod.Patch, url, body, true, ct);

    private sealed record ApiError(string? Code, string? Message);

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method, string url, object? body, bool isChange, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, body.GetType(), options: Json);
            }

            using var response = await _http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
                return value is null
                    ? Fail<T>(ApiOutcome.ServerError, "The server sent an empty answer. Refresh and try again.")
                    : new ApiResult<T> { Outcome = ApiOutcome.Success, Value = value };
            }

            // The API answered with an error status, so this is NOT a connection problem.
            var (code, message) = await ReadErrorAsync(response, ct);

            // The server says the commit result is not known (BR-18): never retry blindly.
            if (code == "outcome_unknown")
            {
                return Fail<T>(ApiOutcome.UnknownOutcome, message);
            }

            var outcome = response.StatusCode switch
            {
                HttpStatusCode.BadRequest => ApiOutcome.ValidationError,
                HttpStatusCode.NotFound => ApiOutcome.NotFound,
                HttpStatusCode.Conflict => ApiOutcome.Conflict,
                _ => ApiOutcome.ServerError
            };
            return Fail<T>(outcome, message);
        }
        catch (HttpRequestException ex)
        {
            // Connection refused or host not found: there was no HTTP response.
            if (ex.InnerException is SocketException se &&
                se.SocketErrorCode is SocketError.ConnectionRefused
                    or SocketError.HostNotFound
                    or SocketError.NetworkUnreachable)
            {
                return Fail<T>(ApiOutcome.Unreachable,
                    "Cannot reach the server. Check that the API is running, then try again.");
            }

            return isChange
                ? Fail<T>(ApiOutcome.UnknownOutcome,
                    "The connection was lost. The change may or may not have been saved. Refresh the lists and check before trying again.")
                : Fail<T>(ApiOutcome.Unreachable,
                    "Cannot reach the server. Check that the API is running, then try again.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // The HttpClient timed out.
            return isChange
                ? Fail<T>(ApiOutcome.UnknownOutcome,
                    "The server did not answer in time. The change may or may not have been saved. Refresh the lists and check before trying again.")
                : Fail<T>(ApiOutcome.Unreachable,
                    "The server did not answer in time. Try again.");
        }
        catch (JsonException)
        {
            return Fail<T>(ApiOutcome.ServerError, "The server sent an answer the app could not read. Refresh and try again.");
        }
    }

    private static async Task<(string? Code, string Message)> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(Json, ct);
            if (!string.IsNullOrWhiteSpace(error?.Message)) return (error!.Code, error.Message!);
        }
        catch (JsonException) { }

        return (null, "The server could not complete the request. Refresh the lists, then try again.");
    }

    private static ApiResult<T> Fail<T>(ApiOutcome outcome, string message)
        => new() { Outcome = outcome, Message = message };
}