using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using Aonik.Platform.Persistence;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Platform.Services.Integrations;

internal sealed record ConnectorPolicy(string Code, IReadOnlyList<ConnectorActionRef> AllowedActions,
    bool AllowUserConnections, bool AllowSelfService, bool AllowWorkflowConnections, Guid? ReviewerRoleId, int RequestExpiryDays,
    bool AllowPermanentDelete = false);

/// <summary>Intersects explicit platform and tenant restrictions. User preferences never enter an authorization decision.</summary>
internal sealed class ConnectorPolicyService(PlatformDbContext database, ITenantProvider tenantProvider, IConfiguration configuration, ConnectorCatalog? catalog = null)
{
    public async Task<ConnectorPolicy> GetAsync(string connectorKey, CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        bool gmail = connectorKey == GmailActionKeys.Connector;
        ConnectorDescriptor? serviceConnector = gmail ? null : catalog?.Find(connectorKey);
        if (!gmail && serviceConnector?.AuthenticationKind != "ServiceAccount") return new("ConnectorUnavailable", [], false, false, false, null, 7);
        List<Entities.Settings.Setting> values = await database.Settings.AsNoTracking().Where(setting => setting.Key.StartsWith("Integrations.")
            && ((setting.Scope == SettingScope.Global && setting.TenantId == null && setting.UserId == null)
                || (setting.Scope == SettingScope.Tenant && setting.TenantId == tenantId && setting.UserId == null))).ToListAsync(cancellationToken);
        string? Stored(string key, SettingScope scope) => values.SingleOrDefault(setting => setting.Key == key && setting.Scope == scope)?.Value;
        string? Host(string key) => configuration[$"Settings:{key}"] ?? configuration[key.Replace('.', ':')];
        bool Flag(string key, bool tenantDefault) => Parse(Stored(key, SettingScope.Global) ?? Host(key), true)
            && Parse(Stored(key, SettingScope.Tenant), tenantDefault);
        string enabledSetting = gmail ? IntegrationSettingNames.GmailEnabled : IntegrationSettingNames.CuratedEnabled;
        string actionsSetting = gmail ? IntegrationSettingNames.GmailAllowedActions : IntegrationSettingNames.CuratedAllowedActions;
        string? tenantEnabled = Stored(enabledSetting, SettingScope.Tenant);
        string code = !Parse(Stored(enabledSetting, SettingScope.Global) ?? Host(enabledSetting), true)
            ? "ForbiddenByPolicy" : tenantEnabled is null ? "TenantNotEnabled" : Parse(tenantEnabled, false) ? "Ready" : "ForbiddenByPolicy";
        try
        {
            ConnectorActionRef[] defaults = gmail ? GmailActionKeys.ReadActions.Select(key => new ConnectorActionRef(key, 1)).ToArray()
                : serviceConnector!.Actions.Select(action => new ConnectorActionRef(action.Key, action.Version)).ToArray();
            IReadOnlyList<ConnectorActionRef> platformActions = Actions(Stored(actionsSetting, SettingScope.Global) ?? Host(actionsSetting), defaults);
            IReadOnlyList<ConnectorActionRef> tenantActions = Actions(Stored(actionsSetting, SettingScope.Tenant), defaults);
            string? role = Stored(IntegrationSettingNames.RequestReviewerRoleId, SettingScope.Tenant);
            string? days = Stored(IntegrationSettingNames.RequestExpiryDays, SettingScope.Tenant)
                ?? Stored(IntegrationSettingNames.RequestExpiryDays, SettingScope.Global) ?? Host(IntegrationSettingNames.RequestExpiryDays);
            if (days is not null && (!int.TryParse(days, out int count) || count is < 1 or > 30))
                return new("ForbiddenByPolicy", [], false, false, false, null, 7);
            return new(code, tenantActions.Intersect(platformActions).Distinct().ToList(),
                gmail && Flag(IntegrationSettingNames.GmailAllowUserConnections, true), gmail && Flag(IntegrationSettingNames.GmailAllowSelfService, false),
                Flag(gmail ? IntegrationSettingNames.GmailAllowWorkflowConnections : IntegrationSettingNames.CuratedAllowWorkflowConnections, false), Guid.TryParse(role, out Guid roleId) ? roleId : null,
                days is null ? 7 : int.Parse(days, System.Globalization.CultureInfo.InvariantCulture),
                gmail && Parse(Stored(IntegrationSettingNames.GmailAllowPermanentDelete, SettingScope.Global) ?? Host(IntegrationSettingNames.GmailAllowPermanentDelete), false)
                    && Parse(Stored(IntegrationSettingNames.GmailAllowPermanentDelete, SettingScope.Tenant), false));
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or ArgumentException)
        {
            return new("ForbiddenByPolicy", [], false, false, false, null, 7);
        }
    }

    public async Task<IReadOnlyList<Guid>> PreferredConnectionsAsync(Guid userId, string connectorKey, CancellationToken cancellationToken = default)
    {
        if (connectorKey != GmailActionKeys.Connector) return [];
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        var preferences = await database.Settings.AsNoTracking().Where(setting => setting.Key == IntegrationSettingNames.GmailDefaultConnectionId
            && ((setting.Scope == SettingScope.User && setting.TenantId == tenantId && setting.UserId == userId)
                || (setting.Scope == SettingScope.Tenant && setting.TenantId == tenantId && setting.UserId == null)
                || (setting.Scope == SettingScope.Global && setting.TenantId == null && setting.UserId == null)))
            .OrderByDescending(setting => setting.Scope).Select(setting => setting.Value).ToListAsync(cancellationToken);
        return preferences.Select(value => Guid.TryParse(value, out Guid id) ? (Guid?)id : null).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
    }

    private static bool Parse(string? value, bool fallback) => value is null ? fallback : bool.TryParse(value, out bool enabled) && enabled;
    private static IReadOnlyList<ConnectorActionRef> Actions(string? value, IReadOnlyList<ConnectorActionRef> fallback)
    {
        if (value is null) return fallback;
        List<ConnectorActionRef> actions = ConnectorJson.Read<List<ConnectorActionRef>>(value);
        if (actions.Count > 100 || actions.Any(action => action is null || string.IsNullOrWhiteSpace(action.Key) || action.Version < 1))
            throw new ArgumentException("Invalid connector action policy.");
        return actions;
    }
}
