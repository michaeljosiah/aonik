using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Partners.Entities;

public class PayoutSchema : AuditableEntity
{
    public Guid PayoutSchemaId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string SchemaJson { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private PayoutSchema() { }

    public PayoutSchema(Guid tenantId, string name, string schemaJson)
    {
        PayoutSchemaId = Id;
        TenantId = tenantId;
        Name = name;
        SchemaJson = schemaJson;
        IsActive = true;
    }

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void UpdateSchema(string schemaJson)
    {
        SchemaJson = schemaJson;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
