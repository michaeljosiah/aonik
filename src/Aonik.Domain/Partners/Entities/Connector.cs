using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Partners.Entities;

public class Connector : AuditableEntity, ITenantScoped
{
    public Guid ConnectorId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PartnerId { get; private set; }
    public string ConnectorType { get; private set; } = string.Empty;
    public string? CredentialsRef { get; private set; }
    public string ConfigJson { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;

    private Connector() { }

    public Connector(Guid tenantId, Guid partnerId, string connectorType)
    {
        ConnectorId = Id;
        TenantId = tenantId;
        PartnerId = partnerId;
        ConnectorType = connectorType;
        ConfigJson = "{}";
        Status = "Active";
    }

    public void UpdateCredentialsRef(string credentialsRef)
    {
        CredentialsRef = credentialsRef;
    }

    public void UpdateConfig(string configJson)
    {
        ConfigJson = configJson;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void Activate()
    {
        Status = "Active";
    }

    public void Deactivate()
    {
        Status = "Inactive";
    }
}
