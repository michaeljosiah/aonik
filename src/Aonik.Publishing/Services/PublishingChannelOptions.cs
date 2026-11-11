namespace Aonik.Publishing.Services;

public sealed class PublishingChannelOptions
{
    public Guid TenantId { get; set; }
    public bool Enabled { get; set; }
    public long MaxPackageBytes { get; set; }
    public string RightsDeclarationVersion { get; set; } = string.Empty;
    public bool NamespaceAdmissionEnabled { get; set; }
    public bool ReviewEnabled { get; set; }
    public string ReviewPolicyVersion { get; set; } = string.Empty;
    public HashSet<Guid> ReviewerUserIds { get; set; } = [];
    public HashSet<Guid> PublisherUserIds { get; set; } = [];
    public bool DeliveryEnabled { get; set; }
    public bool PublicCatalogueEnabled { get; set; }
    public string DeliveryPolicyVersion { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
    public string BaselineCatalogueJson { get; set; } = "";
    public string BaselineSha256 { get; set; } = "";
    public string BaselineEvidenceReference { get; set; } = "";
    public Dictionary<string, LegacyWorldOwnership> BaselineOwnership { get; set; } = [];
    public bool LegacyMirrorEnabled { get; set; }
    public string LegacyMirrorMode { get; set; } = "contents";
    public string LegacyMirrorReleaseTag { get; set; } = "";
    public string LegacyMirrorRepository { get; set; } = "";
    public string LegacyMirrorPath { get; set; } = "";
    public string LegacyMirrorBranch { get; set; } = "main";
}
