using System.Text.Json;
using HyperlightSandbox.Api;
using HyperlightSandbox.Extensions.AI;
using HyperlightSandbox.Guest.Python;
using Microsoft.Extensions.AI;

namespace Aonik.Finance.Agents.CodeAct;

/// <summary>
/// Builds the single MAF-shaped <c>execute_code</c> <see cref="AIFunction"/>
/// each sub-agent surfaces when running in CodeAct mode (Spec 025 Phase 1).
/// </summary>
/// <remarks>
/// <para>
/// On the CodeAct path, instead of passing N read-only host tools as
/// individual <see cref="AIFunction"/>s on <c>ChatOptions.Tools</c>, the
/// agent gets exactly one tool: a wrapped Hyperlight Python sandbox. The
/// LLM emits Python that calls <c>call_tool(name, **kwargs)</c> for each
/// host operation; the sandbox dispatches those calls to the underlying
/// <see cref="AIFunction"/> delegates registered here.
/// </para>
/// <para>
/// <b>Lifetime:</b> v1 builds a fresh <see cref="CodeExecutionTool"/> per
/// sub-agent invocation. The ~2.5 s cold-start cost is acceptable while
/// we gather telemetry; pooling sandboxes per sub-agent name (with scope
/// bridging for request-scoped services) is the natural next optimisation
/// once usage shows it earns the complexity.
/// </para>
/// <para>
/// <b>Disposal:</b> the returned <see cref="AIFunction"/> captures a
/// reference to the <see cref="CodeExecutionTool"/>; the tool's finalizer
/// + the underlying <c>SafeHandle</c> reclaim native resources when the
/// agent (and therefore the function) becomes unreachable. No explicit
/// <c>Dispose</c> handoff is required for the per-request usage pattern.
/// </para>
/// </remarks>
internal static class CodeActSubAgentFactory
{
    private const string ExecuteCodeToolName = "execute_code";

    private const string ExecuteCodeToolDescription =
        "Execute Python code in a secure isolated sandbox. Inside the " +
        "sandbox, call_tool(name, **kwargs) invokes a registered host tool " +
        "by name. Use this when an analysis needs to loop over data, do " +
        "parametric arithmetic, or compose multiple tool results in one " +
        "pass. The sandbox has no filesystem or network access beyond what " +
        "is explicitly registered.";

    /// <summary>
    /// Builds the wrapping <c>execute_code</c> tool. Each tool returned by
    /// <paramref name="toolFactory"/> is registered into the sandbox's
    /// <c>call_tool</c> registry under its existing name (e.g.
    /// <c>pf_get_category_breakdown</c>) so guest Python can invoke it
    /// transparently.
    /// </summary>
    public static AIFunction BuildExecuteCodeTool(
        IServiceProvider serviceProvider,
        Func<IServiceProvider, IEnumerable<AITool>> toolFactory)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(toolFactory);

        var codeTool = new CodeExecutionTool(
            new SandboxBuilder()
                .WithPythonModule()
                .WithTempOutput());

        foreach (var tool in toolFactory(serviceProvider).OfType<AIFunction>())
        {
            RegisterAsCallToolBridge(codeTool, tool);
        }

        return codeTool.AsAIFunction(ExecuteCodeToolName, ExecuteCodeToolDescription);
    }

    /// <summary>
    /// Bridges one host-side <see cref="AIFunction"/> into the sandbox's
    /// raw-JSON tool registry. The guest passes a JSON object whose keys
    /// are the parameter names; we materialise those into an
    /// <see cref="AIFunctionArguments"/> bag and let the underlying
    /// <see cref="AIFunction"/>'s parameter metadata handle per-parameter
    /// type coercion.
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
            // Clone so the value outlives the using-disposed JsonDocument.
            bag[property.Name] = property.Value.Clone();
        }

        return bag;
    }
}
