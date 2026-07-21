using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Builds the Spec 066 launch fixture — the option catalogue and a personalisable product — so the
/// selection tests read as behaviour rather than setup.
/// </summary>
/// <remarks>
/// Deliberately mirrors the client's Step 2 launch table, including the detail that makes the
/// pricing interesting: the default side ("Wild rice") already costs 2.00, so choosing "No side"
/// produces a legitimately <em>negative</em> adjustment.
/// </remarks>
internal sealed class OptionCatalogueBuilder
{
    private readonly CommerceDbContext _ctx;
    private readonly Guid _tenantId;
    private readonly ProductOptionService _options;

    public OptionCatalogueBuilder(CommerceDbContext ctx, Guid tenantId)
    {
        _ctx = ctx;
        _tenantId = tenantId;
        _options = CommerceTestHarness.NewOptionService(ctx, tenantId);
    }

    public async Task<Guid> BuildProductAsync(string slug = "jollof")
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Slug = slug,
            Name = "Jollof Rice",
            Kind = ProductKinds.Simple,
            Status = ProductStatuses.Active,
        };
        _ctx.Products.Add(product);
        await _ctx.SaveChangesAsync();
        return product.Id;
    }

    /// <summary>portion (light* / full +10), protein (chicken* / salmon +3 / prawns 0),
    /// side (wildrice* 2 / none 0), heat (medium* / high), all in GBP. * = recommended default.</summary>
    public async Task BuildCatalogueAsync()
    {
        await GroupAsync("portion", "Portion", OptionSelectionModes.One, 1,
            ("light", "Light table", 0m, true),
            ("full", "Full table", 10m, false));

        await GroupAsync("protein", "Protein", OptionSelectionModes.One, 2,
            ("chicken", "Chicken", 0m, true),
            ("salmon", "Salmon", 3m, false),
            ("prawns", "King prawns", 0m, false));

        await GroupAsync("side", "Side", OptionSelectionModes.One, 3,
            ("wildrice", "Wild rice", 2m, true),
            ("none", "No side", 0m, false));

        await GroupAsync("heat", "Heat", OptionSelectionModes.One, 4,
            ("medium", "Medium", 0m, true),
            ("high", "High", 0m, false));
    }

    public async Task<OptionGroupDto> GroupAsync(
        string key, string label, string mode, int sortOrder,
        params (string Key, string Label, decimal Price, bool IsDefault)[] choices)
    {
        var group = await _options.CreateGroupAsync(new CreateOptionGroupCommand(key, label, SelectionMode: mode, SortOrder: sortOrder));

        var order = 0;
        foreach (var (choiceKey, choiceLabel, price, isDefault) in choices)
        {
            await _options.AddChoiceAsync(group.Id, new AddOptionChoiceCommand(
                choiceKey, choiceLabel, Price: price, IsRecommendedDefault: isDefault, SortOrder: order++));
        }

        return group;
    }

    /// <summary>Offers every catalogue group to the product, unnarrowed.</summary>
    public async Task OfferAllAsync(Guid productId)
    {
        var catalogue = await _options.GetCatalogueAsync(includeInactive: true);
        await _options.SetProductOptionGroupsAsync(productId, new SetProductOptionGroupsCommand(
            catalogue.Select((g, i) => new ProductOptionGroupLine(g.Key, SortOrder: i)).ToList()));
    }

    public Task OfferAsync(Guid productId, params ProductOptionGroupLine[] lines)
        => _options.SetProductOptionGroupsAsync(productId, new SetProductOptionGroupsCommand(lines));

    public async Task<Guid> GroupIdAsync(string key)
        => (await _options.GetCatalogueAsync(includeInactive: true)).First(g => g.Key == key).Id;

    public async Task<Guid> ChoiceIdAsync(string groupKey, string choiceKey)
        => (await _options.GetCatalogueAsync(includeInactive: true))
            .First(g => g.Key == groupKey).Choices.First(c => c.Key == choiceKey).Id;
}
