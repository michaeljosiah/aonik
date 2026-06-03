namespace Aonik.Application.Tests.Documents;

using System;
using System.Linq;
using System.Threading.Tasks;
using Aonik.Platform.Entities.Compliance;
using Aonik.Platform.IntegrationEvents;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Events.Integration;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Compliance's reaction to a document erasure (Spec 035 §12/§15): dependent <see cref="DocumentUsage"/>
/// rows are marked <c>Expired</c> — never deleted — preserving the KYC audit trail.
/// </summary>
public sealed class DocumentDeletedComplianceHandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private PlatformDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"PlatformDocDel_{Guid.NewGuid()}")
            .Options;
        return new PlatformDbContext(options, new TestTenantProvider(_tenantId));
    }

    private DocumentUsage NewUsage(Guid documentId, string status) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        DocumentId = documentId,
        OwnerPartyId = Guid.NewGuid(),
        Purpose = "IdVerification",
        Status = status,
    };

    [Fact]
    public async Task HandleAsync_Should_Expire_Dependent_Usages_And_Preserve_Other_Documents()
    {
        await using var context = CreateContext();
        var deletedDoc = Guid.NewGuid();
        var otherDoc = Guid.NewGuid();
        context.DocumentUsages.Add(NewUsage(deletedDoc, "Pending"));
        context.DocumentUsages.Add(NewUsage(deletedDoc, "Satisfied"));
        context.DocumentUsages.Add(NewUsage(deletedDoc, "Expired")); // already terminal — untouched
        var otherUsage = NewUsage(otherDoc, "Pending");
        context.DocumentUsages.Add(otherUsage);
        await context.SaveChangesAsync();

        var handler = new DocumentDeletedComplianceHandler(
            context, NullLogger<DocumentDeletedComplianceHandler>.Instance);
        await handler.HandleAsync(new DocumentDeletedEvent(_tenantId, deletedDoc, Guid.NewGuid()));

        // Every usage for the deleted document ends Expired, and all three rows still exist.
        var forDeletedDoc = await context.DocumentUsages
            .Where(u => u.DocumentId == deletedDoc).ToListAsync();
        forDeletedDoc.Should().HaveCount(3, "usages are expired, never deleted — the audit trail survives");
        forDeletedDoc.Should().OnlyContain(u => u.Status == "Expired");

        // A usage tied to a different document is left alone.
        (await context.DocumentUsages.FirstAsync(u => u.Id == otherUsage.Id)).Status.Should().Be("Pending");
    }

    [Fact]
    public async Task HandleAsync_Should_NoOp_When_No_Dependent_Usages()
    {
        await using var context = CreateContext();
        var handler = new DocumentDeletedComplianceHandler(
            context, NullLogger<DocumentDeletedComplianceHandler>.Instance);

        var act = async () =>
            await handler.HandleAsync(new DocumentDeletedEvent(_tenantId, Guid.NewGuid(), Guid.NewGuid()));

        await act.Should().NotThrowAsync();
    }
}
