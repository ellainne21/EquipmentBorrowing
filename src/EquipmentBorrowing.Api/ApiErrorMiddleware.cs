using EquipmentBorrowing.Contracts;

namespace EquipmentBorrowing.Api;

// Makes every failure leave the API in the same safe shape: { "code": "...", "message": "..." }.
// - An unexpected exception becomes a 500 with a safe message (no stack trace, no connection details).
// - A route that does not exist becomes a 404, and a wrong method on a real route becomes a 405.
public sealed class ApiErrorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiErrorMiddleware> _logger;

    public ApiErrorMiddleware(RequestDelegate next, ILogger<ApiErrorMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The caller closed the connection. There is nobody to answer.
            return;
        }
        catch (Exception ex)
        {
            // The server log gets the exception type and message only (no stack trace, no secrets are added).
            _logger.LogError("Request {Method} {Path} failed: {Type}: {Message}",
                context.Request.Method, context.Request.Path, ex.GetType().Name, ex.Message);

            if (context.Response.HasStarted) return;

            context.Response.Clear();
            await WriteAsync(context, StatusCodes.Status500InternalServerError, ErrorCodes.ServerError,
                "The server could not complete the request. No changes are confirmed. " +
                "Check that the database is reachable, refresh the data, then try again.");
            return;
        }

        // Bare 404 / 405 from routing (no body, no content type yet): add the standard error body.
        if (!context.Response.HasStarted
            && context.Response.ContentLength is null
            && string.IsNullOrEmpty(context.Response.ContentType))
        {
            if (context.Response.StatusCode == StatusCodes.Status404NotFound)
            {
                await WriteAsync(context, StatusCodes.Status404NotFound, ErrorCodes.NotFound,
                    "There is no such route. Check the address and the /api/v1 prefix.");
            }
            else if (context.Response.StatusCode == StatusCodes.Status405MethodNotAllowed)
            {
                await WriteAsync(context, StatusCodes.Status405MethodNotAllowed, ErrorCodes.MethodNotAllowed,
                    "This route does not support that HTTP method.");
            }
        }
    }

    private static Task WriteAsync(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ErrorResponse(code, message), context.RequestAborted);
    }
}
