using FluentAssertions;
using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests;

public class WorkflowDraftSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableFact]
    public async Task ConcurrentEdit_Should_RefuseTheStaleWriter_AndRollBackItsSnapshotAndNodes()
    {
        // Arrange
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        Guid tenantId = Guid.NewGuid();
        await using AgentsDbContext first = Context(tenantId);
        await using AgentsDbContext second = Context(tenantId);
        WorkflowService writerOne = Service(first, tenantId);
        WorkflowService writerTwo = Service(second, tenantId);
        WorkflowSaveRequest request = Draft();
        WorkflowGraphResponse created = await writerOne.CreateAsync(request);
        created.ExpectedVersion.Should().NotBeEmpty();
        // Keep the second writer's original rowversion tracked before the first commits.
        await second.Workflows.SingleAsync(workflow => workflow.Id == created.Id);
        await writerOne.UpdateAsync(request with { ExpectedVersion = created.ExpectedVersion, Name = "First writer" });

        // Act
        Func<Task> stale = async () => await writerTwo.UpdateAsync(request with
        {
            ExpectedVersion = created.ExpectedVersion, Name = "Stale writer",
            Nodes = request.Nodes.Select(node => node with { Label = "Stale node" }).ToList(),
        });

        // Assert
        await stale.Should().ThrowAsync<WorkflowConflictException>();
        await using AgentsDbContext verify = Context(tenantId);
        (await verify.Workflows.SingleAsync(workflow => workflow.Id == created.Id)).Name.Should().Be("First writer");
        (await verify.WorkflowVersions.CountAsync(version => version.WorkflowId == created.Id)).Should().Be(2);
        (await verify.WorkflowNodes.Where(node => node.WorkflowId == created.Id).Select(node => node.Label).ToListAsync())
            .Should().NotContain("Stale node");
    }

    [SkippableFact]
    public async Task SlugAndNodeIndexes_Should_EnforceTenantUniqueness_WhileAllowingLegacyNullKeys()
    {
        // Arrange
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        Guid tenantId = Guid.NewGuid();
        Guid workflowId;
        await using (AgentsDbContext first = Context(tenantId))
        {
            WorkflowGraphResponse graph = await Service(first, tenantId).CreateAsync(Draft());
            workflowId = graph.Id;
        }
        await using (AgentsDbContext duplicate = Context(tenantId))
        {
            duplicate.Workflows.Add(new Workflow { TenantId = tenantId, Slug = "invoice-routing", Name = "Duplicate" });
            Func<Task> save = async () => await duplicate.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>();
        }
        Guid otherTenantId = Guid.NewGuid();
        await using (AgentsDbContext otherTenant = Context(otherTenantId))
            await Service(otherTenant, otherTenantId).CreateAsync(Draft());

        // Act / Assert: a legacy graph may contain multiple null keys; an actual key cannot repeat.
        await using (AgentsDbContext legacy = Context(tenantId))
        {
            legacy.WorkflowNodes.AddRange(
                new WorkflowNode { TenantId = tenantId, WorkflowId = workflowId, Kind = "End", Label = "Legacy 1" },
                new WorkflowNode { TenantId = tenantId, WorkflowId = workflowId, Kind = "End", Label = "Legacy 2" });
            await legacy.SaveChangesAsync();
        }
        await using AgentsDbContext nodeDuplicate = Context(tenantId);
        nodeDuplicate.WorkflowNodes.Add(new WorkflowNode
        {
            TenantId = tenantId, WorkflowId = workflowId, LogicalKey = "start", Kind = "Trigger", Label = "Duplicate node",
        });
        Func<Task> duplicateNode = async () => await nodeDuplicate.SaveChangesAsync();
        await duplicateNode.Should().ThrowAsync<DbUpdateException>();

    }

    private AgentsDbContext Context(Guid tenantId) => new(database.CreateOptions<AgentsDbContext>(), new TestTenantProvider(tenantId));

    private static WorkflowService Service(AgentsDbContext context, Guid tenantId)
    {
        var tenant = new TestTenantProvider(tenantId);
        return new(context, tenant, new Clock(), new WorkflowAuthorization(
            tenant, new TestCurrentUserProvider(Guid.NewGuid()), new AllowAllPermissionService()));
    }

    private static WorkflowSaveRequest Draft() => new("invoice-routing", "Invoice routing", "", "Draft", "v0.1", false,
        "", null, [], [new("start", "Trigger", "Start", "", "", 0, 0, "{}"), new("end", "End", "Done", "", "", 1, 0, "{}")],
        [new("start", "end", 0, "")], null);

    private sealed class Clock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
}
