namespace EquipmentBorrowing.Application.Models;

public enum OperationOutcome
{
    Success,   // the change was saved
    Rejected,  // a business rule or validation said no; nothing changed
    Failed,    // a known database/technical problem; nothing was saved
    Unknown    // the connection was lost while saving; it is NOT known whether the change was saved
}

public class OperationResult
{
    public OperationOutcome Outcome { get; }
    public string Message { get; }
    public int? EntityId { get; }
    public bool IsSuccess => Outcome == OperationOutcome.Success;

    private OperationResult(OperationOutcome outcome, string message, int? entityId)
    {
        Outcome = outcome;
        Message = message;
        EntityId = entityId;
    }

    public static OperationResult Success(string message, int? entityId = null)
        => new(OperationOutcome.Success, message, entityId);

    public static OperationResult Rejected(string message)
        => new(OperationOutcome.Rejected, message, null);

    public static OperationResult Failed(string message)
        => new(OperationOutcome.Failed, message, null);

    // Safe message: never includes connection details or credentials (BR-18).
    public static OperationResult DatabaseFailure() => Failed(
        "The database could not complete the request. Check the connection, " +
        "refresh the data to see its current state, then try again.");

    // BR-18: the outcome of a commit is unknown. Do not claim success and do not claim rollback.
    public static OperationResult UnknownOutcome() => new(
        OperationOutcome.Unknown,
        "The connection was lost while saving, so it is not known whether the change was saved. " +
        "Nothing was retried automatically. Refresh the lists to see the current state " +
        "before deciding what to do.",
        null);
}
