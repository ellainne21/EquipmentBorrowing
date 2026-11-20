using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentBorrowing.Api.Controllers;

// Shared helpers: one place decides how a Lab 4 result becomes an HTTP status.
//   400 = the request itself is not valid (checked here, before any business rule)
//   404 = the student / item / borrowing does not exist
//   409 = a business rule or a duplicate said no (the Lab 4 service "Rejected" outcome)
//   500 = known failure (nothing saved) or unknown outcome (commit result not known, BR-18)
public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult ApiError(int status, string code, string message)
        => StatusCode(status, new ErrorResponse(code, message));

    protected ObjectResult BadInput(string message)
        => ApiError(StatusCodes.Status400BadRequest, ErrorCodes.ValidationError, message);

    protected ObjectResult Missing(string message)
        => ApiError(StatusCodes.Status404NotFound, ErrorCodes.NotFound, message);

    protected ObjectResult ServerFailure(string? message = null)
        => ApiError(StatusCodes.Status500InternalServerError, ErrorCodes.ServerError,
            message ?? "The database could not complete the request. Refresh the data, then try again.");

    // For an operation that did NOT succeed.
    protected ObjectResult FromOperation(OperationResult result) => result.Outcome switch
    {
        OperationOutcome.Rejected => ApiError(StatusCodes.Status409Conflict, ErrorCodes.Conflict, result.Message),
        OperationOutcome.Failed => ApiError(StatusCodes.Status500InternalServerError, ErrorCodes.ServerError, result.Message),
        OperationOutcome.Unknown => ApiError(StatusCodes.Status500InternalServerError, ErrorCodes.OutcomeUnknown, result.Message),
        _ => ServerFailure()
    };

    // After a successful save, the saved record is read again. If that read fails, the change IS saved,
    // so the answer must not look like a failed save.
    protected async Task<IActionResult> AfterSave(Func<Task<IActionResult>> readBack)
    {
        try
        {
            return await readBack();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ServerFailure("The change was saved, but the saved record could not be read back. Refresh to see the current data.");
        }
    }

    protected static string? RequiredText(string? value, string label, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return $"{label} is required.";
        if (value.Trim().Length > maxLength) return $"{label} must be at most {maxLength} characters.";
        return null;
    }

    protected static string? OptionalText(string? value, string label, int maxLength)
        => value is not null && value.Trim().Length > maxLength
            ? $"{label} must be at most {maxLength} characters."
            : null;
}
