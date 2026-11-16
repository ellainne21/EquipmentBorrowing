namespace EquipmentBorrowing.Application.Exceptions;

// Thrown when the database refuses a change because the data changed underneath us
// (for example another user checked out the same item first).
public class ConflictException : Exception
{
    public ConflictException(string message, Exception inner) : base(message, inner) { }
}
