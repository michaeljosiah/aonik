using FastEndpoints;

namespace Aonik.Authorization;

/// <summary>
/// Extension methods that let FastEndpoints endpoints declare permission
/// requirements at the route definition, ensuring the check cannot be
/// silently bypassed by forgetting to call <c>EnsurePermissionAsync</c>
/// inside a service method.
/// </summary>
public static class EndpointPermissionExtensions
{
    /// <summary>
    /// Declares that the endpoint requires the specified permission key.
    /// Attaches a <see cref="RequirePermission"/> pre-processor that runs
    /// before the handler; throws <see cref="Aonik.SharedKernel.Abstractions.PermissionDeniedException"/>
    /// (mapped to a 403 response by the API exception filter) if the
    /// current user lacks the permission.
    /// </summary>
    /// <param name="endpoint">The endpoint configuration target — call as <c>this.RequiresPermission(...)</c> from inside <c>Configure()</c>.</param>
    /// <param name="permissionKey">The permission key the user must have, e.g. <c>"Invoice.Create"</c>.</param>
    /// <example>
    /// <code>
    /// public override void Configure()
    /// {
    ///     Post("/billing/invoices");
    ///     Policies("AdminUserPolicy");
    ///     this.RequiresPermission("Invoice.Create");
    /// }
    /// </code>
    /// </example>
    public static void RequiresPermission(this BaseEndpoint endpoint, string permissionKey)
    {
        if (endpoint is null)
        {
            throw new ArgumentNullException(nameof(endpoint));
        }

        endpoint.Definition.PreProcessors(Order.Before, new RequirePermission(permissionKey));
    }
}
