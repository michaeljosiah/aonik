using Aonik.Documents.Persistence;
using Aonik.Documents.Services;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Events;
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

        // Generic document read/write contracts (Spec 035 §11). One service implements both;
        // search/index (IDocumentSearch/IDocumentVectorIndex) are registered in Infrastructure.
        services.AddScoped<DocumentService>();
        services.AddScoped<IDocumentReader>(sp => sp.GetRequiredService<DocumentService>());
        services.AddScoped<IDocumentWriter>(sp => sp.GetRequiredService<DocumentService>());

        // Spec 046 — document linking (Vault). One service implements the cross-module
        // IDocumentLinkReader contract and the internal IDocumentLinkService CRUD.
        services.AddScoped<DocumentLinkService>();
        services.AddScoped<IDocumentLinkReader>(sp => sp.GetRequiredService<DocumentLinkService>());
        services.AddScoped<IDocumentLinkService>(sp => sp.GetRequiredService<DocumentLinkService>());

        // Async RAG ingestion pipeline (Spec 035 §13). The indexer orchestrates
        // extract→chunk→embed→upsert; DocumentIngestionHandler consumes DocumentUploadedEvent.
        // Handlers are registered by assembly scan so the outbox dispatcher can resolve them — the
        // Api registers them too, but only the Worker drains the outbox, so ingestion runs exactly
        // once, in the Worker. The text extractor and scoped vector index are supplied by
        // Infrastructure at the composition root.
        services.AddScoped<IDocumentIndexer, DocumentIndexer>();
        services.AddEventHandlersFromAssembly(typeof(DocumentsModule).Assembly);

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
