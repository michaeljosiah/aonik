using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class EvalSuite : AuditableEntity
{
    public Guid EvalSuiteId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Domain { get; private set; } = string.Empty;
    public string ScenariosJson { get; private set; } = string.Empty;
    public string MetricsJson { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private EvalSuite() { }

    public EvalSuite(string name, string domain, string scenariosJson, string metricsJson)
    {
        EvalSuiteId = Id;
        Name = name;
        Domain = domain;
        ScenariosJson = scenariosJson;
        MetricsJson = metricsJson;
        IsActive = true;
    }

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void UpdateScenarios(string scenariosJson)
    {
        ScenariosJson = scenariosJson;
    }

    public void UpdateMetrics(string metricsJson)
    {
        MetricsJson = metricsJson;
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
