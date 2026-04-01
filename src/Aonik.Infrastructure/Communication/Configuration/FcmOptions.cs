namespace Aonik.Infrastructure.Communication.Configuration;

public class FcmOptions
{
    public string ProjectId { get; set; } = string.Empty;
    public string ServiceAccountJson { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ProjectId)
        && !string.IsNullOrWhiteSpace(ServiceAccountJson);
}
