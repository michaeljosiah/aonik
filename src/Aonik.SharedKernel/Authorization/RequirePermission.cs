using Aonik.SharedKernel.Abstractions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Authorization;

/// <summary>
/// FastEndpoints pre-processor that enforces a permission check before
/// the endpoint handler runs. Throws <see cref="PermissionDeniedException"/>
/// if the current user is missing the required permission, which the API
/// exception filter then maps to a 403 Forbidden response carrying the
/// permission key.
/// </summary>
/// <remarks>
/// Use this in endpoint <c>Configure()</c> via <c>PreProcessors(...)</c> to
/// declare the required permission AT the route definition. This is the
/// preferred enforcement point because:
/// <list type="bullet">
///   <item>The permission requirement is visible alongside the route, summary, and policies.</item>
///   <item>The check cannot be silently disabled by forgetting to call <c>EnsurePermissionAsync</c> inside a service method.</item>
///   <item>The check runs before the request body is bound, so the cost of a denied call is small.</item>
/// </list>
///
/// Implements <see cref="IGlobalPreProcessor"/> so the same instance can
/// be attached to any FastEndpoints endpoint regardless of request type
/// — register it via <c>Definition.PreProcessors(Order.Before, ...)</c>
/// (or use the <c>RequiresPermission(string)</c> extension method).
///
/// Example usage inside a FastEndpoints endpoint:
/// <code>
/// public override void Configure()
/// {
///     Post("/billing/invoices");
///     Policies("AdminUserPolicy");
///     this.RequiresPermission("Invoice.Create");
/// }
/// </code>
/// </remarks>
public sealed class RequirePermission : IGlobalPreProcessor
{
    private readonly string _permissionKey;

    public RequirePermission(string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            throw new ArgumentException("Permission key must be non-empty.", nameof(permissionKey));
        }

        _permissionKey = permissionKey;
    }

    /// <summary>The permission key this pre-processor requires.</summary>
    public string PermissionKey => _permissionKey;

    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        var http = context.HttpContext;

        var currentUserProvider = http.RequestServices.GetRequiredService<ICurrentUserProvider>();
        var permissionService = http.RequestServices.GetRequiredService<IPermissionService>();

        var userId = currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new PermissionDeniedException(_permissionKey, "Authenticated user is required.");
        }

        var hasPermission = await permissionService.HasPermissionAsync(userId.Value, _permissionKey, ct);
        if (!hasPermission)
        {
            throw new PermissionDeniedException(_permissionKey);
        }
    }
}
