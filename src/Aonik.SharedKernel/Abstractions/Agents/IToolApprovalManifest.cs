namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Module-owned classification of the module's agent tools. Each module that exposes
/// mutating agent tools contributes one manifest; the central <see cref="IToolApprovalGate"/>
/// aggregates them. A manifest only needs to list its <em>mutating</em> tools — read-only
/// tools may be returned as <see cref="ToolClassification.ReadOnly"/> or simply omitted
/// (the gate passes a read-looking, unclassified tool through).
/// </summary>
public interface IToolApprovalManifest
{
    /// <summary>The module this manifest covers, for diagnostics (e.g. "Finance").</summary>
    string Module { get; }

    /// <summary>
    /// Classify a tool by name, or return <see langword="null"/> if this manifest does not
    /// own the tool.
    /// </summary>
    ToolClassification? Classify(string toolName);
}
