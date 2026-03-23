using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Aonik.Platform.Contracts.Api.Bootstrap;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;

namespace Aonik.Platform.Endpoints.Bootstrap;

internal class BootstrapStatusEndpoint : EndpointWithoutRequest<BootstrapStatusResponse>
{
    private readonly PlatformDbContext _dbContext;
    private readonly BootstrapOptions _options;
    private readonly ILogger<BootstrapStatusEndpoint> _logger;

    public BootstrapStatusEndpoint(
        PlatformDbContext dbContext,
        IOptions<BootstrapOptions> options,
        ILogger<BootstrapStatusEndpoint> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
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
            var tenantCount = await _dbContext.Tenants.CountAsync(ct);
            var setupSecretConfigured = !string.IsNullOrWhiteSpace(_options.SetupSecret);

            var response = tenantCount > 0
                ? new BootstrapStatusResponse(
                    "completed",
                    _options.Enabled,
                    setupSecretConfigured,
                    tenantCount,
                    false,
                    "Bootstrap has already completed because at least one tenant exists.")
                : !_options.Enabled
                    ? new BootstrapStatusResponse(
                        "disabled",
                        false,
                        setupSecretConfigured,
                        tenantCount,
                        false,
                        "Bootstrap is disabled. Enable Bootstrap:Enabled to perform first-run setup.")
                    : !setupSecretConfigured
                        ? new BootstrapStatusResponse(
                            "misconfigured",
                            true,
                            false,
                            tenantCount,
                            false,
                            "Bootstrap is enabled but Bootstrap:SetupSecret is not configured.")
                        : new BootstrapStatusResponse(
                            "ready",
                            true,
                            true,
                            tenantCount,
                            true,
                            "Enter the install code and owner email to create the first tenant.");

            await Send.OkAsync(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bootstrap status");
            HttpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Unable to determine bootstrap status right now. Please retry once the API is healthy."
            }, ct);
        }
    }
}
