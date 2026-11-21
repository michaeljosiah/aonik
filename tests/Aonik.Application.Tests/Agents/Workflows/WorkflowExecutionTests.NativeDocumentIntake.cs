using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Documents.Persistence;
using Aonik.Finance.Persistence;
using Aonik.Infrastructure.Documents;
using Aonik.Infrastructure.Tests.Storage;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Application.Tests.Agents.Workflows;

public partial class WorkflowExecutionTests
{
    [SkippableTheory]
    [InlineData("sales-invoice.png", "image/png", true)]
    [InlineData("sales-invoice-native.pdf", "application/pdf", true)]
    [InlineData("sales-invoice-scanned.pdf", "application/pdf", true)]
    [InlineData("supplier-invoice-scanned.pdf", "application/pdf", false)]
    public async Task NativeDocumentIntake_Should_GroundRealOcrEvidence_BeforeHumanCreationAndIssueRouting(string fileName, string contentType, bool sales)
    {
        var settings = new DocumentOcrOptions { Enabled = true };
        bool available = File.Exists(settings.TesseractPath) && File.Exists(settings.PdfToTextPath) && File.Exists(settings.PdfToPpmPath);
        if (Environment.GetEnvironmentVariable("AONIK_OCR_TESTS_REQUIRED") == "true")
            available.Should().BeTrue("CI must qualify real OCR through reviewed domain creation");
        Skip.IfNot(available, "Configure Tesseract and Poppler to run the real scanned-to-domain journey.");
        await using var scanner = new ScriptedClamAvServer();
        var fixture = new FinancialFixture
        {
            Bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "DocumentFixtures", fileName)),
            ContentType = contentType,
            RealExtractor = new DocumentTextExtractor(new ProcessDocumentOcrExtractor(Microsoft.Extensions.Options.Options.Create(settings), scanner.Scanner), NullLogger<DocumentTextExtractor>.Instance),
        };
        if (!sales) fixture.Fields = fixture.Fields with { Kind = "SupplierBill", InvoiceNumber = "SUP-2001", CounterpartyName = "Example Supplier",
            Lines = [new("Office supplies", 1m, 80m, 80m)], Subtotal = 80m, Total = 80m, Evidence = ["SUPPLIER INVOICE", "Office supplies: 1 x 80.00 = 80.00"] };
        using var host = new Host(fixture.Configure);
        WorkflowVersionPin child = await host.PublishChild();
        WorkflowVersionPin intake = await host.PublishChild(draft => fixture.Definition(draft, child), "document-intake");
        await fixture.SeedAsync(host); Guid runId = await fixture.StartAsync(host, intake, child, automatic: true);
        host.Actor.Authenticated = false;

        var waiting = await host.Continue(runId);

        using (IServiceScope evidence = host.Scope())
        {
            string candidate = (await evidence.ServiceProvider.GetRequiredService<DocumentsDbContext>().DocumentExtractions.SingleAsync()).OutputJson;
            candidate.Should().Contain(fileName == "sales-invoice-native.pdf" ? "Poppler" : "Tesseract").And.Contain(fixture.AiRunId.ToString());
            (await evidence.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.CountAsync()).Should().Be(0);
        }
        if (!sales)
        {
            waiting.Outcome.Should().Be("NeedsManualReview");
            return;
        }
        waiting.Status.Should().Be(WorkflowRunStatuses.WaitingForApproval, waiting.ErrorCode);
        await fixture.DecideAsync(host, runId, true); host.Actor.Authenticated = false;
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForChildren);
        Guid childId;
        using (IServiceScope scope = host.Scope()) childId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(run => run.ParentRunId == runId)).Id;
        (await host.Continue(childId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        await fixture.DecideAsync(host, childId, true); host.Actor.Authenticated = false;
        (await host.Continue(childId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        using IServiceScope final = host.Scope();
        var invoice = await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync();
        invoice.Status.Should().Be("Issued"); invoice.Total.Should().Be(250m); invoice.InvoiceNumber.Should().Be("DEMO-1001");
        fixture.ParserCalls.Should().Be(1);
    }
}
