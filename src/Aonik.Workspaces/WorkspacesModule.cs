using Aonik.SharedKernel.Modules;

using Microsoft.EntityFrameworkCore;
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
public sealed class WorkspacesModule : IModule
{
    public static string Name => "Workspaces";
    public static string Id => ModuleIds.Workspaces;

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<Services.WorkspaceOptions>(
            configuration.GetSection(Services.WorkspaceOptions.SectionName));

        // Unlike Groups, this module owns its context outright. Groups defers the choice because a
        // membership write must share a transaction with a contributor's reaction in another module;
        // nothing here reaches outside itself, so deferring would be ceremony.
        services.AddDbContext<Persistence.WorkspacesDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                options.UseInMemoryDatabase(
                    configuration.GetValue<string>("InMemoryDatabaseName") ?? $"WorkspacesDb_{Guid.NewGuid()}");
            }
            else
            {
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection")
                        ?? configuration.GetConnectionString("AonikDb")
                        ?? "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;",
                    sql => sql.EnableRetryOnFailure());
            }
        });

        services.AddScoped<Persistence.IWorkspaceDataContext>(
            sp => sp.GetRequiredService<Persistence.WorkspacesDbContext>());

        services.AddScoped<Services.IWorkspaceBlobService, Services.WorkspaceBlobService>();
        services.AddScoped<Services.IWorkspaceBlobSweeper, Services.WorkspaceBlobSweeper>();
        services.AddScoped<SharedKernel.Abstractions.Workspaces.IWorkspaceSyncService, Services.WorkspaceSyncService>();
        services.AddScoped<Services.IBlobPossessionService, Services.BlobPossessionService>();
        services.AddScoped<Services.IWorkspaceUploadService, Services.WorkspaceUploadService>();
        services.AddScoped<SharedKernel.Abstractions.Workspaces.IWorkspaceService, Services.WorkspaceService>();
        services.AddScoped<SharedKernel.Abstractions.Workspaces.IWorkspaceReader>(
            sp => (Services.WorkspaceService)sp.GetRequiredService<SharedKernel.Abstractions.Workspaces.IWorkspaceService>());

        // Registering the kind is all it takes to inherit every Spec 086 mechanic — invite tokens,
        // expiry, revocation, ownership validation — with no new code.
        services.AddScoped<SharedKernel.Abstractions.Groups.IShareResourceResolver, Services.WorkspaceShareResourceResolver>();

        return services;
    }
}

public static class WorkspacesModuleExtensions
{
    public static IServiceCollection AddWorkspacesModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => WorkspacesModule.ConfigureServices(services, configuration);
}
