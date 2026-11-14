using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.Agents.Services.Workflows;
using Aonik.Agents.Workflows.Graph;
using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests;

public sealed class WorkflowCompositionSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableTheory]
    [InlineData("Timer")]
    [InlineData("Human")]
    [InlineData("HumanTimeout")]
    [InlineData("Loop")]
    public async Task NativeChildRequestAndCheckpoint_Should_RestoreFromSqlAfterAllServicesAreRecreated(string mode)
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        var fixture = new Scenario(); Guid parentId; Guid childId;
        using (ServiceProvider initial = Provider(fixture))
        {
            using IServiceScope scope = initial.CreateScope(); var services = scope.ServiceProvider;
            WorkflowVersion parentVersion = await SeedAsync(services.GetRequiredService<AgentsDbContext>(), fixture, mode);
            parentId = (await services.GetRequiredService<WorkflowRunService>().StartAsync("sql-parent", new(parentVersion.Id, "Invoice", fixture.InvoiceId, "parent-start"))).Id;
            var parent = await services.GetRequiredService<IGraphWorkflowRunner>().ContinueAsync(parentId, 0);
            parent.Status.Should().Be(WorkflowRunStatuses.WaitingForChildren, parent.ErrorCode);
            childId = (await services.GetRequiredService<AgentsDbContext>().WorkflowRuns.AsNoTracking().SingleAsync(run => run.ParentRunId == parentId)).Id;
            var child = await services.GetRequiredService<IGraphWorkflowRunner>().ContinueAsync(childId, 0);
            child.Status.Should().Be(mode is "Timer" or "Loop" ? WorkflowRunStatuses.WaitingForTimer : WorkflowRunStatuses.WaitingForApproval, child.ErrorCode);
        }
        // No EF change tracker, MAF executor, session or in-memory checkpoint manager survives this boundary.
        using (ServiceProvider restarted = Provider(fixture, fixture.ReviewerId))
        {
            using IServiceScope scope = restarted.CreateScope(); var services = scope.ServiceProvider;
            AgentsDbContext saved = services.GetRequiredService<AgentsDbContext>();
            (await saved.WorkflowCheckpoints.Where(checkpoint => checkpoint.RunId == parentId || checkpoint.RunId == childId)
                .Select(checkpoint => checkpoint.RuntimeSchemaVersion).Distinct().ToListAsync()).Should().Equal(2);
            if (mode == "Human")
            {
                ToolApprovalRequest request = await saved.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId != null);
                (await services.GetRequiredService<IToolApprovalService>().DecideAsync(request.Id,
                    new(ToolApprovalDecisionType.Approve, Response: JsonSerializer.SerializeToElement(new { choice = "accept" })))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
            }
            else fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddSeconds(60);
        }
        using (ServiceProvider final = Provider(fixture))
        {
            using IServiceScope scope = final.CreateScope(); var services = scope.ServiceProvider;
            var child = await services.GetRequiredService<IGraphWorkflowRunner>().ContinueAsync(childId, 0);
            child.Status.Should().Be(WorkflowRunStatuses.Completed, child.ErrorCode);
            var parent = await services.GetRequiredService<IGraphWorkflowRunner>().ContinueAsync(parentId, 0);
            parent.Status.Should().Be(WorkflowRunStatuses.Completed, parent.ErrorCode);
            parent.Outcome.Should().Be(mode is "HumanTimeout" or "Loop" ? "NeedsManualReview" : "Completed");
            (await services.GetRequiredService<IGraphWorkflowRunner>().ContinueAsync(parentId, 0)).Status.Should().Be(WorkflowRunStatuses.Completed);
            AgentsDbContext saved = services.GetRequiredService<AgentsDbContext>();
            (await saved.WorkflowRuns.CountAsync(run => run.ParentRunId == parentId)).Should().Be(1);
            var wake = await saved.Set<OutboxMessage>().SingleAsync(message => message.EventId == childId);
            JsonSerializer.Deserialize<WorkflowContinuationRequestedEvent>(wake.Payload, OutboxSerialization.Options)!.RunId.Should().Be(parentId);
        }
    }

    [SkippableFact]
    public async Task ChildAdmission_Should_DeduplicateConcurrentReplays_WithOneChildAndAtomicInitialAndDeadlineWakes()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        var fixture = new Scenario(); Guid parentId; Guid stepId; WorkflowVersionPin pin;
        using (ServiceProvider initial = Provider(fixture))
        {
            using IServiceScope scope = initial.CreateScope(); var services = scope.ServiceProvider; var context = services.GetRequiredService<AgentsDbContext>();
            WorkflowVersion parentVersion = await SeedAsync(context, fixture, "Timer");
            parentId = (await services.GetRequiredService<WorkflowRunService>().StartAsync("sql-parent", new(parentVersion.Id, "Invoice", fixture.InvoiceId, "parent-race"))).Id;
            WorkflowRun parent = await context.WorkflowRuns.SingleAsync(run => run.Id == parentId);
            parent.Status = WorkflowRunStatuses.Running; parent.LeaseId = Guid.NewGuid(); parent.LeaseExpiresAt = fixture.Clock.UtcNow.AddMinutes(2);
            var step = new WorkflowStepRun { TenantId = fixture.TenantId, RunId = parent.Id, NodeKey = "child", OperationId = Guid.NewGuid(), Status = "WaitingForChild" };
            context.WorkflowStepRuns.Add(step); await context.SaveChangesAsync(); stepId = step.Id;
            WorkflowVersion child = await context.WorkflowVersions.SingleAsync(version => version.Id != parentVersion.Id);
            pin = new(child.Id, child.DefinitionHash!);
        }
        async Task<Guid> Admit()
        {
            for (int attempt = 0; ; attempt++)
            {
                using ServiceProvider provider = Provider(fixture); using IServiceScope scope = provider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
                WorkflowRun parent = await context.WorkflowRuns.SingleAsync(run => run.Id == parentId);
                WorkflowStepRun step = await context.WorkflowStepRuns.SingleAsync(step => step.Id == stepId);
                try { return (await scope.ServiceProvider.GetRequiredService<WorkflowRunService>().AdmitChildAsync(parent, step, pin, fixture.InvoiceId)).Id; }
                catch (WorkflowConflictException) when (attempt < 3) { }
            }
        }
        Guid[] childIds = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Admit()));
        childIds.Distinct().Should().ContainSingle();
        using ServiceProvider final = Provider(fixture); using IServiceScope finalScope = final.CreateScope();
        var saved = finalScope.ServiceProvider.GetRequiredService<AgentsDbContext>();
        (await saved.WorkflowRuns.CountAsync(run => run.ParentStepRunId == stepId)).Should().Be(1);
        Guid childId = childIds[0];
        var wakes = (await saved.Set<OutboxMessage>().Where(message => message.EventType == typeof(WorkflowContinuationRequestedEvent).FullName).ToListAsync())
            .Select(message => JsonSerializer.Deserialize<WorkflowContinuationRequestedEvent>(message.Payload, OutboxSerialization.Options)!).Where(message => message.RunId == childId).ToArray();
        wakes.Should().HaveCount(2); wakes.Should().ContainSingle(message => message.NotBeforeUtc == fixture.Clock.UtcNow);
        wakes.Should().ContainSingle(message => message.NotBeforeUtc == fixture.Clock.UtcNow.AddDays(7));
    }

    private ServiceProvider Provider(Scenario fixture, Guid? currentUserId = null)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<ITenantProvider>(new TestTenantProvider(fixture.TenantId));
        services.AddSingleton<ICurrentUserProvider>(new TestCurrentUserProvider(currentUserId ?? fixture.AuthorId));
        services.AddSingleton<IClock>(fixture.Clock); services.AddSingleton<IPermissionService>(new AllowAllPermissionService());
        var users = new Mock<IUserDirectoryReader>();
        users.Setup(reader => reader.GetByIdsAsync(fixture.TenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, IReadOnlyCollection<Guid> ids, CancellationToken _) => (IReadOnlyList<UserDirectoryItem>)ids
                .Where(id => id == fixture.AuthorId || id == fixture.ReviewerId).Select(id => new UserDirectoryItem(id, null, "Active")).ToArray());
        services.AddSingleton(users.Object); services.AddSingleton(Mock.Of<ITenantRoleReader>());
        var settings = new Mock<ITenantSettingStore>();
        settings.Setup(store => store.GetTenantValueAsync(WorkflowSettingNames.ExecutionEnabled, fixture.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync("true");
        services.AddSingleton(settings.Object); services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IWorkflowFactsProvider>(new Facts(fixture));
        services.AddSingleton(database.CreateOptions<AgentsDbContext>()); services.AddScoped<AgentsDbContext>();
        services.AddScoped<WorkflowAuthorization>(); services.AddScoped<WorkflowExecutionAuthorization>(); services.AddScoped<WorkflowReviewerAuthorization>();
        services.AddScoped<WorkflowInvocationContext>(); services.AddScoped<WorkflowActionRegistry>(); services.AddScoped<WorkflowDependencyValidator>();
        services.AddScoped<WorkflowContinuationWriter>(); services.AddScoped<WorkflowApprovalAuthority>();
        services.AddScoped<WorkflowRunService>(); services.AddScoped<WorkflowStepExecutionService>(); services.AddScoped<IGraphWorkflowRunner, GraphWorkflowRunner>();
        services.AddScoped<IToolApprovalRequestStore, ToolApprovalRequestStore>(); services.AddScoped<IToolApprovalService, ToolApprovalService>();
        services.AddSingleton(Mock.Of<IAgentProposalStore>()); services.AddSingleton(Mock.Of<IProposalApprovalService>()); services.AddSingleton(Mock.Of<IProposalApprovalPolicy>());
        return services.BuildServiceProvider();
    }

    private static async Task<WorkflowVersion> SeedAsync(AgentsDbContext context, Scenario fixture, string mode)
    {
        JsonElement Empty() => JsonSerializer.SerializeToElement(new { });
        var child = new Workflow { TenantId = fixture.TenantId, Slug = "sql-child", Name = "SQL child", State = WorkflowStates.Active, DefinitionSchemaVersion = 2 };
        WorkflowDefinition childDefinition = mode is "Timer" or "Loop"
            ? new(2, "Workflow", child.Slug, child.Name, "", [new("start", "Trigger", "Start", Empty()), new("wait", "Wait", "Wait", JsonSerializer.SerializeToElement(new WorkflowWaitParameters(1, DelaySeconds: 60), WorkflowDefinitionCodec.JsonOptions)), new("done", "End", "Done", Empty())],
                [new("first", "start", "wait", 0, null), new("last", "wait", "done", 0, null)])
            : new(2, "Workflow", child.Slug, child.Name, "", [new("start", "Trigger", "Start", Empty()), new("human", "Human", "Review", JsonSerializer.SerializeToElement(new WorkflowHumanParameters(1, "Review evidence", new(UserId: fixture.ReviewerId), 60,
                    [new("accept", "Accept evidence", "accepted")], "rejected", "timeout"), WorkflowDefinitionCodec.JsonOptions)), new("done", "End", "Done", Empty()), new("manual", "End", "Manual", JsonSerializer.SerializeToElement(new { outcome = "NeedsManualReview" }))],
                [new("first", "start", "human", 0, null), new("yes", "human", "done", 0, "accepted"), new("no", "human", "manual", 1, "rejected"), new("late", "human", "manual", 2, "timeout")]);
        WorkflowVersion childVersion = Version(child, childDefinition);
        var parent = new Workflow { TenantId = fixture.TenantId, Slug = "sql-parent", Name = "SQL parent", State = WorkflowStates.Active, DefinitionSchemaVersion = 2 };
        var parentDefinition = new WorkflowDefinition(2, "Workflow", parent.Slug, parent.Name, "", [new("start", "Trigger", "Start", Empty()),
            new("child", "Child", "Child", JsonSerializer.SerializeToElement(new WorkflowChildParameters(1, new(childVersion.Id, childVersion.DefinitionHash!), new(Fact: "invoice.id")), WorkflowDefinitionCodec.JsonOptions)),
            new("done", "End", "Done", Empty()), new("manual", "End", "Manual", JsonSerializer.SerializeToElement(new { outcome = "NeedsManualReview" }))],
            [new("first", "start", "child", 0, null), new("yes", "child", "done", 0, "completed"), new("no", "child", "manual", 1, "error")]);
        if (mode == "Loop") parentDefinition = parentDefinition with
        {
            Nodes = parentDefinition.Nodes.Select(node => node.Key == "child" ? node with { Kind = "Loop", Parameters = JsonSerializer.SerializeToElement(
                new WorkflowLoopParameters(1, new(childVersion.Id, childVersion.DefinitionHash!), [new("first", fixture.InvoiceId), new("second", fixture.InvoiceId)], 1, 3600), WorkflowDefinitionCodec.JsonOptions) } : node).ToList(),
            Edges = [.. parentDefinition.Edges, new("exhausted", "child", "manual", 2, "exhausted")],
        };
        WorkflowVersion parentVersion = Version(parent, parentDefinition);
        context.Workflows.AddRange(child, parent); context.WorkflowVersions.AddRange(childVersion, parentVersion); await context.SaveChangesAsync();
        return parentVersion;

        WorkflowVersion Version(Workflow workflow, WorkflowDefinition definition)
        {
            string json = WorkflowDefinitionCodec.Serialize(definition);
            var version = new WorkflowVersion { TenantId = fixture.TenantId, WorkflowId = workflow.Id, Tag = "v1", SchemaVersion = 2,
                DefinitionJson = json, DefinitionHash = WorkflowDefinitionCodec.Hash(json), PublishedAt = fixture.Clock.UtcNow, PublishedByUserId = fixture.AuthorId };
            workflow.PublishedVersionId = version.Id; return version;
        }
    }

    private sealed class Scenario
    {
        public Guid TenantId { get; } = Guid.NewGuid(); public Guid AuthorId { get; } = Guid.NewGuid(); public Guid ReviewerId { get; } = Guid.NewGuid(); public Guid InvoiceId { get; } = Guid.NewGuid();
        public Clock Clock { get; } = new();
    }
    private sealed class Clock : IClock { public DateTime UtcNow { get; set; } = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class Facts(Scenario fixture) : IWorkflowFactsProvider
    {
        public string SubjectType => "Invoice"; public string Schema => "invoice-approval.v1";
        public Task<WorkflowFactsSnapshot?> LoadAsync(Guid id, WorkflowActor actor, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkflowFactsSnapshot?>(id == fixture.InvoiceId ? new(Schema, new Dictionary<string, JsonElement> { ["invoice.id"] = JsonSerializer.SerializeToElement(id) }, "facts-v1", SubjectCreatedByUserId: fixture.AuthorId) : null);
    }
}
