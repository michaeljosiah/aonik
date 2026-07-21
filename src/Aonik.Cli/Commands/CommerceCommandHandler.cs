using System.Text.Json;

using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Commands;

/// <summary>
/// Storefront option commands (Spec 066). These call the <c>AllowAnonymous</c> catalog endpoints
/// with an explicit <c>--tenant-id</c>, so they need no session and are unaffected by the CLI's
/// login limitations — point them at any running API and they work.
/// </summary>
public sealed class CommerceCommandHandler
{
    private readonly IAonikCliApiClient _apiClient;
    private readonly ICliOutputWriter _outputWriter;

    public CommerceCommandHandler(IAonikCliApiClient apiClient, ICliOutputWriter outputWriter)
    {
        _apiClient = apiClient;
        _outputWriter = outputWriter;
    }

    public async Task<int> ListOptionsAsync(
        StorefrontTarget target, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var catalogue = await _apiClient.GetOptionCatalogueAsync(target, cancellationToken);
        await _outputWriter.WriteCollectionAsync(catalogue, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ShowProductAsync(
        StorefrontTarget target, string slug, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var product = await _apiClient.GetStorefrontProductAsync(target, slug, cancellationToken);
        await _outputWriter.WriteObjectAsync(product, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> QuoteAsync(
        StorefrontTarget target,
        string slug,
        IReadOnlyList<string> selections,
        string? currency,
        OutputMode outputMode,
        CancellationToken cancellationToken = default)
    {
        var selection = ParseSelections(selections);
        var quote = await _apiClient.GetSelectionQuoteAsync(target, slug, selection, currency, cancellationToken);
        await _outputWriter.WriteObjectAsync(quote, outputMode, cancellationToken);
        return 0;
    }

    /// <summary>
    /// Runs the Spec 066 acceptance behaviours against a live API and reports pass/fail per check.
    /// Exit code is non-zero if any check fails, so this is usable as a deployment gate.
    /// </summary>
    public async Task<int> VerifyAsync(
        StorefrontTarget target,
        string slug,
        string? currency,
        OutputMode outputMode,
        CancellationToken cancellationToken = default)
    {
        var checks = new List<CliVerificationCheck>();

        // 1 — the anonymous catalogue is reachable and tenant-resolved.
        IReadOnlyList<CliOptionGroup> catalogue;
        try
        {
            catalogue = await _apiClient.GetOptionCatalogueAsync(target, cancellationToken);
            checks.Add(CliVerificationCheck.Pass("catalogue-reachable", $"{catalogue.Count} servable group(s)."));
        }
        catch (AonikCliException ex)
        {
            checks.Add(CliVerificationCheck.Fail("catalogue-reachable", ex.Message));
            return await ReportAsync(checks, outputMode, cancellationToken);
        }

        // 2 — the product resolves and reports what it offers.
        CliStorefrontProduct product;
        try
        {
            product = await _apiClient.GetStorefrontProductAsync(target, slug, cancellationToken);
            checks.Add(CliVerificationCheck.Pass(
                "product-resolves",
                $"'{product.Slug}' is {product.Status} with {product.EffectiveOptionGroups.Count} option group(s)."));
        }
        catch (AonikCliException ex)
        {
            checks.Add(CliVerificationCheck.Fail("product-resolves", ex.Message));
            return await ReportAsync(checks, outputMode, cancellationToken);
        }

        var groups = product.EffectiveOptionGroups;
        if (groups.Count == 0)
        {
            // Not a failure — an empty list is the documented "not personalisable" signal. But the
            // remaining checks have nothing to exercise, so say so rather than imply coverage.
            checks.Add(CliVerificationCheck.Skip(
                "personalisation-checks",
                "Product offers no option groups (a valid state); nothing further to verify."));
            return await ReportAsync(checks, outputMode, cancellationToken);
        }

        // 3 — an omitted selection fills defaults, prices to zero, and stays complete.
        try
        {
            var defaults = await _apiClient.GetSelectionQuoteAsync(
                target, slug, new Dictionary<string, object>(), currency, cancellationToken);

            checks.Add(defaults is { IsDefault: true, Adjustment: 0m }
                ? CliVerificationCheck.Pass("defaults-price-zero", $"canonical: {defaults.CanonicalSelectionJson}")
                : CliVerificationCheck.Fail(
                    "defaults-price-zero",
                    $"expected isDefault=true and adjustment=0, got {defaults.IsDefault} / {defaults.Adjustment}."));

            var missing = groups
                .Select(g => g.Key)
                .Where(key => !defaults.CanonicalSelectionJson.Contains($"\"{key}\":", StringComparison.Ordinal))
                .ToList();

            checks.Add(missing.Count == 0
                ? CliVerificationCheck.Pass("canonical-complete", "every offered group is present in the stored form.")
                : CliVerificationCheck.Fail("canonical-complete", $"missing group(s): {string.Join(", ", missing)}."));
        }
        catch (AonikCliException ex)
        {
            checks.Add(CliVerificationCheck.Fail("defaults-price-zero", ex.Message));
        }

        // 4 — byte-stability: the same selection with the group keys submitted in the opposite
        // order must canonicalise identically, or Spec 068's cart line-merge key is unsound.
        if (groups.Count >= 2)
        {
            try
            {
                var forward = new Dictionary<string, object>();
                foreach (var group in groups)
                {
                    forward[group.Key] = group.DefaultChoiceKey;
                }

                var reversed = new Dictionary<string, object>();
                foreach (var group in groups.Reverse())
                {
                    reversed[group.Key] = group.DefaultChoiceKey;
                }

                var a = await _apiClient.GetSelectionQuoteAsync(target, slug, forward, currency, cancellationToken);
                var b = await _apiClient.GetSelectionQuoteAsync(target, slug, reversed, currency, cancellationToken);

                checks.Add(a.CanonicalSelectionJson == b.CanonicalSelectionJson
                    ? CliVerificationCheck.Pass("canonical-stable", "group order does not affect the canonical form.")
                    : CliVerificationCheck.Fail(
                        "canonical-stable",
                        $"'{a.CanonicalSelectionJson}' != '{b.CanonicalSelectionJson}'."));
            }
            catch (AonikCliException ex)
            {
                checks.Add(CliVerificationCheck.Fail("canonical-stable", ex.Message));
            }
        }
        else
        {
            checks.Add(CliVerificationCheck.Skip("canonical-stable", "needs at least two option groups."));
        }

        // 5 — difference pricing: a non-default choice must price at (chosen − default), including
        // when that is negative.
        var priced = groups
            .Select(g => new
            {
                Group = g,
                Default = g.Choices.FirstOrDefault(c => c.Key == g.DefaultChoiceKey),
                Alternative = g.Choices.FirstOrDefault(c => c.Key != g.DefaultChoiceKey),
            })
            .FirstOrDefault(x => x.Default is not null && x.Alternative is not null);

        if (priced is null)
        {
            checks.Add(CliVerificationCheck.Skip("difference-pricing", "no group offers an alternative choice."));
        }
        else
        {
            try
            {
                var expected = priced.Alternative!.Price - priced.Default!.Price;
                var quote = await _apiClient.GetSelectionQuoteAsync(
                    target, slug,
                    new Dictionary<string, object> { [priced.Group.Key] = priced.Alternative.Key },
                    currency, cancellationToken);

                checks.Add(quote.Adjustment == expected
                    ? CliVerificationCheck.Pass(
                        "difference-pricing",
                        $"{priced.Group.Key}={priced.Alternative.Key} priced {quote.Adjustment:0.####} " +
                        $"({priced.Alternative.Price:0.####} − {priced.Default.Price:0.####}).")
                    : CliVerificationCheck.Fail(
                        "difference-pricing",
                        $"expected {expected:0.####}, got {quote.Adjustment:0.####}."));
            }
            catch (AonikCliException ex)
            {
                checks.Add(CliVerificationCheck.Fail("difference-pricing", ex.Message));
            }
        }

        // 6 — a choice this product does not offer must be rejected, even if it exists elsewhere
        // in the tenant catalogue.
        checks.Add(await ExpectRejectionAsync(
            "unknown-choice-rejected",
            target, slug,
            new Dictionary<string, object> { [groups[0].Key] = "__not-a-real-choice__" },
            currency, cancellationToken));

        // 7 — a group this product does not offer must be rejected.
        checks.Add(await ExpectRejectionAsync(
            "unoffered-group-rejected",
            target, slug,
            new Dictionary<string, object> { ["__not-a-real-group__"] = "x" },
            currency, cancellationToken));

        return await ReportAsync(checks, outputMode, cancellationToken);
    }

    private async Task<CliVerificationCheck> ExpectRejectionAsync(
        string checkName,
        StorefrontTarget target,
        string slug,
        IReadOnlyDictionary<string, object> selection,
        string? currency,
        CancellationToken cancellationToken)
    {
        try
        {
            var quote = await _apiClient.GetSelectionQuoteAsync(target, slug, selection, currency, cancellationToken);
            return CliVerificationCheck.Fail(
                checkName,
                $"expected the API to reject this selection, but it returned '{quote.CanonicalSelectionJson}'.");
        }
        catch (AonikCliException)
        {
            return CliVerificationCheck.Pass(checkName, "rejected, as required.");
        }
    }

    private async Task<int> ReportAsync(
        IReadOnlyList<CliVerificationCheck> checks, OutputMode outputMode, CancellationToken cancellationToken)
    {
        await _outputWriter.WriteCollectionAsync(checks, outputMode, cancellationToken);

        var failed = checks.Count(c => c.Failed);
        var skipped = checks.Count(c => !c.Failed && !c.Passed);

        await _outputWriter.WriteInfoAsync(
            failed == 0
                ? $"All {checks.Count(c => c.Passed)} check(s) passed{(skipped > 0 ? $", {skipped} skipped" : string.Empty)}."
                : $"{failed} of {checks.Count} check(s) FAILED.",
            cancellationToken);

        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// Parses <c>--select group=choice</c> pairs. A comma-separated value becomes an array, which
    /// is how a multi-select group is expressed: <c>--select protein=salmon,prawns</c>.
    /// </summary>
    internal static Dictionary<string, object> ParseSelections(IReadOnlyList<string> selections)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var entry in selections)
        {
            var separator = entry.IndexOf('=');
            if (separator <= 0 || separator == entry.Length - 1)
            {
                throw new AonikCliException($"Invalid --select '{entry}'; expected group=choice (or group=a,b for multi-select).");
            }

            var key = entry[..separator].Trim();
            var raw = entry[(separator + 1)..].Trim();

            var values = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            result[key] = values.Length > 1 ? values : values.FirstOrDefault() ?? string.Empty;
        }

        return result;
    }

    /// <summary>Pretty-prints a canonical selection for humans without losing its exact form.</summary>
    internal static string DescribeCanonical(string canonicalJson)
    {
        using var document = JsonDocument.Parse(canonicalJson);
        return string.Join(", ", document.RootElement.EnumerateObject().Select(p =>
            $"{p.Name}={(p.Value.ValueKind == JsonValueKind.Array
                ? string.Join("+", p.Value.EnumerateArray().Select(v => v.GetString()))
                : p.Value.GetString())}"));
    }
}
