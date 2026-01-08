using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class ToolSpec : AuditableEntity
{
    public Guid ToolSpecId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Domain { get; private set; } = string.Empty;
    public string ContractJson { get; private set; } = string.Empty;
    public string? AuthScope { get; private set; }
    public string RateLimitsJson { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private ToolSpec() { }

    public ToolSpec(string name, string domain, string contractJson)
    {
        ToolSpecId = Id;
        Name = name;
        Domain = domain;
        ContractJson = contractJson;
        RateLimitsJson = "{}";
        IsActive = true;
    }

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void UpdateContract(string contractJson)
    {
        ContractJson = contractJson;
    }

    public void UpdateAuthScope(string authScope)
    {
        AuthScope = authScope;
    }

    public void UpdateRateLimits(string rateLimitsJson)
    {
        RateLimitsJson = rateLimitsJson;
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
