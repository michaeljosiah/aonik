namespace Aonik.SharedKernel.Abstractions.Settings;

/// <summary>
/// Well-known setting keys for observability (Application Insights)
/// integration. Used by Ai's trace query services and Infrastructure's
/// AppInsightsQueryService; Platform registers their schemas in
/// <c>SettingDefinitions</c>.
/// </summary>
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
