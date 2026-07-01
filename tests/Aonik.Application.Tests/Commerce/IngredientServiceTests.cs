using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Sourcing;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>Ingredient (raw-material) master lifecycle (Spec 050 §8/R1): create, list, update, deactivate.</summary>
public class IngredientServiceTests
{
    private static (IngredientService Service, CommerceDbContext Ctx) Build(
        DbContextOptions<CommerceDbContext> options, Guid tenantId)
    {
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var svc = new IngredientService(ctx, new TestTenantProvider(tenantId));
        return (svc, ctx);
    }

    [Fact]
    public async Task Create_Should_PersistIngredient_AndListReturnsIt()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        await svc.CreateAsync(new CreateIngredientCommand("Tomato", "kg", Sku: "ING-TOM", Category: "Produce"));
        var rice = await svc.CreateAsync(new CreateIngredientCommand("Basmati rice", "kg"));

        rice.Name.Should().Be("Basmati rice");
        rice.BaseUnit.Should().Be("kg");
        rice.IsActive.Should().BeTrue();

        var list = await svc.ListAsync();
        list.Should().HaveCount(2);
        // Ordered by name.
        list[0].Name.Should().Be("Basmati rice");
        list[1].Name.Should().Be("Tomato");
        list[1].Sku.Should().Be("ING-TOM");
        list[1].Category.Should().Be("Produce");
    }

    [Fact]
    public async Task Create_Should_Throw_WhenDuplicateNameInTenant()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        await svc.CreateAsync(new CreateIngredientCommand("Tomato", "kg"));

        var act = async () => await svc.CreateAsync(new CreateIngredientCommand("Tomato", "g"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Tomato*already exists*");
    }

    [Fact]
    public async Task Create_Should_Throw_WhenBaseUnitMissing()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var act = async () => await svc.CreateAsync(new CreateIngredientCommand("Tomato", "  "));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Update_Should_OverwriteMasterData()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var created = await svc.CreateAsync(new CreateIngredientCommand("Tomato", "kg"));

        var updated = await svc.UpdateAsync(new UpdateIngredientCommand(
            created.Id, "Plum tomato", "g", Sku: "ING-PT", Category: "Produce", Notes: "prefer ripe"));

        updated.Id.Should().Be(created.Id);
        updated.Name.Should().Be("Plum tomato");
        updated.BaseUnit.Should().Be("g");
        updated.Sku.Should().Be("ING-PT");
        updated.Notes.Should().Be("prefer ripe");
        updated.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_Should_HideFromDefaultList_ButKeepInInactiveList()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var (svc, ctx) = Build(options, tenantId);
        await using var _ctx = ctx;

        var tomato = await svc.CreateAsync(new CreateIngredientCommand("Tomato", "kg"));
        await svc.CreateAsync(new CreateIngredientCommand("Rice", "kg"));

        await svc.DeactivateAsync(tomato.Id);

        var active = await svc.ListAsync();
        active.Should().ContainSingle(i => i.Name == "Rice");

        var all = await svc.ListAsync(includeInactive: true);
        all.Should().HaveCount(2);
        all.Single(i => i.Name == "Tomato").IsActive.Should().BeFalse();
    }
}
