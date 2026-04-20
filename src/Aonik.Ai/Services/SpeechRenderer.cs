using System.Globalization;
using System.Text.RegularExpressions;
using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Ai.Services;

public sealed class SpeechRenderer : ISpeechRenderer
{
    private static readonly Regex MultiWhitespaceRegex = new("\\s+", RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkRegex = new(@"\[(?<text>[^\]]+)\]\([^)]+\)", RegexOptions.Compiled);
    private static readonly Regex LeadingListMarkerRegex = new(@"^\s*(?:[-*•]+|\d+[.)])\s+", RegexOptions.Compiled);
    private static readonly Regex LeadingHeadingRegex = new(@"^\s*#+\s*", RegexOptions.Compiled);
    private static readonly Regex SpeechPreambleRegex = new(
        @"^(?:(?:here(?:'s| is)(?:\s+a)?\s+quick\s+summary|here(?:'s| is)\s+the\s+summary|quick\s+summary|summary|in\s+summary|overall|to\s+summari[sz]e)\s*:?\s*)+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StandaloneDecimalRegex = new(
        @"(?<![£€$₦₹¥\w])(?<number>[+-]?(?:\d{1,3}(?:,\d{3})+|\d+)\.\d+)(?!\s*(?:USD|EUR|GBP|NGN|GHS|ZAR|ZWL|ZIG|KES|INR|CNY)\b)",
        RegexOptions.Compiled);
    private static readonly Regex EmojiRegex = new(
        @"[\u200D\uFE0F\u00A9\u00AE\u203C\u2049\u2122\u2139\u2194-\u21AA\u231A-\u23FA\u24C2\u25AA-\u27BF\u2934-\u2935\u2B05-\u2B55\u3030\u303D\u3297\u3299]|[\uD83C-\uDBFF][\uDC00-\uDFFF]",
        RegexOptions.Compiled);

    private const string SupportedCurrencyCodes = "USD|EUR|GBP|NGN|GHS|ZAR|ZWL|ZIG|KES|INR|CNY";
    private const string SupportedAmountPattern = @"[+-]?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?";

    private static readonly Regex CurrencyBeforeAmountRegex = new(
        $@"\b(?<code>{SupportedCurrencyCodes})\s*(?<amount>{SupportedAmountPattern})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AmountBeforeCurrencyRegex = new(
        $@"\b(?<amount>{SupportedAmountPattern})\s*(?<code>{SupportedCurrencyCodes})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CurrencySymbolAmountRegex = new(
        $@"(?<!\w)(?<symbol>GH₵|KSh|₦|£|€|₹|¥|R|\$)\s*(?<amount>{SupportedAmountPattern})",
        RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, CurrencyDescriptor> Currencies =
        new Dictionary<string, CurrencyDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = new("dollar", "dollars"),
            ["EUR"] = new("euro", "euros"),
            ["GBP"] = new("pound", "pounds"),
            ["NGN"] = new("naira", "naira"),
            ["GHS"] = new("cedi", "cedis"),
            ["ZAR"] = new("rand", "rand"),
            ["ZWL"] = new("Zimbabwe dollar", "Zimbabwe dollars"),
            ["ZIG"] = new("Zimbabwe Gold", "Zimbabwe Gold"),
            ["KES"] = new("Kenyan shilling", "Kenyan shillings"),
            ["INR"] = new("rupee", "rupees"),
            ["CNY"] = new("yuan", "yuan"),
        };

    private static readonly IReadOnlyDictionary<string, string> CurrencySymbolToCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["$"] = "USD",
            ["€"] = "EUR",
            ["£"] = "GBP",
            ["₦"] = "NGN",
            ["GH₵"] = "GHS",
            ["R"] = "ZAR",
            ["KSh"] = "KES",
            ["₹"] = "INR",
            ["¥"] = "CNY",
        };

    public string Render(string assistantText, bool requiresVisualAttention, bool requiresApproval)
    {
        var speechText = NormalizeForSpeech(assistantText);
        speechText = AppendChatReviewGuidance(speechText, requiresVisualAttention, requiresApproval);

        return speechText;
    }

    public string RenderChunk(string chunkText)
    {
        return NormalizeForSpeech(chunkText);
    }

    public string RenderGuidance(bool requiresVisualAttention, bool requiresApproval)
    {
        return BuildChatReviewGuidance(requiresVisualAttention, requiresApproval);
    }

    private static string NormalizeForSpeech(string? rawText)
    {
        var speechText = string.Empty;
        if (!string.IsNullOrWhiteSpace(rawText))
        {
            var normalized = rawText.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            normalized = MarkdownLinkRegex.Replace(normalized, "${text}");

            var lines = normalized
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeLine)
                .Where(line => !string.IsNullOrWhiteSpace(line));

            speechText = string.Join(' ', lines);
        }

        speechText = SpeechPreambleRegex.Replace(speechText, string.Empty);
        speechText = ReplaceSymbolsWithWords(speechText);
        speechText = EmojiRegex.Replace(speechText, string.Empty);
        speechText = ExpandCurrencyAmounts(speechText);
        speechText = NormalizeStandaloneDecimals(speechText);
        speechText = Regex.Replace(speechText, @"\s+([,.;!?])", "$1");
        speechText = MultiWhitespaceRegex.Replace(speechText, " ").Trim();

        return speechText;
    }

    private static string AppendChatReviewGuidance(
        string speechText,
        bool requiresVisualAttention,
        bool requiresApproval)
    {
        var guidance = BuildChatReviewGuidance(requiresVisualAttention, requiresApproval);
        if (string.IsNullOrWhiteSpace(guidance))
        {
            return speechText;
        }

        if (string.IsNullOrWhiteSpace(speechText))
        {
            return guidance;
        }

        var normalized = speechText.TrimEnd();
        if (normalized.EndsWith(guidance, StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        var separator = normalized.EndsWith('.') || normalized.EndsWith('!') || normalized.EndsWith('?')
            ? " "
            : ". ";

        return $"{normalized}{separator}{guidance}";
    }

    private static string BuildChatReviewGuidance(bool requiresVisualAttention, bool requiresApproval)
    {
        if (requiresVisualAttention && requiresApproval)
        {
            return "I've opened the chat so you can review the details and approve this action.";
        }

        if (requiresApproval)
        {
            return "I've opened the chat so you can review and approve this action.";
        }

        if (requiresVisualAttention)
        {
            return "I've opened the chat so you can review the details.";
        }

        return string.Empty;
    }

    private static string NormalizeLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var wasListItem = LeadingListMarkerRegex.IsMatch(trimmed);
        trimmed = LeadingHeadingRegex.Replace(trimmed, string.Empty);
        trimmed = LeadingListMarkerRegex.Replace(trimmed, string.Empty);
        trimmed = trimmed
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace('`', ' ');
        trimmed = MultiWhitespaceRegex.Replace(trimmed, " ").Trim();

        if (wasListItem && trimmed.Length > 0 && !trimmed.EndsWith('.') && !trimmed.EndsWith('!') && !trimmed.EndsWith('?'))
        {
            trimmed = $"{trimmed}.";
        }

        return trimmed;
    }

    private static string ExpandCurrencyAmounts(string value)
    {
        var expanded = CurrencyBeforeAmountRegex.Replace(value, match =>
            BuildSpokenAmount(match.Groups["amount"].Value, match.Groups["code"].Value));

        expanded = AmountBeforeCurrencyRegex.Replace(expanded, match =>
            BuildSpokenAmount(match.Groups["amount"].Value, match.Groups["code"].Value));

        expanded = CurrencySymbolAmountRegex.Replace(expanded, match =>
        {
            var symbol = match.Groups["symbol"].Value;
            if (!CurrencySymbolToCode.TryGetValue(symbol, out var currencyCode))
            {
                return match.Value;
            }

            return BuildSpokenAmount(match.Groups["amount"].Value, currencyCode);
        });

        return expanded;
    }

    private static string BuildSpokenAmount(string amount, string currencyCode)
    {
        if (!Currencies.TryGetValue(currencyCode, out var descriptor))
        {
            return $"{FormatSpokenNumber(amount)} {currencyCode}";
        }

        var normalizedAmount = amount.Replace(",", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(normalizedAmount, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsedAmount))
        {
            return $"{amount} {descriptor.Plural}";
        }

        return FormatSpokenCurrencyPhrase(parsedAmount, descriptor);
    }

    private static string FormatSpokenNumber(string rawAmount)
    {
        var normalized = rawAmount.Replace(",", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var value))
        {
            return rawAmount;
        }

        if (value == decimal.Truncate(value))
        {
            return decimal.Truncate(value).ToString("#,0", CultureInfo.InvariantCulture);
        }

        return value.ToString("G", CultureInfo.InvariantCulture);
    }

    private static string FormatSpokenCurrencyPhrase(
        decimal amount,
        CurrencyDescriptor descriptor)
    {
        var abs = decimal.Abs(amount);
        var sign = amount < 0 ? "minus " : "";
        var wholePart = decimal.Truncate(abs);

        var currencyName = abs == 1m ? descriptor.Singular : descriptor.Plural;

        if (abs == wholePart)
        {
            return $"{sign}{wholePart:#,0} {currencyName}";
        }

        var fractional = (abs - wholePart) * 100;
        var minorUnits = Math.Round(fractional, 0);

        if (fractional == minorUnits && minorUnits > 0)
        {
            return $"{sign}{wholePart:#,0} {currencyName} {minorUnits:0}";
        }

        return $"{sign}{abs.ToString("G", CultureInfo.InvariantCulture)} {currencyName}";
    }

    private static string ReplaceSymbolsWithWords(string text)
    {
        var result = text;

        result = result.Replace(" >= ", " greater than or equal to ", StringComparison.Ordinal);
        result = result.Replace(" <= ", " less than or equal to ", StringComparison.Ordinal);
        result = result.Replace(" != ", " not equal to ", StringComparison.Ordinal);
        result = result.Replace(" => ", " leads to ", StringComparison.Ordinal);
        result = result.Replace(" -> ", " to ", StringComparison.Ordinal);
        result = result.Replace(" <- ", " from ", StringComparison.Ordinal);
        result = result.Replace(" <> ", " versus ", StringComparison.Ordinal);

        result = result.Replace(" > ", " greater than ", StringComparison.Ordinal);
        result = result.Replace(" < ", " less than ", StringComparison.Ordinal);
        result = result.Replace(" = ", " equals ", StringComparison.Ordinal);
        result = result.Replace(" + ", " plus ", StringComparison.Ordinal);
        result = result.Replace(" - ", " minus ", StringComparison.Ordinal);
        result = result.Replace(" x ", " times ", StringComparison.Ordinal);
        result = result.Replace(" / ", " divided by ", StringComparison.Ordinal);
        result = result.Replace(" & ", " and ", StringComparison.Ordinal);
        result = result.Replace(" | ", " or ", StringComparison.Ordinal);
        result = result.Replace(" @ ", " at ", StringComparison.Ordinal);
        result = result.Replace(" % ", " percent ", StringComparison.Ordinal);

        result = Regex.Replace(result, @"(\d)%", "$1 percent");

        result = result.Replace("→", " to ", StringComparison.Ordinal);
        result = result.Replace("←", " from ", StringComparison.Ordinal);
        result = result.Replace("↑", " up ", StringComparison.Ordinal);
        result = result.Replace("↓", " down ", StringComparison.Ordinal);
        result = result.Replace("✓", " yes ", StringComparison.Ordinal);
        result = result.Replace("✔", " yes ", StringComparison.Ordinal);
        result = result.Replace("✗", " no ", StringComparison.Ordinal);
        result = result.Replace("✘", " no ", StringComparison.Ordinal);
        result = result.Replace("•", ",", StringComparison.Ordinal);
        result = result.Replace("–", " to ", StringComparison.Ordinal);
        result = result.Replace("—", ", ", StringComparison.Ordinal);
        result = result.Replace("…", "...", StringComparison.Ordinal);
        result = result.Replace("©", " copyright ", StringComparison.Ordinal);
        result = result.Replace("®", " registered ", StringComparison.Ordinal);
        result = result.Replace("™", " trademark ", StringComparison.Ordinal);

        return result;
    }

    private static string NormalizeStandaloneDecimals(string text)
    {
        return StandaloneDecimalRegex.Replace(text, match =>
            FormatSpokenNumber(match.Groups["number"].Value));
    }

    private sealed record CurrencyDescriptor(string Singular, string Plural);
}
