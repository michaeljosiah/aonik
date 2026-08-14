using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Workspaces;

/// <summary>
/// Composition root for the workspaces module (Spec 089 / ADR-016).
///
/// <para>
/// P2 registers the schema only. The services arrive with P3–P5, in that order, because each depends on the one
/// before it: blobs cannot be reference-counted before they can be stored by key, and a revision cannot refuse a
/// manifest naming content the tenant does not possess before possession is a thing the module can answer.
/// </para>
/// </summary>
public static class WorkspacesModule
{
    public static IServiceCollection AddWorkspacesModule(
        this IServiceCollection services, IConfiguration configuration)
        => services;
}
