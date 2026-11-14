using System.Text.Json;
using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Aonik.Application.Tests.Agents.Workflows;

public class WorkflowServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;
        public bool TryGetCurrentTenantId(out Guid id) { id = tenantId; return true; }
    }

    private sealed class FixedClock(DateTime now) : IClock { public DateTime UtcNow => now; }

    private static (IWorkflowService Service, AgentsDbContext Db) BuildService(
        Guid tenantId,
        DateTime? now = null,
        bool grantPermissions = true)
    {
        var services = new ServiceCollection();
        var dbName = $"WorkflowServiceTests_{Guid.NewGuid()}";

        services.AddSingleton<ITenantProvider>(new TestTenantProvider(tenantId));
        services.AddSingleton<IClock>(new FixedClock(now ?? DateTime.UtcNow));
        services.AddDbContext<AgentsDbContext>(options =>
        {
            options.UseInMemoryDatabase(dbName);
        });

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AgentsDbContext>();
        Guid userId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var user = new Mock<ICurrentUserProvider>();
        user.Setup(provider => provider.TryGetCurrentUserId(out userId)).Returns(true);
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(service => service.HasPermissionAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(grantPermissions);
        var tenant = sp.GetRequiredService<ITenantProvider>();
        var service = new WorkflowService(db, tenant, sp.GetRequiredService<IClock>(),
            new WorkflowAuthorization(tenant, user.Object, permissions.Object));
        return (service, db);
    }

    [Fact]
    public async Task ListAsync_Should_ReturnEmpty_When_NoWorkflowsSeeded()
    {
        var (service, _) = BuildService(TenantA);

        var result = await service.ListAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_Should_ReturnWorkflowsForCurrentTenant_With_OwnerNameAndStepRail()
    {
        var now = new DateTime(2026, 04, 30, 12, 0, 0, DateTimeKind.Utc);
        var (service, db) = BuildService(TenantA, now);

        var ownerId = Guid.NewGuid();
        db.Agents.Add(new Agent
        {
            Id = ownerId,
            TenantId = TenantA,
            Name = "Billing",
            CreatedAt = now,
        });

        var workflowId = Guid.NewGuid();
        db.Workflows.Add(new Workflow
        {
            Id = workflowId,
            TenantId = TenantA,
            Slug = "match_and_apply",
            Name = "Match & apply",
            Description = "Reconcile invoice → bank txn",
            OwnerAgentId = ownerId,
            OwnerColor = "#eb5c37",
            ContributorsJson = "[]",
            State = WorkflowStates.Active,
            Version = "v1.4",
            AutoRetry = true,
            TriggerCount = 4,
            CreatedAt = now,
        });

        db.WorkflowNodes.AddRange(
            new WorkflowNode { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Kind = WorkflowNodeKinds.Trigger, Label = "On bank txn", X = 0, Y = 0, CreatedAt = now },
            new WorkflowNode { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Kind = WorkflowNodeKinds.Tool, Label = "search_invoices", X = 200, Y = 0, CreatedAt = now },
            new WorkflowNode { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Kind = WorkflowNodeKinds.End, Label = "Done", X = 400, Y = 0, CreatedAt = now });

        db.WorkflowRuns.AddRange(
            new WorkflowRun { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Status = WorkflowRunStatuses.Success, DurationMs = 2000, StartedAt = now.AddMinutes(-30), CreatedAt = now.AddMinutes(-30) },
            new WorkflowRun { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Status = WorkflowRunStatuses.Success, DurationMs = 3000, StartedAt = now.AddHours(-1), CreatedAt = now.AddHours(-1) },
            new WorkflowRun { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Status = WorkflowRunStatuses.Failed, DurationMs = 800, StartedAt = now.AddHours(-2), CreatedAt = now.AddHours(-2) });

        await db.SaveChangesAsync();

        var result = await service.ListAsync();

        result.Should().HaveCount(1);
        var wf = result[0];
        wf.Slug.Should().Be("match_and_apply");
        wf.OwnerName.Should().Be("Billing");
        wf.OwnerColor.Should().Be("#eb5c37");
        wf.RunsToday.Should().Be(3);
        wf.Success.Should().BeApproximately(2.0 / 3.0, 0.01);
        // Avg of 2000 + 3000 + 800 = 5800 / 3 ≈ 1933
        wf.AvgMs.Should().BeInRange(1900, 1950);
        wf.Steps.Should().HaveCount(3);
        wf.Steps[0].Kind.Should().Be(WorkflowNodeKinds.Trigger);
    }

    [Fact]
    public async Task ListAsync_Should_NotReturnOtherTenantWorkflows()
    {
        var (service, db) = BuildService(TenantA);

        db.Workflows.Add(new Workflow
        {
            Id = Guid.NewGuid(),
            TenantId = TenantB, // different tenant
            Slug = "tenantb_only",
            Name = "Other tenant",
            ContributorsJson = "[]",
            State = WorkflowStates.Active,
            Version = "v1.0",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await service.ListAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBySlugAsync_Should_ReturnGraph_With_NodesEdgesComments()
    {
        var (service, db) = BuildService(TenantA);

        var workflowId = Guid.NewGuid();
        var nodeAId = Guid.NewGuid();
        var nodeBId = Guid.NewGuid();

        db.Workflows.Add(new Workflow
        {
            Id = workflowId,
            TenantId = TenantA,
            Slug = "test_flow",
            Name = "Test flow",
            ContributorsJson = "[]",
            State = WorkflowStates.Draft,
            Version = "v0.1",
            CreatedAt = DateTime.UtcNow,
        });
        db.WorkflowNodes.AddRange(
            new WorkflowNode { Id = nodeAId, TenantId = TenantA, WorkflowId = workflowId, Kind = WorkflowNodeKinds.Trigger, Label = "Start", CreatedAt = DateTime.UtcNow },
            new WorkflowNode { Id = nodeBId, TenantId = TenantA, WorkflowId = workflowId, Kind = WorkflowNodeKinds.End, Label = "Done", CreatedAt = DateTime.UtcNow });
        db.WorkflowEdges.Add(new WorkflowEdge { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, FromNodeId = nodeAId, ToNodeId = nodeBId, CreatedAt = DateTime.UtcNow });
        db.WorkflowComments.Add(new WorkflowComment { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Author = "Maria", Body = "Approved", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var graph = await service.GetBySlugAsync("test_flow");

        graph.Should().NotBeNull();
        graph!.Nodes.Should().HaveCount(2);
        graph.Edges.Should().HaveCount(1);
        graph.Comments.Should().HaveCount(1);
        graph.Comments[0].Author.Should().Be("Maria");
    }

    [Fact]
    public async Task GetBySlugAsync_Should_ReturnNull_When_SlugNotFound()
    {
        var (service, _) = BuildService(TenantA);
        var graph = await service.GetBySlugAsync("does_not_exist");
        graph.Should().BeNull();
    }

    [Fact]
    public async Task ListRunsAsync_Should_ReturnNewestFirst_With_FormattedDuration()
    {
        var now = new DateTime(2026, 04, 30, 12, 0, 0, DateTimeKind.Utc);
        var (service, db) = BuildService(TenantA, now);

        var workflowId = Guid.NewGuid();
        db.WorkflowRuns.AddRange(
            new WorkflowRun { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Status = WorkflowRunStatuses.Success, DurationMs = 2400, StartedAt = now.AddMinutes(-2), CreatedAt = now.AddMinutes(-2), StartedBy = "auto · banking.transaction.received" },
            new WorkflowRun { Id = Guid.NewGuid(), TenantId = TenantA, WorkflowId = workflowId, Status = WorkflowRunStatuses.Held, DurationMs = 434000, StartedAt = now.AddMinutes(-38), CreatedAt = now.AddMinutes(-38), StartedBy = "held · over ceiling" });
        await db.SaveChangesAsync();

        var runs = await service.ListRunsAsync(workflowId);

        runs.Should().HaveCount(2);
        runs[0].Duration.Should().Be("2.4s");
        runs[0].When.Should().Be("2m ago");
        runs[1].Status.Should().Be(WorkflowRunStatuses.Held);
    }

    [Fact]
    public async Task CreateAsync_Should_SaveAnImmutableInitialDraftSnapshot_AndRejectDuplicateSlugs()
    {
        // Arrange
        var (service, db) = BuildService(TenantA);
        WorkflowSaveRequest request = Draft();

        // Act
        WorkflowGraphResponse created = await service.CreateAsync(request);
        Func<Task> duplicate = async () => await service.CreateAsync(request with { Name = "Overwrite" });

        // Assert
        created.State.Should().Be(WorkflowStates.Draft);
        created.Version.Should().Be("v0.1");
        WorkflowVersion snapshot = await db.WorkflowVersions.SingleAsync();
        snapshot.DefinitionJson.Should().NotBeNull();
        snapshot.DefinitionHash.Should().HaveLength(64);
        snapshot.PublishedAt.Should().BeNull();
        await duplicate.Should().ThrowAsync<WorkflowConflictException>();
        (await service.GetBySlugAsync(request.Slug))!.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task UpdateAsync_Should_PreserveNodeKeysAndIds_AndPriorSnapshots()
    {
        // Arrange
        var (service, db) = BuildService(TenantA);
        WorkflowSaveRequest request = Draft();
        WorkflowGraphResponse initial = await service.CreateAsync(request);
        string originalSnapshot = (await db.WorkflowVersions.SingleAsync()).DefinitionJson!;

        // Act
        WorkflowGraphResponse? edited = await service.UpdateAsync(request with
        {
            ExpectedVersion = initial.ExpectedVersion,
            Nodes = request.Nodes.Select(node => node with { Label = node.Label + " edited" }).ToList(),
        });

        // Assert
        edited.Should().NotBeNull();
        edited!.Nodes.Select(node => node.Id).Should().BeEquivalentTo(initial.Nodes.Select(node => node.Id));
        edited.Nodes.Select(node => node.Key).Should().BeEquivalentTo(initial.Nodes.Select(node => node.Key));
        edited.Edges.Select(edge => edge.Id).Should().Equal(initial.Edges.Select(edge => edge.Id));
        (await db.WorkflowVersions.SingleAsync(version => version.Tag == "v0.1")).DefinitionJson.Should().Be(originalSnapshot);
        (await db.WorkflowVersions.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task UpdateAsync_Should_NotCreateUnknownWorkflows_AndRejectAStaleToken()
    {
        // Arrange
        var (service, db) = BuildService(TenantA);
        WorkflowSaveRequest request = Draft();
        (await service.UpdateAsync(request with { ExpectedVersion = "" })).Should().BeNull();
        WorkflowGraphResponse created = await service.CreateAsync(request);
        // InMemory does not generate SQL rowversions; emulate a concurrent database update.
        (await db.Workflows.SingleAsync()).RowVersion = [1, 2, 3];
        await db.SaveChangesAsync();

        // Act
        Func<Task> staleEdit = async () => await service.UpdateAsync(request with { ExpectedVersion = created.ExpectedVersion });

        // Assert
        await staleEdit.Should().ThrowAsync<WorkflowConflictException>();
        (await db.WorkflowVersions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_Should_SaveIncompleteDrafts_WithoutMakingThemExecutable()
    {
        // Arrange
        var (service, _) = BuildService(TenantA);
        WorkflowSaveRequest request = Draft() with { Nodes = [], Edges = [] };

        // Act
        await service.CreateAsync(request);
        WorkflowValidation? validation = await service.ValidateAsync(request.Slug);

        // Assert
        validation.Should().NotBeNull();
        validation!.IsValid.Should().BeFalse();
        validation.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "GraphSize");
    }

    [Theory]
    [InlineData("publication")]
    [InlineData("run")]
    [InlineData("trigger")]
    public async Task DeleteAsync_Should_RefusePublishedOrUsedWorkflows(string reference)
    {
        // Arrange
        var (service, db) = BuildService(TenantA);
        WorkflowGraphResponse graph = await service.CreateAsync(Draft());
        Workflow workflow = await db.Workflows.SingleAsync();
        if (reference == "publication") workflow.PublishedVersionId = Guid.NewGuid();
        if (reference == "trigger") workflow.TriggerCount = 1;
        if (reference == "run") db.WorkflowRuns.Add(new WorkflowRun { TenantId = TenantA, WorkflowId = workflow.Id });
        await db.SaveChangesAsync();

        // Act
        Func<Task> delete = async () => await service.DeleteAsync(graph.Slug, graph.ExpectedVersion);

        // Assert
        await delete.Should().ThrowAsync<WorkflowConflictException>();
        workflow.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_Should_PreserveDraftHistory_AndRequireTheCurrentToken()
    {
        // Arrange
        var (service, db) = BuildService(TenantA);
        WorkflowGraphResponse graph = await service.CreateAsync(Draft());
        Func<Task> missingToken = async () => await service.DeleteAsync(graph.Slug, null);

        // Act
        await missingToken.Should().ThrowAsync<ArgumentException>();
        bool deleted = await service.DeleteAsync(graph.Slug, graph.ExpectedVersion);

        // Assert
        deleted.Should().BeTrue();
        (await service.GetBySlugAsync(graph.Slug)).Should().BeNull();
        (await db.WorkflowVersions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Authoring_Should_EnforcePermissions_ForDirectServiceCalls()
    {
        // Arrange
        var (service, db) = BuildService(TenantA, grantPermissions: false);
        Func<Task> create = async () => await service.CreateAsync(Draft());
        Func<Task> read = async () => await service.ListAsync();

        // Act / Assert
        await create.Should().ThrowAsync<PermissionDeniedException>();
        await read.Should().ThrowAsync<PermissionDeniedException>();
        (await db.Workflows.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_Should_RejectRenamingOrAliasingAnExistingLogicalNode()
    {
        // Arrange
        var (service, db) = BuildService(TenantA);
        WorkflowSaveRequest original = Draft();
        WorkflowGraphResponse graph = await service.CreateAsync(original);
        WorkflowGraphNode node = graph.Nodes.Single(node => node.Key == "start");
        WorkflowSaveRequest renamed = original with
        {
            ExpectedVersion = graph.ExpectedVersion,
            Nodes = [original.Nodes[0] with { ClientId = node.Id.ToString("D"), Key = "renamed" }, original.Nodes[1]],
            Edges = [new(node.Id.ToString("D"), "end", 0, "")],
        };

        // Act
        Func<Task> edit = async () => await service.UpdateAsync(renamed);

        // Assert
        await edit.Should().ThrowAsync<ArgumentException>();
        (await db.WorkflowVersions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_Should_RejectDuplicateJsonProperties_BeforePersistingAnything()
    {
        // Arrange
        var (service, db) = BuildService(TenantA);
        WorkflowSaveRequest original = Draft();
        WorkflowSaveRequest invalid = original with
        {
            Nodes = [original.Nodes[0] with { ParamsJson = "{\"value\":1,\"value\":2}" }, original.Nodes[1]],
        };

        // Act
        Func<Task> create = async () => await service.CreateAsync(invalid);

        // Assert
        await create.Should().ThrowAsync<ArgumentException>();
        (await db.Workflows.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Capabilities_Should_ExposeTheActualDecisionSchema_AndRequiredInvoiceFacts()
    {
        // Arrange
        var (service, _) = BuildService(TenantA);

        // Act
        WorkflowCapabilities capabilities = await service.GetCapabilitiesAsync();

        // Assert
        capabilities.DecisionParametersSchema.GetProperty("properties").TryGetProperty("fixtures", out _).Should().BeTrue();
        capabilities.InvoiceFacts.Should().Contain(field => field.Name == "invoice.total" && field.Required && field.Type == "Decimal");
        capabilities.ComparisonOperators.Should().Contain("gte").And.NotContain("script");
    }

    [Fact]
    public async Task LegacyGraphs_Should_BePresentedAsDrafts_UntilTheyHaveAnExplicitPublication()
    {
        // Arrange
        var (service, db) = BuildService(TenantA);
        db.Workflows.Add(new Workflow { TenantId = TenantA, Slug = "legacy", Name = "Legacy", State = WorkflowStates.Active });
        await db.SaveChangesAsync();

        // Act
        WorkflowGraphResponse? graph = await service.GetBySlugAsync("legacy");

        // Assert
        graph!.State.Should().Be(WorkflowStates.Draft);
        graph.PublishedVersionId.Should().BeNull();
    }

    private static WorkflowSaveRequest Draft() => new("invoice-routing", "Invoice routing", "", "Draft", "v0.1", false,
        "", null, [], [new("start", "Trigger", "Start", "", "", 0, 0, "{}"), new("end", "End", "Done", "", "", 1, 0, "{}")],
        [new("start", "end", 0, "")], null);
}
