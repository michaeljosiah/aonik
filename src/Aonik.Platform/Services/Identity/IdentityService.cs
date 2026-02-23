using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.Platform.Persistence;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Contracts.Models.Authentication;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Services.Identity;

internal class IdentityService : IIdentityService
{
    private readonly ISettingProvider _settingProvider;
    private readonly IAuthTokenServiceFactory _authTokenServiceFactory;
    private readonly IIdpPasswordResetServiceFactory _passwordResetServiceFactory;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;
    private readonly PlatformDbContext _dbContext;
    private readonly IUserProvisioningService _userProvisioningService;
    private readonly IPermissionService _permissionService;

    public IdentityService(
        ISettingProvider settingProvider,
        IAuthTokenServiceFactory authTokenServiceFactory,
        IIdpPasswordResetServiceFactory passwordResetServiceFactory,
        ICurrentUserContext currentUserContext,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext,
        PlatformDbContext dbContext,
        IUserProvisioningService userProvisioningService,
        IPermissionService permissionService)
    {
        _settingProvider = settingProvider;
        _authTokenServiceFactory = authTokenServiceFactory;
        _passwordResetServiceFactory = passwordResetServiceFactory;
        _currentUserContext = currentUserContext;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
        _dbContext = dbContext;
        _userProvisioningService = userProvisioningService;
        _permissionService = permissionService;
    }

    public async Task<TokenResponse> TokenAsync(TokenRequest request, CancellationToken cancellationToken = default)
    {
        var provider = await _settingProvider.GetAsync(AuthSettingNames.Provider, cancellationToken) ?? "AzureAd";
        var service = _authTokenServiceFactory.GetService(provider);
        return await service.ExchangeAsync(request, cancellationToken);
    }

    public async Task<UserInfoResponse> GetUserInfoAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserContext.UserId.HasValue || !_currentUserContext.TenantId.HasValue)
        {
            throw new InvalidOperationException("Authenticated user context is missing.");
        }

        var userId = _currentUserContext.UserId.Value;
        var tenantId = _currentUserContext.TenantId.Value;

        var hasPermission = await _permissionService.HasPermissionAsync(userId, "UserInfo.Read", cancellationToken);
        if (!hasPermission)
        {
            throw new InvalidOperationException("Permission UserInfo.Read is required.");
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException("Authenticated user not found.");
        }

        var userPartyLink = await _dbContext.UserParties
            .Where(link => link.UserId == userId && link.TenantId == tenantId)
            .OrderByDescending(link => link.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var partyId = userPartyLink?.PartyId;

        if (!partyId.HasValue)
        {
            var identity = new UserInfoExternalIdentity(
                tenantId,
                _currentUserContext.ExternalIssuer ?? string.Empty,
                _currentUserContext.ExternalSubject ?? string.Empty,
                null,
                user.Email);

            var provisioned = await _userProvisioningService.EnsureUserAndCustomerAsync(identity, cancellationToken);
            partyId = provisioned.PartyId;
        }

        var names = await GetPrimaryNameAsync(partyId.Value, cancellationToken);
        
        var personProfile = await _dbContext.PersonProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(pp => pp.PartyId == partyId.Value, cancellationToken);
        
        var response = new UserInfoResponse(
            userId,
            user.Email ?? string.Empty,
            names?.FirstName,
            names?.LastName,
            _currentUserContext.Roles,
            tenantId,
            partyId.Value,
            personProfile?.PhotoUrl,
            personProfile?.PhotoUrlSmall,
            personProfile?.PhotoUrlTiny);

        await _auditLogWriter.LogAsync(
            AuditEventNames.CurrentUserViewed,
            "User",
            userId,
            tenantId,
            userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new
            {
                UserId = userId,
                TenantId = tenantId,
                PartyId = partyId.Value
            }),
            cancellationToken);

        return response;
    }

    public async Task<ForgotPasswordResponse> SendPasswordResetAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await _settingProvider.GetAsync(AuthSettingNames.Provider, cancellationToken) ?? "AzureAd";
        var resetService = _passwordResetServiceFactory.GetService(provider);
        await resetService.TriggerResetAsync(request.Email, request.TenantId, cancellationToken);

        var actorId = _currentUserContext.UserId;
        await _auditLogWriter.LogAsync(
            AuditEventNames.PasswordResetRequested,
            "User",
            actorId ?? Guid.NewGuid(),
            request.TenantId,
            actorId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new
            {
                Email = request.Email,
                request.TenantId
            }),
            cancellationToken);

        return new ForgotPasswordResponse("ok");
    }

    private async Task<NameResult?> GetPrimaryNameAsync(Guid partyId, CancellationToken cancellationToken)
    {
        var party = await _dbContext.Parties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == partyId, cancellationToken);

        if (party == null)
        {
            return null;
        }

        var parts = party.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var firstName = parts.Length > 0 ? parts[0] : null;
        var lastName = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : null;

        return new NameResult(firstName, lastName);
    }

    private sealed record NameResult(string? FirstName, string? LastName);

    private sealed record UserInfoExternalIdentity(
        Guid TenantId,
        string ExternalIssuer,
        string ExternalSubject,
        string? ExternalTenantId,
        string? Email) : IExternalIdentity;
}
