using Aonik.Commerce.Entities.Catalog;
using Aonik.Database.Tests.Support;
using Aonik.IntegrationTests.Support;

using FluentAssertions;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Database.Tests.Commerce;

/// <summary>
/// Spec 066 §5 — the at-most-one-recommended-default invariant is enforced by
/// <c>IX_AnkOptionChoices_RecommendedDefault_Unique</c>, a filtered unique index
/// on (TenantId, OptionGroupId) WHERE IsRecommendedDefault = 1 AND IsActive = 1
/// AND IsDeleted = 0. The InMemory provider ignores index definitions entirely,
/// so both the constraint and its filter shape were previously only verifiable
/// by reading the configuration. Here the engine enforces it: a second active
/// default is rejected, and rows the filter excludes (inactive, soft-deleted)
/// do not collide.
/// </summary>
public class RecommendedDefaultUniqueIndexSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private const string IndexName = "IX_AnkOptionChoices_RecommendedDefault_Unique";

    private readonly SqlLocalDbFixture _db;

    public RecommendedDefaultUniqueIndexSqlServerTests(SqlLocalDbFixture db)
    {
        _db = db;
    }

    [SkippableFact]
    public async Task Insert_Should_ViolateFilteredUniqueIndex_When_GroupAlreadyHasAnActiveDefault()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var (groupId, _) = await CommerceSqlServerHarness.SeedPortionGroupAndProductAsync(_db, tenantId);

        // Bypass the service (whose V7 checks exist precisely to avoid this) and
        // write the second active default straight through the context — the
        // database must be the last line of defence.
        await using var context = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        context.OptionChoices.Add(new OptionChoice
        {
            TenantId = tenantId,
            OptionGroupId = groupId,
            Key = "rogue",
            Label = "Rogue second default",
            IsRecommendedDefault = true,
            IsActive = true,
        });

        var act = () => context.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        var sqlException = thrown.Which.InnerException.Should().BeOfType<SqlException>().Subject;
        sqlException.Number.Should().BeOneOf([2601, 2627]);
        sqlException.Message.Should().Contain(IndexName);
    }

    [SkippableFact]
    public async Task Insert_Should_Succeed_When_SecondDefaultIsInactiveOrPredecessorIsSoftDeleted()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var (groupId, _) = await CommerceSqlServerHarness.SeedPortionGroupAndProductAsync(_db, tenantId);

        // An INACTIVE second default sits outside the filter — no collision.
        await using (var context = CommerceSqlServerHarness.CreateContext(_db, tenantId))
        {
            context.OptionChoices.Add(new OptionChoice
            {
                TenantId = tenantId,
                OptionGroupId = groupId,
                Key = "backup",
                Label = "Inactive backup default",
                IsRecommendedDefault = true,
                IsActive = false,
            });
            await context.SaveChangesAsync();
        }

        // Soft-deleting the live default takes it out of the filter too, making
        // room for a successor even though the row physically remains.
        await using (var context = CommerceSqlServerHarness.CreateContext(_db, tenantId))
        {
            var light = await context.OptionChoices.SingleAsync(c => c.OptionGroupId == groupId && c.Key == "light");
            context.OptionChoices.Remove(light); // converted to a soft delete by AonikDbContextBase
            await context.SaveChangesAsync();
        }

        await using (var context = CommerceSqlServerHarness.CreateContext(_db, tenantId))
        {
            context.OptionChoices.Add(new OptionChoice
            {
                TenantId = tenantId,
                OptionGroupId = groupId,
                Key = "successor",
                Label = "Successor default",
                IsRecommendedDefault = true,
                IsActive = true,
            });
            var act = () => context.SaveChangesAsync();
            await act.Should().NotThrowAsync("the soft-deleted predecessor is outside the index filter");
        }
    }

    [SkippableFact]
    public async Task Index_Should_ExistWithTheSpec066FilterShape()
    {
        RequireSqlServer();

        await using var connection = new SqlConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.is_unique, i.has_filter, i.filter_definition
            FROM sys.indexes i
            JOIN sys.tables t ON t.object_id = i.object_id
            WHERE t.name = 'AnkOptionChoices' AND i.name = @indexName
            """;
        command.Parameters.AddWithValue("@indexName", IndexName);

        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue($"{IndexName} must exist on AnkOptionChoices");
        reader.GetBoolean(0).Should().BeTrue("the index must be unique");
        reader.GetBoolean(1).Should().BeTrue("the index must be filtered");
        var filter = reader.GetString(2);
        filter.Should().Contain("[IsRecommendedDefault]=(1)");
        filter.Should().Contain("[IsActive]=(1)");
        filter.Should().Contain("[IsDeleted]=(0)");
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
