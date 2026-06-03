using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.IntegrationEvents;

/// <summary>
/// Compliance reaction to a document erasure (Spec 035 §12/§15). When a document is deleted, its
/// dependent <see cref="Aonik.Platform.Entities.Compliance.DocumentUsage"/> rows are marked
/// <c>Expired</c> — never deleted — so the KYC audit trail survives even though the underlying
/// evidence is gone. Runs in the Worker via the outbox dispatcher with the originating tenant
/// restored, so the usage query is tenant-scoped to the deleted document's tenant.
/// </summary>
internal sealed class DocumentDeletedComplianceHandler : IEventHandler<DocumentDeletedEvent>
{
    private const string ExpiredStatus = "Expired";

    private readonly PlatformDbContext _dbContext;
    private readonly ILogger<DocumentDeletedComplianceHandler> _logger;

    public DocumentDeletedComplianceHandler(
        PlatformDbContext dbContext,
        ILogger<DocumentDeletedComplianceHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task HandleAsync(DocumentDeletedEvent @event, CancellationToken cancellationToken = default)
    {
        var usages = await _dbContext.DocumentUsages
            .Where(u => u.DocumentId == @event.DocumentId && u.Status != ExpiredStatus)
            .ToListAsync(cancellationToken);

        if (usages.Count == 0)
        {
            return;
        }

        foreach (var usage in usages)
        {
            usage.Status = ExpiredStatus;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Marked {Count} document usage(s) Expired after document {DocumentId} was deleted.",
            usages.Count, @event.DocumentId);
    }
}
