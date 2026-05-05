namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Thrown when an authenticated principal lacks a permission required to
/// perform an operation. The exception carries the permission key so the
/// API exception filter can map it to a 403 Forbidden response without
/// regex-matching the message text.
/// </summary>
/// <remarks>
/// Prefer enforcing permissions declaratively at the endpoint boundary
/// (e.g. via the <c>RequirePermission</c> FastEndpoints pre-processor) so
/// the check is visible at the route definition and cannot be silently
/// disabled by forgetting to call <c>EnsurePermissionAsync</c> inside a
/// service method.
/// </remarks>
public sealed class PermissionDeniedException : Exception
{
    /// <summary>
    /// The permission key that was required and missing (e.g. "Invoice.Create").
    /// </summary>
    public string PermissionKey { get; }

    public PermissionDeniedException(string permissionKey)
        : base($"Permission {permissionKey} is required.")
    {
        PermissionKey = permissionKey ?? throw new ArgumentNullException(nameof(permissionKey));
    }

    public PermissionDeniedException(string permissionKey, string message)
        : base(message)
    {
        PermissionKey = permissionKey ?? throw new ArgumentNullException(nameof(permissionKey));
    }

    public PermissionDeniedException(string permissionKey, string message, Exception innerException)
        : base(message, innerException)
    {
        PermissionKey = permissionKey ?? throw new ArgumentNullException(nameof(permissionKey));
    }
}
