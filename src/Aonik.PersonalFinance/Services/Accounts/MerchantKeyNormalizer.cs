using System.Text.RegularExpressions;

namespace Aonik.PersonalFinance.Services.Accounts;

/// <summary>
/// Normalises a raw merchant name into a stable lookup key so the categorizer
/// matches across slightly different spellings ("Spotify AB" vs "spotify ab").
/// Lowercases, strips noise suffixes (ltd, inc, plc, ...), removes
/// non-alphanumerics, collapses whitespace, and truncates to 200 chars.
/// </summary>
internal static class MerchantKeyNormalizer
{
    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    private static readonly Regex NoiseSuffix = new(
        @"\b(ltd|limited|inc|plc|llc|corp|corporation)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NonAlpha = new(@"[^a-z0-9 \-]", RegexOptions.Compiled);

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var s = raw.ToLowerInvariant().Trim();
        s = NoiseSuffix.Replace(s, "");
        s = NonAlpha.Replace(s, " ");
        s = WhitespaceRun.Replace(s, " ").Trim();

        if (s.Length == 0)
        {
            return null;
        }

        return s.Length <= 200 ? s : s[..200];
    }
}
