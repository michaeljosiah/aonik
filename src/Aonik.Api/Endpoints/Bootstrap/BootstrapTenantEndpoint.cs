using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Identity.Provisioning;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Api.Endpoints.Bootstrap;

public class BootstrapTenantEndpoint : EndpointWithoutRequest<BootstrapTenantResult>
{
    private readonly IBootstrapService _bootstrapService;
    private readonly IWebHostEnvironment _environment;
    private readonly IAuthorizationService _authorizationService;
    private readonly BootstrapOptions _options;
    private readonly ICurrentUserContext _currentUserContext;

    public BootstrapTenantEndpoint(
        IBootstrapService bootstrapService,
        IWebHostEnvironment environment,
        IAuthorizationService authorizationService,
        IOptions<BootstrapOptions> options,
        ICurrentUserContext currentUserContext)
    {
        _bootstrapService = bootstrapService;
        _environment = environment;
        _authorizationService = authorizationService;
        _options = options.Value;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("/bootstrap");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Bootstrap is disabled." }, ct);
            return;
        }

        if (!_environment.IsDevelopment())
        {
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, null, "PlatformAdmin");
            if (!authorizationResult.Succeeded)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                await HttpContext.Response.WriteAsJsonAsync(new { error = "Platform admin access required." }, ct);
                return;
            }
        }

        if (!_currentUserContext.IsAuthenticated)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Authentication required." }, ct);
            return;
        }

        var externalIssuer = _currentUserContext.ExternalIssuer;
        var externalSubject = _currentUserContext.ExternalSubject;
        if (string.IsNullOrWhiteSpace(externalIssuer) || string.IsNullOrWhiteSpace(externalSubject))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "External identity claims missing." }, ct);
            return;
        }

        var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
                    ?? User.Claims.FirstOrDefault(c => c.Type == "email")?.Value
                    ?? User.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;
        var externalTenantId = User.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;

        var result = await _bootstrapService.BootstrapAsync(
            new BootstrapUserContext(
                externalIssuer,
                externalSubject,
                externalTenantId,
                email),
            ct);

        await Send.OkAsync(result, ct);
    }
}
