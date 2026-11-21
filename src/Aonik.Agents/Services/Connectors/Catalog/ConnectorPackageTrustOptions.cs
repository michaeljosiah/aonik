namespace Aonik.Agents.Services.Connectors.Catalog;

/// <summary>Deployment-owned trust and artifact locations; tenant settings cannot add publishers or executable adapters.</summary>
public sealed class ConnectorPackageTrustOptions
{
    public const string SectionName = "Agents:ConnectorCatalog";
    public string ArtifactDirectory { get; set; } = string.Empty;
    public List<string> ArtifactFiles { get; set; } = [];
    public List<ConnectorPackagePublisher> TrustedPublishers { get; set; } = [];
}

public sealed record ConnectorPackagePublisher(string PublisherId, string KeyId, string PublicKeyPem);
