namespace Aonik.Platform.Settings;

public static class ObservabilitySettingNames
{
    /// <summary>
    /// Azure Application Insights Application ID for REST API queries.
    /// </summary>
    public const string AppInsightsAppId = "Observability.AppInsights.AppId";

    /// <summary>
    /// Azure Application Insights API key. Encrypted at rest.
    /// </summary>
    public const string AppInsightsApiKey = "Observability.AppInsights.ApiKey";
}
