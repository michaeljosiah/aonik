using System.Text.Json;
using Aonik.SharedKernel.Abstractions.Agents;
using HyperlightSandbox.Api;
using HyperlightSandbox.Extensions.AI;
using HyperlightSandbox.Guest.Python;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Agents.CodeAct;

/// <summary>
/// CodeAct sandbox provider backed by Hyperlight — the in-process Python
/// sandbox we use on local Linux dev hosts that expose <c>/dev/kvm</c> or
/// <c>/dev/mshv</c>. On Azure Container Apps (no hypervisor device exposed)
/// this provider's <see cref="TryBuildExecuteCodeTool"/> returns <c>null</c>
/// and the caller falls through to the conventional tool-loop path.
/// </summary>
/// <remarks>
/// <para>
/// This is the same machinery that lived in the old
/// <c>CodeActSubAgentFactory</c> static class — refactored behind the
/// <see cref="ICodeActSandboxProvider"/> interface so the descriptor code
/// can pick between Hyperlight and ACA Sessions at runtime.
/// </para>
/// <para>
/// Lifetime: a fresh <see cref="CodeExecutionTool"/> is built per
/// <see cref="TryBuildExecuteCodeTool"/> invocation. The native sandbox
/// reclaims resources via finalizer + <c>SafeHandle</c>.
/// </para>
/// </remarks>
public sealed class HyperlightCodeActSandboxProvider : ICodeActSandboxProvider
{
    private const string ExecuteCodeToolName = "execute_code";

    private const string ExecuteCodeToolDescription =
        "Execute Python code in a secure isolated sandbox. Inside the " +
        "sandbox, call_tool(name, **kwargs) invokes a registered host tool " +
        "by name. Use this when an analysis needs to loop over data, do " +
        "parametric arithmetic, or compose multiple tool results in one " +
        "pass. The sandbox has no filesystem or network access beyond what " +
        "is explicitly registered.";

    private readonly ILogger<HyperlightCodeActSandboxProvider> _logger;

    public HyperlightCodeActSandboxProvider(ILogger<HyperlightCodeActSandboxProvider> logger)
    {
        _logger = logger;
    }

    public AIFunction? TryBuildExecuteCodeTool(
        CodeActSandboxContext context,
        IReadOnlyList<AIFunction> hostTools)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(hostTools);

        if (!HyperlightHostAvailability.IsAvailable)
        {
            return null;
        }

        var codeTool = new CodeExecutionTool(
            new SandboxBuilder()
                .WithPythonModule()
                .WithTempOutput());

        foreach (var tool in hostTools)
        {
            RegisterAsCallToolBridge(codeTool, tool);
        }

        return codeTool.AsAIFunction(ExecuteCodeToolName, ExecuteCodeToolDescription);
    }

    /// <summary>
    /// Bridges one host-side <see cref="AIFunction"/> into the sandbox's
    /// raw-JSON tool registry. Identical semantics to the legacy
    /// <c>CodeActSubAgentFactory.RegisterAsCallToolBridge</c>.
    /// </summary>
    private static void RegisterAsCallToolBridge(CodeExecutionTool codeTool, AIFunction function)
    {
        codeTool.RegisterToolAsync(function.Name, async (string jsonArgs) =>
        {
            var args = ParseArguments(jsonArgs);

            var result = await function
                .InvokeAsync(new AIFunctionArguments(args), CancellationToken.None)
                .ConfigureAwait(false);

            return JsonSerializer.Serialize(result);
        });
    }

    private static Dictionary<string, object?> ParseArguments(string jsonArgs)
    {
        var bag = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(jsonArgs))
        {
            return bag;
        }

        using var doc = JsonDocument.Parse(jsonArgs);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return bag;
        }

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            bag[property.Name] = property.Value.Clone();
        }

        return bag;
    }
}
