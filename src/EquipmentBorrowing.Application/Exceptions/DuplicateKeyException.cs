namespace EquipmentBorrowing.Application.Exceptions;

// Thrown by repositories when the database rejects a duplicate student number
// or equipment identifier. Lets the Application layer stay free of Npgsql types.
public class DuplicateKeyException : Exception
{
    public DuplicateKeyException(string message, Exception inner) : base(message, inner) { }
}