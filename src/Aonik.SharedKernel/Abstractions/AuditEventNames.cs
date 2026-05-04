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
    public const string TenantDemoSeeded = "TenantDemoSeeded";
    public const string PermissionsSeeded = "PermissionsSeeded";
    public const string UserProvisioned = "UserProvisioned";
    public const string UserIdentityLinked = "UserIdentityLinked";
    public const string UserRoleAssigned = "UserRoleAssigned";
    public const string UserRoleRemoved = "UserRoleRemoved";
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

    public const string ScheduledJobCommandQueued = "ScheduledJobCommandQueued";
    public const string ScheduledJobCommandSucceeded = "ScheduledJobCommandSucceeded";
    public const string ScheduledJobCommandFailed = "ScheduledJobCommandFailed";
    public const string ScheduledJobRunSucceeded = "ScheduledJobRunSucceeded";
    public const string ScheduledJobRunFailed = "ScheduledJobRunFailed";
    public const string RuntimeServiceStartRequested = "RuntimeServiceStartRequested";
}
