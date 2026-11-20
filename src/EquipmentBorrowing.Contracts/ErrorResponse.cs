namespace EquipmentBorrowing.Contracts;

// Body of every error response: { "code": "...", "message": "..." }.
// It never contains a password, a connection string or a stack trace.
public sealed record ErrorResponse(string Code, string Message);

// Stable error codes. The Avalonia client uses them to choose the right message.
public static class ErrorCodes
{
    public const string ValidationError = "validation_error";   // HTTP 400
    public const string NotFound = "not_found";                 // HTTP 404
    public const string MethodNotAllowed = "method_not_allowed"; // HTTP 405
    public const string Conflict = "conflict";                  // HTTP 409
    public const string ServerError = "server_error";           // HTTP 500, known failure, nothing was changed
    public const string OutcomeUnknown = "outcome_unknown";     // HTTP 500, commit result not known (BR-18)
}
