using Microsoft.Extensions.AI;

namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Central, fail-closed seam that classifies and gates agent tools before they reach the
/// model. It aggregates every module <see cref="IToolApprovalManifest"/> and is applied at
/// the one place tools are assembled — the agent descriptor's tool-build step — so gating
/// cannot be forgotten (Spec 032, finding C3).
/// </summary>
public interface IToolApprovalGate
{
    /// <summary>
    /// Classify and gate a single tool:
    /// <list type="bullet">
    ///   <item>read-only (classified RO, or unclassified and not mutating-looking) → passes through unchanged;</item>
    ///   <item>classified mutating → wrapped so it cannot execute ungated;</item>
    ///   <item>unclassified but mutating-looking → throws <see cref="ToolNotClassifiedException"/>.</item>
    /// </list>
    /// <para>
    /// <paramref name="serviceProvider"/> is the request-scoped provider captured at tool-build
    /// time. The wrapper uses it to resolve the scoped <see cref="IToolApprovalService"/> when a
    /// High-tier tool is invoked (the gate itself is a singleton and cannot hold scoped services).
    /// May be null in tests or hosts that do not exercise the High marshalling path.
    /// </para>
    /// </summary>
    AITool Gate(AITool tool, IServiceProvider? serviceProvider = null);

    /// <summary>Gate every tool in the sequence. See <see cref="Gate"/>.</summary>
    IEnumerable<AITool> GateAll(IEnumerable<AITool> tools, IServiceProvider? serviceProvider = null);

    /// <summary>
    /// Returns the aggregated classification for a tool name, or <see langword="null"/> when no
    /// registered manifest claims it. Lets a transport introspect whether an agent's tools are
    /// classified (and whether any is mutating) <em>without</em> building the agent — e.g. the voice
    /// endpoint uses it to decide whether to expose the full gated toolset (Spec 032 §7.7) or fall
    /// back to a read-only variant. A <see langword="null"/> result whose name
    /// <see cref="MutatingToolNameHeuristic.LooksMutating"/> is what would make <see cref="Gate"/> throw.
    /// </summary>
    ToolClassification? Classify(string toolName);
}
