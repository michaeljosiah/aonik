using Aonik.Application.Abstractions.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Aonik.Application.Services.Identity.Provisioning;
using Aonik.Infrastructure.Authentication.Configuration;

namespace Aonik.Api.Endpoints.Bootstrap;

public class BootstrapStatusEndpoint : EndpointWithoutRequest<BootstrapStatusResponse>
{
    private readonly IAonikDbContext _dbContext;
    private readonly BootstrapOptions _bootstrapOptions;
    private readonly PlatformAdminOptions _platformAdminOptions;

    public BootstrapStatusEndpoint(
        IAonikDbContext dbContext,
        IOptions<BootstrapOptions> bootstrapOptions,
        IOptions<PlatformAdminOptions> platformAdminOptions)
    {
        _dbContext = dbContext;
        _bootstrapOptions = bootstrapOptions.Value;
        _platformAdminOptions = platformAdminOptions.Value;
    }

    public override void Configure()
    {
        Get("/bootstrap/status");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantCount = await _dbContext.Tenants.CountAsync(ct);
        var hasAdminEmails = _platformAdminOptions.AdminEmails.Length > 0;
        var isCurrentUserAllowed = false;

        if (User?.Identity?.IsAuthenticated == true)
        {
            var userEmail = User.Claims
                .FirstOrDefault(c => c.Type == "email" || c.Type == "preferred_username" || c.Type == "upn")?.Value;

            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                isCurrentUserAllowed = _platformAdminOptions.AdminEmails.Any(adminEmail =>
                    string.Equals(adminEmail, userEmail, StringComparison.OrdinalIgnoreCase));
            }
        }

        await Send.OkAsync(new BootstrapStatusResponse(
            _bootstrapOptions.Enabled,
            hasAdminEmails,
            isCurrentUserAllowed,
            tenantCount),
            ct);
    }
}
