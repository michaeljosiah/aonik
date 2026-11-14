using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.Documents.Persistence;
using Aonik.Documents.Services;
using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.ReferenceData;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Billing;
using Aonik.Platform.Entities.Compliance;
using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Application.Tests.Agents.Workflows;

public partial class WorkflowExecutionTests
{
    [Fact]
    public async Task MailboxIntake_Should_AdmitItsPrivateDocumentThroughProtectedOutbox_AndWaitForReviewedInvoiceCreation()
    {
        var fixture = new FinancialFixture(); ConnectorSubscriptionBinding? active = null;
        using var host = new Host(services =>
        {
            fixture.Configure(services);
            services.AddScoped<IWorkflowMailboxBindingReader, WorkflowMailboxBindingReader>();
            services.AddScoped<MailboxWorkflowTriggerHandler>(); services.AddScoped<IFinancialDocumentMailboxIntake, FinancialDocumentMailboxIntake>();
            var connectors = new Mock<IConnectorService>();
            connectors.Setup(value => value.ValidateWorkflowBindingAsync(It.IsAny<Guid>(), It.IsAny<WorkflowConnectorBinding>(), It.IsAny<ConnectorActionRef>(), It.IsAny<WorkflowActor>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            services.AddSingleton(connectors.Object);
            var subscriptions = new Mock<IConnectorSubscriptionService>(); subscriptions.Setup(value => value.ValidateAsync(It.IsAny<ConnectorSubscriptionBinding>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            services.AddSingleton(subscriptions.Object);
            var sources = new Mock<IFinancialDocumentSourceAccess>(); sources.Setup(value => value.RequireAsync(It.IsAny<FinancialDocumentSource>(), It.IsAny<WorkflowActor>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            services.AddSingleton(sources.Object);
            var parties = new Mock<IPartyReader>(); parties.Setup(value => value.GetTenantPartyIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid()); services.AddSingleton(parties.Object);
            services.AddSingleton(Mock.Of<IAuditLogWriter>());
            var mailbox = new Mock<IFinancialDocumentMailboxReader>();
            mailbox.Setup(value => value.ReadAsync(It.IsAny<Guid>(), 1, "source-message", It.IsAny<CancellationToken>())).ReturnsAsync(() =>
            {
                ConnectorSubscriptionBinding binding = active!;
                return new FinancialDocumentAttachment[] { new(new(binding.Connection.ConnectionId, "synthetic-google-account", binding.WorkflowId, binding.WorkflowVersionId,
                    binding.Connection.WorkflowGrantId, binding.AutomationIdentityId, binding.BindingId, 1, "source-message", "source-attachment",
                    Convert.ToHexStringLower(SHA256.HashData(fixture.Bytes))), "invoice.txt", "text/plain", fixture.Bytes) };
            }); services.AddSingleton(mailbox.Object);
            var store = new Mock<IDocumentFileStore>(); store.Setup(value => value.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => new MemoryStream(fixture.Bytes));
            store.Setup(value => value.UploadPrivateDocumentFileAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid documentId, Stream _, string name, string type, CancellationToken _) => new("PrivateTest", null, "private:" + documentId, type, name, fixture.Bytes.Length, Convert.ToHexStringLower(SHA256.HashData(fixture.Bytes))));
            services.AddSingleton(store.Object);
        });
        WorkflowVersionPin child = await host.PublishChild(); WorkflowVersionPin intake = await host.PublishChild(draft => fixture.Definition(draft, child), "document-intake");
        await fixture.SeedAsync(host);
        Guid tenantId; Guid workflowId;
        using (IServiceScope scope = host.Scope())
        {
            var services = scope.ServiceProvider; tenantId = services.GetRequiredService<ITenantProvider>().GetCurrentTenantId();
            workflowId = (await services.GetRequiredService<AgentsDbContext>().Workflows.SingleAsync(workflow => workflow.Slug == "document-intake")).Id;
            var identity = new AutomationIdentity { TenantId = tenantId, Name = "Approved mailbox intake", ConfiguredByUserId = host.Actor.Author, AccountableUserId = host.Actor.Author,
                PermissionsJson = JsonSerializer.Serialize(new[] { "Workflow.Read", "Workflow.Run", "Integration.Use", "Document.Read", "Document.Extract", "Invoice.Read", "Invoice.Create", "Invoice.Issue" }),
                GrantsJson = JsonSerializer.Serialize(new[] { new AutomationWorkflowGrant(intake.VersionId, "Document", true, [],
                    [new(FinancialDocumentActions.Extract, 1), new(FinancialDocumentActions.CreateInvoice, 1), .. GmailActionKeys.ReadActions.Select(key => new AutomationActionRef(key, 1))]),
                    new(child.VersionId, "Invoice", true, [], [new("finance.invoices.issue", 1)]) }, WorkflowDefinitionCodec.JsonOptions) };
            var platform = services.GetRequiredService<PlatformDbContext>(); platform.AutomationIdentities.Add(identity); await platform.SaveChangesAsync();
            var triggers = services.GetRequiredService<WorkflowTriggerService>();
            var definition = new WorkflowTriggerDefinition(1, "GmailMessageAdded", intake.VersionId, identity.Id, new("Document", "MessageAttachments", []), new(),
                Mailbox: new(Guid.NewGuid(), Guid.NewGuid(), ["INBOX"], "Polling", 15, SubjectPrefix: "Qualification:"));
            var draft = await triggers.SaveAsync("document-intake", null, new("Synthetic attachment intake", definition));
            var publication = await triggers.RequestAsync("document-intake", draft.Id, new(draft.ExpectedVersion, draft.DraftHash), false);
            (await services.GetRequiredService<IToolApprovalService>().DecideAsync(publication.ApprovalRequestId, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
            active = await services.GetRequiredService<IWorkflowMailboxBindingReader>().RequireActiveAsync(draft.Id, 1);
        }
        host.Actor.Authenticated = false;
        var message = new ConnectorMessageAddedEvent(Guid.NewGuid(), host.Clock.UtcNow, tenantId, active.Connection.ConnectionId, "source-message", "thread", host.Clock.UtcNow, ["INBOX"]);
        Guid runId;
        using (IServiceScope scope = host.Scope())
        {
            var services = scope.ServiceProvider; var handler = services.GetRequiredService<MailboxWorkflowTriggerHandler>();
            await handler.HandleAsync(message); await handler.HandleAsync(message);
            var wakes = await TriggerWakesAsync(services); wakes.Should().HaveCount(2);
            foreach (var wake in wakes) (await services.GetRequiredService<WorkflowTriggerAdmissionTaskHandler>().ExecuteAsync(AdmissionContext(wake))).Outcome.Should().Be(SharedKernel.Abstractions.Tasks.TaskActionOutcome.Succeeded);
            var run = await services.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(run => run.TriggerBindingId == active.BindingId);
            runId = run.Id; run.ActorType.Should().Be("AutomationIdentity"); run.SubjectType.Should().Be("Document"); run.InitiatingUserId.Should().BeNull();
            (await services.GetRequiredService<DocumentsDbContext>().Documents.CountAsync(document => document.Source == "Gmail")).Should().Be(1);
        }
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        await fixture.DecideAsync(host, runId, true); host.Actor.Authenticated = false;
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForChildren);
        using IServiceScope final = host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
        fixture.ParserCalls.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DocumentIntake_Should_ExtractOnce_WaitForARealHuman_ThenRouteTheCreatedInvoiceThroughAPinnedChild(bool automatic)
    {
        var fixture = new FinancialFixture(); using var host = new Host(fixture.Configure);
        WorkflowVersionPin child = await host.PublishChild();
        WorkflowVersionPin intake = await host.PublishChild(draft => fixture.Definition(draft, child), "document-intake");
        await fixture.SeedAsync(host);
        Guid runId = await fixture.StartAsync(host, intake, child, automatic);
        host.Actor.Authenticated = false;

        var waiting = await host.Continue(runId);
        waiting.Status.Should().Be(WorkflowRunStatuses.WaitingForApproval, waiting.ErrorCode);
        fixture.ParserCalls.Should().Be(1);
        using (IServiceScope scope = host.Scope())
        {
            (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.CountAsync()).Should().Be(0);
            DocumentExtractionAssertions(scope, fixture);
        }
        await fixture.DecideAsync(host, runId, true);
        host.Actor.Authenticated = false;
        var parent = await host.Continue(runId);
        parent.Status.Should().Be(WorkflowRunStatuses.WaitingForChildren, parent.ErrorCode);
        Guid childId;
        using (IServiceScope scope = host.Scope()) childId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(run => run.ParentRunId == runId)).Id;
        (await host.Continue(childId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        await fixture.DecideAsync(host, childId, true);
        host.Actor.Authenticated = false;
        (await host.Continue(childId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        using IServiceScope final = host.Scope();
        Invoice invoice = await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync();
        invoice.Status.Should().Be("Issued"); invoice.Total.Should().Be(250m); invoice.InvoiceNumber.Should().Be("DEMO-1001");
        invoice.ProvenanceJson.Should().Contain(fixture.AiRunId.ToString()).And.Contain("sourceHash").And.Contain("approvalRequestId");
        invoice.IssueOperationId.Should().NotBeNull();
        (await final.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowStepRuns.Where(step => step.RunId == runId
            && (step.ActionKey == FinancialDocumentActions.Extract || step.ActionKey == FinancialDocumentActions.CreateInvoice))
            .Select(step => step.AiRunId).ToListAsync()).Should().Equal(fixture.AiRunId, fixture.AiRunId);
        fixture.ParserCalls.Should().Be(1);
    }

    [Theory]
    [InlineData("SupplierBill", "UnsupportedTenantPayable")]
    [InlineData("PersonalBill", "ExplicitPersonalOwnerRequired")]
    [InlineData("Unknown", "DocumentKindUnknown")]
    [InlineData("LowConfidence", "LowConfidence")]
    [InlineData("ConflictingTotals", "TotalsConflict")]
    [InlineData("Tax", "UnsupportedTaxOrDiscount")]
    public async Task DocumentIntake_Should_StopWithEvidence_WithoutAnyFinancialApproval_ForUnsupportedOrConflictingCandidates(string kind, string issue)
    {
        var fixture = new FinancialFixture(); fixture.Fields = kind switch
        {
            "LowConfidence" => fixture.Fields with { Confidence = .6 },
            "ConflictingTotals" => fixture.Fields with { Total = 251m },
            "Tax" => fixture.Fields with { Tax = 5m, Total = 255m },
            _ => fixture.Fields with { Kind = kind },
        };
        using var host = new Host(fixture.Configure); WorkflowVersionPin child = await host.PublishChild();
        WorkflowVersionPin intake = await host.PublishChild(draft => fixture.Definition(draft, child), "document-intake");
        await fixture.SeedAsync(host); Guid runId = await fixture.StartAsync(host, intake, child, false);

        var result = await host.Continue(runId);

        result.Status.Should().Be(WorkflowRunStatuses.Completed, result.ErrorCode); result.Outcome.Should().Be("NeedsManualReview");
        (await host.Continue(runId)).Outcome.Should().Be("NeedsManualReview");
        using IServiceScope scope = host.Scope();
        var extraction = await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().DocumentExtractions.SingleAsync();
        extraction.Status.Should().Be("NeedsManualReview"); extraction.OutputJson.Should().Contain(issue);
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.CountAsync()).Should().Be(0);
        (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests.CountAsync(request => request.WorkflowStepRunId != null && request.RiskTier != "Low")).Should().Be(0);
    }

    [Fact]
    public async Task DocumentIntake_Should_RejectCreation_WithoutWritingAnInvoice()
    {
        var fixture = new FinancialFixture(); using var host = new Host(fixture.Configure); var child = await host.PublishChild();
        var intake = await host.PublishChild(draft => fixture.Definition(draft, child), "document-intake"); await fixture.SeedAsync(host);
        Guid runId = await fixture.StartAsync(host, intake, child, false); await host.Continue(runId);

        await fixture.DecideAsync(host, runId, false);
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.Completed);

        using IServiceScope scope = host.Scope();
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.CountAsync()).Should().Be(0);
        (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.CountAsync(run => run.ParentRunId == runId)).Should().Be(0);
    }

    [Fact]
    public async Task DocumentIntake_Should_RefuseApproval_WhenTheSourceBytesChangedOutsideTheDatabase()
    {
        var fixture = new FinancialFixture(); using var host = new Host(fixture.Configure); var child = await host.PublishChild();
        var intake = await host.PublishChild(draft => fixture.Definition(draft, child), "document-intake"); await fixture.SeedAsync(host);
        Guid runId = await fixture.StartAsync(host, intake, child, false); await host.Continue(runId);
        fixture.Bytes = "changed source"u8.ToArray(); host.Actor.Id = host.Reviewer;
        using IServiceScope scope = host.Scope();
        var request = await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId != null && request.RiskTier == "Medium");

        Func<Task> decide = async () => { await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id, new(ToolApprovalDecisionType.Approve)); };

        await decide.Should().ThrowAsync<InvalidOperationException>().WithMessage("FinancialSourceHashMismatch");
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DocumentIntake_Should_KeepTheOriginalSourceGrantRequired_AfterCreatingAnInvoiceChild()
    {
        var fixture = new FinancialFixture(); using var host = new Host(fixture.Configure); var child = await host.PublishChild();
        var intake = await host.PublishChild(draft => fixture.Definition(draft, child), "document-intake"); await fixture.SeedAsync(host);
        Guid runId = await fixture.StartAsync(host, intake, child, true); host.Actor.Authenticated = false; await host.Continue(runId);
        await fixture.DecideAsync(host, runId, true); host.Actor.Authenticated = false; await host.Continue(runId);
        using IServiceScope scope = host.Scope();
        Guid childId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(run => run.ParentRunId == runId)).Id;
        PlatformDbContext platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        AutomationIdentity actor = await platform.AutomationIdentities.SingleAsync();
        var grants = JsonSerializer.Deserialize<List<AutomationWorkflowGrant>>(actor.GrantsJson, WorkflowDefinitionCodec.JsonOptions)!;
        actor.GrantsJson = JsonSerializer.Serialize(grants.Where(grant => grant.WorkflowVersionId != intake.VersionId), WorkflowDefinitionCodec.JsonOptions);
        await platform.SaveChangesAsync();

        var result = await host.Continue(childId);

        result.Status.Should().Be(WorkflowRunStatuses.NeedsAttention);
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    private static void DocumentExtractionAssertions(IServiceScope scope, FinancialFixture fixture)
    {
        var extraction = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().DocumentExtractions.Single();
        extraction.AiRunId.Should().Be(fixture.AiRunId); extraction.OutputJson.Should().Contain("Poppler").And.Contain("sourceHash");
        extraction.InputHash.Should().HaveLength(64); extraction.Status.Should().Be("ReadyForReview");
    }

    [Fact]
    public async Task DocumentIntake_Should_ReconcileAWorkerLossAfterInvoiceCommit_WithoutAnotherApprovalOrInvoice()
    {
        var fixture = new FinancialFixture { CrashAfterCreate = true }; using var host = new Host(fixture.Configure);
        var child = await host.PublishChild(); var intake = await host.PublishChild(draft => fixture.Definition(draft, child), "document-intake");
        await fixture.SeedAsync(host); Guid runId = await fixture.StartAsync(host, intake, child, false);
        await host.Continue(runId); await fixture.DecideAsync(host, runId, true); host.Actor.Authenticated = false;
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.NeedsAttention);
        using (IServiceScope scope = host.Scope())
        {
            (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.CountAsync()).Should().Be(1);
            host.Actor.Authenticated = true; host.Actor.Id = host.Actor.Author;
            var runs = scope.ServiceProvider.GetRequiredService<IWorkflowRunService>(); var current = (await runs.GetAsync(runId))!;
            await runs.ResumeAsync(runId, new(current.ExpectedVersion));
        }
        host.Actor.Authenticated = false;

        var recovered = await host.Continue(runId, 1);

        recovered.Status.Should().Be(WorkflowRunStatuses.WaitingForChildren, recovered.ErrorCode);
        using IServiceScope final = host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.CountAsync()).Should().Be(1);
        (await final.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests.CountAsync(request => request.ToolName == FinancialDocumentActions.CreateInvoiceTool)).Should().Be(1);
        fixture.ParserCalls.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DocumentIntake_Should_HoldDuplicateForwardingOrAlteredContent_WithoutDiscardingTheNewCandidate(bool changed)
    {
        var fixture = new FinancialFixture(); using var host = new Host(fixture.Configure);
        var child = await host.PublishChild(); var intake = await host.PublishChild(draft => fixture.Definition(draft, child), "document-intake");
        await fixture.SeedAsync(host); Guid firstId = await fixture.StartAsync(host, intake, child, false);
        await host.Continue(firstId); await fixture.DecideAsync(host, firstId, true); host.Actor.Authenticated = false; await host.Continue(firstId);
        Guid documentId = Guid.NewGuid(); byte[] forwarded = changed ? [.. fixture.Bytes, .. "\nAmended evidence"u8.ToArray()] : fixture.Bytes;
        fixture.AdditionalSources["private:forwarded.txt"] = forwarded;
        host.Actor.Authenticated = true; host.Actor.Id = host.Actor.Author;
        using (IServiceScope scope = host.Scope())
        {
            Guid tenant = scope.ServiceProvider.GetRequiredService<ITenantProvider>().GetCurrentTenantId();
            var docs = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
            docs.Documents.Add(new Document { Id = documentId, TenantId = tenant, OwnerPartyId = Guid.NewGuid(), DocumentType = "FinancialSource",
                Classification = DocumentClassification.Restricted, IndexStatus = DocumentIndexStatus.NotIndexable, Status = "Received" });
            docs.DocumentFiles.Add(new DocumentFile { TenantId = tenant, DocumentId = documentId, StorageProvider = "PrivateTest", StorageKey = "private:forwarded.txt",
                ContentType = "text/plain", FileSizeBytes = forwarded.Length, Sha256 = Convert.ToHexStringLower(SHA256.HashData(forwarded)) }); await docs.SaveChangesAsync();
            var second = await scope.ServiceProvider.GetRequiredService<IWorkflowRunService>().StartAsync("document-intake", new(intake.VersionId, "Document", documentId, "forwarded-1"));
            documentId = second.Id;
        }
        host.Actor.Authenticated = false;

        var held = await host.Continue(documentId);

        held.Status.Should().Be(WorkflowRunStatuses.NeedsAttention); held.ErrorCode.Should().Be("DuplicateInvoiceEvidenceRequiresReview");
        using IServiceScope final = host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.CountAsync()).Should().Be(1);
        (await final.ServiceProvider.GetRequiredService<DocumentsDbContext>().DocumentExtractions.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData("GBP", 2, "0.66")]
    [InlineData("BHD", 3, "0.663")]
    [InlineData("JPY", 0, "1")]
    public async Task DocumentIntake_Should_PreserveTheReviewedLineTotal_AtTheCurrentCurrencyPrecision(string currency, int scale, string total)
    {
        decimal amount = decimal.Parse(total, System.Globalization.CultureInfo.InvariantCulture);
        var fixture = new FinancialFixture(); fixture.Fields = fixture.Fields with { Currency = currency,
            Lines = [new("Consulting", .3333m, 1.99m, amount)], Subtotal = amount, Total = amount };
        fixture.Fields = fixture.Fields with { Evidence = ["AONIK DEMO SALES INVOICE", "Customer: Demo Customer"] };
        fixture.Bytes = Encoding.UTF8.GetBytes($"AONIK DEMO SALES INVOICE\nCustomer: Demo Customer\nInvoice: DEMO-1001\nCurrency: {currency}\nConsulting: 0.3333 x 1.99 = {total}\nTOTAL: {total}\nDue: 2026-12-01");
        using var host = new Host(fixture.Configure); var child = await host.PublishChild();
        var intake = await host.PublishChild(draft => fixture.Definition(draft, child), "document-intake"); await fixture.SeedAsync(host);
        if (currency != "GBP")
        {
            using IServiceScope scope = host.Scope(); var finance = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            finance.Currencies.Add(new CurrencyReadModel { Code = currency, MinorUnit = scale, IsActive = true }); await finance.SaveChangesAsync();
        }
        Guid runId = await fixture.StartAsync(host, intake, child, false); await host.Continue(runId);
        await fixture.DecideAsync(host, runId, true); host.Actor.Authenticated = false;

        var result = await host.Continue(runId);

        result.Status.Should().Be(WorkflowRunStatuses.WaitingForChildren, result.ErrorCode);
        using IServiceScope final = host.Scope(); Invoice invoice = await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.Include(invoice => invoice.Lines).SingleAsync();
        invoice.Currency.Should().Be(currency); invoice.Total.Should().Be(amount); invoice.Lines.Single().LineTotal.Should().Be(amount);
    }

    private sealed class CrashAfterDocumentCreation(FinancialFixture fixture, InvoiceCreateFromDocumentWorkflowAction inner) : IWorkflowActionHandler
    {
        public WorkflowActionDescriptor Descriptor => inner.Descriptor;
        public void ValidateArguments(JsonElement arguments) => inner.ValidateArguments(arguments);
        public Task<ToolApprovalPreview?> GetPreviewAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default) => inner.GetPreviewAsync(invocation, cancellationToken);
        public Task<WorkflowActionReconciliation> ReconcileAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default) => inner.ReconcileAsync(invocation, cancellationToken);
        public async Task<WorkflowActionResult> ExecuteAsync(WorkflowActionInvocation invocation, CancellationToken cancellationToken = default)
        {
            WorkflowActionResult result = await inner.ExecuteAsync(invocation, cancellationToken);
            if (fixture.CrashAfterCreate) { fixture.CrashAfterCreate = false; throw new InvalidOperationException("Simulated worker loss after invoice commit."); }
            return result;
        }
    }

    private sealed class FinancialFixture
    {
        public Guid DocumentId { get; } = Guid.NewGuid();
        public Guid FileId { get; } = Guid.NewGuid();
        public Guid CustomerId { get; } = Guid.NewGuid();
        public Guid AiRunId { get; } = Guid.NewGuid();
        public byte[] Bytes { get; set; } = Encoding.UTF8.GetBytes("AONIK DEMO SALES INVOICE\nCustomer: Demo Customer\nInvoice: DEMO-1001\nCurrency: GBP\nConsulting: 2 x 125.00 = 250.00\nTOTAL: 250.00\nDue: 2026-12-01");
        public FinancialDocumentFields Fields { get; set; } = new("SalesInvoice", "DEMO-1001", "Demo Customer", "GBP",
            new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), [new("Consulting", 2m, 125m, 250m)], 250m, 0m, 0m, 250m, .99,
            ["AONIK DEMO SALES INVOICE", "Consulting: 2 x 125.00 = 250.00"]);
        public int ParserCalls { get; private set; }
        public bool CrashAfterCreate { get; set; }
        public string ContentType { get; set; } = "text/plain";
        public IDocumentTextExtractor? RealExtractor { get; set; }
        public Dictionary<string, byte[]> AdditionalSources { get; } = new(StringComparer.Ordinal);

        public void Configure(IServiceCollection services)
        {
            services.AddDbContext<DocumentsDbContext>(options => options.UseInMemoryDatabase("FinancialDocuments_" + DocumentId));
            services.AddScoped<FinancialDocumentAuthorization>(); services.AddScoped<FinancialDocumentService>();
            services.AddScoped<IFinancialDocumentReader>(provider => provider.GetRequiredService<FinancialDocumentService>());
            services.AddScoped<IWorkflowFactsProvider, FinancialDocumentFactsProvider>();
            services.AddScoped<IWorkflowActionHandler, FinancialDocumentWorkflowAction>();
            services.AddScoped<InvoiceCreateFromDocumentWorkflowAction>();
            services.AddScoped<IWorkflowActionHandler>(provider => new CrashAfterDocumentCreation(this, provider.GetRequiredService<InvoiceCreateFromDocumentWorkflowAction>()));
            services.AddSingleton<IToolApprovalManifest, FinancialDocumentToolApprovalManifest>();
            var store = new Mock<IDocumentFileStore>();
            store.Setup(value => value.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string key, CancellationToken _) => (Stream)new MemoryStream(AdditionalSources.GetValueOrDefault(key) ?? Bytes));
            services.AddSingleton(store.Object);
            var text = new Mock<IDocumentTextExtractor>();
            text.Setup(value => value.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new DocumentTextExtractionResult(Encoding.UTF8.GetString(Bytes), ExtractedTextStatus.Native, new("Poppler", "fixture-1", 1, null)));
            services.AddSingleton(text.Object);
            if (RealExtractor is not null) services.AddSingleton(RealExtractor);
            var scanner = new Mock<IDocumentSafetyScanner>(); scanner.Setup(value => value.ScanAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            services.AddSingleton(scanner.Object);
            var parser = new Mock<IFinancialDocumentParser>();
            parser.Setup(value => value.ParseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => { ParserCalls++; return new ParsedFinancialDocument(Fields, AiRunId, "routed-test-model", new string('b', 64)); });
            services.AddSingleton(parser.Object);
        }
        public async Task SeedAsync(Host host)
        {
            using IServiceScope scope = host.Scope(); var services = scope.ServiceProvider;
            Guid tenant = services.GetRequiredService<ITenantProvider>().GetCurrentTenantId();
            var documents = services.GetRequiredService<DocumentsDbContext>();
            documents.Documents.Add(new Document { Id = DocumentId, TenantId = tenant, OwnerPartyId = Guid.NewGuid(), DocumentType = "FinancialSource",
                Classification = DocumentClassification.Restricted, IndexStatus = DocumentIndexStatus.NotIndexable, Status = "Received", CreatedBy = host.Actor.Author });
            documents.DocumentFiles.Add(new DocumentFile { Id = FileId, DocumentId = DocumentId, TenantId = tenant, StorageProvider = "PrivateTest",
                StorageKey = "private:source.txt", FileName = "source.txt", ContentType = ContentType, FileSizeBytes = Bytes.Length,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(Bytes)) });
            await documents.SaveChangesAsync();
            FinanceDbContext finance = services.GetRequiredService<FinanceDbContext>(); Guid partyId = Guid.NewGuid();
            finance.Parties.Add(new PartyReadModel { Id = partyId, TenantId = tenant, DisplayName = "Demo Customer", Status = "Active" });
            finance.CustomerAccounts.Add(new CustomerAccount { Id = CustomerId, TenantId = tenant, CustomerPartyId = partyId, MerchantPartyId = Guid.NewGuid(), Status = "Active" });
            finance.Currencies.Add(new CurrencyReadModel { Code = "GBP", Name = "Pound sterling", MinorUnit = 2, IsActive = true });
            await finance.SaveChangesAsync();
        }
        public async Task<Guid> StartAsync(Host host, WorkflowVersionPin intake, WorkflowVersionPin child, bool automatic)
        {
            using IServiceScope scope = host.Scope(); var services = scope.ServiceProvider;
            if (!automatic) return (await services.GetRequiredService<IWorkflowRunService>().StartAsync("document-intake", new(intake.VersionId, "Document", DocumentId, "document-1"))).Id;
            Guid tenant = services.GetRequiredService<ITenantProvider>().GetCurrentTenantId();
            var identity = new AutomationIdentity { TenantId = tenant, Name = "Financial intake", AccountableUserId = host.Actor.Author, ConfiguredByUserId = host.Actor.Author,
                PermissionsJson = JsonSerializer.Serialize(new[] { "Workflow.Read", "Workflow.Run", "Document.Read", "Document.Extract", "Invoice.Read", "Invoice.Create", "Invoice.Issue" }),
                GrantsJson = JsonSerializer.Serialize(new[] {
                    new AutomationWorkflowGrant(intake.VersionId, "Document", true, [], [new(FinancialDocumentActions.Extract, 1), new(FinancialDocumentActions.CreateInvoice, 1)]),
                    new AutomationWorkflowGrant(child.VersionId, "Invoice", true, [], [new("finance.invoices.issue", 1)]),
                }, WorkflowDefinitionCodec.JsonOptions) };
            PlatformDbContext platform = services.GetRequiredService<PlatformDbContext>(); platform.AutomationIdentities.Add(identity); await platform.SaveChangesAsync();
            return await services.GetRequiredService<WorkflowRunService>().AdmitAsync(identity.Id, intake.VersionId, "Document", DocumentId, "document-automatic-1");
        }
        public WorkflowSaveRequest Definition(WorkflowSaveRequest draft, WorkflowVersionPin child)
        {
            var reviewer = JsonSerializer.Deserialize<WorkflowToolParameters>(draft.Nodes.Single(node => node.ClientId == "issue").ParamsJson, WorkflowDefinitionCodec.JsonOptions)!.Reviewer;
            string extract = JsonSerializer.Serialize(new WorkflowToolParameters(FinancialDocumentActions.Extract, 1,
                new Dictionary<string, WorkflowInputBinding> { ["documentId"] = new(Fact: "document.id"), ["fileId"] = new(Fact: "document.fileId") }), WorkflowDefinitionCodec.JsonOptions);
            string create = JsonSerializer.Serialize(new WorkflowToolParameters(FinancialDocumentActions.CreateInvoice, 1,
                new Dictionary<string, WorkflowInputBinding> { ["extractionId"] = new(Step: "extract", Output: "extractionId"),
                    ["candidateHash"] = new(Step: "extract", Output: "candidateHash"), ["customerId"] = new(Literal: JsonSerializer.SerializeToElement(CustomerId)) }, reviewer), WorkflowDefinitionCodec.JsonOptions);
            return draft with { SchemaVersion = 2,
                Nodes = [new("start", "Trigger", "Financial source", "", "", 0, 0, "{\"subjectType\":\"Document\"}"),
                    new("extract", "Tool", "Extract candidate", "", "", 1, 0, extract), new("create", "Tool", "Review sales invoice", "", "", 2, 0, create),
                    new("child", "Child", "Invoice approval routing", "", "", 3, 0, JsonSerializer.Serialize(new WorkflowChildParameters(1, child, new(Step: "create", Output: "invoiceId")), WorkflowDefinitionCodec.JsonOptions)),
                    new("done", "End", "Done", "", "", 4, 0, "{}"), new("manual", "End", "Manual review", "", "", 4, 1, "{\"outcome\":\"NeedsManualReview\"}")],
                Edges = [new("start", "extract", 0, ""), new("extract", "create", 0, ""), new("create", "child", 0, ""), new("child", "done", 0, "", "completed"), new("child", "manual", 1, "", "error")],
            };
        }
        public async Task DecideAsync(Host host, Guid runId, bool approve)
        {
            host.Actor.Authenticated = true; host.Actor.Id = host.Reviewer;
            using IServiceScope scope = host.Scope(); var database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            var run = await database.WorkflowRuns.SingleAsync(run => run.Id == runId);
            var request = await database.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == run.PendingStepRunId);
            var result = await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id,
                new(approve ? ToolApprovalDecisionType.Approve : ToolApprovalDecisionType.Reject));
            result.Outcome.Should().Be(approve ? ToolApprovalDecisionOutcome.Approved : ToolApprovalDecisionOutcome.Rejected);
        }
    }
}
