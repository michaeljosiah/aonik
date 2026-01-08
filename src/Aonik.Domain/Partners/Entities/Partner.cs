using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Partners.Entities;

public class Partner : AuditableEntity, ITenantScoped
{
    public Guid PartnerId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string CapabilitiesJson { get; private set; } = string.Empty;
    public string OperatingHoursJson { get; private set; } = string.Empty;

    private readonly List<PartnerBranch> _branches = new();
    public IReadOnlyCollection<PartnerBranch> Branches => _branches.AsReadOnly();

    private Partner() { }

    public Partner(Guid tenantId, string name)
    {
        PartnerId = Id;
        TenantId = tenantId;
        Name = name;
        Status = "Active";
        CapabilitiesJson = "{}";
        OperatingHoursJson = "{}";
    }

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void UpdateCapabilities(string capabilitiesJson)
    {
        CapabilitiesJson = capabilitiesJson;
    }

    public void UpdateOperatingHours(string operatingHoursJson)
    {
        OperatingHoursJson = operatingHoursJson;
    }

    public void AddBranch(PartnerBranch branch)
    {
        _branches.Add(branch);
    }
}
