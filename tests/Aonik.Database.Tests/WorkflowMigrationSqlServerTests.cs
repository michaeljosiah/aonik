using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Aonik.Infrastructure.Persistence;
using Aonik.IntegrationTests.Support;
using Aonik.TestSupport.Multitenancy;
using Aonik.Database.Tests.Support;

namespace Aonik.Database.Tests;

public class WorkflowMigrationSqlServerTests(WorkflowMigrationFixture fixture) : IClassFixture<WorkflowMigrationFixture>
{
    [SkippableFact]
    public async Task Migration_Should_PreserveExistingGraphs_AsUnpublishedWorkflows_WithoutInventingExecutableHistory()
    {
        // Arrange: all changes and rollback occur in the fixture's isolated throwaway database.
        SqlLocalDbFixture database = fixture.Database;
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        Guid tenantId = Guid.NewGuid();
        Guid workflowId = Guid.NewGuid();
        Guid versionId = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;
        await using var context = new AonikDbContext(database.CreateOptions<AonikDbContext>(), new TestTenantProvider(tenantId));
        IMigrator migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261111000745_ExpandPublicationVerificationEvidence");
        // Seed historical data with the old schema, before the new columns exist.
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.AnkWorkflows
              (Id,TenantId,Slug,Name,Description,State,Version,OwnerColor,ContributorsJson,AutoRetry,TriggerCount,CreatedAt,IsDeleted)
            VALUES ({workflowId},{tenantId},N'legacy-routing',N'Legacy routing',N'',N'Active',N'v0.1',N'',N'[]',0,0,{now},0)
            """);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.AnkWorkflowVersions
              (Id,TenantId,WorkflowId,Tag,Message,AuthorName,AuthorColor,CreatedAt,IsDeleted)
            VALUES ({versionId},{tenantId},{workflowId},N'v0.1',N'Historical metadata',N'Editor',N'',{now},0)
            """);

        // Act
        await migrator.MigrateAsync();
        var workflow = await context.Workflows.AsNoTracking().SingleAsync(workflow => workflow.Id == workflowId);
        var history = await context.WorkflowVersions.AsNoTracking().SingleAsync(version => version.Id == versionId);

        // Assert
        workflow.DefinitionKind.Should().Be("Workflow");
        workflow.PublishedVersionId.Should().BeNull();
        history.DefinitionJson.Should().BeNull();
        history.DefinitionHash.Should().BeNull();
        history.PublishedAt.Should().BeNull();
        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }
}
