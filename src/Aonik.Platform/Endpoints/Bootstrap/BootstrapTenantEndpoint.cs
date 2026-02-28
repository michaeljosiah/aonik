using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;


namespace Aonik.Platform.Endpoints.Bootstrap;

internal class BootstrapTenantEndpoint : EndpointWithoutRequest<BootstrapTenantResult>
{
    private readonly IBootstrapService _bootstrapService;
    private readonly IWebHostEnvironment _environment;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly PlatformDbContext _dbContext;

    public BootstrapTenantEndpoint(
        IBootstrapService bootstrapService,
        IWebHostEnvironment environment,
        IAuthorizationService authorizationService,
        ICurrentUserContext currentUserContext,
        PlatformDbContext dbContext)
    {
        _bootstrapService = bootstrapService;
        _environment = environment;
        _authorizationService = authorizationService;
        _currentUserContext = currentUserContext;
        _dbContext = dbContext;
    }


    public override void Configure()
    {
        Post("/bootstrap");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {

        if (await _dbContext.Tenants.AnyAsync(ct))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Bootstrap has already completed. Use the tenant administration endpoints for additional tenant setup."
            }, ct);
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

        var email = ClaimsEmailResolver.GetEmail(User);
        var externalTenantId = User.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;

        try
        {
            var result = await _bootstrapService.BootstrapAsync(
                new BootstrapUserContext(
                    externalIssuer,
                    externalSubject,
                    externalTenantId,
                    email),
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
}
