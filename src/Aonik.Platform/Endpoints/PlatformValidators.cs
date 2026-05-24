using Aonik.Platform.Contracts.Api.Bootstrap;
using Aonik.Platform.Contracts.Api.Features;
using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Api.Operations;
using Aonik.Platform.Contracts.Api.Party;
using Aonik.Platform.Contracts.Api.PersonalFinance;
using Aonik.Platform.Contracts.Api.ReferenceData;
using Aonik.Platform.Contracts.Api.Registrations;
using Aonik.Platform.Contracts.Api.Seeding;
using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Models.Autonumbering;
using Aonik.Platform.Contracts.Models.Cms;
using Aonik.Platform.Contracts.Models.Customers;
using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

// ── Disambiguating aliases ───────────────────────────────────────────
// Several DTOs share names across Contracts.Api.* and Contracts.Models.*.
// The missing ones in the architecture-test report are the Api.* variants —
// alias them explicitly to avoid `using` collisions.
using AddDocumentFileRequest = Aonik.Platform.Contracts.Api.Compliance.AddDocumentFileRequest;
using AddDocumentUsageRequest = Aonik.Platform.Contracts.Api.Compliance.AddDocumentUsageRequest;
using AddDocumentVerificationRequest = Aonik.Platform.Contracts.Api.Compliance.AddDocumentVerificationRequest;
using CreateDocumentRequest = Aonik.Platform.Contracts.Api.Compliance.CreateDocumentRequest;
using ListDocumentsRequest = Aonik.Platform.Contracts.Models.Compliance.ListDocumentsRequest;
using AssignUserRoleRequest = Aonik.Platform.Contracts.Api.Identity.AssignUserRoleRequest;
using ConfirmEmailVerificationRequest = Aonik.Platform.Contracts.Api.Identity.ConfirmEmailVerificationRequest;
using ConfirmPhoneVerificationRequest = Aonik.Platform.Contracts.Api.Identity.ConfirmPhoneVerificationRequest;
using ForgotPasswordRequestDto = Aonik.Platform.Contracts.Api.Identity.ForgotPasswordRequestDto;
using RegisterNotificationDeviceRequestDto = Aonik.Platform.Contracts.Api.Identity.RegisterNotificationDeviceRequestDto;
using StartEmailVerificationRequest = Aonik.Platform.Contracts.Api.Identity.StartEmailVerificationRequest;
using StartPhoneVerificationRequest = Aonik.Platform.Contracts.Api.Identity.StartPhoneVerificationRequest;
using TokenRequestDto = Aonik.Platform.Contracts.Api.Identity.TokenRequestDto;
using UpdateCustomerEmailRequest = Aonik.Platform.Contracts.Api.Identity.UpdateCustomerEmailRequest;
using UpdateCustomerPasswordRequest = Aonik.Platform.Contracts.Api.Identity.UpdateCustomerPasswordRequest;
using UpdateCustomerProfileRequest = Aonik.Platform.Contracts.Api.Identity.UpdateCustomerProfileRequest;
using UpdateMarketingPreferencesRequest = Aonik.Platform.Contracts.Api.Identity.UpdateMarketingPreferencesRequest;
using UpdateNotificationPreferencesRequest = Aonik.Platform.Contracts.Api.Identity.UpdateNotificationPreferencesRequest;
using CreateRoleRequest = Aonik.Platform.Contracts.Models.Identity.CreateRoleRequest;
using CreateTenantRequest = Aonik.Platform.Contracts.Models.Identity.CreateTenantRequest;
using CommunicationProviderSettingsUpdateRequest = Aonik.Platform.Contracts.Api.Settings.CommunicationProviderSettingsUpdateRequest;
using SendCommunicationTestRequest = Aonik.Platform.Contracts.Api.Settings.SendCommunicationTestRequest;
using InviteUserRequest = Aonik.Platform.Contracts.Models.Identity.InviteUserRequest;
using ListRolesRequest = Aonik.Platform.Contracts.Models.Identity.ListRolesRequest;
using ListTenantsRequest = Aonik.Platform.Contracts.Models.Identity.ListTenantsRequest;
using ListUsersRequest = Aonik.Platform.Contracts.Models.Identity.ListUsersRequest;
using UpdateRolePermissionsRequest = Aonik.Platform.Contracts.Models.Identity.UpdateRolePermissionsRequest;
using UpdateRoleRequest = Aonik.Platform.Contracts.Models.Identity.UpdateRoleRequest;
using UpdateTenantRequest = Aonik.Platform.Contracts.Models.Identity.UpdateTenantRequest;
using UpdateUserProfileRequest = Aonik.Platform.Contracts.Models.Identity.UpdateUserProfileRequest;
using UpdateUserRolesRequest = Aonik.Platform.Contracts.Models.Identity.UpdateUserRolesRequest;
using AcceptInviteRequest = Aonik.Platform.Contracts.Models.Identity.AcceptInviteRequest;
using DeleteUserRequest = Aonik.Platform.Contracts.Models.Identity.DeleteUserRequest;
using RevokeUserSessionsRequest = Aonik.Platform.Contracts.Models.Identity.RevokeUserSessionsRequest;

namespace Aonik.Platform.Endpoints;

// ────────────────────────────────────────────────────────────────────
// Validators for the Platform module's request DTOs.
// ────────────────────────────────────────────────────────────────────

// ── Bootstrap ───────────────────────────────────────────────────────

public sealed class BootstrapInitializeRequestValidator : Validator<BootstrapInitializeRequest>
{
    public BootstrapInitializeRequestValidator()
    {
        RuleFor(x => x.SetupSecret).RequiredText(512);
        RuleFor(x => x.OwnerEmail).Email();
        RuleFor(x => x.OwnerDisplayName).MaximumLength(256);
    }
}

// ── Compliance ──────────────────────────────────────────────────────

public sealed class CreateDocumentRequestValidator : Validator<CreateDocumentRequest>
{
    public CreateDocumentRequestValidator()
    {
        RuleFor(x => x.OwnerPartyId).RequiredId();
        RuleFor(x => x.DocumentType).RequiredText(64);
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.IssuerName).MaximumLength(256);
        RuleFor(x => x.CountryCode)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
        RuleFor(x => x.ReferenceNumber).MaximumLength(128);
        RuleFor(x => x.Tags)
            .NotNull().WithMessage("Tags is required (may be empty).")
            .Must(t => t == null || t.Count <= 50);
        RuleForEach(x => x.Tags).MaximumLength(64);
        RuleFor(x => x.AttributesJson).MaximumLength(64_000);
    }
}

public sealed class AddDocumentFileRequestValidator : Validator<AddDocumentFileRequest>
{
    public AddDocumentFileRequestValidator()
    {
        RuleFor(x => x.StorageProvider).RequiredText(64);
        RuleFor(x => x.StorageContainer).MaximumLength(256);
        RuleFor(x => x.StorageKey).RequiredText(2048);
        RuleFor(x => x.ContentType).RequiredText(128);
        RuleFor(x => x.FileName).MaximumLength(512);
        RuleFor(x => x.FileSizeBytes)
            .InclusiveBetween(0, 5L * 1024 * 1024 * 1024) // 5GB cap
            .When(x => x.FileSizeBytes.HasValue);
        RuleFor(x => x.Sha256)
            .Length(64).Matches("^[A-Fa-f0-9]{64}$").WithMessage("Sha256 must be 64 hex characters.")
            .When(x => !string.IsNullOrEmpty(x.Sha256));
        RuleFor(x => x.Side).MaximumLength(32);
        RuleFor(x => x.CapturedBy).MaximumLength(128);
        RuleFor(x => x.MetadataJson).MaximumLength(16_000);
    }
}

public sealed class AddDocumentUsageRequestValidator : Validator<AddDocumentUsageRequest>
{
    public AddDocumentUsageRequestValidator()
    {
        RuleFor(x => x.OwnerPartyId).RequiredId();
        RuleFor(x => x.Purpose).RequiredText(128);
        RuleFor(x => x.RelatedEntityType).MaximumLength(64);
        RuleFor(x => x.RelatedEntityId).ValidIdWhenSupplied();
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.Notes).MaximumLength(2048);
    }
}

public sealed class AddDocumentVerificationRequestValidator : Validator<AddDocumentVerificationRequest>
{
    public AddDocumentVerificationRequestValidator()
    {
        RuleFor(x => x.Decision).RequiredText(64);
        RuleFor(x => x.DecisionReasonCode).MaximumLength(64);
        RuleFor(x => x.DecisionNotes).MaximumLength(2048);
        RuleFor(x => x.VerifierType).RequiredText(64);
        RuleFor(x => x.VerifierId).MaximumLength(128);
        RuleFor(x => x.AiRunId).ValidIdWhenSupplied();
    }
}

public sealed class ListDocumentsRequestValidator : Validator<ListDocumentsRequest>
{
    public ListDocumentsRequestValidator()
    {
        RuleFor(x => x.PageNumber).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 200);
        RuleFor(x => x.DocumentType).MaximumLength(64);
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.OwnerPartyId).ValidIdWhenSupplied();
        RuleFor(x => x.CountryCode)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
        RuleFor(x => x.Tag).MaximumLength(64);
        RuleFor(x => x.UsagePurpose).MaximumLength(128);
    }
}

// ── Features ────────────────────────────────────────────────────────

public sealed class TenantFeatureUpdateRequestValidator : Validator<TenantFeatureUpdateRequest>
{
    public TenantFeatureUpdateRequestValidator()
    {
        RuleFor(x => x.Features)
            .NotNull().WithMessage("Features collection is required.")
            .Must(f => f != null && f.Count <= 1000)
            .WithMessage("Features may include at most 1000 entries.");
        RuleForEach(x => x.Features).SetValidator(new TenantFeatureToggleRequestValidator());
    }
}

public sealed class TenantFeatureToggleRequestValidator : Validator<TenantFeatureToggleRequest>
{
    public TenantFeatureToggleRequestValidator()
    {
        RuleFor(x => x.FeatureName).RequiredText(256);
        RuleFor(x => x.Reason).MaximumLength(2048);
    }
}

// ── Identity (Auth) ─────────────────────────────────────────────────

public sealed class TokenRequestDtoValidator : Validator<TokenRequestDto>
{
    public TokenRequestDtoValidator()
    {
        RuleFor(x => x.GrantType).RequiredText(64);
        RuleFor(x => x.ClientId).RequiredText(256);
        RuleFor(x => x.Username).MaximumLength(254);
        RuleFor(x => x.Scope).MaximumLength(2048);
        RuleFor(x => x.RedirectUri).MaximumLength(2048);
        RuleFor(x => x.CodeVerifier).MaximumLength(2048);
        RuleFor(x => x.AuthorizationCode).MaximumLength(2048);
        RuleFor(x => x.RefreshToken).MaximumLength(8192);
    }
}

public sealed class ForgotPasswordRequestDtoValidator : Validator<ForgotPasswordRequestDto>
{
    public ForgotPasswordRequestDtoValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.TenantId).RequiredId();
    }
}

public sealed class StartEmailVerificationRequestValidator : Validator<StartEmailVerificationRequest>
{
    public StartEmailVerificationRequestValidator() => RuleFor(x => x.Email).Email();
}

public sealed class StartPhoneVerificationRequestValidator : Validator<StartPhoneVerificationRequest>
{
    public StartPhoneVerificationRequestValidator() => RuleFor(x => x.Phone).PhoneE164();
}

public sealed class ConfirmEmailVerificationRequestValidator : Validator<ConfirmEmailVerificationRequest>
{
    public ConfirmEmailVerificationRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .Length(4, 12).WithMessage("Code must be between 4 and 12 characters.");
    }
}

public sealed class ConfirmPhoneVerificationRequestValidator : Validator<ConfirmPhoneVerificationRequest>
{
    public ConfirmPhoneVerificationRequestValidator()
    {
        RuleFor(x => x.Phone).PhoneE164();
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .Length(4, 12).WithMessage("Code must be between 4 and 12 characters.");
    }
}

public sealed class UpdateCustomerProfileRequestValidator : Validator<UpdateCustomerProfileRequest>
{
    public UpdateCustomerProfileRequestValidator()
    {
        RuleFor(x => x.FirstName).MaximumLength(128);
        RuleFor(x => x.LastName).MaximumLength(128);
        RuleFor(x => x.Title).MaximumLength(64);
        RuleFor(x => x.Phone)
            .Matches(@"^\+[1-9]\d{7,14}$").WithMessage("Phone must be in E.164 format.")
            .When(x => !string.IsNullOrEmpty(x.Phone));
        RuleFor(x => x.CountryCode)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
    }
}

public sealed class UpdateCustomerEmailRequestValidator : Validator<UpdateCustomerEmailRequest>
{
    public UpdateCustomerEmailRequestValidator()
    {
        RuleFor(x => x.CurrentEmail).Email();
        RuleFor(x => x.NewEmail).Email();
        RuleFor(x => x.Password).RequiredText(256);
    }
}

public sealed class UpdateCustomerPasswordRequestValidator : Validator<UpdateCustomerPasswordRequest>
{
    public UpdateCustomerPasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).RequiredText(256);
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("New password must be at least 8 characters.")
            .MaximumLength(256);
    }
}

public sealed class UpdateNotificationPreferencesRequestValidator : Validator<UpdateNotificationPreferencesRequest>
{
    public UpdateNotificationPreferencesRequestValidator()
    {
        RuleFor(x => x.Email).Email().When(x => !string.IsNullOrEmpty(x.Email));
    }
}

public sealed class UpdateMarketingPreferencesRequestValidator : Validator<UpdateMarketingPreferencesRequest>
{
    public UpdateMarketingPreferencesRequestValidator()
    {
        RuleFor(x => x.Email).Email().When(x => !string.IsNullOrEmpty(x.Email));
    }
}

public sealed class RegisterNotificationDeviceRequestDtoValidator : Validator<RegisterNotificationDeviceRequestDto>
{
    public RegisterNotificationDeviceRequestDtoValidator()
    {
        RuleFor(x => x.Provider).RequiredText(64);
        RuleFor(x => x.Platform).RequiredText(32);
        RuleFor(x => x.DeviceToken).RequiredText(2048);
    }
}

public sealed class AssignUserRoleRequestValidator : Validator<AssignUserRoleRequest>
{
    public AssignUserRoleRequestValidator() => RuleFor(x => x.RoleId).RequiredId();
}

// ── Identity (Tenants) ──────────────────────────────────────────────

public sealed class CreateTenantRequestValidator : Validator<CreateTenantRequest>
{
    private static readonly string[] Environments = ["Dev", "Test", "Staging", "Prod"];

    public CreateTenantRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(256);
        RuleFor(x => x.Environment)
            .NotEmpty()
            .Must(e => Environments.Contains(e))
            .WithMessage($"Environment must be one of: {string.Join(", ", Environments)}.");
        RuleFor(x => x.DefaultCurrency).CurrencyCode();
        RuleFor(x => x.SupportedCountries)
            .NotNull().WithMessage("SupportedCountries is required.")
            .Must(c => c != null && c.Length >= 1).WithMessage("At least one supported country is required.")
            .Must(c => c == null || c.Length <= 250).WithMessage("Too many supported countries.");
        RuleForEach(x => x.SupportedCountries)
            .Length(2).Matches("^[A-Z]{2}$").WithMessage("Country codes must be 2 uppercase letters.");
        RuleFor(x => x.OwnerEmail).Email();
        RuleFor(x => x.OwnerDisplayName).MaximumLength(256);
        RuleForEach(x => x.SupportedCurrencies)
            .Length(3).Matches("^[A-Z]{3}$").WithMessage("Currency codes must be 3 uppercase letters.");
        RuleForEach(x => x.AllowedOriginCountries)
            .Length(2).Matches("^[A-Z]{2}$");
        RuleForEach(x => x.AllowedDestinationCountries)
            .Length(2).Matches("^[A-Z]{2}$");
    }
}

public sealed class UpdateTenantRequestValidator : Validator<UpdateTenantRequest>
{
    public UpdateTenantRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(256);
        RuleFor(x => x.DefaultCurrency)
            .Length(3).Matches("^[A-Z]{3}$")
            .When(x => !string.IsNullOrEmpty(x.DefaultCurrency));
        RuleFor(x => x.Environment).MaximumLength(64);
        RuleFor(x => x.LogoUrl).MaximumLength(2048);
        RuleFor(x => x.Industry).MaximumLength(128);
        RuleFor(x => x.CompanySize).MaximumLength(64);
        RuleFor(x => x.Website).MaximumLength(2048);
        RuleFor(x => x.ContactEmail).Email().When(x => !string.IsNullOrEmpty(x.ContactEmail));
        RuleFor(x => x.ContactMobile)
            .Matches(@"^\+[1-9]\d{7,14}$").WithMessage("ContactMobile must be in E.164 format.")
            .When(x => !string.IsNullOrEmpty(x.ContactMobile));
        RuleFor(x => x.AddressLine1).MaximumLength(256);
        RuleFor(x => x.AddressLine2).MaximumLength(256);
        RuleFor(x => x.City).MaximumLength(128);
        RuleFor(x => x.StateProvince).MaximumLength(128);
        RuleFor(x => x.PostalCode).MaximumLength(32);
        RuleFor(x => x.Country)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.Country));
        RuleFor(x => x.SetupStep)
            .InclusiveBetween(0, 100)
            .When(x => x.SetupStep.HasValue);
    }
}

public sealed class ListTenantsRequestValidator : Validator<ListTenantsRequest>
{
    public ListTenantsRequestValidator()
    {
        RuleFor(x => x.PageNumber).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 200);
        RuleFor(x => x.Environment).MaximumLength(64);
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.NameFilter).MaximumLength(256);
    }
}

// ── Identity (Access Management) ────────────────────────────────────

public sealed class ListUsersRequestValidator : Validator<ListUsersRequest>
{
    public ListUsersRequestValidator()
    {
        RuleFor(x => x.PageNumber).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 200);
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.Search).MaximumLength(256);
    }
}

public sealed class ListRolesRequestValidator : Validator<ListRolesRequest>
{
    public ListRolesRequestValidator()
    {
        RuleFor(x => x.PageNumber).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 200);
        RuleFor(x => x.Search).MaximumLength(256);
    }
}

public sealed class InviteUserRequestValidator : Validator<InviteUserRequest>
{
    public InviteUserRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.DisplayName).MaximumLength(256);
        RuleFor(x => x.RoleIds)
            .Must(r => r == null || r.Count <= 50).WithMessage("RoleIds may include at most 50 entries.");
        RuleForEach(x => x.RoleIds).RequiredId();
        // Spec 026 follow-up — optional party linkage; when supplied,
        // must be a non-empty GUID. The service validates tenant scope
        // (the party must belong to the current tenant).
        RuleFor(x => x.PartyId).ValidIdWhenSupplied();
    }
}

// Spec 026 Part 1 — accept invite (anonymous-but-authenticated)
public sealed class AcceptInviteRequestValidator : Validator<AcceptInviteRequest>
{
    public AcceptInviteRequestValidator()
    {
        RuleFor(x => x.InviteToken)
            .NotEmpty().WithMessage("Invite token is required.")
            .MaximumLength(64);
    }
}

// Spec 026 Part 2 — destructive delete
public sealed class DeleteUserRequestValidator : Validator<DeleteUserRequest>
{
    public DeleteUserRequestValidator()
    {
        RuleFor(x => x.EmailConfirmation).Email();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MinimumLength(10).WithMessage("Reason must be at least 10 characters.")
            .MaximumLength(500);
    }
}

// Spec 026 Part 3 — revoke active sessions
public sealed class RevokeUserSessionsRequestValidator : Validator<RevokeUserSessionsRequest>
{
    public RevokeUserSessionsRequestValidator()
        => RuleFor(x => x.Reason).MaximumLength(200);
}

public sealed class UpdateUserRolesRequestValidator : Validator<UpdateUserRolesRequest>
{
    public UpdateUserRolesRequestValidator()
    {
        RuleFor(x => x.RoleIds)
            .NotNull().WithMessage("RoleIds is required (may be empty).")
            .Must(r => r != null && r.Count <= 50);
        RuleForEach(x => x.RoleIds).RequiredId();
    }
}

public sealed class CreateRoleRequestValidator : Validator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(128);
        RuleFor(x => x.Description).MaximumLength(2048);
        RuleFor(x => x.PermissionKeys)
            .NotNull().WithMessage("PermissionKeys is required (may be empty).")
            .Must(p => p != null && p.Count <= 500);
        RuleForEach(x => x.PermissionKeys).MaximumLength(128);
    }
}

public sealed class UpdateRoleRequestValidator : Validator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(2048);
    }
}

public sealed class UpdateRolePermissionsRequestValidator : Validator<UpdateRolePermissionsRequest>
{
    public UpdateRolePermissionsRequestValidator()
    {
        RuleFor(x => x.PermissionKeys)
            .NotNull().WithMessage("PermissionKeys is required (may be empty).")
            .Must(p => p != null && p.Count <= 500);
        RuleForEach(x => x.PermissionKeys).MaximumLength(128);
    }
}

public sealed class UpdateUserProfileRequestValidator : Validator<UpdateUserProfileRequest>
{
    public UpdateUserProfileRequestValidator()
    {
        RuleFor(x => x.FirstName).MaximumLength(128);
        RuleFor(x => x.LastName).MaximumLength(128);
        RuleFor(x => x.Title).MaximumLength(64);
        RuleFor(x => x.CountryCode)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
        RuleFor(x => x.Nationality).MaximumLength(64);
        RuleFor(x => x.Occupation).MaximumLength(128);
    }
}

// ── Jobs ────────────────────────────────────────────────────────────

public sealed class UpdateScheduledJobConfigurationRequestValidator : Validator<UpdateScheduledJobConfigurationRequest>
{
    public UpdateScheduledJobConfigurationRequestValidator()
        => RuleFor(x => x.ConfigurationJson).MaximumLength(64_000);
}

// ── Observability ───────────────────────────────────────────────────

public sealed class ExplainObservabilityPanelRequestValidator : Validator<ExplainObservabilityPanelRequest>
{
    public ExplainObservabilityPanelRequestValidator()
        => RuleFor(x => x.PanelKind).RequiredText(64);
}

public sealed class ExplainTraceRequestValidator : Validator<ExplainTraceRequest>
{
    public ExplainTraceRequestValidator()
        => RuleFor(x => x.TraceId).RequiredText(128);
}

public sealed class ObservabilityQueryRequestValidator : Validator<ObservabilityQueryRequest>
{
    public ObservabilityQueryRequestValidator()
    {
        RuleFor(x => x.TimeRange).RequiredText(32);
        RuleFor(x => x.OperationId).MaximumLength(128);
        RuleFor(x => x.Severity)
            .Must(s => s is null or "debug" or "info" or "warn" or "error" or "all")
            .WithMessage("Severity must be one of: debug, info, warn, error, all.");
    }
}

// ── Operations / Alerts ─────────────────────────────────────────────

public sealed class AzureMonitorAlertWebhookRequestValidator : Validator<AzureMonitorAlertWebhookRequest>
{
    public AzureMonitorAlertWebhookRequestValidator()
        => RuleFor(x => x.SchemaId).MaximumLength(128);
}

// ── Party ───────────────────────────────────────────────────────────

public sealed class CreatePartyRequestValidator : Validator<CreatePartyRequest>
{
    public CreatePartyRequestValidator()
    {
        RuleFor(x => x.DisplayName).RequiredText(256);
        RuleFor(x => x.PartyType)
            .NotEmpty()
            .Must(t => t is "Person" or "Organization")
            .WithMessage("PartyType must be 'Person' or 'Organization'.");
        RuleFor(x => x.FirstName).MaximumLength(128);
        RuleFor(x => x.LastName).MaximumLength(128);
        RuleFor(x => x.Phone)
            .Matches(@"^\+[1-9]\d{7,14}$").WithMessage("Phone must be in E.164 format.")
            .When(x => !string.IsNullOrEmpty(x.Phone));
        RuleFor(x => x.Email).Email().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.CountryCode)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
    }
}

public sealed class CreateRelatedPartyRequestValidator : Validator<CreateRelatedPartyRequest>
{
    public CreateRelatedPartyRequestValidator()
    {
        RuleFor(x => x.RelationshipTypeCode).RequiredText(64);
        RuleFor(x => x.DisplayName).RequiredText(256);
        RuleFor(x => x.FirstName).MaximumLength(128);
        RuleFor(x => x.LastName).MaximumLength(128);
        RuleFor(x => x.Phone)
            .Matches(@"^\+[1-9]\d{7,14}$").WithMessage("Phone must be in E.164 format.")
            .When(x => !string.IsNullOrEmpty(x.Phone));
        RuleFor(x => x.Email).Email().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.CountryCode)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
        RuleFor(x => x.Notes).MaximumLength(2048);
    }
}

// ── PersonalFinance (Payabo Setup) ──────────────────────────────────

public sealed class PayaboSetupProfileRequestValidator : Validator<PayaboSetupProfileRequest>
{
    public PayaboSetupProfileRequestValidator()
    {
        RuleFor(x => x.SelectedUseCases)
            .NotNull().Must(c => c == null || c.Count <= 50);
        RuleForEach(x => x.SelectedUseCases).MaximumLength(64);
        RuleFor(x => x.AccountSourceTypes)
            .NotNull().Must(c => c == null || c.Count <= 50);
        RuleForEach(x => x.AccountSourceTypes).MaximumLength(64);
        RuleFor(x => x.ConnectChoice).MaximumLength(64);
        RuleFor(x => x.Responsibilities)
            .NotNull().Must(c => c == null || c.Count <= 50);
        RuleForEach(x => x.Responsibilities).MaximumLength(64);
        RuleFor(x => x.SupportType).MaximumLength(64);
        RuleFor(x => x.FinancialGoals)
            .NotNull().Must(c => c == null || c.Count <= 50);
        RuleForEach(x => x.FinancialGoals).MaximumLength(64);
    }
}

// ── Reference Data ──────────────────────────────────────────────────

public sealed class ReferenceDataItemUpsertRequestValidator : Validator<ReferenceDataItemUpsertRequest>
{
    public ReferenceDataItemUpsertRequestValidator()
    {
        RuleFor(x => x.DisplayName).RequiredText(256);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 1_000_000);
    }
}

// ── Registrations ───────────────────────────────────────────────────

public sealed class IndividualRegistrationRequestValidator : Validator<IndividualRegistrationRequest>
{
    public IndividualRegistrationRequestValidator()
    {
        RuleFor(x => x.TenantId).ValidIdWhenSupplied();
        RuleFor(x => x.RegistrationCountry)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.RegistrationCountry));
        RuleFor(x => x.Title).MaximumLength(64);
        RuleFor(x => x.FirstName).RequiredText(128);
        RuleFor(x => x.LastName).RequiredText(128);
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Phone)
            .Matches(@"^\+[1-9]\d{7,14}$").WithMessage("Phone must be in E.164 format.")
            .When(x => !string.IsNullOrEmpty(x.Phone));
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(256);
    }
}

public sealed class SendRegistrationPhoneOtpRequestValidator : Validator<SendRegistrationPhoneOtpRequest>
{
    public SendRegistrationPhoneOtpRequestValidator()
    {
        RuleFor(x => x.TenantId).ValidIdWhenSupplied();
        RuleFor(x => x.Phone).PhoneE164();
    }
}

public sealed class VerifyRegistrationPhoneOtpRequestValidator : Validator<VerifyRegistrationPhoneOtpRequest>
{
    public VerifyRegistrationPhoneOtpRequestValidator()
    {
        RuleFor(x => x.ChallengeId).RequiredId();
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .Length(4, 12).WithMessage("Code must be between 4 and 12 characters.");
    }
}

// ── Seeding ─────────────────────────────────────────────────────────

public sealed class DataSeedRequestValidator : Validator<DataSeedRequest>
{
    public DataSeedRequestValidator()
    {
        RuleFor(x => x.Keys)
            .Must(k => k == null || k.Count <= 200);
        RuleForEach(x => x.Keys).MaximumLength(128);
    }
}

public sealed class DemoSeedRequestValidator : Validator<DemoSeedRequest>
{
    public DemoSeedRequestValidator() => RuleFor(x => x.SeedType).MaximumLength(128);
}

// ── Settings ────────────────────────────────────────────────────────

public sealed class AuthProviderSettingsUpdateRequestValidator : Validator<AuthProviderSettingsUpdateRequest>
{
    // Spec 029 — added "Keycloak" as a third operator-choice provider.
    private static readonly string[] ActiveProviders = ["Auth0", "AzureAd", "Keycloak"];

    public AuthProviderSettingsUpdateRequestValidator()
    {
        RuleFor(x => x.ActiveProvider)
            .NotEmpty()
            .Must(p => ActiveProviders.Contains(p))
            .WithMessage($"ActiveProvider must be one of: {string.Join(", ", ActiveProviders)}.");
    }
}

public sealed class CommunicationProviderSettingsUpdateRequestValidator
    : Validator<CommunicationProviderSettingsUpdateRequest>
{
    // Single provider per channel today. Listed as sets so SendGrid /
    // Mailgun for email and Twilio / MessageBird for SMS can slot in
    // independently without changing the validator shape.
    private static readonly string[] EmailProviders = ["AzureCommunicationServices"];
    private static readonly string[] SmsProviders = ["AzureCommunicationServices"];

    public CommunicationProviderSettingsUpdateRequestValidator()
    {
        When(x => x.Email != null, () =>
        {
            RuleFor(x => x.Email!.ActiveProvider)
                .NotEmpty()
                .Must(p => EmailProviders.Contains(p))
                .WithMessage($"Email.ActiveProvider must be one of: {string.Join(", ", EmailProviders)}.");
        });
        When(x => x.Sms != null, () =>
        {
            RuleFor(x => x.Sms!.ActiveProvider)
                .NotEmpty()
                .Must(p => SmsProviders.Contains(p))
                .WithMessage($"Sms.ActiveProvider must be one of: {string.Join(", ", SmsProviders)}.");
        });
    }
}

public sealed class SendCommunicationTestRequestValidator
    : Validator<SendCommunicationTestRequest>
{
    private static readonly string[] Channels = ["Email", "SMS"];

    public SendCommunicationTestRequestValidator()
    {
        RuleFor(x => x.Channel)
            .NotEmpty()
            .Must(c => Channels.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Channel must be one of: {string.Join(", ", Channels)}.");
        RuleFor(x => x.Recipient).RequiredText(254);
        RuleFor(x => x.Subject).MaximumLength(998);  // RFC 5322 hard limit
        RuleFor(x => x.Body).MaximumLength(64_000);
    }
}

public sealed class BatchGetSettingValuesRequestValidator : Validator<BatchGetSettingValuesRequest>
{
    public BatchGetSettingValuesRequestValidator()
    {
        RuleFor(x => x.Keys)
            .NotNull().WithMessage("Keys is required.")
            .Must(k => k != null && k.Count >= 1).WithMessage("At least one key is required.")
            .Must(k => k == null || k.Count <= 500).WithMessage("Too many keys (max 500).");
        RuleForEach(x => x.Keys).MaximumLength(256);
    }
}

public sealed class CreateTextToSpeechVoiceRequestValidator : Validator<CreateTextToSpeechVoiceRequest>
{
    public CreateTextToSpeechVoiceRequestValidator()
    {
        RuleFor(x => x.Provider).RequiredText(64);
        RuleFor(x => x.Name).RequiredText(128);
        RuleFor(x => x.SampleAudioBase64)
            .NotEmpty().WithMessage("Sample audio is required.")
            .MaximumLength(50_000_000).WithMessage("Sample audio is too large.");
        RuleFor(x => x.SampleFilename).MaximumLength(256);
        RuleFor(x => x.Languages)
            .Must(l => l == null || l.Count <= 50);
        RuleFor(x => x.Gender).MaximumLength(32);
        RuleFor(x => x.Age)
            .InclusiveBetween(0, 150)
            .When(x => x.Age.HasValue);
        RuleFor(x => x.Tags)
            .Must(t => t == null || t.Count <= 50);
    }
}

public sealed class DeleteTextToSpeechVoiceRequestValidator : Validator<DeleteTextToSpeechVoiceRequest>
{
    public DeleteTextToSpeechVoiceRequestValidator()
    {
        RuleFor(x => x.Provider).RequiredText(64);
        RuleFor(x => x.VoiceId).RequiredText(256);
    }
}

public sealed class GetTenantTextToSpeechCredentialRequestValidator : Validator<GetTenantTextToSpeechCredentialRequest>
{
    public GetTenantTextToSpeechCredentialRequestValidator()
        => RuleFor(x => x.Provider).MaximumLength(64);
}

public sealed class GetTextToSpeechVoicesRequestValidator : Validator<GetTextToSpeechVoicesRequest>
{
    public GetTextToSpeechVoicesRequestValidator()
        => RuleFor(x => x.Provider).MaximumLength(64);
}

public sealed class InvalidateCacheSetRequestValidator : Validator<InvalidateCacheSetRequest>
{
    public InvalidateCacheSetRequestValidator() => RuleFor(x => x.CacheSet).RequiredText(128);
}

public sealed class SettingKeyRequestValidator : Validator<SettingKeyRequest>
{
    public SettingKeyRequestValidator() => RuleFor(x => x.Key).RequiredText(256);
}

public sealed class SettingValueUpdateRequestValidator : Validator<SettingValueUpdateRequest>
{
    public SettingValueUpdateRequestValidator()
    {
        RuleFor(x => x.Key).RequiredText(256);
        RuleFor(x => x.Value).MaximumLength(64_000);
    }
}

public sealed class TextToSpeechCredentialUpdateRequestValidator : Validator<TextToSpeechCredentialUpdateRequest>
{
    public TextToSpeechCredentialUpdateRequestValidator()
    {
        RuleFor(x => x.Provider).RequiredText(64);
        RuleFor(x => x.ApiKey).MaximumLength(8192);
    }
}

public sealed class TextToSpeechPreviewRequestValidator : Validator<TextToSpeechPreviewRequest>
{
    public TextToSpeechPreviewRequestValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Text is required.")
            .MaximumLength(8_000);
        RuleFor(x => x.Locale).MaximumLength(16);
        RuleFor(x => x.Provider).MaximumLength(64);
        RuleFor(x => x.VoiceId).MaximumLength(256);
        RuleFor(x => x.ModelId).MaximumLength(128);
        RuleFor(x => x.OutputFormat).MaximumLength(32);
    }
}

public sealed class TextToSpeechSettingsUpdateValidator : Validator<TextToSpeechSettingsUpdate>
{
    public TextToSpeechSettingsUpdateValidator()
    {
        RuleFor(x => x.DefaultProfile).NotNull();
        RuleFor(x => x.DefaultProfile.Provider).RequiredText(64).When(x => x.DefaultProfile != null);
        RuleFor(x => x.DefaultProfile.VoiceId).RequiredText(256).When(x => x.DefaultProfile != null);
        RuleFor(x => x.DefaultProfile.ModelId).MaximumLength(128).When(x => x.DefaultProfile != null);
        RuleFor(x => x.DefaultProfile.Locale).MaximumLength(16).When(x => x.DefaultProfile != null);
        RuleFor(x => x.DefaultProfile.OutputFormat).MaximumLength(32).When(x => x.DefaultProfile != null);
        RuleFor(x => x.Policy).NotNull();
        RuleFor(x => x.Policy.MaxCharactersPerUtterance)
            .InclusiveBetween(1, 500_000)
            .When(x => x.Policy != null);
        RuleFor(x => x.Policy.MaxRequestsPerMinutePerUser)
            .InclusiveBetween(1, 10_000)
            .When(x => x.Policy != null);
        RuleFor(x => x.Policy.MonthlyCharacterBudget)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Policy != null && x.Policy.MonthlyCharacterBudget.HasValue);
    }
}

// ── Autonumbering ───────────────────────────────────────────────────

public sealed class AutonumberGenerateRequestValidator : Validator<AutonumberGenerateRequest>
{
    public AutonumberGenerateRequestValidator()
    {
        RuleFor(x => x.EntityType).RequiredText(64);
        RuleFor(x => x.TenantId).ValidIdWhenSupplied();
    }
}

public sealed class AutonumberProfileUpsertValidator : Validator<AutonumberProfileUpsert>
{
    public AutonumberProfileUpsertValidator()
    {
        RuleFor(x => x.EntityType).RequiredText(64);
        RuleFor(x => x.PrefixTemplate).MaximumLength(64);
        RuleFor(x => x.SuffixTemplate).MaximumLength(64);
        RuleFor(x => x.PaddingLength).InclusiveBetween(0, 20);
        RuleFor(x => x.MinValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxValue).GreaterThan(0);
        RuleFor(x => x)
            .Must(x => x.MinValue < x.MaxValue)
            .WithMessage("MinValue must be less than MaxValue.");
    }
}

// ── CMS ─────────────────────────────────────────────────────────────

public sealed class CreateContentBlockRequestValidator : Validator<CreateContentBlockRequest>
{
    public CreateContentBlockRequestValidator()
    {
        RuleFor(x => x.ContentKey).RequiredText(128);
        RuleFor(x => x.Title).RequiredText(256);
        RuleFor(x => x.Slug).MaximumLength(256);
        RuleFor(x => x.Area).RequiredText(64);
        RuleFor(x => x.Format).RequiredText(32);
        RuleFor(x => x.Body).MaximumLength(1_048_576);
        RuleFor(x => x.Locale).RequiredText(16);
        RuleFor(x => x.Priority).InclusiveBetween(0, 1_000_000);
        RuleFor(x => x.TargetingJson).MaximumLength(64_000);
        RuleFor(x => x.AiRunId).ValidIdWhenSupplied();
    }
}

public sealed class UpdateContentBlockRequestValidator : Validator<UpdateContentBlockRequest>
{
    public UpdateContentBlockRequestValidator()
    {
        RuleFor(x => x.Title).RequiredText(256);
        RuleFor(x => x.Slug).MaximumLength(256);
        RuleFor(x => x.Area).RequiredText(64);
        RuleFor(x => x.Format).RequiredText(32);
        RuleFor(x => x.Body).MaximumLength(1_048_576);
        RuleFor(x => x.Locale).RequiredText(16);
        RuleFor(x => x.Priority).InclusiveBetween(0, 1_000_000);
        RuleFor(x => x.TargetingJson).MaximumLength(64_000);
    }
}

public sealed class AddContentBlockMediaRequestValidator : Validator<AddContentBlockMediaRequest>
{
    public AddContentBlockMediaRequestValidator()
    {
        RuleFor(x => x.Url).RequiredText(2048);
        RuleFor(x => x.Alt).MaximumLength(512);
        RuleFor(x => x.Caption).MaximumLength(1024);
        RuleFor(x => x.MimeType).MaximumLength(128);
        RuleFor(x => x.LinkUrl).MaximumLength(2048);
    }
}

public sealed class ReorderContentBlockMediaRequestValidator : Validator<ReorderContentBlockMediaRequest>
{
    public ReorderContentBlockMediaRequestValidator()
    {
        RuleFor(x => x.MediaIds)
            .NotNull().WithMessage("MediaIds is required.")
            .Must(m => m != null && m.Count <= 200);
        RuleForEach(x => x.MediaIds).RequiredId();
    }
}

// ── Customers ───────────────────────────────────────────────────────

public sealed class CreateCustomerRequestValidator : Validator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.DisplayName).RequiredText(256);
        RuleFor(x => x.PartyType)
            .NotEmpty()
            .Must(t => t is "Person" or "Organization")
            .WithMessage("PartyType must be 'Person' or 'Organization'.");
        RuleFor(x => x.Status).RequiredText(64);
        RuleFor(x => x.CustomerTierCode).MaximumLength(64);
        RuleFor(x => x.Title).MaximumLength(64);
        RuleFor(x => x.FirstName).MaximumLength(128);
        RuleFor(x => x.LastName).MaximumLength(128);
        RuleFor(x => x.Nationality).MaximumLength(64);
        RuleFor(x => x.Occupation).MaximumLength(128);
        RuleFor(x => x.CountryCode)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
        RuleFor(x => x.RegistrationNumber).MaximumLength(64);
        RuleFor(x => x.IncorporationCountry)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.IncorporationCountry));
        RuleFor(x => x.Industry).MaximumLength(128);
        RuleFor(x => x.Contacts)
            .NotNull().WithMessage("Contacts is required (may be empty).")
            .Must(c => c == null || c.Count <= 50);
        RuleForEach(x => x.Contacts).SetValidator(new CreateCustomerContactRequestValidator());
        RuleFor(x => x.Addresses)
            .NotNull().WithMessage("Addresses is required (may be empty).")
            .Must(a => a == null || a.Count <= 20);
        RuleForEach(x => x.Addresses).SetValidator(new CreateCustomerAddressRequestValidator());
    }
}

public sealed class CreateCustomerContactRequestValidator : Validator<CreateCustomerContactRequest>
{
    public CreateCustomerContactRequestValidator()
    {
        // The Admin UI's customer-create form posts placeholder rows for the
        // Email/Phone slots with `type` already filled (e.g. "Email"/"Phone")
        // but an empty `value` when the user leaves them blank. The service
        // silently drops those, so the validator must too — only enforce
        // field rules when `Value` is actually supplied. (Empty Value =
        // user didn't fill in this contact slot.)
        When(x => !string.IsNullOrWhiteSpace(x.Value), () =>
        {
            RuleFor(x => x.Type).RequiredText(32);
            RuleFor(x => x.Value).MaximumLength(512);
        });
    }
}

public sealed class CreateCustomerAddressRequestValidator : Validator<CreateCustomerAddressRequest>
{
    public CreateCustomerAddressRequestValidator()
    {
        // Same tolerance as contacts: an empty placeholder address row is
        // considered absent and skipped.
        When(IsRowSupplied, () =>
        {
            RuleFor(x => x.Type).RequiredText(32);
            RuleFor(x => x.Line1).RequiredText(256);
            RuleFor(x => x.Line2).MaximumLength(256);
            RuleFor(x => x.Line3).MaximumLength(256);
            RuleFor(x => x.City).RequiredText(128);
            RuleFor(x => x.State).MaximumLength(128);
            RuleFor(x => x.Postcode).RequiredText(32);
            RuleFor(x => x.Country)
                .Length(2).Matches("^[A-Za-z]{2}$").WithMessage("Country must be a 2-letter ISO code.");
        });
    }

    private static bool IsRowSupplied(CreateCustomerAddressRequest x) =>
        !string.IsNullOrWhiteSpace(x.Type)
        || !string.IsNullOrWhiteSpace(x.Line1)
        || !string.IsNullOrWhiteSpace(x.City)
        || !string.IsNullOrWhiteSpace(x.Postcode)
        || !string.IsNullOrWhiteSpace(x.Country);
}

public sealed class ListCustomersRequestValidator : Validator<ListCustomersRequest>
{
    public ListCustomersRequestValidator()
    {
        RuleFor(x => x.PageNumber).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 200);
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.PartyType).MaximumLength(64);
        RuleFor(x => x.Search).MaximumLength(256);
    }
}

// ── Notification Templates ──────────────────────────────────────────

public sealed class CreateNotificationTemplateRequestValidator : Validator<CreateNotificationTemplateRequest>
{
    public CreateNotificationTemplateRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(128);
        RuleFor(x => x.Channel).RequiredText(32);
        RuleFor(x => x.SubjectTemplate).MaximumLength(1024);
        RuleFor(x => x.BodyTemplate)
            .NotEmpty().WithMessage("BodyTemplate is required.")
            .MaximumLength(64_000);
        RuleFor(x => x.Description).MaximumLength(2048);
    }
}

public sealed class UpdateNotificationTemplateRequestValidator : Validator<UpdateNotificationTemplateRequest>
{
    public UpdateNotificationTemplateRequestValidator()
    {
        RuleFor(x => x.SubjectTemplate).MaximumLength(1024);
        RuleFor(x => x.BodyTemplate)
            .NotEmpty().WithMessage("BodyTemplate is required.")
            .MaximumLength(64_000);
        RuleFor(x => x.Description).MaximumLength(2048);
    }
}

public sealed class PreviewNotificationTemplateRequestValidator : Validator<PreviewNotificationTemplateRequest>
{
    public PreviewNotificationTemplateRequestValidator()
    {
        RuleFor(x => x.SubjectTemplate).MaximumLength(1024);
        RuleFor(x => x.BodyTemplate)
            .NotEmpty().WithMessage("BodyTemplate is required.")
            .MaximumLength(64_000);
        RuleFor(x => x.SampleModelJson).MaximumLength(64_000);
    }
}

public sealed class CreateNotificationTemplateBindingRequestValidator : Validator<CreateNotificationTemplateBindingRequest>
{
    public CreateNotificationTemplateBindingRequestValidator()
    {
        RuleFor(x => x.TemplateName).RequiredText(128);
        RuleFor(x => x.Channel).RequiredText(32);
        RuleFor(x => x.BaseTemplateId).ValidIdWhenSupplied();
        RuleFor(x => x.OverrideTemplateId).ValidIdWhenSupplied();
    }
}

public sealed class UpdateNotificationTemplateBindingRequestValidator : Validator<UpdateNotificationTemplateBindingRequest>
{
    public UpdateNotificationTemplateBindingRequestValidator()
    {
        RuleFor(x => x.BaseTemplateId).ValidIdWhenSupplied();
        RuleFor(x => x.OverrideTemplateId).ValidIdWhenSupplied();
    }
}
