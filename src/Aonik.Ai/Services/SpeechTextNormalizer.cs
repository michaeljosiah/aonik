using System.Text.RegularExpressions;

namespace Aonik.Ai.Services;

/// <summary>
/// Normalises raw text (often markdown-flavoured) into a form that reads
/// naturally when spoken by a TTS provider.
///
/// The pipeline:
///   1. Strip markdown formatting (bold, italic, bullet markers)
///   2. Remove UUIDs and parenthetical references containing them
///   3. Normalise currencies so symbols+codes don't double up
///   4. Collapse whitespace
/// </summary>
internal static partial class SpeechTextNormalizer
{
    // ── Currency maps ───────────────────────────────────────────────────

    /// <summary>
    /// ISO 4217 codes → spoken name (singular, plural).
    /// Covers the currencies AONIK users are most likely to encounter.
    /// </summary>
    private static readonly Dictionary<string, (string Singular, string Plural)> CurrencyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GBP"] = ("pound", "pounds"),
        ["USD"] = ("dollar", "dollars"),
        ["EUR"] = ("euro", "euros"),
        ["NGN"] = ("naira", "naira"),
        ["XOF"] = ("CFA franc", "CFA francs"),
        ["XAF"] = ("CFA franc", "CFA francs"),
        ["KES"] = ("Kenyan shilling", "Kenyan shillings"),
        ["ZAR"] = ("rand", "rand"),
        ["GHS"] = ("cedi", "cedis"),
        ["TZS"] = ("Tanzanian shilling", "Tanzanian shillings"),
        ["UGX"] = ("Ugandan shilling", "Ugandan shillings"),
        ["RWF"] = ("Rwandan franc", "Rwandan francs"),
        ["MAD"] = ("dirham", "dirhams"),
        ["EGP"] = ("Egyptian pound", "Egyptian pounds"),
        ["INR"] = ("rupee", "rupees"),
        ["JPY"] = ("yen", "yen"),
        ["CNY"] = ("yuan", "yuan"),
        ["CAD"] = ("Canadian dollar", "Canadian dollars"),
        ["AUD"] = ("Australian dollar", "Australian dollars"),
        ["CHF"] = ("Swiss franc", "Swiss francs"),
        ["BRL"] = ("real", "reais"),
        ["MXN"] = ("Mexican peso", "Mexican pesos"),
        ["AED"] = ("dirham", "dirhams"),
        ["SAR"] = ("riyal", "riyals"),
    };

    /// <summary>
    /// Currency symbols → the spoken word the TTS provider will naturally
    /// produce.  Used to detect and de-duplicate when the source text
    /// contains both a symbol AND an ISO code (e.g. "£380 GBP").
    /// </summary>
    private static readonly Dictionary<string, string> SymbolToSpokenWord = new()
    {
        ["£"] = "pounds",
        ["$"] = "dollars",
        ["€"] = "euros",
        ["¥"] = "yen",
        ["₦"] = "naira",
        ["₹"] = "rupees",
        ["₵"] = "cedis",
        ["R"] = "rand", // ZAR — only used in the symbol+code pattern
    };

    // ── Compiled regex patterns ─────────────────────────────────────────

    // Pattern 1: symbol + amount + code  (e.g. "£380 GBP", "€1,890 EUR")
    // Captures: symbol, amount (with commas/decimals), code
    [GeneratedRegex(@"([£$€¥₦₹₵])\s*([\d,]+(?:\.\d+)?)\s+([A-Z]{3})\b", RegexOptions.Compiled)]
    private static partial Regex SymbolAmountCodePattern();

    // Pattern 2: symbol + amount without code  (e.g. "£280", "€150")
    // Only matches when NOT followed by a currency code (to avoid double-matching pattern 1)
    [GeneratedRegex(@"([£$€¥₦₹₵])\s*([\d,]+(?:\.\d+)?)(?!\s*[A-Z]{3}\b)", RegexOptions.Compiled)]
    private static partial Regex SymbolAmountOnlyPattern();

    // Pattern 3: amount + code without symbol  (e.g. "45,000 XOF", "75,000 XOF")
    // Only matches when NOT preceded by a currency symbol
    [GeneratedRegex(@"(?<![£$€¥₦₹₵]\s?)([\d,]+(?:\.\d+)?)\s+([A-Z]{3})\b", RegexOptions.Compiled)]
    private static partial Regex AmountCodeOnlyPattern();

    // Pattern 4: ~amount + code (approximate, e.g. "~£450 GBP" or "~450 GBP")
    [GeneratedRegex(@"~\s*([£$€¥₦₹₵])?\s*([\d,]+(?:\.\d+)?)\s+([A-Z]{3})\b", RegexOptions.Compiled)]
    private static partial Regex ApproxAmountPattern();

    // UUIDs (8-4-4-4-12 hex), with optional prefix character (e.g. "b4444...")
    [GeneratedRegex(@"\b[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}\b", RegexOptions.Compiled)]
    private static partial Regex UuidPattern();

    // Parenthetical references containing a UUID  (e.g. "(bill b4444444-...)")
    [GeneratedRegex(@"\s*\([^)]*[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}[^)]*\)", RegexOptions.Compiled)]
    private static partial Regex ParenthesizedUuidPattern();

    // Markdown bold/italic markers
    [GeneratedRegex(@"(\*{1,3}|_{1,3})", RegexOptions.Compiled)]
    private static partial Regex MarkdownEmphasisPattern();

    // Markdown bullet-point list markers at the start of a line
    [GeneratedRegex(@"(?m)^\s*[-*•]\s+", RegexOptions.Compiled)]
    private static partial Regex MarkdownBulletPattern();

    // Collapsed whitespace (2+ spaces, or space around newlines)
    [GeneratedRegex(@"[ \t]{2,}", RegexOptions.Compiled)]
    private static partial Regex ExcessWhitespacePattern();

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>
    /// Normalise <paramref name="text"/> for natural TTS playback.
    /// Safe to call on any string — returns the input unchanged if null/empty.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var result = text;

        // 1. Strip markdown formatting
        result = MarkdownEmphasisPattern().Replace(result, "");
        result = MarkdownBulletPattern().Replace(result, "");

        // 2. Remove parenthetical UUID references  (e.g. "(bill b4444...)")
        //    then strip any remaining bare UUIDs
        result = ParenthesizedUuidPattern().Replace(result, "");
        result = UuidPattern().Replace(result, "");

        // 3. Approximate amounts: "~£450 GBP" → "approximately 450 pounds"
        result = ApproxAmountPattern().Replace(result, match =>
        {
            var amount = match.Groups[2].Value;
            var code = match.Groups[3].Value;
            var spokenCurrency = ResolveCurrencyName(code, amount);
            return $"approximately {amount} {spokenCurrency}";
        });

        // 4. Symbol + amount + code  →  amount + spoken name
        //    e.g. "£380 GBP" → "380 pounds" (not "380 pounds pounds")
        result = SymbolAmountCodePattern().Replace(result, match =>
        {
            var amount = match.Groups[2].Value;
            var code = match.Groups[3].Value;
            var spokenCurrency = ResolveCurrencyName(code, amount);
            return $"{amount} {spokenCurrency}";
        });

        // 5. Symbol + amount (no code)  →  leave as-is (providers handle £280 fine)
        //    But we could normalise if needed in future.

        // 6. Amount + code (no symbol)  →  amount + spoken name
        //    e.g. "45,000 XOF" → "45,000 CFA francs"
        result = AmountCodeOnlyPattern().Replace(result, match =>
        {
            var amount = match.Groups[1].Value;
            var code = match.Groups[2].Value;

            // Only replace if we recognise the currency code — otherwise leave it
            if (!CurrencyNames.ContainsKey(code))
                return match.Value;

            var spokenCurrency = ResolveCurrencyName(code, amount);
            return $"{amount} {spokenCurrency}";
        });

        // 7. Clean up whitespace
        result = ExcessWhitespacePattern().Replace(result, " ");
        result = result.Trim();

        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string ResolveCurrencyName(string code, string amount)
    {
        if (!CurrencyNames.TryGetValue(code, out var names))
            return code; // Unknown code — return as-is

        // Determine singular vs plural.  "1" or "1.00" → singular; everything else → plural.
        var numericPart = amount.Replace(",", "");
        return double.TryParse(numericPart, out var value) && value == 1.0
            ? names.Singular
            : names.Plural;
    }
}
