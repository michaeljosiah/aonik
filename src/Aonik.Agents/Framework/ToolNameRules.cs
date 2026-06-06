using System.Text.RegularExpressions;

namespace Aonik.Agents.Framework;

/// <summary>
/// The naming constraint a tenant-contributed tool must satisfy to be a valid function/tool name for
/// OpenAI-compatible chat providers: 1–64 chars of letters, digits, underscores, or hyphens (no
/// spaces, dots, or other punctuation). A name that violates this would be exposed verbatim as
/// <c>AIFunction.Name</c> and make <em>every</em> agent request for that tenant fail when the tool
/// list is serialized — so names are rejected at create/update (Spec 033) and any non-conforming
/// tool is skipped at agent-build time as defense-in-depth.
/// </summary>
internal static class ToolNameRules
{
    public const string Pattern = "^[A-Za-z0-9_-]{1,64}$";

    public const string Message =
        "Tool name must be 1-64 characters using only letters, numbers, underscores, or hyphens (no spaces or dots).";

    private static readonly Regex Regex = new(Pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsValid(string? name) => !string.IsNullOrEmpty(name) && Regex.IsMatch(name);
}
