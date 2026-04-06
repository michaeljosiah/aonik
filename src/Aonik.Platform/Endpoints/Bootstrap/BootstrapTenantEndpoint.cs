using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Http;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.Platform.Contracts.Api.Bootstrap;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;

namespace Aonik.Platform.Endpoints.Bootstrap;

internal class BootstrapTenantEndpoint : Endpoint<BootstrapInitializeRequest, BootstrapTenantResult>
{
    private readonly IBootstrapService _bootstrapService;
    private readonly PlatformDbContext _dbContext;
    private readonly BootstrapOptions _options;

    public BootstrapTenantEndpoint(
        IBootstrapService bootstrapService,
        PlatformDbContext dbContext,
        IOptions<BootstrapOptions> options)
    {
        _bootstrapService = bootstrapService;
        _dbContext = dbContext;
        _options = options.Value;
    }


    public override void Configure()
    {
        Post("/bootstrap");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Bootstrap first tenant";
            s.Description = "Performs initial platform setup by creating the first tenant and owner account. Requires a valid setup secret.";
            s.Response(200, "Tenant bootstrapped successfully");
            s.Response(400, "Invalid request");
        });
        Options(x => x.WithTags("Bootstrap"));
    }

    public override async Task HandleAsync(BootstrapInitializeRequest req, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Bootstrap is disabled." }, ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.SetupSecret))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Bootstrap is enabled but Bootstrap:SetupSecret is not configured." }, ct);
            return;
        }

        if (await _dbContext.Tenants.AnyAsync(ct))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Bootstrap has already completed. Use the tenant administration endpoints for additional tenant setup."
            }, ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.SetupSecret))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Install code is required." }, ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.OwnerEmail))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Owner email is required." }, ct);
            return;
        }

        if (!LooksLikeEmail(req.OwnerEmail))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Owner email must be a valid email address." }, ct);
            return;
        }

        if (!SecretsMatch(req.SetupSecret, _options.SetupSecret))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "The provided install code is invalid." }, ct);
            return;
        }

        try
        {
            var result = await _bootstrapService.BootstrapAsync(
                new BootstrapOwnerContext(
                    req.OwnerEmail.Trim(),
                    req.OwnerDisplayName?.Trim()),
                ct);

            await Send.OkAsync(result, ct);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.StartsWith("Bootstrap has already completed", StringComparison.OrdinalIgnoreCase))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Bootstrap has already completed. Use the tenant administration endpoints for additional tenant setup."
            }, ct);
        }
    }

    private static bool LooksLikeEmail(string email)
    {
        try
        {
            _ = new MailAddress(email.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool SecretsMatch(string providedSecret, string configuredSecret)
    {
        var providedBytes = Encoding.UTF8.GetBytes(providedSecret.Trim());
        var configuredBytes = Encoding.UTF8.GetBytes(configuredSecret.Trim());

        return CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }
}
