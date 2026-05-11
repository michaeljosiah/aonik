using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Aonik.Platform.Contracts.Api.Host;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Endpoints.Host.Me;

/// <summary>
/// Authenticated lookup for the tenants the current external identity has a
/// membership in. Used by the post-auth tenant-resolution step on web (apex)
/// and desktop — replaces the public enumeration via
/// <c>/host/tenants/list-for-login</c>.
/// </summary>
/// <remarks>
/// Lives under <c>/host/*</c> so it bypasses the tenant-context middleware
/// (the caller has not yet picked a tenant — this is the call that surfaces
/// the choices). Authentication is still required: <c>iss</c> and <c>sub</c>
/// are read from the validated JWT via <see cref="ICurrentUserContext"/>.
/// </remarks>
internal class ListMyTenantsEndpoint : EndpointWithoutRequest<MyTenantsResponseDto>
{
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<ListMyTenantsEndpoint> _logger;

    public ListMyTenantsEndpoint(
        ITenantService tenantService,
        ICurrentUserContext currentUserContext,
        ILogger<ListMyTenantsEndpoint> logger)
    {
        _tenantService = tenantService;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/host/me/tenants");
        // Any authenticated role — the user must have a valid JWT but we
        // don't care which roles they hold in any particular tenant: we're
        // resolving "which tenants do I have access to in the first place?".
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List my tenants";
            s.Description =
                "Returns the active tenants the authenticated external " +
                "identity has a User membership in. Used to drive the " +
                "post-login organization picker (or auto-select when there " +
                "is exactly one).";
            s.Response(200, "Tenant list returned");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Identity"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var iss = _currentUserContext.ExternalIssuer;
        var sub = _currentUserContext.ExternalSubject;

        if (string.IsNullOrWhiteSpace(iss) || string.IsNullOrWhiteSpace(sub))
        {
            _logger.LogWarning("ListMyTenants invoked without iss/sub on the user context.");
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(
                new { error = "Authentication required." }, ct);
            return;
        }

        var result = await _tenantService.ListTenantsForCurrentUserAsync(iss, sub, ct);

        var dto = new MyTenantsResponseDto(result.Tenants
            .Select(t => new MyTenantSummaryResponse(
                t.TenantId,
                t.Name,
                t.Subdomain,
                t.Environment))
            .ToList());

        await Send.OkAsync(dto, ct);
    }
}
