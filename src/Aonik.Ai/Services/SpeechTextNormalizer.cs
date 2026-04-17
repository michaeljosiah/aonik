using System.Text.RegularExpressions;

namespace Aonik.Ai.Services;

/// <summary>
/// Normalises raw text (often markdown-flavoured) into a form that reads
/// naturally when spoken by a TTS provider.
///
/// The pipeline:
///   1. Strip markdown formatting (bold, italic, bullet markers)
///   2. Remove UUIDs and parenthetical references containing them
///   3. Replace directional/math/trademark symbols with spoken words
///   4. Strip emojis and common unpronounceable pictographs
///   5. Expand percentages ("45%" → "45 percent")
///   6. Normalise currencies so symbols+codes don't double up
///   7. Spell out remaining ALL-CAPS acronyms (e.g. "FBI" → "F-B-I")
///   8. Collapse whitespace
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

    // Amount pattern building block. Accepts plain digits ("45000"),
    // comma-grouped digits ("1,890" / "1,000,000"), optional decimal
    // ("200.50"), and optional leading sign ("-500"). Crucially it does NOT
    // match trailing commas, so "100," in text like "GBP 100, USD 200" is
    // parsed as amount "100" with a trailing comma outside the match.
    //
    // Duplicated in each regex below because [GeneratedRegex] attributes
    // require compile-time literal patterns.

    // Pattern 1: symbol + amount + code  (e.g. "£380 GBP", "€1,890 EUR")
    [GeneratedRegex(@"([£$€¥₦₹₵])\s*(\d+(?:,\d+)*(?:\.\d+)?)\s+([A-Z]{3})\b", RegexOptions.Compiled)]
    private static partial Regex SymbolAmountCodePattern();

    // Pattern 2: symbol + amount without code  (e.g. "£280", "€150")
    [GeneratedRegex(@"([£$€¥₦₹₵])\s*(\d+(?:,\d+)*(?:\.\d+)?)(?!\s*[A-Z]{3}\b)", RegexOptions.Compiled)]
    private static partial Regex SymbolAmountOnlyPattern();

    // Pattern 3: amount + code without symbol  (e.g. "45,000 XOF", "-500 GBP")
    [GeneratedRegex(@"(?<![£$€¥₦₹₵]\s?)([+-]?\d+(?:,\d+)*(?:\.\d+)?)\s+([A-Z]{3})\b", RegexOptions.Compiled)]
    private static partial Regex AmountCodeOnlyPattern();

    // Pattern 4: code + amount without symbol  (e.g. "GBP 200", "USD -1,250.50")
    [GeneratedRegex(@"\b([A-Z]{3})\s+([+-]?\d+(?:,\d+)*(?:\.\d+)?)\b", RegexOptions.Compiled)]
    private static partial Regex CodeAmountOnlyPattern();

    // Pattern 5: ~amount + code (approximate, e.g. "~£450 GBP" or "~450 GBP")
    [GeneratedRegex(@"~\s*([£$€¥₦₹₵])?\s*(\d+(?:,\d+)*(?:\.\d+)?)\s+([A-Z]{3})\b", RegexOptions.Compiled)]
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

    // Emojis / pictographs. Covers dingbats, transport/map symbols, flags,
    // skin-tone modifiers, variation selectors, zero-width joiners, and the
    // common BMP + supplementary emoji ranges (matched via surrogate pairs).
    [GeneratedRegex(
        @"[\u200D\uFE0F\u00A9\u00AE\u203C\u2049\u2122\u2139\u2194-\u21AA\u231A-\u23FA\u24C2\u25AA-\u27BF\u2934-\u2935\u2B05-\u2B55\u3030\u303D\u3297\u3299]|[\uD83C-\uDBFF][\uDC00-\uDFFF]",
        RegexOptions.Compiled)]
    private static partial Regex EmojiPattern();

    // Percent attached to a digit: "45%" → "45 percent"
    [GeneratedRegex(@"(\d)\s*%", RegexOptions.Compiled)]
    private static partial Regex PercentPattern();

    // ALL-CAPS acronym of 3-5 letters, not preceded by a digit (optionally
    // followed by a single whitespace) so "100 XYZ" and "100XYZ" — which
    // represent unknown currency codes — are left intact.
    [GeneratedRegex(@"(?<!\d\s?)\b[A-Z]{3,5}\b", RegexOptions.Compiled)]
    private static partial Regex AcronymPattern();

    // Acronyms we never spell out (mostly currency codes handled elsewhere).
    private static readonly HashSet<string> AcronymSkipList = new(StringComparer.Ordinal)
    {
        "GBP", "USD", "EUR", "NGN", "XOF", "XAF", "KES", "ZAR", "GHS",
        "TZS", "UGX", "RWF", "MAD", "EGP", "INR", "JPY", "CNY", "CAD",
        "AUD", "CHF", "BRL", "MXN", "AED", "SAR", "ZWL", "ZIG",
    };

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

        // 3. Replace directional/math/trademark symbols with spoken words.
        //    Runs before the emoji strip so ©, ®, ™ — which also live inside
        //    the emoji Unicode ranges — are turned into words first.
        result = ReplaceSymbolsWithWords(result);

        // 4. Strip emojis and common unpronounceable pictographs
        result = EmojiPattern().Replace(result, "");

        // 5. "45%" → "45 percent"
        result = PercentPattern().Replace(result, "$1 percent");

        // 6. Approximate amounts: "~£450 GBP" → "approximately 450 pounds"
        result = ApproxAmountPattern().Replace(result, match =>
        {
            var amount = match.Groups[2].Value;
            var code = match.Groups[3].Value;
            var spokenCurrency = ResolveCurrencyName(code, amount);
            return $"approximately {amount} {spokenCurrency}";
        });

        // 7. Symbol + amount + code  →  amount + spoken name
        //    e.g. "£380 GBP" → "380 pounds" (not "380 pounds pounds")
        result = SymbolAmountCodePattern().Replace(result, match =>
        {
            var amount = match.Groups[2].Value;
            var code = match.Groups[3].Value;
            var spokenCurrency = ResolveCurrencyName(code, amount);
            return $"{amount} {spokenCurrency}";
        });

        // 8. Symbol + amount (no code)  →  leave as-is (providers handle £280 fine)
        //    But we could normalise if needed in future.

        // 9. Amount + code (no symbol)  →  amount + spoken name
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

        // 9b. Code + amount (no symbol)  →  amount + spoken name
        //     e.g. "GBP 200" → "200 pounds", "USD 1,250.50" → "1,250.50 dollars"
        result = CodeAmountOnlyPattern().Replace(result, match =>
        {
            var code = match.Groups[1].Value;
            var amount = match.Groups[2].Value;

            if (!CurrencyNames.ContainsKey(code))
                return match.Value;

            var spokenCurrency = ResolveCurrencyName(code, amount);
            return $"{amount} {spokenCurrency}";
        });

        // 10. Spell out remaining ALL-CAPS acronyms (FBI → F-B-I). Runs AFTER
        //     currency processing so recognised codes like "USD" have already
        //     been replaced with "dollars" and will never reach this step.
        result = AcronymPattern().Replace(result, match =>
        {
            var token = match.Value;
            return AcronymSkipList.Contains(token) ? token : string.Join('-', token.ToCharArray());
        });

        // 11. Tidy spaces around punctuation produced by symbol/percent expansion
        result = Regex.Replace(result, @"\s+([,.;!?])", "$1");

        // 12. Clean up whitespace
        result = ExcessWhitespacePattern().Replace(result, " ");
        result = result.Trim();

        return result;
    }

    /// <summary>
    /// Replace directional arrows, math operators, and trademark symbols with
    /// their spoken-word equivalents so TTS engines don't skip or mispronounce
    /// them. The set mirrors the curated list used by
    /// <c>AguiStreamingEndpoint.BuildSpeechRender</c>.
    /// </summary>
    private static string ReplaceSymbolsWithWords(string text)
    {
        // Order matters: multi-char operators first, bare symbols after.
        var result = text;

        // Comparison / arrow digraphs (surrounded by spaces so we don't
        // clobber email addresses, URLs, or currency symbols like ">$100")
        result = result.Replace(" >= ", " greater than or equal to ", StringComparison.Ordinal);
        result = result.Replace(" <= ", " less than or equal to ", StringComparison.Ordinal);
        result = result.Replace(" != ", " not equal to ", StringComparison.Ordinal);
        result = result.Replace(" => ", " leads to ", StringComparison.Ordinal);
        result = result.Replace(" -> ", " to ", StringComparison.Ordinal);
        result = result.Replace(" <- ", " from ", StringComparison.Ordinal);
        result = result.Replace(" <> ", " versus ", StringComparison.Ordinal);

        // Common single-char operators between words
        result = result.Replace(" > ", " greater than ", StringComparison.Ordinal);
        result = result.Replace(" < ", " less than ", StringComparison.Ordinal);
        result = result.Replace(" = ", " equals ", StringComparison.Ordinal);
        result = result.Replace(" & ", " and ", StringComparison.Ordinal);
        result = result.Replace(" | ", " or ", StringComparison.Ordinal);
        result = result.Replace(" @ ", " at ", StringComparison.Ordinal);

        // Unicode symbols TTS engines stumble over
        result = result.Replace("→", " to ", StringComparison.Ordinal);
        result = result.Replace("←", " from ", StringComparison.Ordinal);
        result = result.Replace("↑", " up ", StringComparison.Ordinal);
        result = result.Replace("↓", " down ", StringComparison.Ordinal);
        result = result.Replace("✓", " yes ", StringComparison.Ordinal);
        result = result.Replace("✔", " yes ", StringComparison.Ordinal);
        result = result.Replace("✗", " no ", StringComparison.Ordinal);
        result = result.Replace("✘", " no ", StringComparison.Ordinal);
        result = result.Replace("•", ",", StringComparison.Ordinal);
        result = result.Replace("–", " to ", StringComparison.Ordinal);   // en-dash (range)
        result = result.Replace("—", ", ", StringComparison.Ordinal);     // em-dash (pause)
        result = result.Replace("…", "...", StringComparison.Ordinal);
        result = result.Replace("©", " copyright ", StringComparison.Ordinal);
        result = result.Replace("®", " registered ", StringComparison.Ordinal);
        result = result.Replace("™", " trademark ", StringComparison.Ordinal);

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
