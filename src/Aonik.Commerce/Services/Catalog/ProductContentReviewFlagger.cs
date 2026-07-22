using Aonik.Commerce.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>
/// The §6 default-change reaction: when a product's effective default combination changes, its
/// default content block becomes suspect. STAGES only — the caller's SaveChanges commits, so the
/// flag-set and the caller's own write land in ONE transaction, and the ContentVersion bump
/// invalidates cached content in the same instant the flag starts changing what resolution
/// returns (A18). A lean, DbContext-only dependency so the Spec 066 writers can call it without
/// a service cycle (content → selection → options ← this).
/// </summary>
internal interface IProductContentReviewFlagger
{
    /// <summary>Marks the products' default blocks RequiresReview and bumps their ContentVersion.
    /// No SaveChanges — the caller's transaction owns the commit. Products without a content
    /// block are untouched; already-flagged blocks still bump (the effective default moved AGAIN,
    /// and caches keyed on the version must move with it).</summary>
    Task StageAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default);
}

internal sealed class ProductContentReviewFlagger : IProductContentReviewFlagger
{
    private readonly CommerceDbContext _dbContext;

    public ProductContentReviewFlagger(CommerceDbContext dbContext) => _dbContext = dbContext;

    public async Task StageAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return;
        }

        var blocks = await _dbContext.ProductContents
            .Where(c => productIds.Contains(c.ProductId))
            .ToListAsync(cancellationToken);

        foreach (var block in blocks)
        {
            block.RequiresReview = true;
            block.ContentVersion++;
        }
    }
}
