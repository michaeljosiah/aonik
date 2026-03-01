using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Contracts.Models.Authentication;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Models.Registration;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Services.Onboarding;
using Aonik.Platform.Contracts.Services.Registration;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Settings;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Services.Registration;

internal class RegistrationService : IRegistrationService
{
    private readonly ISettingProvider _settingProvider;
    private readonly IIdpUserProvisionerFactory _idpUserProvisionerFactory;
    private readonly IUserProvisioningService _userProvisioningService;
    private readonly IUserProfileService _userProfileService;
    private readonly IVerificationService _verificationService;
    private readonly IOnboardingPolicyEvaluator _onboardingPolicyEvaluator;
    private readonly PlatformDbContext _dbContext;
    private readonly ILogger<RegistrationService> _logger;

    public RegistrationService(
        ISettingProvider settingProvider,
        IIdpUserProvisionerFactory idpUserProvisionerFactory,
        IUserProvisioningService userProvisioningService,
        IUserProfileService userProfileService,
        IVerificationService verificationService,
        IOnboardingPolicyEvaluator onboardingPolicyEvaluator,
        PlatformDbContext dbContext,
        ILogger<RegistrationService> logger)
    {
        _settingProvider = settingProvider;
        _idpUserProvisionerFactory = idpUserProvisionerFactory;
        _userProvisioningService = userProvisioningService;
        _userProfileService = userProfileService;
        _verificationService = verificationService;
        _onboardingPolicyEvaluator = onboardingPolicyEvaluator;
        _dbContext = dbContext;
        _logger = logger;
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
        await EnsurePersonalUserRoleAssignmentAsync(request.TenantId.Value, provisioningResult.UserId, cancellationToken);

        await _userProfileService.UpdateCustomerProfileForRegistrationAsync(
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
            try
            {
                await _verificationService.StartEmailVerificationForRegistrationAsync(
                    provisioningResult.UserId,
                    request.Email,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Registration completed but email verification challenge could not start for user {UserId}.",
                    provisioningResult.UserId);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            try
            {
                await _verificationService.StartPhoneVerificationForRegistrationAsync(
                    provisioningResult.UserId,
                    request.Phone,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Registration completed but phone verification challenge could not start for user {UserId}.",
                    provisioningResult.UserId);
            }
        }

        var onboarding = await _onboardingPolicyEvaluator.EvaluateAsync(
            provisioningResult.UserId,
            cancellationToken);

        return new IndividualRegistrationResult(
            provisioningResult.UserId,
            provisioningResult.PartyId,
            onboarding);
    }

    private async Task EnsurePersonalUserRoleAssignmentAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var personalUserRole = await _dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                role => role.TenantId == tenantId && role.Name == "PersonalUser",
                cancellationToken);

        if (personalUserRole == null)
        {
            _logger.LogWarning(
                "Registration user {UserId} in tenant {TenantId} has no 'PersonalUser' role available for assignment.",
                userId,
                tenantId);
            return;
        }

        var hasRole = await _dbContext.UserRoles
            .AnyAsync(
                userRole => userRole.UserId == userId && userRole.RoleId == personalUserRole.Id,
                cancellationToken);

        if (hasRole)
        {
            return;
        }

        _dbContext.UserRoles.Add(new UserRole
        {
            UserId = userId,
            RoleId = personalUserRole.Id,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record RegistrationExternalIdentity(

        Guid TenantId,
        string ExternalIssuer,
        string ExternalSubject,
        string? ExternalTenantId,
        string? Email) : IExternalIdentity;
}
