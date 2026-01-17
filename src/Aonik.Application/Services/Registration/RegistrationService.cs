using Aonik.Application.Abstractions.Authentication;
using Aonik.Application.Abstractions.Settings;
using Aonik.Application.Models.Authentication;
using Aonik.Application.Models.Identity;
using Aonik.Application.Models.Registration;
using Aonik.Application.Services.Identity;
using Aonik.Application.Services.Onboarding;
using Aonik.Application.Settings;

namespace Aonik.Application.Services.Registration;

public class RegistrationService : IRegistrationService
{
    private readonly ISettingProvider _settingProvider;
    private readonly IIdpUserProvisionerFactory _idpUserProvisionerFactory;
    private readonly IUserProvisioningService _userProvisioningService;
    private readonly IUserProfileService _userProfileService;
    private readonly IVerificationService _verificationService;
    private readonly IOnboardingPolicyEvaluator _onboardingPolicyEvaluator;

    public RegistrationService(
        ISettingProvider settingProvider,
        IIdpUserProvisionerFactory idpUserProvisionerFactory,
        IUserProvisioningService userProvisioningService,
        IUserProfileService userProfileService,
        IVerificationService verificationService,
        IOnboardingPolicyEvaluator onboardingPolicyEvaluator)
    {
        _settingProvider = settingProvider;
        _idpUserProvisionerFactory = idpUserProvisionerFactory;
        _userProvisioningService = userProvisioningService;
        _userProfileService = userProfileService;
        _verificationService = verificationService;
        _onboardingPolicyEvaluator = onboardingPolicyEvaluator;
    }

    public async Task<IndividualRegistrationResult> RegisterIndividualAsync(
        IndividualRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.TenantId.HasValue || request.TenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("TenantId is required for registration.");
        }

        var provider = await _settingProvider.GetAsync(AuthSettingNames.Provider, cancellationToken) ?? "AzureAd";
        var provisioner = _idpUserProvisionerFactory.GetProvisioner(provider);

        var externalIdentity = await provisioner.CreateUserAsync(
            new IdpUserRegistration(
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.Phone),
            cancellationToken);

        var identity = new RegistrationExternalIdentity(
            request.TenantId.Value,
            externalIdentity.ExternalIssuer,
            externalIdentity.ExternalSubject,
            externalIdentity.ExternalTenantId,
            request.Email);

        var provisioningResult = await _userProvisioningService.EnsureUserAndCustomerAsync(identity, cancellationToken);

        await _userProfileService.UpdateCustomerProfileAsync(
            provisioningResult.UserId,
            request.TenantId.Value,
            new UpdateCustomerProfileRequest(
                request.FirstName,
                request.LastName,
                request.Title,
                request.Phone,
                request.RegistrationCountry),
            cancellationToken);


        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            await _verificationService.StartEmailVerificationAsync(
                provisioningResult.UserId,
                request.Email,
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            await _verificationService.StartPhoneVerificationAsync(
                provisioningResult.UserId,
                request.Phone,
                cancellationToken);
        }

        var onboarding = await _onboardingPolicyEvaluator.EvaluateAsync(
            provisioningResult.UserId,
            cancellationToken);

        return new IndividualRegistrationResult(
            provisioningResult.UserId,
            provisioningResult.PartyId,
            onboarding);
    }

    private sealed record RegistrationExternalIdentity(

        Guid TenantId,
        string ExternalIssuer,
        string ExternalSubject,
        string? ExternalTenantId,
        string? Email) : IExternalIdentity;
}
