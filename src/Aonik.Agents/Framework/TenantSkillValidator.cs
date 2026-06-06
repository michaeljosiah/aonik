using System.Text.Json;
using Microsoft.Agents.AI;

namespace Aonik.Agents.Framework;

/// <summary>Outcome of validating an uploaded <c>SKILL.md</c> (Spec 033 §8.1, §10.2).</summary>
public sealed record TenantSkillValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    string Name,
    string Description,
    string Version,
    IReadOnlyList<string> AllowedTools,
    bool ScriptsPresent,
    string FrontmatterJson);

/// <summary>
/// Validates an uploaded skill package server-side (Spec 033 §8.1): it reads the YAML frontmatter
/// block of the <c>SKILL.md</c>, runs the framework <c>AgentSkillFrontmatter</c> validators verbatim,
/// and intersects the frontmatter's <c>allowed-tools</c> down to the agent's existing tool allow-list
/// — rejecting any tool the agent lacks and any money-moving built-in. The same code path runs in the
/// "validate skill" harness and at upload, so a harness pass means the real wiring works.
/// <para>
/// The frontmatter is parsed here (rather than via MAF's file source, which is not public) so the
/// validator needs no temp files; the framework validators remain the authority on name / description
/// / compatibility limits.
/// </para>
/// </summary>
public interface ITenantSkillValidator
{
    Task<TenantSkillValidationResult> ValidateAsync(
        string skillMarkdown,
        IReadOnlyCollection<string> agentToolAllowList,
        CancellationToken cancellationToken = default);
}

internal sealed class TenantSkillValidator : ITenantSkillValidator
{
    // Money-moving built-ins a tenant skill must never reference, even if the agent has them
    // (Spec 033 §8.1 "the allowed-tools ceiling"). Matched as a contains-check so variants trip too.
    private static readonly string[] MoneyToolMarkers =
    {
        "capture_payment", "cancel_payment", "payment_intent", "mark_invoice_paid",
        "_payout", "_transfer", "_refund",
    };

    public Task<TenantSkillValidationResult> ValidateAsync(
        string skillMarkdown,
        IReadOnlyCollection<string> agentToolAllowList,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(skillMarkdown))
        {
            return Task.FromResult(new TenantSkillValidationResult(
                false, ["SKILL.md is empty."], "", "", "", [], false, "{}"));
        }

        if (!TryParseFrontmatter(skillMarkdown, out var fm))
        {
            return Task.FromResult(new TenantSkillValidationResult(
                false, ["SKILL.md must begin with a YAML frontmatter block delimited by '---'."], "", "", "", [], false, "{}"));
        }

        // Framework validators (verbatim) own the name / description / compatibility rules.
        if (!AgentSkillFrontmatter.ValidateName(fm.Name, out var nameError))
        {
            errors.Add(nameError);
        }
        if (!AgentSkillFrontmatter.ValidateDescription(fm.Description, out var descError))
        {
            errors.Add(descError);
        }
        if (!string.IsNullOrWhiteSpace(fm.Compatibility)
            && !AgentSkillFrontmatter.ValidateCompatibility(fm.Compatibility, out var compatError))
        {
            errors.Add(compatError);
        }

        // allowed-tools ∩ agent allow-list, minus money-moving built-ins (the key safety property).
        var accepted = new List<string>();
        foreach (var tool in fm.AllowedTools)
        {
            if (string.IsNullOrWhiteSpace(tool))
            {
                continue;
            }

            if (IsMoneyMovingBuiltin(tool))
            {
                errors.Add($"allowed-tools may not reference the money-moving built-in '{tool}'.");
                continue;
            }

            if (agentToolAllowList.Count > 0
                && !agentToolAllowList.Contains(tool, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"allowed-tools references '{tool}', which the agent does not have.");
                continue;
            }

            accepted.Add(tool);
        }

        var frontmatterJson = JsonSerializer.Serialize(new
        {
            name = fm.Name,
            description = fm.Description,
            license = fm.License,
            compatibility = fm.Compatibility,
            allowedTools = accepted,
        });

        var result = new TenantSkillValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors,
            Name: fm.Name,
            Description: fm.Description,
            Version: fm.Compatibility,
            AllowedTools: accepted,
            ScriptsPresent: fm.ScriptsPresent,
            FrontmatterJson: frontmatterJson);

        return Task.FromResult(result);
    }

    private static bool IsMoneyMovingBuiltin(string toolName)
    {
        foreach (var marker in MoneyToolMarkers)
        {
            if (toolName.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private sealed record ParsedFrontmatter(
        string Name,
        string Description,
        string License,
        string Compatibility,
        IReadOnlyList<string> AllowedTools,
        bool ScriptsPresent);

    // Minimal YAML-frontmatter reader for the SKILL.md header. Handles scalar `key: value` lines and
    // a list-valued `allowed-tools` (inline `[a, b]` or a block of `- item` lines). Sufficient for the
    // SKILL.md header shape; the framework validators enforce the value rules.
    private static bool TryParseFrontmatter(string markdown, out ParsedFrontmatter frontmatter)
    {
        frontmatter = new ParsedFrontmatter("", "", "", "", [], false);

        var text = markdown.Replace("\r\n", "\n").Replace("\r", "\n");
        if (!text.StartsWith("---", StringComparison.Ordinal))
        {
            return false;
        }

        var closing = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (closing < 0)
        {
            return false;
        }

        var block = text.Substring(3, closing - 3);
        var lines = block.Split('\n');

        var scalars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var allowedTools = new List<string>();
        var scriptsPresent = false;
        string? currentList = null;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) && currentList is not null)
            {
                var item = StripQuotes(trimmed[2..].Trim());
                if (currentList == "allowed-tools" && item.Length > 0)
                {
                    allowedTools.Add(item);
                }
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();
            currentList = null;

            switch (key)
            {
                case "allowed-tools":
                case "allowed_tools":
                    if (value.StartsWith("[", StringComparison.Ordinal))
                    {
                        foreach (var item in value.Trim('[', ']').Split(','))
                        {
                            var t = StripQuotes(item.Trim());
                            if (t.Length > 0)
                            {
                                allowedTools.Add(t);
                            }
                        }
                    }
                    else if (value.Length == 0)
                    {
                        currentList = "allowed-tools"; // block list follows
                    }
                    break;

                case "scripts":
                    scriptsPresent = true;
                    if (value.Length == 0)
                    {
                        currentList = "scripts";
                    }
                    break;

                default:
                    scalars[key] = StripQuotes(value);
                    break;
            }
        }

        frontmatter = new ParsedFrontmatter(
            Name: scalars.GetValueOrDefault("name", ""),
            Description: scalars.GetValueOrDefault("description", ""),
            License: scalars.GetValueOrDefault("license", ""),
            Compatibility: scalars.GetValueOrDefault("compatibility", ""),
            AllowedTools: allowedTools,
            ScriptsPresent: scriptsPresent);
        return true;
    }

    private static string StripQuotes(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }
        return value;
    }
}
