namespace Aonik.Agents.Contracts.Models.Workflows;

/// <summary>
/// Compact registry-page representation of a workflow. Mirrors the shape
/// the Admin UI's <c>WorkflowsListPage</c> renders, denormalising the owner
/// agent display name + colour and the inline step rail so the page can
/// paint without a second round-trip per row.
/// </summary>
public sealed record WorkflowSummaryResponse(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    string State,
    string Version,
    bool AutoRetry,
    int TriggerCount,
    int RunsToday,
    /// <summary>0..1 ratio over recent runs.</summary>
    double Success,
    /// <summary>Average run duration in milliseconds.</summary>
    int AvgMs,
    string OwnerName,
    string OwnerColor,
    IReadOnlyList<string> Contributors,
    /// <summary>Compact step list for the inline rail (kind + label + meta only).</summary>
    IReadOnlyList<WorkflowStepSummary> Steps,
    DateTime UpdatedAt);

/// <summary>
/// One entry in the inline horizontal step rail. Just enough metadata to
/// render the chip — the full node list arrives via
/// <see cref="WorkflowGraphResponse"/> when the editor opens.
/// </summary>
public sealed record WorkflowStepSummary(
    string Kind,
    string Label,
    string? Meta);
