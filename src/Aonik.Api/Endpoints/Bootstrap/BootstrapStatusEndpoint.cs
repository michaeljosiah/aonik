using Aonik.Application.Abstractions.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Aonik.Infrastructure.Authentication;
using Aonik.Infrastructure.Authentication.Configuration;

namespace Aonik.Api.Endpoints.Bootstrap;

public class BootstrapStatusEndpoint : EndpointWithoutRequest<BootstrapStatusResponse>
{
    private readonly IAonikDbContext _dbContext;
    private readonly PlatformAdminOptions _platformAdminOptions;
    private readonly ILogger<BootstrapStatusEndpoint> _logger;

    public BootstrapStatusEndpoint(
        IAonikDbContext dbContext,
        IOptions<PlatformAdminOptions> platformAdminOptions,
        ILogger<BootstrapStatusEndpoint> logger)
    {
        _dbContext = dbContext;
        _platformAdminOptions = platformAdminOptions.Value;
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
            
            // Use a timeout to prevent long-running queries
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            
            var tenantCount = await _dbContext.Tenants.CountAsync(linkedCts.Token);
            var hasAdminEmails = _platformAdminOptions.AdminEmails.Length > 0;
            var isCurrentUserAllowed = false;

            if (User?.Identity?.IsAuthenticated == true)
            {
                var userEmail = ClaimsEmailResolver.GetEmail(User);

                if (string.IsNullOrWhiteSpace(userEmail))
                {
                    _logger.LogWarning(
                        "Bootstrap status could not resolve user email. Claims: {Claims}",
                        string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
                }
                else
                {
                    _logger.LogInformation("Bootstrap status resolved user email: {Email}", userEmail);
                    isCurrentUserAllowed = _platformAdminOptions.AdminEmails.Any(adminEmail =>
                        string.Equals(adminEmail, userEmail, StringComparison.OrdinalIgnoreCase));
                }
            }

            await Send.OkAsync(new BootstrapStatusResponse(
                hasAdminEmails,
                isCurrentUserAllowed,
                tenantCount),
                ct);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Bootstrap status query was cancelled");
            
            // Return a safe default response when cancelled
            await Send.OkAsync(new BootstrapStatusResponse(
                _platformAdminOptions.AdminEmails.Length > 0,
                false,
                0),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bootstrap status");
            throw;
        }
    }
}
