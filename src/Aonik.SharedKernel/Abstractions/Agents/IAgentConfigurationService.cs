namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Service for managing persisted agent configurations. Supports a two-level
/// override model: global defaults (seeded from code-based descriptors) and
/// tenant-specific overrides that replace the global config for a given tenant.
///
/// Resolution order: tenant row → global row → code-based descriptor.
/// </summary>
/// <remarks>
/// Lives on SharedKernel so domain modules (Finance, Platform) can read agent
/// configuration without taking a back-pointing reference on the Agents runtime.
/// </remarks>
public interface IAgentConfigurationService
{
    /// <summary>
    /// Lists all agent configurations visible to the current tenant.
    /// Returns both global defaults and any tenant-specific overrides.
    /// </summary>
    Task<IReadOnlyList<AgentConfigurationResponse>> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the resolved agent configuration for the given agent name.
    /// Returns the tenant-specific override if it exists, otherwise the global default.
    /// Returns <c>null</c> if no configuration exists in the database.
    /// </summary>
    Task<AgentConfigurationResponse?> GetResolvedAsync(
        string agentName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a tenant-scoped agent configuration override.
    /// The override is a full copy — fields not provided in the request are
    /// populated from the global default (if it exists) or from the code-based descriptor.
    /// </summary>
    Task<AgentConfigurationResponse> UpsertOverrideAsync(
        string agentName,
        UpsertAgentConfigurationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the tenant-specific override for the given agent name,
    /// reverting the tenant to the global default configuration.
    /// </summary>
    Task DeleteOverrideAsync(
        string agentName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the agent's instructions (prompt) to the hard-coded default
    /// defined by its <see cref="IDomainAgentDescriptor"/>.
    /// Only the <c>InstructionsText</c> field is overwritten — tools, model,
    /// risk tier, and other customizations remain intact. Targets the tenant
    /// override if one exists for the current tenant, otherwise the global row.
    /// </summary>
    Task<AgentConfigurationResponse> ResetPromptAsync(
        string agentName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the agent's tool catalogue (<c>ToolsetIdsJson</c>) to the live
    /// list returned by <see cref="IDomainAgentDescriptor.GetToolNames"/>.
    /// Useful when a tool surface has changed in code (e.g. a sub-agent
    /// trigger was renamed or added) but the persisted Agent row still
    /// reflects the old whitelist — <see cref="SeedGlobalDefaultsAsync"/> is
    /// idempotent for the toolset to preserve admin customisations, so this
    /// method is the explicit opt-in path to refresh from the descriptor.
    /// Only the toolset field is overwritten; prompt, model, risk tier, etc.
    /// remain intact. Targets the tenant override if one exists for the
    /// current tenant, otherwise the global row.
    /// </summary>
    /// <param name="serviceProvider">
    /// Service provider used to resolve domain services when calling
    /// <see cref="IDomainAgentDescriptor.GetToolNames"/>. Required because
    /// tools are built from DI-resolved service instances.
    /// </param>
    Task<AgentConfigurationResponse> ResetToolsetAsync(
        string agentName,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds global default Agent rows (TenantId = null) for each registered
    /// <see cref="IDomainAgentDescriptor"/>. Idempotent — skips agents that
    /// already have a global row.
    /// </summary>
    /// <param name="serviceProvider">
    /// Service provider used to resolve domain services when calling
    /// <see cref="IDomainAgentDescriptor.GetToolNames"/>. Required because
    /// tools are built from DI-resolved service instances.
    /// </param>
    Task SeedGlobalDefaultsAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}
