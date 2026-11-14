namespace EquipmentBorrowing.Application.Models;

public enum OperationOutcome
{
    Success,   // the change was saved
    Rejected,  // a business rule or validation said no; nothing changed
    Failed     // a database/technical problem; see message
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
}