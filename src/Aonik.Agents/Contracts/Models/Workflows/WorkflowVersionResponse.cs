namespace Aonik.Agents.Contracts.Models.Workflows;

/// <summary>
/// One entry in a workflow's version history sidebar.
/// </summary>
public sealed record WorkflowVersionResponse(
    Guid Id,
    string Tag,
    string Message,
    string AuthorName,
    string AuthorColor,
    DateTime CreatedAt,
    /// <summary>Relative-time label, e.g. "today", "8d ago".</summary>
    string When);
