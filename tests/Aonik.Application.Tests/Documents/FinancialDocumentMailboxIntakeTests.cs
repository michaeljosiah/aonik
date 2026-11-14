using System.Security.Cryptography;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

using Aonik.Documents.Persistence;
using Aonik.Documents.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Documents;

public class FinancialDocumentMailboxIntakeTests
{
    [Fact]
    public async Task Intake_Should_StoreOnePrivateRestrictedSource_AndReuseItOnOutboxReplay()
    {
        using var host = new Host();

        IReadOnlyList<Guid> first = await host.Intake.AdmitAsync(host.Binding.BindingId, 1, "message");
        IReadOnlyList<Guid> replay = await host.Intake.AdmitAsync(host.Binding.BindingId, 1, "message");

        replay.Should().Equal(first); first.Should().ContainSingle(); host.Uploads.Should().Be(1);
        var document = await host.Database.Documents.SingleAsync(); var file = await host.Database.DocumentFiles.SingleAsync();
        document.Classification.Should().Be(DocumentClassification.Restricted); document.OwnerPartyId.Should().Be(host.OwnerId);
        document.DocumentType.Should().Be("FinancialSource"); document.IndexStatus.Should().Be(DocumentIndexStatus.NotIndexable);
        file.StorageKey.Should().StartWith("private:"); file.SourceFingerprint.Should().HaveLength(64);
        file.SourceEvidenceJson.Should().Contain(host.Source.ProviderAccountId).And.Contain(host.Source.WorkflowGrantId.ToString());
        file.MetadataJson.Should().Be("{}");
        host.Files.Verify(value => value.UploadDocumentFileAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Intake_Should_RetainTheDeduplicationTombstone_WithoutRecreatingAnErasedSource()
    {
        using var host = new Host(); await host.Intake.AdmitAsync(host.Binding.BindingId, 1, "message");
        host.Database.DocumentFiles.Remove(await host.Database.DocumentFiles.SingleAsync());
        host.Database.Documents.Remove(await host.Database.Documents.SingleAsync()); await host.Database.SaveChangesAsync();

        (await host.Intake.AdmitAsync(host.Binding.BindingId, 1, "message")).Should().BeEmpty();

        host.Uploads.Should().Be(1); (await host.Database.Documents.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("binding")]
    [InlineData("unsafe")]
    [InlineData("too-many")]
    [InlineData("scope")]
    public async Task Intake_Should_RefuseInvalidOrUnauthorizedSources_BeforeUploading(string invalid)
    {
        using var host = new Host();
        if (invalid == "hash") host.Attachment = host.Attachment with { Source = host.Source with { ContentHash = new string('a', 64) } };
        if (invalid == "binding") host.Attachment = host.Attachment with { Source = host.Source with { WorkflowVersionId = Guid.NewGuid() } };
        if (invalid == "unsafe") host.Scanner.Setup(value => value.ScanAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ThrowsAsync(new DocumentTextExtractionException("DocumentUnsafe"));
        if (invalid == "too-many") host.Count = 6;
        if (invalid == "scope") host.Automation.Setup(value => value.RequireAllSubjectsAsync(It.IsAny<WorkflowActor>(), "Document", It.IsAny<CancellationToken>())).ThrowsAsync(new PermissionDeniedException("Document.Read"));

        Func<Task> admit = () => host.Intake.AdmitAsync(host.Binding.BindingId, 1, "message");

        await admit.Should().ThrowAsync<Exception>(); host.Uploads.Should().Be(0);
        (await host.Database.Documents.CountAsync()).Should().Be(0); (await host.Database.DocumentFiles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Intake_Should_CleanUpItsUncommittedUpload_WhenBindingAuthorityChanges()
    {
        using var host = new Host(); host.AfterUpload = () => host.Enabled = false;

        Func<Task> admit = () => host.Intake.AdmitAsync(host.Binding.BindingId, 1, "message");

        await admit.Should().ThrowAsync<PermissionDeniedException>(); host.Uploads.Should().Be(1);
        host.Files.Verify(value => value.DeleteAsync(It.Is<string>(key => key.StartsWith("private:")), It.IsAny<CancellationToken>()), Times.Once);
        (await host.Database.Documents.CountAsync()).Should().Be(0);
    }

    private sealed class Host : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid(); public Guid OwnerId { get; } = Guid.NewGuid();
        public DocumentsDbContext Database { get; } public FinancialDocumentMailboxIntake Intake { get; }
        public Mock<IDocumentFileStore> Files { get; } = new(); public Mock<IDocumentSafetyScanner> Scanner { get; } = new();
        public Mock<IAutomationIdentityAuthorization> Automation { get; } = new();
        public ConnectorSubscriptionBinding Binding { get; } public FinancialDocumentSource Source { get; }
        public FinancialDocumentAttachment Attachment { get; set; } public int Count { get; set; } = 1;
        public bool Enabled { get; set; } = true; public int Uploads { get; private set; } public Action? AfterUpload { get; set; }
        public Host()
        {
            var tenant = new TestTenantProvider(TenantId);
            Database = new(new DbContextOptionsBuilder<DocumentsDbContext>().UseInMemoryDatabase("FinancialMailbox_" + Guid.NewGuid()).Options, tenant);
            Binding = new(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid(), Guid.NewGuid(),
                GmailActionKeys.ReadActions.Select(key => new ConnectorActionRef(key, 1)).ToArray()), "Polling", 15, ["INBOX"], false, null, SubjectType: "Document");
            byte[] bytes = "synthetic invoice"u8.ToArray(); string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            Source = new(Binding.Connection.ConnectionId, "synthetic-account", Binding.WorkflowId, Binding.WorkflowVersionId, Binding.Connection.WorkflowGrantId,
                Binding.AutomationIdentityId, Binding.BindingId, 1, "message", "attachment", hash);
            Attachment = new(Source, "invoice.txt", "text/plain", bytes);
            var reader = new Mock<IFinancialDocumentMailboxReader>();
            reader.Setup(value => value.ReadAsync(Binding.BindingId, 1, "message", It.IsAny<CancellationToken>())).ReturnsAsync(() => Enumerable.Repeat(Attachment, Count).ToArray());
            var bindings = new Mock<IWorkflowMailboxBindingReader>();
            bindings.Setup(value => value.RequireActiveAsync(Binding.BindingId, 1, It.IsAny<CancellationToken>())).ReturnsAsync(() => Enabled ? Binding : throw new PermissionDeniedException("Workflow.Run"));
            var source = new Mock<IFinancialDocumentSourceAccess>();
            source.Setup(value => value.RequireAsync(It.IsAny<FinancialDocumentSource>(), It.IsAny<WorkflowActor>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Automation.Setup(value => value.RequireAllSubjectsAsync(It.IsAny<WorkflowActor>(), "Document", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Scanner.Setup(value => value.ScanAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Files.Setup(value => value.UploadPrivateDocumentFileAsync(TenantId, It.IsAny<Guid>(), It.IsAny<Stream>(), "invoice.txt", "text/plain", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid documentId, Stream _, string name, string type, CancellationToken _) =>
                { Uploads++; AfterUpload?.Invoke(); return new("Local", "documents-private", "private:" + documentId, type, name, bytes.Length, hash); });
            Files.Setup(value => value.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var parties = new Mock<IPartyReader>(); parties.Setup(value => value.GetTenantPartyIdAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnerId);
            Intake = new(Database, tenant, reader.Object, bindings.Object, source.Object, Automation.Object, Files.Object, Scanner.Object, parties.Object, Mock.Of<IAuditLogWriter>());
        }
        public void Dispose() => Database.Dispose();
    }
}
