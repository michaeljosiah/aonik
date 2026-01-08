using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class AiProvider : AuditableEntity
{
    public Guid AiProviderId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? AuthConfigRef { get; private set; }
    public string CapabilitiesJson { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private readonly List<AiModel> _models = new();
    public IReadOnlyCollection<AiModel> Models => _models.AsReadOnly();

    private AiProvider() { }

    public AiProvider(string name, string? authConfigRef = null)
    {
        AiProviderId = Id;
        Name = name;
        AuthConfigRef = authConfigRef;
        CapabilitiesJson = "{}";
        IsActive = true;
    }

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void UpdateAuthConfigRef(string authConfigRef)
    {
        AuthConfigRef = authConfigRef;
    }

    public void UpdateCapabilities(string capabilitiesJson)
    {
        CapabilitiesJson = capabilitiesJson;
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
