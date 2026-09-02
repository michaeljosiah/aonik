namespace Aonik.Application.Tests.Documents;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aonik.Documents.Entities;
using Aonik.Documents.Persistence;
using Aonik.Platform.Entities.Compliance; // Document/DocumentFile — namespace preserved (Spec 035)
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.SharedKernel.Modules;
using Aonik.Worker.Jobs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// The opt-in catch-up backfill (Spec 035 Phase 4). Verifies eligibility (Pending + has file + no
/// successful ingestion), that it re-uses the pipeline by enqueuing DocumentUploadedEvent, that it
/// reaches across tenants, and that it never re-indexes already-succeeded, non-Pending, or
/// soft-deleted documents.
/// </summary>
public sealed class DocumentIngestionBackfillJobTests
{
    private sealed class MutableTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }

    private sealed class ContextTenantProvider : ITenantProvider
    {
        private readonly ITenantContext _context;
        public ContextTenantProvider(ITenantContext context) => _context = context;
        public Guid GetCurrentTenantId() => _context.TenantId ?? Guid.Empty;
        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _context.TenantId ?? Guid.Empty;
            return _context.TenantId.HasValue;
        }
    }

    private sealed class MutableClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>Every module on for every tenant except Documents for the given tenants.</summary>
    private sealed class FakeReader(params Guid[] documentsOffFor) : IModuleEnablementReader
    {
        public Task<ModuleEnablementSet> GetAsync(Guid tenantId, CancellationToken ct = default)
        {
            var enabled = ModuleCatalog.All.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
            if (documentsOffFor.Contains(tenantId))
                enabled.Remove(ModuleIds.Documents);
            return Task.FromResult(new ModuleEnablementSet(tenantId, enabled));
        }

        public Task<IReadOnlyList<Guid>> FilterEnabledTenantsAsync(
            IEnumerable<Guid> tenantIds, string moduleId, CancellationToken ct = default)
        {
            IReadOnlyList<Guid> result = moduleId == ModuleIds.Documents
                ? tenantIds.Distinct().Where(id => !documentsOffFor.Contains(id)).ToList()
                : tenantIds.Distinct().ToList();
            return Task.FromResult(result);
        }
    }

    private readonly MutableTenantContext _tenantContext = new();
    private readonly MutableClock _clock = new();
    private readonly DocumentsDbContext _dbContext;

    public DocumentIngestionBackfillJobTests()
    {
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase($"DocsBackfill_{Guid.NewGuid()}")
            .Options;
        _dbContext = new DocumentsDbContext(options, new ContextTenantProvider(_tenantContext), clock: _clock);
    }

    private DocumentIngestionBackfillJob CreateJob(int batchSize = 200, IModuleEnablementReader? reader = null)
    {
        var jobOptions = new ScheduledJobOptions
        {
            DocumentIngestionBackfill = new DocumentIngestionBackfillJobOptions { Enabled = true, BatchSize = batchSize },
        };
        return new DocumentIngestionBackfillJob(
            _dbContext, _tenantContext, Options.Create(jobOptions),
            NullLogger<DocumentIngestionBackfillJob>.Instance, reader);
    }

    private Guid SeedDocumentWithFile(
        Guid tenantId,
        DocumentIndexStatus indexStatus,
        bool deleted = false,
        bool succeededIngestion = false)
    {
        _tenantContext.TenantId = tenantId;
        var documentId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        _dbContext.Documents.Add(new Document
        {
            Id = documentId,
            TenantId = tenantId,
            OwnerPartyId = Guid.NewGuid(),
            DocumentType = "tax_return",
            Status = "Submitted",
            Classification = DocumentClassification.Personal,
            IndexStatus = indexStatus,
            IsDeleted = deleted,
        });
        _dbContext.DocumentFiles.Add(new DocumentFile
        {
            Id = fileId,
            TenantId = tenantId,
            DocumentId = documentId,
            StorageProvider = "azure",
            StorageKey = $"tenants/{tenantId:N}/{documentId:N}/file.txt",
            ContentType = "text/plain",
            FileName = "tax.txt",
            ExtractedTextStatus = ExtractedTextStatus.Native,
        });
        if (succeededIngestion)
        {
            _dbContext.DocumentIngestions.Add(new DocumentIngestion
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DocumentId = documentId,
                DocumentFileId = fileId,
                VectorCollection = "documents",
                Status = "Succeeded",
                ChunkCount = 3,
            });
        }

        return documentId;
    }

    private static int UploadedEventCount(DocumentsDbContext context) =>
        context.Set<OutboxMessage>().Count(m => m.EventType.Contains("DocumentUploadedEvent"));

    [Fact]
    public async Task RunAsync_Should_Republish_Only_Eligible_Pending_Documents()
    {
        var tenant = Guid.NewGuid();
        SeedDocumentWithFile(tenant, DocumentIndexStatus.Pending);                              // eligible
        SeedDocumentWithFile(tenant, DocumentIndexStatus.Pending, succeededIngestion: true);    // already indexed
        SeedDocumentWithFile(tenant, DocumentIndexStatus.Indexed);                              // not pending
        SeedDocumentWithFile(tenant, DocumentIndexStatus.Pending, deleted: true);               // soft-deleted
        await _dbContext.SaveChangesAsync();

        var published = await CreateJob().RunAsync(CancellationToken.None);

        published.Should().Be(1, "only the Pending, non-deleted document without a successful ingestion is eligible");
        UploadedEventCount(_dbContext).Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_Should_Reach_Across_Tenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        SeedDocumentWithFile(tenantA, DocumentIndexStatus.Pending);
        SeedDocumentWithFile(tenantB, DocumentIndexStatus.Pending);
        await _dbContext.SaveChangesAsync();

        var published = await CreateJob().RunAsync(CancellationToken.None);

        published.Should().Be(2, "the backfill scans every tenant");
        var eventTenants = _dbContext.Set<OutboxMessage>()
            .Where(m => m.EventType.Contains("DocumentUploadedEvent"))
            .Select(m => m.TenantId)
            .ToList();
        eventTenants.Should().BeEquivalentTo(new Guid?[] { tenantA, tenantB },
            "each re-published event is stamped with its document's own tenant");
    }

    [Fact]
    public async Task RunAsync_Should_BackfillEnabledTenant_When_DisabledTenantHoldsMorePendingFilesThanTheBatch()
    {
        // Spec 097 §12.2 starvation regression: the disabled tenant's files are OLDER and outnumber
        // the batch (2 > 1). Gating after the batch would select only those, skip them, and never
        // reach the enabled tenant; gating before the batch must re-publish the enabled file.
        var disabledTenant = Guid.NewGuid();
        var enabledTenant = Guid.NewGuid();
        SeedDocumentWithFile(disabledTenant, DocumentIndexStatus.Pending);
        SeedDocumentWithFile(disabledTenant, DocumentIndexStatus.Pending);
        await _dbContext.SaveChangesAsync();
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        var enabledDocument = SeedDocumentWithFile(enabledTenant, DocumentIndexStatus.Pending);
        await _dbContext.SaveChangesAsync();

        var published = await CreateJob(batchSize: 1, reader: new FakeReader(disabledTenant)).RunAsync(CancellationToken.None);

        published.Should().Be(1, "the enabled tenant's file must not be starved by the disabled backlog");
        var events = _dbContext.Set<OutboxMessage>().Where(m => m.EventType.Contains("DocumentUploadedEvent")).ToList();
        events.Should().ContainSingle().Which.TenantId.Should().Be(enabledTenant);
        events.Single().Payload.Should().Contain(enabledDocument.ToString());
    }

    [Fact]
    public async Task Execute_Should_RecordTheSkip_When_ATenantHasDocumentsOff()
    {
        var disabledTenant = Guid.NewGuid();
        var enabledTenant = Guid.NewGuid();
        SeedDocumentWithFile(disabledTenant, DocumentIndexStatus.Pending);
        SeedDocumentWithFile(enabledTenant, DocumentIndexStatus.Pending);
        await _dbContext.SaveChangesAsync();
        var context = new Moq.Mock<Quartz.IJobExecutionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        context.SetupProperty(c => c.Result);

        await CreateJob(reader: new FakeReader(disabledTenant)).Execute(context.Object);

        context.Object.Result.Should().BeOfType<string>().Which.Should()
            .Contain("Re-published 1")
            .And.Contain($"Skipped 1 tenant(s) with module '{ModuleIds.Documents}' disabled");
    }

    [Fact]
    public async Task RunAsync_Should_Return_Zero_When_Nothing_Eligible()
    {
        var tenant = Guid.NewGuid();
        SeedDocumentWithFile(tenant, DocumentIndexStatus.Indexed);
        await _dbContext.SaveChangesAsync();

        var published = await CreateJob().RunAsync(CancellationToken.None);

        published.Should().Be(0);
        UploadedEventCount(_dbContext).Should().Be(0);
    }
}
