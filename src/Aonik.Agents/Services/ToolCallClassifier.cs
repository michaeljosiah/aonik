using Aonik.Agents.Contracts.Services;
using Microsoft.Extensions.AI;

namespace Aonik.Agents.Services;

public sealed class ToolCallClassifier : IToolCallClassifier
{
    private const string DisplayToolPrefix = "display_";
    private const string ApprovalToolName = "confirmAction";

    public bool IsDisplay(string toolName)
        => toolName.StartsWith(DisplayToolPrefix, StringComparison.OrdinalIgnoreCase);

    public bool RequiresApproval(string toolName)
        => string.Equals(toolName, ApprovalToolName, StringComparison.OrdinalIgnoreCase);

    public string ResolveCallId(FunctionCallContent functionCall)
        => !string.IsNullOrWhiteSpace(functionCall.CallId)
            ? functionCall.CallId
            : Guid.NewGuid().ToString("N");
}
