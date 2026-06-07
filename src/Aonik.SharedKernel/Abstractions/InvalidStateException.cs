namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Thrown when a domain operation is rejected because the entity is not in a
/// valid state for it (e.g. "only draft invoices can be issued"). Mapped to
/// HTTP 422 by the global exception handler.
/// </summary>
public sealed class InvalidStateException : Exception
{
    public InvalidStateException(string message) : base(message) { }
}
