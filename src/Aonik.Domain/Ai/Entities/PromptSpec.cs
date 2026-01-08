using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class PromptSpec : AuditableEntity
{
    public Guid PromptSpecId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;
    public string SystemTemplate { get; private set; } = string.Empty;
    public string DeveloperTemplate { get; private set; } = string.Empty;
    public string VariablesSchemaJson { get; private set; } = string.Empty;
    public string OutputSchemaJson { get; private set; } = string.Empty;
    public string? SafetyPolicyRef { get; private set; }
    public bool IsPublished { get; private set; }

    private PromptSpec() { }

    public PromptSpec(string name, string version, string systemTemplate, string developerTemplate)
    {
        PromptSpecId = Id;
        Name = name;
        Version = version;
        SystemTemplate = systemTemplate;
        DeveloperTemplate = developerTemplate;
        VariablesSchemaJson = "{}";
        OutputSchemaJson = "{}";
        IsPublished = false;
    }

    public void UpdateTemplates(string systemTemplate, string developerTemplate)
    {
        SystemTemplate = systemTemplate;
        DeveloperTemplate = developerTemplate;
    }

    public void UpdateSchemas(string variablesSchemaJson, string outputSchemaJson)
    {
        VariablesSchemaJson = variablesSchemaJson;
        OutputSchemaJson = outputSchemaJson;
    }

    public void UpdateSafetyPolicyRef(string safetyPolicyRef)
    {
        SafetyPolicyRef = safetyPolicyRef;
    }

    public void Publish()
    {
        IsPublished = true;
    }

    public void Unpublish()
    {
        IsPublished = false;
    }
}
