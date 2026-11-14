using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Application.Tests.Agents.Workflows;

public partial class WorkflowExecutionTests
{
    [Theory]
    [InlineData("Unique")]
    [InlineData("Score")]
    public async Task AdvancedRoute_Should_StillWaitForTheExactNamedHumanFinancialApproval(string policy)
    {
        using var host = new Host();
        Guid runId = await host.Start(draft =>
        {
            DecisionTable table = AdvancedDecisionTableTests.Table(policy, new DecisionRow("draft", 1,
                [new("invoice.status", "eq", JsonSerializer.SerializeToElement("Draft"))], "standard", ScoreWeight: policy == "Score" ? "1" : null));
            table = table with { ScoreBands = policy == "Score" ? [new("0", "2", "standard")] : null,
                Fixtures = [new("normal", "Normal", DecisionTableEvaluatorTests.Facts("42.00"), "Completed", ["standard"]),
                    new("boundary", "Boundary", DecisionTableEvaluatorTests.Facts("0.00"), "Completed", ["standard"]),
                    new("missing", "MissingData", new Dictionary<string, JsonElement>(), "NeedsManualReview", [])] };
            return draft with { SchemaVersion = 2, Nodes = draft.Nodes.Select(node => node.ClientId == "route" ? node with
                { ParamsJson = JsonSerializer.Serialize(table, WorkflowDefinitionCodec.JsonOptions) } : node).ToList(), Edges = draft.Edges.Where(edge => edge.RouteKey != "elevated").ToList() };
        });

        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);

        using (IServiceScope scope = host.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            WorkflowRun run = await database.WorkflowRuns.SingleAsync(run => run.Id == runId);
            Guid approvalId = (await database.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == run.PendingStepRunId)).Id;
            (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
            host.Actor.Id = host.Reviewer;
            (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(approvalId, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        }
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        using IServiceScope final = host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Issued");
    }

    [Fact]
    public async Task CollectWithoutAnExplicitTypedConsumer_Should_FailPublicationBeforeAnyFinancialAction()
    {
        using var host = new Host(); using IServiceScope scope = host.Scope(); var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        DecisionTable table = AdvancedDecisionTableTests.Table("Collect", new DecisionRow("draft", 1,
            [new("invoice.status", "eq", JsonSerializer.SerializeToElement("Draft"))], "item", CollectInvoiceId: new(Fact: "invoice.id"))) with
        { CollectRoute = "standard", Fixtures = [new("normal", "Normal", DecisionTableEvaluatorTests.Facts("42.00"), "Completed", ["standard"]),
            new("boundary", "Boundary", DecisionTableEvaluatorTests.Facts("0.00"), "Completed", ["standard"]), new("missing", "MissingData", new Dictionary<string, JsonElement>(), "NeedsManualReview", [])] };
        WorkflowSaveRequest request = host.Draft(); request = request with { SchemaVersion = 2,
            Nodes = request.Nodes.Select(node => node.ClientId == "route" ? node with { ParamsJson = JsonSerializer.Serialize(table, WorkflowDefinitionCodec.JsonOptions) } : node).ToList(),
            Edges = request.Edges.Where(edge => edge.RouteKey != "elevated").ToList() };
        WorkflowGraphResponse saved = await workflows.CreateAsync(request);

        WorkflowValidation validation = (await workflows.ValidateAsync(saved.Slug))!;

        validation.Diagnostics.Should().Contain(error => error.Code == "CollectConsumer");
        (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().Proposals.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DecisionLibrary_Should_UseTheGovernedLifecycle_AndKeepConsumersPinnedUntilExplicitAdoption()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild(LibraryDraft, "approval-library");
        Guid runId = await host.Start(draft => WithLibrary(draft, pin));
        using (IServiceScope scope = host.Scope())
        {
            var services = scope.ServiceProvider; var workflows = services.GetRequiredService<IWorkflowService>();
            var simulation = await workflows.SimulateAsync("invoice-route", new(DecisionTableEvaluatorTests.Facts("42.00")));
            simulation!.Steps.Single(step => step.Decision is not null).Decision!.Library!.VersionId.Should().Be(pin.VersionId);
            WorkflowGraphResponse graph = (await workflows.GetBySlugAsync("approval-library"))!;
            WorkflowSaveRequest updated = SaveGraph(graph);
            DecisionTable table = JsonSerializer.Deserialize<DecisionTable>(updated.Nodes.Single(node => node.Key == "route").ParamsJson, WorkflowDefinitionCodec.JsonOptions)!;
            table = table with { Rows = [table.Rows.Single() with { Route = "manual" }], Fixtures = table.Fixtures!.Select(fixture => fixture.ExpectedStatus == "Completed"
                ? fixture with { ExpectedRoutes = ["manual"] } : fixture).ToList() };
            updated = updated with { Nodes = updated.Nodes.Where(node => node.Key != "done").Select(node => node.Key == "route"
                ? node with { ParamsJson = JsonSerializer.Serialize(table, WorkflowDefinitionCodec.JsonOptions) }
                : node.Key == "start" ? node with { ParamsJson = JsonSerializer.Serialize(new DecisionLibraryTriggerParameters(new(1, LibraryInputs(), ["manual"], "Route")), WorkflowDefinitionCodec.JsonOptions) } : node).ToList(),
                Edges = [new("start", "route", 0, ""), new("route", "manual", 0, "", "manual")] };
            WorkflowGraphResponse saved = (await workflows.UpdateAsync(updated))!;
            WorkflowValidation validation = (await workflows.ValidateAsync(graph.Slug))!;
            validation.Diagnostics.Where(item => item.Severity == "Error").Should().BeEmpty();
            var publication = await services.GetRequiredService<WorkflowGovernanceService>().RequestAsync(new(graph.Slug, saved.ExpectedVersion, validation.DefinitionHash, pin.VersionId), false);
            (await services.GetRequiredService<IToolApprovalService>().DecideAsync(publication.ApprovalRequestId, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
            (await services.GetRequiredService<AgentsDbContext>().Workflows.SingleAsync(workflow => workflow.Slug == graph.Slug)).PublishedVersionId.Should().NotBe(pin.VersionId);
        }

        var result = await host.Continue(runId);

        result.Status.Should().Be(WorkflowRunStatuses.WaitingForApproval, result.ErrorCode);
        using IServiceScope final = host.Scope(); var database = final.ServiceProvider.GetRequiredService<AgentsDbContext>();
        WorkflowStepRun decision = await database.WorkflowStepRuns.SingleAsync(step => step.RunId == runId && step.NodeKey == "route");
        decision.SelectedRoute.Should().Be("standard");
        DecisionEvaluation trace = JsonSerializer.Deserialize<DecisionEvaluation>(decision.DecisionTraceJson!, WorkflowDefinitionCodec.JsonOptions)!;
        trace.Library!.VersionId.Should().Be(pin.VersionId); trace.Library.DefinitionHash.Should().Be(pin.DefinitionHash);
        trace.Library.Inputs.Keys.Should().BeEquivalentTo("invoice.currency", "invoice.status");
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task LibraryRetirement_Should_BlockAPendingFinancialApproval_AndNeverSilentlyAdoptTheCurrentVersion()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild(LibraryDraft, "approval-library");
        Guid runId = await host.Start(draft => WithLibrary(draft, pin)); await host.Continue(runId);
        using IServiceScope scope = host.Scope(); var services = scope.ServiceProvider; var database = services.GetRequiredService<AgentsDbContext>();
        Workflow library = await database.Workflows.SingleAsync(workflow => workflow.Slug == "approval-library");
        library.RetiredAt = host.Clock.UtcNow; library.State = WorkflowStates.Retired; await database.SaveChangesAsync();
        WorkflowRun run = await database.WorkflowRuns.SingleAsync(run => run.Id == runId);
        Guid requestId = (await database.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == run.PendingStepRunId)).Id;
        host.Actor.Id = host.Reviewer;
        Func<Task> approve = async () => await services.GetRequiredService<IToolApprovalService>().DecideAsync(requestId, new(ToolApprovalDecisionType.Approve));

        await approve.Should().ThrowAsync<WorkflowConflictException>();
        (await host.Continue(runId)).ErrorCode.Should().Be("LibraryUnavailable");
        (await services.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task PureLibrary_Should_RejectFinancialRunAdmission_AndWrongInputOrOutputContracts()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild(LibraryDraft, "approval-library"); await host.AddInvoice();
        using IServiceScope scope = host.Scope(); var services = scope.ServiceProvider;
        Func<Task> runLibrary = async () => await services.GetRequiredService<IWorkflowRunService>().StartAsync("approval-library", new(pin.VersionId, "Invoice", Guid.NewGuid(), "not-a-run"));
        await runLibrary.Should().ThrowAsync<WorkflowConflictException>();
        DecisionLibraryReference reference = LibraryReference(pin) with { Inputs = new Dictionary<string, WorkflowInputBinding> { ["invoice.status"] = new(Fact: "invoice.status") } };
        WorkflowDefinition definition = WorkflowDefinitionCodec.FromGraph((await services.GetRequiredService<IWorkflowService>().GetBySlugAsync("approval-library"))!);
        Action wrongInputs = () => WorkflowDecisionLibraries.ValidateReference(reference, definition);
        wrongInputs.Should().Throw<ArgumentException>();
        Action wrongOutputs = () => WorkflowDecisionLibraries.ValidateReference(LibraryReference(pin) with { Routes = ["approve"] }, definition);
        wrongOutputs.Should().Throw<ArgumentException>();
        WorkflowDefinition effectful = definition with { Nodes = [.. definition.Nodes, new("write", "Tool", "Write", JsonSerializer.SerializeToElement(new { }))] };
        WorkflowDefinitionValidator.Validate(effectful, true).Should().Contain(error => error.Code == "DecisionLibrary");
        WorkflowDecisionLibraries.EvaluateDefinition(definition, new Dictionary<string, JsonElement> { ["invoice.status"] = JsonSerializer.SerializeToElement("Draft"),
            ["invoice.currency"] = JsonSerializer.SerializeToElement("GBP") }).Routes.Should().Equal("standard");
    }

    [Fact]
    public async Task Collect_Should_FeedOnlyItsExplicitTypedForeach_AndReuseDurableItemIdentitiesAcrossResumes()
    {
        using var host = new Host(); Guid secondId = await host.AddInvoice(); WorkflowVersionPin body = await host.PublishChild(PureChild);
        Guid runId = await host.Start(draft =>
        {
            DecisionTable table = AdvancedDecisionTableTests.Table("Collect",
                new DecisionRow("first", 1, [new("invoice.status", "eq", JsonSerializer.SerializeToElement("Draft"))], "first", CollectInvoiceId: new(Fact: "invoice.id")),
                new DecisionRow("second", 2, [new("invoice.status", "eq", JsonSerializer.SerializeToElement("Draft"))], "second", CollectInvoiceId: new(Literal: JsonSerializer.SerializeToElement(secondId))));
            table = table with { Fixtures = [new("normal", "Normal", DecisionTableEvaluatorTests.Facts("42.00"), "Completed", ["consume"]),
                new("boundary", "Boundary", DecisionTableEvaluatorTests.Facts("0.00"), "Completed", ["consume"]), new("missing", "MissingData", new Dictionary<string, JsonElement>(), "NeedsManualReview", [])] };
            return draft with { SchemaVersion = 2,
                Nodes = draft.Nodes.Where(node => node.ClientId is "start" or "route" or "done" or "manual").Select(node => node.ClientId == "route"
                    ? node with { ParamsJson = JsonSerializer.Serialize(table, WorkflowDefinitionCodec.JsonOptions) } : node).Append(new("each", "Loop", "Consume collected invoices", "", "", 2, 0,
                        JsonSerializer.Serialize(new WorkflowLoopParameters(2, body, [], 100, 3600, ItemsFromDecision: "route"), WorkflowDefinitionCodec.JsonOptions))).ToList(),
                Edges = [new("start", "route", 0, ""), new("route", "each", 0, "", "consume"), new("route", "manual", 1, "", "manual"),
                    new("each", "done", 0, "", "completed"), new("each", "manual", 1, "", "error"), new("each", "manual", 2, "", "exhausted")] };
        });
        for (int index = 0; index < 2; index++)
        {
            var parent = await host.Continue(runId); parent.Status.Should().Be(WorkflowRunStatuses.WaitingForChildren, parent.ErrorCode);
            Guid childId;
            using (IServiceScope scope = host.Scope()) childId = (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(run => run.ParentRunId == runId && run.Status == WorkflowRunStatuses.Queued)).Id;
            (await host.Continue(childId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        }

        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.Completed);

        using IServiceScope final = host.Scope(); var database = final.ServiceProvider.GetRequiredService<AgentsDbContext>();
        (await database.WorkflowRuns.Where(run => run.ParentRunId == runId).Select(run => run.SubjectId).ToListAsync()).Should().BeEquivalentTo(new Guid?[] { host.InvoiceId, secondId });
        var loop = await database.WorkflowStepRuns.SingleAsync(step => step.RunId == runId && step.NodeKey == "each" && step.InvocationPath == "root");
        JsonSerializer.Deserialize<WorkflowLoopParameters>(loop.ArgumentsJson!, WorkflowDefinitionCodec.JsonOptions)!.Items.Select(item => item.Key).Should().Equal("first", "second");
        JsonSerializer.Deserialize<WorkflowActionResult>(loop.ResultJson!, WorkflowDefinitionCodec.JsonOptions)!.Output.GetProperty("totalCount").GetInt32().Should().Be(2);
        (await database.ToolApprovalRequests.CountAsync(request => request.WorkflowStepRunId != null)).Should().Be(0);
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.Select(invoice => invoice.Status).ToListAsync()).Should().OnlyContain(status => status == "Draft");
    }

    private static IReadOnlyList<DecisionFactField> LibraryInputs() => [new("invoice.status", "String", true), new("invoice.currency", "Currency", true)];
    private static DecisionLibraryReference LibraryReference(WorkflowVersionPin pin) => new(1, pin,
        new Dictionary<string, WorkflowInputBinding> { ["invoice.status"] = new(Fact: "invoice.status"), ["invoice.currency"] = new(Fact: "invoice.currency") }, ["standard", "manual"], "Route");
    private static WorkflowSaveRequest WithLibrary(WorkflowSaveRequest draft, WorkflowVersionPin pin) => draft with { SchemaVersion = 2,
        Nodes = draft.Nodes.Select(node => node.ClientId == "route" ? node with { ParamsJson = JsonSerializer.Serialize(LibraryReference(pin), WorkflowDefinitionCodec.JsonOptions) } : node).ToList(),
        Edges = draft.Edges.Where(edge => edge.RouteKey != "elevated").ToList() };
    private static WorkflowSaveRequest LibraryDraft(WorkflowSaveRequest draft)
    {
        DecisionTable table = AdvancedDecisionTableTests.Table("FirstMatch", new DecisionRow("draft", 1, [new("invoice.status", "eq", JsonSerializer.SerializeToElement("Draft"))], "standard")) with
        { Fixtures = [new("normal", "Normal", new Dictionary<string, JsonElement> { ["invoice.status"] = JsonSerializer.SerializeToElement("Draft"), ["invoice.currency"] = JsonSerializer.SerializeToElement("GBP") }, "Completed", ["standard"]),
            new("boundary", "Boundary", new Dictionary<string, JsonElement> { ["invoice.status"] = JsonSerializer.SerializeToElement("Draft"), ["invoice.currency"] = JsonSerializer.SerializeToElement("GBP") }, "Completed", ["standard"]),
            new("missing", "MissingData", new Dictionary<string, JsonElement>(), "NeedsManualReview", [])] };
        return draft with { SchemaVersion = 2, DefinitionKind = "DecisionLibrary",
            Nodes = draft.Nodes.Where(node => node.ClientId is "start" or "route" or "done" or "manual").Select(node => node.ClientId == "route"
                ? node with { ParamsJson = JsonSerializer.Serialize(table, WorkflowDefinitionCodec.JsonOptions) }
                : node.ClientId == "start" ? node with { ParamsJson = JsonSerializer.Serialize(new DecisionLibraryTriggerParameters(new(1, LibraryInputs(), ["standard", "manual"], "Route")), WorkflowDefinitionCodec.JsonOptions) }
                : node with { ParamsJson = "{}" }).ToList(),
            Edges = [new("start", "route", 0, ""), new("route", "done", 0, "", "standard"), new("route", "manual", 1, "", "manual")] };
    }
    private static WorkflowSaveRequest SaveGraph(WorkflowGraphResponse graph)
    {
        Dictionary<Guid, string> keys = graph.Nodes.ToDictionary(node => node.Id, node => node.Key!);
        return new(graph.Slug, graph.Name, graph.Description, graph.State, graph.Version, graph.AutoRetry, graph.OwnerColor, null, [],
            graph.Nodes.Select(node => new WorkflowSaveNode(node.Key!, node.Kind, node.Label, node.Summary, node.Notes, node.X, node.Y, node.ParamsJson, node.Key)).ToList(),
            graph.Edges.Select(edge => new WorkflowSaveEdge(keys[edge.FromNodeId], keys[edge.ToNodeId], edge.FromIndex, edge.Label, edge.RouteKey)).ToList(), null, graph.ExpectedVersion, graph.DefinitionKind, graph.SchemaVersion);
    }
}
