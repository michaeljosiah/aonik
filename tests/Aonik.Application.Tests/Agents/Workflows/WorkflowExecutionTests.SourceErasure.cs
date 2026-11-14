using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.Documents.Persistence;
using Aonik.Documents.Services;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Application.Tests.Agents.Workflows;

public partial class WorkflowExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceErasure_Should_ImmediatelyRefuseReview_ThenCancelRemainingWorkAndScrubCopiedPreviews(bool invoiceChild)
    {
        var fixture = new FinancialFixture(); using var host = new Host(fixture.Configure);
        var child = await host.PublishChild(); var intake = await host.PublishChild(draft => fixture.Definition(draft, child), "document-intake");
        await fixture.SeedAsync(host); Guid parentId = await fixture.StartAsync(host, intake, child, true);
        host.Actor.Authenticated = false; await host.Continue(parentId);
        Guid runId = parentId;
        if (invoiceChild)
        {
            await fixture.DecideAsync(host, parentId, true); host.Actor.Authenticated = false; await host.Continue(parentId);
            using IServiceScope childScope = host.Scope();
            runId = (await childScope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(run => run.ParentRunId == parentId)).Id;
            await host.Continue(runId);
        }
        using IServiceScope scope = host.Scope(); var services = scope.ServiceProvider;
        var agents = services.GetRequiredService<AgentsDbContext>();
        var run = await agents.WorkflowRuns.SingleAsync(run => run.Id == runId);
        var approval = await agents.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == run.PendingStepRunId);
        string hash = approval.ArgsHash;
        approval.PreviewJson.Should().NotBeNull();
        var other = new ToolApprovalRequest { TenantId = Guid.NewGuid(), PreviewJson = "other tenant preview", ToolName = "unrelated", ArgsHash = "other", ArgumentsRedactedJson = "{}" };
        agents.ToolApprovalRequests.Add(other); await agents.SaveChangesAsync();
        var documents = services.GetRequiredService<DocumentsDbContext>();
        var document = await documents.Documents.SingleAsync(document => document.Id == fixture.DocumentId);
        var writer = new DocumentService(documents, services.GetRequiredService<ITenantProvider>(), services.GetRequiredService<IDocumentFileStore>(),
            Mock.Of<IDocumentVectorIndex>(), Mock.Of<ICurrentUserContext>(), Mock.Of<IUserPartyResolver>());
        await writer.DeleteDocumentAsync(fixture.DocumentId);

        host.Actor.Authenticated = true; host.Actor.Id = host.Reviewer;
        Func<Task> staleDecision = () => services.GetRequiredService<IToolApprovalService>().DecideAsync(approval.Id, new(ToolApprovalDecisionType.Approve));
        await staleDecision.Should().ThrowAsync<NotFoundException>();
        using IServiceScope erase = host.Scope(); var erased = erase.ServiceProvider.GetRequiredService<AgentsDbContext>();
        var handler = new FinancialSourceErasedHandler(erased, services.GetRequiredService<ITenantProvider>(), host.Clock);
        var message = new DocumentDeletedEvent(document.TenantId, document.Id, document.OwnerPartyId);
        await handler.HandleAsync(message); await handler.HandleAsync(message);

        (await erased.WorkflowRuns.SingleAsync(run => run.Id == parentId)).Status.Should().Be(WorkflowRunStatuses.Cancelled);
        var request = await erased.ToolApprovalRequests.SingleAsync(request => request.Id == approval.Id);
        request.Status.Should().Be(ToolApprovalRequestStatus.Cancelled); request.PreviewJson.Should().BeNull(); request.ArgsHash.Should().Be(hash);
        (await agents.ToolApprovalRequests.AcrossTenants().AsNoTracking().SingleAsync(request => request.Id == other.Id)).PreviewJson.Should().Be("other tenant preview");
        (await services.GetRequiredService<FinanceDbContext>().Invoices.AsNoTracking().Select(invoice => invoice.Status).ToListAsync())
            .Should().Equal(invoiceChild ? new[] { "Draft" } : []);
        (await documents.DocumentExtractions.SingleAsync()).AiRunId.Should().Be(fixture.AiRunId);
    }
}
