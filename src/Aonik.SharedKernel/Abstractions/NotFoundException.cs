namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Thrown when a requested resource does not exist. Mapped to HTTP 404 by the
/// global exception handler — callers never need to string-match the message.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
