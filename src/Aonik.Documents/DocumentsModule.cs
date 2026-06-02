using Aonik.Documents.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Documents;

/// <summary>
/// Composition-root registration for the Documents module (Spec 035 §17).
/// Phase 1 wires the module-scoped <see cref="DocumentsDbContext"/>. The document
/// services (<c>IDocumentReader</c>/<c>IDocumentWriter</c>/<c>IDocumentSearch</c>/<c>IDocumentIndexer</c>)
/// and endpoints are registered as they land in subsequent commits on this branch;
/// the canonical migration stream stays in <c>AonikDbContext</c>.
/// </summary>
public sealed class DocumentsModule
{
    public static string Name => "Documents";

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<DocumentsDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                var dbName = configuration.GetValue<string>("InMemoryDatabaseName")
                    ?? $"DocumentsDb_{Guid.NewGuid()}";
                options.UseInMemoryDatabase(dbName);
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection")
                    ?? configuration.GetConnectionString("AonikDb")
                    ?? "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";
                options.UseSqlServer(connectionString, o => o.EnableRetryOnFailure());
            }
        });

        return services;
    }
}

public static class DocumentsModuleExtensions
{
    public static IServiceCollection AddDocumentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => DocumentsModule.ConfigureServices(services, configuration);
}
