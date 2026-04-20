using Microsoft.Extensions.AI;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Classifies streamed tool calls for the AG-UI / Playground streaming
/// endpoints. Centralises the tool-naming conventions used by the agents
/// (<c>display_*</c> prefix for display tools, <c>confirmAction</c> for
/// approval gates) so the streaming endpoints don't hardcode them.
/// </summary>
public interface IToolCallClassifier
{
    /// <summary>
    /// True when the tool name signals the agent wants the user to look at
    /// something rendered in the chat (e.g. <c>display_transactions</c>).
    /// </summary>
    bool IsDisplay(string toolName);

    /// <summary>
    /// True when the tool name is the approval gate used by the
    /// human-in-the-loop flow.
    /// </summary>
    bool RequiresApproval(string toolName);

    /// <summary>
    /// Returns the tool call's stable ID, falling back to a generated GUID
    /// when the provider leaves <see cref="FunctionCallContent.CallId"/>
    /// empty so START/ARGS/END events share a consistent identifier.
    /// </summary>
    string ResolveCallId(FunctionCallContent functionCall);
}
