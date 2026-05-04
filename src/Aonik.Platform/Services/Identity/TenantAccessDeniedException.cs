namespace Aonik.Platform.Services.Identity;

/// <summary>
/// Thrown when an authenticated identity attempts to access a tenant
/// it has no link or invitation into. Distinct exception type so the
/// API layer can map it to HTTP 403 with a clear user-facing message
/// instead of treating it as a generic 500.
/// </summary>
public sealed class TenantAccessDeniedException : Exception
{
    public TenantAccessDeniedException(string message) : base(message)
    {
    }

    public TenantAccessDeniedException(string message, Exception inner) : base(message, inner)
    {
    }
}
