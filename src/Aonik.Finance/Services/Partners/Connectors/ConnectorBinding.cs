namespace Aonik.Finance.Services.Partners.Connectors;

/// <summary>
/// The per-row binding that turns a stateless connector kind into an account-specific runtime connector
/// (Spec 042 §7). Carries the persisted <see cref="Aonik.Finance.Entities.Partners.Connector"/> row's
/// identity plus the inputs the config provider needs to resolve credentials: the bundle reference and the
/// non-secret config. A connector built with a binding records <see cref="ConnectorId"/> on every downstream
/// record so two accounts of one provider never alias.
/// </summary>
internal sealed record ConnectorBinding(
    Guid ConnectorId,
    string ConnectorKind,
    string ProviderCode,
    string? CredentialsRef,
    string ConfigJson,
    bool AllowLegacyFallback)
{
    /// <summary>True when the row binds a credential bundle (the only path for a partner-specific connector).</summary>
    public bool HasBundle => !string.IsNullOrWhiteSpace(CredentialsRef);
}
