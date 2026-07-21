using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>
/// Validates, canonicalises and prices customer option selections (Spec 066 §7–§9).
/// </summary>
/// <remarks>
/// Two validation contexts, deliberately different:
/// <list type="bullet">
/// <item><b>Interactive input</b> — a storefront submitting a customer's choice — is strict:
/// anything malformed, not offered, or mis-denominated is rejected.</item>
/// <item><b>Stored selections</b> — a cart line written days ago — get drift semantics: groups that
/// are no longer offered drop, retired choices remap, and every change is reported rather than
/// thrown. Without this split, retiring one option would turn every cart holding it into a hard
/// 400.</item>
/// </list>
/// </remarks>
public interface IOptionSelectionService
{
    /// <summary>
    /// Validate against the product's effective options, fill omitted groups with their defaults,
    /// canonicalise, and price in <paramref name="currency"/>.
    /// </summary>
    /// <exception cref="OptionValidationException">Rules V1–V5, V10.</exception>
    Task<OptionSelectionResult> NormalizeAndPriceAsync(
        Guid productId,
        JsonElement? selection,
        string currency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate and canonicalise without pricing — same rules V1–V5, no currency involved.
    /// Content resolution (Spec 067) needs a canonical selection but has nothing to do with money,
    /// and must not fail on a pricing rule.
    /// </summary>
    /// <exception cref="OptionValidationException">Rules V1–V5.</exception>
    Task<OptionSelectionResult> NormalizeAsync(
        Guid productId,
        JsonElement? selection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-normalise a previously stored canonical selection against the current catalogue with
    /// drift semantics (Spec 066 §7). Only malformed JSON is an error. Consumed by Spec 068 on
    /// cart load to produce its customer-visible change report.
    /// </summary>
    Task<StoredSelectionResult> RenormalizeStoredAsync(
        Guid productId,
        string canonicalSelectionJson,
        string currency,
        CancellationToken cancellationToken = default);
}
