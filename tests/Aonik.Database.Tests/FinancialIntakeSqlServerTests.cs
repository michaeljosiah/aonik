using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Aonik.Documents.Entities;
using Aonik.Finance.Entities.Billing;
using Aonik.Infrastructure.Persistence;
using Aonik.IntegrationTests.Support;
using Aonik.Platform.Entities.Compliance;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests;

public class FinancialIntakeSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableFact]
    public async Task ConcurrentSourceIngestion_Should_CommitOneDocumentAndFile_AndRollBackTheLosingDocuments()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason); Guid tenantId = Guid.NewGuid();
        Task<bool>[] writes = Enumerable.Range(0, 8).Select(async _ =>
        {
            await using AonikDbContext context = Context(tenantId); Document document = Source(tenantId);
            context.Documents.Add(document);
            context.DocumentFiles.Add(new DocumentFile { TenantId = tenantId, DocumentId = document.Id, StorageProvider = "PrivateTest",
                StorageKey = "private:source.pdf", ContentType = "application/pdf", SourceFingerprint = new string('a', 64), Sha256 = new string('b', 64) });
            return await TrySaveAsync(context);
        }).ToArray();

        (await Task.WhenAll(writes)).Count(success => success).Should().Be(1);

        await using AonikDbContext saved = Context(tenantId);
        (await saved.Documents.CountAsync()).Should().Be(1); (await saved.DocumentFiles.CountAsync()).Should().Be(1);
    }

    [SkippableFact]
    public async Task ConcurrentExtraction_Should_RetainOneImmutableResultForTheSameSourceContract()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason); Guid tenantId = Guid.NewGuid(); Document document = Source(tenantId);
        await using (AonikDbContext seed = Context(tenantId)) { seed.Documents.Add(document); await seed.SaveChangesAsync(); }
        Task<bool>[] writes = Enumerable.Range(0, 8).Select(async _ =>
        {
            await using AonikDbContext context = Context(tenantId);
            context.DocumentExtractions.Add(new DocumentExtraction { TenantId = tenantId, DocumentId = document.Id,
                ExtractionType = "FinancialCandidate.v1", InputHash = new string('a', 64), OutputJson = "{}", Status = "ReadyForReview" });
            return await TrySaveAsync(context);
        }).ToArray();

        (await Task.WhenAll(writes)).Count(success => success).Should().Be(1);
        await using AonikDbContext saved = Context(tenantId); (await saved.DocumentExtractions.CountAsync()).Should().Be(1);
    }

    [SkippableFact]
    public async Task ConcurrentAlteredSourcesForTheSameBusinessInvoice_Should_CreateOnlyOneInvoice()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason); Guid tenantId = Guid.NewGuid();
        Task<bool>[] writes = Enumerable.Range(0, 8).Select(async index =>
        {
            await using AonikDbContext context = Context(tenantId);
            context.Set<Invoice>().Add(new Invoice { TenantId = tenantId, CustomerAccountId = Guid.NewGuid(), InvoiceNumber = "DEMO-1001",
                Currency = "GBP", Status = "Draft", ProvenanceJson = "{}", IdempotencyKey = "different-source-" + index,
                IntakeBusinessKey = new string('c', 64), Total = 250, Subtotal = 250 });
            return await TrySaveAsync(context);
        }).ToArray();

        (await Task.WhenAll(writes)).Count(success => success).Should().Be(1);
        await using AonikDbContext saved = Context(tenantId); (await saved.Set<Invoice>().CountAsync()).Should().Be(1);
    }

    [SkippableFact]
    public async Task SourceErasure_Should_FenceAnInFlightExtraction_AndRollBackItsPayload()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason); Guid tenantId = Guid.NewGuid(); Document document = Source(tenantId);
        await using (AonikDbContext seed = Context(tenantId)) { seed.Documents.Add(document); await seed.SaveChangesAsync(); }
        await using AonikDbContext stale = Context(tenantId); Document old = await stale.Documents.SingleAsync();
        await using (AonikDbContext eraser = Context(tenantId))
        {
            Document current = await eraser.Documents.SingleAsync(); eraser.Documents.Remove(current); await eraser.SaveChangesAsync();
        }
        stale.DocumentExtractions.Add(new DocumentExtraction { TenantId = tenantId, DocumentId = document.Id, ExtractionType = "FinancialCandidate.v1",
            InputHash = new string('a', 64), OutputJson = "{\"private\":\"evidence\"}" });
        stale.Entry(old).Property(value => value.Status).IsModified = true;

        Func<Task> save = async () => { await stale.SaveChangesAsync(); };

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
        await using AonikDbContext saved = Context(tenantId); (await saved.DocumentExtractions.CountAsync()).Should().Be(0);
    }

    private AonikDbContext Context(Guid tenantId) => new(database.CreateOptions<AonikDbContext>(), new TestTenantProvider(tenantId));
    private static Document Source(Guid tenantId) => new() { Id = Guid.NewGuid(), TenantId = tenantId, OwnerPartyId = Guid.NewGuid(),
        DocumentType = "FinancialSource", Classification = DocumentClassification.Restricted, IndexStatus = DocumentIndexStatus.NotIndexable, Status = "Received" };
    private static async Task<bool> TrySaveAsync(AonikDbContext context)
    {
        try { await context.SaveChangesAsync(); return true; }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 }) { return false; }
    }
}
