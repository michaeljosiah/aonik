using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Infrastructure.Authentication;
using Aonik.Infrastructure.Authentication.Configuration;
using Aonik.SharedKernel.Abstractions;


namespace Aonik.Api.Endpoints.Bootstrap;

public class BootstrapTenantEndpoint : EndpointWithoutRequest<BootstrapTenantResult>
{
    private readonly IBootstrapService _bootstrapService;
    private readonly IWebHostEnvironment _environment;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly PlatformAdminOptions _platformAdminOptions;

    public BootstrapTenantEndpoint(
        IBootstrapService bootstrapService,
        IWebHostEnvironment environment,
        IAuthorizationService authorizationService,
        ICurrentUserContext currentUserContext,
        IOptions<PlatformAdminOptions> platformAdminOptions)
    {
        _bootstrapService = bootstrapService;
        _environment = environment;
        _authorizationService = authorizationService;
        _currentUserContext = currentUserContext;
        _platformAdminOptions = platformAdminOptions.Value;
    }


    public override void Configure()
    {
        Post("/bootstrap");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {

        if (!_environment.IsDevelopment() && _platformAdminOptions.AdminEmails.Length > 0)
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

        var email = ClaimsEmailResolver.GetEmail(User);
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
