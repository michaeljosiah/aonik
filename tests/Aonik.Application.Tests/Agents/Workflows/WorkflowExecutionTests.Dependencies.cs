using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.Agents.Workflows.Graph;

namespace Aonik.Application.Tests.Agents.Workflows;

public partial class WorkflowExecutionTests
{
    [Fact]
    public async Task DependencyValidation_Should_RejectAChangedPin_AndRejectAnOlderVersionOfTheSameWorkflowAsAChild()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild(PureChild);
        using IServiceScope scope = host.Scope(); var services = scope.ServiceProvider;
        var workflows = services.GetRequiredService<IWorkflowService>();
        var bad = await workflows.CreateAsync(WithChild(host.Draft(), pin with { DefinitionHash = new('f', 64) }));
        (await workflows.ValidateAsync(bad.Slug))!.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "WorkflowDependency");
        WorkflowGraphResponse child = (await workflows.GetBySlugAsync("child-invoice"))!;
        await workflows.UpdateAsync(WithChild(host.Draft(), pin) with { Slug = child.Slug, State = child.State, ExpectedVersion = child.ExpectedVersion });
        (await workflows.ValidateAsync(child.Slug))!.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "WorkflowDependency" && diagnostic.Message.Contains("Recursive"));
    }

    [Fact]
    public async Task DependencyValidation_Should_ApplyTheFiveLevelLimitAcrossEveryPinnedVersion()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild(PureChild, "depth0");
        for (int depth = 1; depth <= 5; depth++)
        {
            WorkflowVersionPin prior = pin;
            pin = await host.PublishChild(draft => WithChild(draft, prior), $"depth{depth}");
        }
        using IServiceScope scope = host.Scope(); var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        WorkflowGraphResponse draft = await workflows.CreateAsync(WithChild(host.Draft(), pin));

        WorkflowValidation validation = (await workflows.ValidateAsync(draft.Slug))!;

        validation.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "WorkflowDependency" && diagnostic.Message.Contains("five"));
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public async Task DependencyValidation_Should_BoundTheEntireRootInvocationTree_BeforeAdmission(int iterations, bool valid)
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild(LongWaitChild);
        using IServiceScope scope = host.Scope(); var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowService>();
        WorkflowGraphResponse draft = await workflows.CreateAsync(WithLoop(host.Draft(), new(1, pin,
            Enumerable.Range(0, 10).Select(index => new WorkflowLoopItem($"invoice-{index}", host.InvoiceId)).ToArray(), iterations, 3600)));

        WorkflowValidation validation = (await workflows.ValidateAsync(draft.Slug))!;

        validation.IsValid.Should().Be(valid);
        if (!valid) validation.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "WorkflowDependency" && diagnostic.Message.Contains("1,000"));
    }

    [Fact]
    public async Task ParentCancellation_Should_PreserveAnUncertainFinancialStep_ForInvestigation()
    {
        using var host = new Host(); WorkflowVersionPin pin = await host.PublishChild();
        Guid parentId = await host.Start(draft => WithChild(draft, pin)); await host.Continue(parentId);
        Guid stepId;
        using (IServiceScope scope = host.Scope())
        {
            var services = scope.ServiceProvider; var database = services.GetRequiredService<AgentsDbContext>();
            WorkflowRun child = await database.WorkflowRuns.SingleAsync(child => child.ParentRunId == parentId);
            var step = new WorkflowStepRun { TenantId = child.TenantId, RunId = child.Id, NodeKey = "issue", ActionKey = "finance.invoices.issue", ActionVersion = 1,
                OperationId = Guid.NewGuid(), Status = "NeedsAttention", ErrorCode = "ActionOutcomeUnknown" };
            database.WorkflowStepRuns.Add(step); await database.SaveChangesAsync(); stepId = step.Id;
            var runs = services.GetRequiredService<IWorkflowRunService>(); var parent = (await runs.GetAsync(parentId))!;
            await runs.CancelAsync(parentId, new(parent.ExpectedVersion));
        }
        using IServiceScope final = host.Scope();
        WorkflowStepRun saved = await final.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowStepRuns.SingleAsync(step => step.Id == stepId);
        saved.Status.Should().Be("NeedsAttention"); saved.ErrorCode.Should().Be("ActionOutcomeUnknown"); saved.ResultJson.Should().BeNull();
    }

    [Fact]
    public void ControlSimulation_Should_UseOnlyTheExplicitRouteFixture_AndPreserveSchemaOneRestrictions()
    {
        var definition = new WorkflowDefinition(2, "Workflow", "fixture-child", "Fixture", "", [new("start", "Trigger", "Start", JsonSerializer.SerializeToElement(new { })),
            new("child", "Child", "Child", JsonSerializer.SerializeToElement(new WorkflowChildParameters(1, new(Guid.NewGuid(), new('a', 64)), new(Fact: "invoice.id")), WorkflowDefinitionCodec.JsonOptions)),
            new("done", "End", "Done", JsonSerializer.SerializeToElement(new { }))],
            [new("start-child", "start", "child", 0, null), new("ok", "child", "done", 0, "completed"), new("error", "child", "done", 1, "error")]);
        WorkflowSimulator.Simulate(definition, new(new Dictionary<string, JsonElement>())).Status.Should().Be("NeedsFixture");
        var fixture = new WorkflowSimulationRequest(new Dictionary<string, JsonElement>(), new Dictionary<string, JsonElement> { ["child"] = JsonSerializer.SerializeToElement(new { route = "completed" }) });
        WorkflowSimulator.Simulate(definition, fixture).Status.Should().Be("Completed");
        WorkflowSimulator.Simulate(definition with { SchemaVersion = 1 }, fixture).Status.Should().Be("Invalid");
    }

    private static WorkflowSaveRequest LongWaitChild(WorkflowSaveRequest draft)
    {
        var nodes = PureChild(draft).Nodes.ToList(); var edges = new List<WorkflowSaveEdge>(); string prior = "start";
        for (int index = 0; index < 98; index++)
        {
            string key = $"wait{index}";
            nodes.Add(new(key, "Wait", key, "", "", index, 0, JsonSerializer.Serialize(new WorkflowWaitParameters(1, DelaySeconds: 1), WorkflowDefinitionCodec.JsonOptions)));
            edges.Add(new(prior, key, 0, "")); prior = key;
        }
        edges.Add(new(prior, "done", 0, "")); return draft with { Nodes = nodes, Edges = edges };
    }
}
