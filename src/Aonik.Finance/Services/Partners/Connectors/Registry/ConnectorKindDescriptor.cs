using Aonik.Finance.Contracts.Services.Partners.Connectors;

namespace Aonik.Finance.Services.Partners.Connectors.Registry;

/// <summary>
/// One secret field a connector kind needs (Spec 042 §4). The set drives both the Connectivity-tab
/// credential form and <see cref="Aonik.Finance.Entities.Partners.CredentialBundle"/> validation. All
/// credential fields are secret by definition — they are stored encrypted and never returned by a read API.
/// </summary>
internal sealed record ConnectorCredentialField(string Name, string Label, bool Required);

/// <summary>
/// One non-secret config field a connector kind allows (Spec 042 §4, §10). <see cref="AllowedValues"/>
/// constrains enum-like fields (e.g. <c>environment</c> ∈ {sandbox, production}); a null list means free text.
/// Saving a connector validates <c>ConfigJson</c> against these fields and rejects unknown keys.
/// </summary>
internal sealed record ConnectorConfigField(
    string Name,
    string Label,
    bool Required,
    IReadOnlyList<string>? AllowedValues = null,
    string? DefaultValue = null);

/// <summary>
/// Transport endpoints for one named environment. Per ADR-010 / Spec 042 §10 these are
/// <strong>code-owned, not operator-authored</strong>: the operator picks an <c>environment</c> and the
/// connector code derives the base / IdP URLs from it. This removes the free-text URL fields that the
/// v4 Payment Gateways page exposes today.
/// </summary>
internal sealed record ConnectorEnvironment(string Name, string BaseUrl, string? IdpTokenUrl);

/// <summary>
/// A single connector kind: a specific integration shipped in code (transport + auth scheme + port).
/// Many kinds can exist per provider — <c>flutterwave-payout-v4</c> and <c>flutterwave-bills-v3</c> are
/// two kinds under provider <c>Flutterwave</c>. <see cref="Aonik.Finance.Entities.Partners.Connector.ConnectorType"/>
/// stores the kind code; this descriptor maps kind → provider code + port + credential / config schemas.
/// </summary>
internal sealed record ConnectorKindDescriptor(
    string Kind,
    string ProviderCode,
    PartnerServiceCategory Port,
    string DisplayName,
    IReadOnlyList<ConnectorCredentialField> CredentialFields,
    IReadOnlyList<ConnectorConfigField> ConfigFields,
    IReadOnlyList<ConnectorEnvironment> Environments)
{
    /// <summary>Resolves the named environment, falling back to the first declared one when null/unknown.</summary>
    public ConnectorEnvironment ResolveEnvironment(string? name) =>
        Environments.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? Environments[0];

    public ConnectorCredentialField? Credential(string name) =>
        CredentialFields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    public ConnectorConfigField? Config(string name) =>
        ConfigFields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
}
