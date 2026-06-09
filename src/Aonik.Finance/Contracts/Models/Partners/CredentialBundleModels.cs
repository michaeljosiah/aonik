namespace Aonik.Finance.Contracts.Models.Partners;

/// <summary>
/// Admin-facing DTOs for partner-owned credential bundles (Spec 042 §6, §12). Secret VALUES are never
/// part of any response — only field state (set / not-set + rotation version) and which connectors bind a
/// bundle.
/// </summary>
public record CredentialFieldStateDto(string Name, string Label, bool Required, bool IsSet, int Version);

public record CredentialBundleListItem(
    string Ref,
    string Name,
    string ConnectorKind,
    IReadOnlyList<CredentialFieldStateDto> Fields,
    IReadOnlyList<Guid> BoundConnectorIds,
    DateTime? UpdatedAt);

// ── Connector kind schema (drives the schema-generated credential form, §4/§12) ──
public record ConnectorCredentialFieldDto(string Name, string Label, bool Required);

public record ConnectorConfigFieldDto(
    string Name,
    string Label,
    bool Required,
    IReadOnlyList<string>? AllowedValues,
    string? DefaultValue);

public record ConnectorKindSchemaDto(
    string Kind,
    string ProviderCode,
    string Port,
    string DisplayName,
    IReadOnlyList<ConnectorCredentialFieldDto> CredentialFields,
    IReadOnlyList<ConnectorConfigFieldDto> ConfigFields,
    IReadOnlyList<string> Environments);

// ── Write requests (secrets are write-only; omitted keys keep their stored value) ──
public record CreateCredentialBundleRequest(
    string Ref,
    string Name,
    string ConnectorKind,
    Dictionary<string, string> Secrets);

public record UpdateCredentialBundleRequest(string? Name, Dictionary<string, string> Secrets);

public record RotateCredentialFieldRequest(string Field, string NewValue, int? PreviousTtlHours);

/// <summary>Result of the idempotent legacy-config lift (Spec 042 §13).</summary>
public record LiftLegacyFlutterwaveResult(
    Guid PartnerId,
    IReadOnlyList<string> BundleRefs,
    IReadOnlyList<Guid> ConnectorIds,
    int PayoutsBackfilled,
    int TransmissionsBackfilled);
