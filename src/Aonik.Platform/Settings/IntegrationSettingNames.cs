namespace Aonik.Platform.Settings;

public static class IntegrationSettingNames
{
    public const string GmailEnabled = "Integrations.Gmail.Enabled";
    public const string GmailAllowedActions = "Integrations.Gmail.AllowedActions";
    public const string GmailAllowUserConnections = "Integrations.Gmail.AllowUserConnections";
    public const string GmailAllowSelfService = "Integrations.Gmail.AllowSelfService";
    public const string GmailAllowWorkflowConnections = "Integrations.Gmail.AllowWorkflowConnections";
    public const string GmailDefaultConnectionId = "Integrations.Gmail.DefaultConnectionId";
    public const string RequestReviewerRoleId = "Integrations.RequestReviewerRoleId";
    public const string RequestExpiryDays = "Integrations.RequestExpiryDays";
    public static readonly IReadOnlyList<string> All = [GmailEnabled, GmailAllowedActions, GmailAllowUserConnections,
        GmailAllowSelfService, GmailAllowWorkflowConnections, GmailDefaultConnectionId, RequestReviewerRoleId, RequestExpiryDays];
}
