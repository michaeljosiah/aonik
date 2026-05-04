namespace Aonik.Infrastructure.Operations;

internal sealed class ContainerAppsRuntimeOptions
{
    public bool Enabled { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string WorkloadName { get; set; } = "aonik";
    public string ManagementBaseUrl { get; set; } = "https://management.azure.com/";
}
