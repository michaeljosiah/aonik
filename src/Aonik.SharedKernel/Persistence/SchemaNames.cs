namespace Aonik.SharedKernel.Persistence;

/// <summary>
/// Canonical SQL schema names used by module-scoped DbContexts.
/// All modules share a single physical database but use separate schemas
/// for logical isolation and ownership clarity.
/// </summary>
public static class SchemaNames
{
    /// <summary>Identity, Party, ReferenceData, Settings, Autonumbering, Compliance, Features, Notifications, Operations, CMS, Catalog</summary>
    public const string Platform = "platform";

    /// <summary>Ledger, Billing, Payments, Orders, Pricing, Partners, PersonalFinance</summary>
    public const string Finance = "finance";

    /// <summary>AiProvider, AiModel, AiRoutePolicy, PromptSpec, ToolSpec, AiPolicy, AiRun, AiTrace, AiFeedback, EvalSuite, EvalRun, Insight, Signal</summary>
    public const string Ai = "ai";

    /// <summary>Agent, AgentRun, OrchestratorPolicy, Proposal</summary>
    public const string Agents = "agents";

    /// <summary>Default schema used by the monolithic AonikDbContext during migration (dbo).</summary>
    public const string Default = "dbo";
}
