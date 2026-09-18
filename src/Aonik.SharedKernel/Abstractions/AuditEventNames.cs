namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Cross-module audit event name constants. Placed in SharedKernel because
/// multiple modules (Platform, Finance) log audit events with these names.
/// </summary>
public static class AuditEventNames
{
    public const string CustomerCreated = "CustomerCreated";
    public const string PartnerCreated = "PartnerCreated";
    public const string PartnerUpdated = "PartnerUpdated";
    public const string PartnerDeleted = "PartnerDeleted";
    public const string GatewaySettingsUpdated = "GatewaySettingsUpdated";
    public const string ConnectorCreated = "ConnectorCreated";
    public const string ConnectorUpdated = "ConnectorUpdated";
    public const string ConnectorDeleted = "ConnectorDeleted";
    public const string CredentialBundleCreated = "CredentialBundleCreated";
    public const string CredentialBundleUpdated = "CredentialBundleUpdated";
    public const string CredentialBundleRotated = "CredentialBundleRotated";
    public const string CustomerProfileUpdated = "CustomerProfileUpdated";
    public const string CustomerEmailUpdated = "CustomerEmailUpdated";
    public const string CustomerPasswordUpdated = "CustomerPasswordUpdated";
    public const string CustomerPhotoUpdated = "CustomerPhotoUpdated";
    public const string CustomerPhotoDeleted = "CustomerPhotoDeleted";
    public const string CurrentUserViewed = "CurrentUserViewed";
    public const string OnboardingActivated = "OnboardingActivated";
    public const string PartyCreated = "PartyCreated";
    public const string PartyLinked = "PartyLinked";
    public const string TenantActivated = "TenantActivated";
    public const string TenantBootstrapCreated = "TenantBootstrapCreated";
    public const string TenantCreated = "TenantCreated";
    public const string TenantDeactivated = "TenantDeactivated";
    public const string TenantProvisioned = "TenantProvisioned";
    public const string TenantUpdated = "TenantUpdated";
    public const string TenantFeaturesUpdated = "TenantFeaturesUpdated";
    public const string TenantModulesUpdated = "TenantModulesUpdated";

    /// <summary>
    /// Spec 097 §9 — an enablement was rejected because a provisioning contributor threw while the
    /// module was being switched on; nothing about the tenant's module set changed. Distinct from
    /// <see cref="TenantModulesUpdated"/> so operators can query failed attempts apart from applied changes.
    /// </summary>
    public const string TenantModulesProvisioningFailed = "TenantModulesProvisioningFailed";

    /// <summary>
    /// Spec 097 §12.1 — an approved proposal was not executed because its handler's module is
    /// disabled for the tenant; the proposal was moved to its terminal Failed state.
    /// </summary>
    public const string ProposalBlockedByModuleGate = "ProposalBlockedByModuleGate";
    public const string TenantDemoSeeded = "TenantDemoSeeded";
    public const string TenantDemoReversed = "TenantDemoReversed";
    public const string PermissionsSeeded = "PermissionsSeeded";
    public const string UserProvisioned = "UserProvisioned";
    public const string UserIdentityLinked = "UserIdentityLinked";
    public const string UserRoleAssigned = "UserRoleAssigned";
    public const string UserRoleRemoved = "UserRoleRemoved";
    public const string UserAccessDenied = "UserAccessDenied";
    public const string UserInvited = "UserInvited";
    public const string UserInviteEmailSent = "UserInviteEmailSent";
    public const string UserInviteAccepted = "UserInviteAccepted";
    public const string UserSessionsRevoked = "UserSessionsRevoked";
    public const string UserDeleted = "UserDeleted";
    public const string UserAuditRedacted = "UserAuditRedacted";
    public const string PasswordResetRequested = "PasswordResetRequested";
    public const string VerificationConfirmed = "VerificationConfirmed";
    public const string VerificationFailed = "VerificationFailed";
    public const string VerificationStarted = "VerificationStarted";

    public const string PricingQuoteCreated = "PricingQuoteCreated";

    public const string OrderCreated = "OrderCreated";
    public const string OrderItemAdded = "OrderItemAdded";
    public const string OrderItemUpdated = "OrderItemUpdated";
    public const string OrderItemRemoved = "OrderItemRemoved";
    public const string OrderSubmitted = "OrderSubmitted";
    public const string OrderCancelled = "OrderCancelled";
    public const string PartyScreened = "PartyScreened";
    public const string ComplianceCaseCreated = "ComplianceCaseCreated";

    public const string TraceAnalysisRequested = "TraceAnalysisRequested";
    public const string TraceAnalysisFailed = "TraceAnalysisFailed";

    public const string ScheduledJobCommandQueued = "ScheduledJobCommandQueued";
    public const string ScheduledJobCommandSucceeded = "ScheduledJobCommandSucceeded";
    public const string ScheduledJobCommandFailed = "ScheduledJobCommandFailed";
    public const string ScheduledJobRunSucceeded = "ScheduledJobRunSucceeded";
    public const string ScheduledJobRunFailed = "ScheduledJobRunFailed";
    public const string RuntimeServiceStartRequested = "RuntimeServiceStartRequested";
}
