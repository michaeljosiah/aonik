using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Partners.Entities;

public class PartnerBranch : AuditableEntity
{
    public Guid PartnerBranchId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PartnerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string MetadataJson { get; private set; } = string.Empty;

    private PartnerBranch() { }

    public PartnerBranch(Guid tenantId, Guid partnerId, string name, string country, string city)
    {
        PartnerBranchId = Id;
        TenantId = tenantId;
        PartnerId = partnerId;
        Name = name;
        Country = country;
        City = city;
        MetadataJson = "{}";
    }

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void UpdateLocation(string country, string city)
    {
        Country = country;
        City = city;
    }

    public void UpdateMetadata(string metadataJson)
    {
        MetadataJson = metadataJson;
    }
}
