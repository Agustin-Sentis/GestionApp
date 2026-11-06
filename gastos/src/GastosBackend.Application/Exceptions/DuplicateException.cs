namespace GastosBackend.Application.Exceptions
{
    /// <summary>
    /// Thrown by repositories when the database rejects a write because of a unique constraint.
    /// Services catch it and turn it into a <see cref="ConflictException"/> with a message for their own resource.
    /// </summary>
    public class DuplicateException : Exception
    {
        public DuplicateException(string message, Exception? innerException = null) : base(message, innerException) { }
    }
}
