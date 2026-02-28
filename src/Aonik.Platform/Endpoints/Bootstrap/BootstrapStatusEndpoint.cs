using Aonik.Platform.Contracts.Api.Bootstrap;
using Aonik.Platform.Persistence;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Models.Configuration;

namespace Aonik.Platform.Endpoints.Bootstrap;

internal class BootstrapStatusEndpoint : EndpointWithoutRequest<BootstrapStatusResponse>
{
    private readonly PlatformDbContext _dbContext;
    private readonly PlatformAdminOptions _platformAdminOptions;
    private readonly IWebHostEnvironment _environment;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<BootstrapStatusEndpoint> _logger;

    public BootstrapStatusEndpoint(
        PlatformDbContext dbContext,
        IOptions<PlatformAdminOptions> platformAdminOptions,
        IWebHostEnvironment environment,
        IAuthorizationService authorizationService,
        ILogger<BootstrapStatusEndpoint> logger)
    {
        _dbContext = dbContext;
        _platformAdminOptions = platformAdminOptions.Value;
        _environment = environment;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/bootstrap/status");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Bootstrap status endpoint called");

            var configuredAdminEmails = _platformAdminOptions.AdminEmails
                .Where(adminEmail => !string.IsNullOrWhiteSpace(adminEmail))
                .Select(adminEmail => adminEmail.Trim())
                .ToArray();

            var rawAuthorizationHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
            var authorizationHeaderPresent = !string.IsNullOrWhiteSpace(rawAuthorizationHeader);
            var tokenText = rawAuthorizationHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                ? rawAuthorizationHeader["Bearer ".Length..].Trim()
                : rawAuthorizationHeader?.Trim();
            var bearerTokenLooksJwt = !string.IsNullOrWhiteSpace(tokenText)
                && tokenText.Count(c => c == '.') == 2;
            var authFailureReason = HttpContext.Items["AonikAuthFailureReason"]?.ToString();
            
            // Use a timeout to prevent long-running queries
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            
            var tenantCount = await _dbContext.Tenants.CountAsync(linkedCts.Token);
            var hasAdminEmails = configuredAdminEmails.Length > 0;
            var principal = User;
            var isAuthenticated = principal?.Identity?.IsAuthenticated == true;
            var isCurrentUserAllowed = false;
            string? resolvedUserEmail = null;

            if (isAuthenticated)
            {
                var userEmail = ClaimsEmailResolver.GetEmail(principal)?.Trim();
                resolvedUserEmail = userEmail;

                if (string.IsNullOrWhiteSpace(userEmail))
                {
                    _logger.LogWarning(
                        "Bootstrap status could not resolve user email. Claims: {Claims}",
                        string.Join(", ", principal!.Claims.Select(c => $"{c.Type}={c.Value}")));
                }
                else
                {
                    _logger.LogInformation("Bootstrap status resolved user email: {Email}", userEmail);
                }

                if (_environment.IsDevelopment())
                {
                    isCurrentUserAllowed = true;
                }
                else
                {
                    var authz = await _authorizationService.AuthorizeAsync(principal!, null, "PlatformAdmin");
                    isCurrentUserAllowed = authz.Succeeded;
                }
            }

            var canBootstrap = tenantCount == 0 && isCurrentUserAllowed;

            await Send.OkAsync(new BootstrapStatusResponse(
                hasAdminEmails,
                isCurrentUserAllowed,
                tenantCount,
                canBootstrap,
                resolvedUserEmail,
                isAuthenticated,
                authorizationHeaderPresent,
                bearerTokenLooksJwt,
                authFailureReason),
                ct);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Bootstrap status query was cancelled");
            
            // Return a safe default response when cancelled
            await Send.OkAsync(new BootstrapStatusResponse(
                _platformAdminOptions.AdminEmails.Length > 0,
                false,
                0,
                false,
                null,
                false,
                false,
                false,
                null),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bootstrap status");
            throw;
        }
    }
}
