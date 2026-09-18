using Aonik.IntegrationTests.Support;
using Aonik.Platform.Entities.Modules;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Database.Tests;

/// <summary>
/// Spec 097 §6 / §16 on a real engine. The InMemory provider enforces neither unique indexes nor
/// rowversion tokens, so the two invariants the <c>AnkTenantModules</c> table exists to hold — one row
/// per (tenant, module), and a concurrent toggle detected rather than silently overwritten — can only
/// fail here.
/// </summary>
public class TenantModuleSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;

    public TenantModuleSqlServerTests(SqlLocalDbFixture db)
    {
        _db = db;
    }

    private PlatformDbContext CreateContext(Guid tenantId)
        => new(
            _db.CreateOptions<PlatformDbContext>(),
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(Guid.NewGuid()));

    private static TenantModule NewRow(Guid tenantId, string moduleId, bool isEnabled)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleId = moduleId,
            IsEnabled = isEnabled,
            Source = TenantModuleSource.Explicit,
        };

    [SkippableFact]
    public async Task UniqueIndex_Should_RejectASecondRowForTheSameTenantAndModule()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();

        await using (var first = CreateContext(tenantId))
        {
            first.TenantModules.Add(NewRow(tenantId, ModuleIds.Commerce, isEnabled: false));
            await first.SaveChangesAsync();
        }

        await using var second = CreateContext(tenantId);
        second.TenantModules.Add(NewRow(tenantId, ModuleIds.Commerce, isEnabled: true));
        var act = async () => await second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            "IX_AnkTenantModules_TenantId_ModuleId is unique; the service upserts and the engine makes a duplicate impossible");
    }

    [SkippableFact]
    public async Task UniqueIndex_Should_AllowTheSameModuleForDifferentTenants()
    {
        RequireSqlServer();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var a = CreateContext(tenantA))
        {
            a.TenantModules.Add(NewRow(tenantA, ModuleIds.Commerce, isEnabled: false));
            await a.SaveChangesAsync();
        }

        await using var b = CreateContext(tenantB);
        b.TenantModules.Add(NewRow(tenantB, ModuleIds.Commerce, isEnabled: false));
        var act = async () => await b.SaveChangesAsync();

        await act.Should().NotThrowAsync("the index is per tenant, not per module");
    }

    [SkippableFact]
    public async Task RowVersion_Should_BePopulatedByTheEngine_AfterSaveChanges()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var row = NewRow(tenantId, ModuleIds.Commerce, isEnabled: false);

        await using var context = CreateContext(tenantId);
        context.TenantModules.Add(row);
        await context.SaveChangesAsync();

        row.RowVersion.Should().HaveCount(8, "SQL Server rowversion is an 8-byte engine-generated token");
        row.RowVersion.Should().Contain(b => b != 0, "a varbinary(max) mis-mapping would leave it empty or zero");
    }

    [SkippableFact]
    public async Task ConcurrentToggle_Should_ThrowDbUpdateConcurrencyException()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var rowId = Guid.NewGuid();

        await using (var seed = CreateContext(tenantId))
        {
            var row = NewRow(tenantId, ModuleIds.Commerce, isEnabled: true);
            row.Id = rowId;
            seed.TenantModules.Add(row);
            await seed.SaveChangesAsync();
        }

        // Two admins load the same row, each with the same original RowVersion.
        await using var admin1 = CreateContext(tenantId);
        await using var admin2 = CreateContext(tenantId);
        var row1 = await admin1.TenantModules.SingleAsync(x => x.Id == rowId);
        var row2 = await admin2.TenantModules.SingleAsync(x => x.Id == rowId);

        row1.IsEnabled = false;
        row1.Reason = "first toggle";
        await admin1.SaveChangesAsync();

        row2.IsEnabled = false;
        row2.Reason = "second toggle, stale token";
        var act = async () => await admin2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "the engine bumped RowVersion on the first UPDATE, so the second UPDATE's WHERE clause matches nothing");
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
