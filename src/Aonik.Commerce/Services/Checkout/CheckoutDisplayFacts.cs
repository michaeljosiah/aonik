using System.Text.Json;

using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions.Settings;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

internal static class CheckoutDisplayFacts
{
    public static async Task<string?> ReadSignatureTagAsync(ITenantSettingStore settings, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tag = (await settings.GetTenantValueAsync(CommerceSettingNames.StorefrontSignatureTag, tenantId, cancellationToken))?.Trim();
        return tag is { Length: > 0 and <= 64 } && !tag.Any(char.IsControl) ? tag : null;
    }

    public static async Task<IReadOnlyDictionary<Guid, bool?>> ReadSignaturesAsync(CommerceDbContext db,
        ITenantSettingStore settings, Guid tenantId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default)
    {
        var tag = await ReadSignatureTagAsync(settings, tenantId, cancellationToken);
        if (tag is null || variantIds.Count == 0) return new Dictionary<Guid, bool?>();
        var products = await db.ProductVariants.AsNoTracking()
            .Where(v => v.TenantId == tenantId && variantIds.Contains(v.Id))
            .Join(db.Products.AsNoTracking().Where(p => p.TenantId == tenantId), v => v.ProductId, p => p.Id,
                (v, p) => new { VariantId = v.Id, p.TagsJson }).ToListAsync(cancellationToken);
        return products.ToDictionary(p => p.VariantId, p => IsSignature(p.TagsJson, tag));
    }

    private static bool? IsSignature(string json, string tag)
    {
        try
        {
            var tags = JsonSerializer.Deserialize<string[]>(json);
            return tags is null ? null : tags.Any(value => string.Equals(value?.Trim(), tag, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException) { return null; }
    }
}
