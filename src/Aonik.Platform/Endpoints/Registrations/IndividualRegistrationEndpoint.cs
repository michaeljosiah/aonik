using Microsoft.AspNetCore.Http;
using Aonik.Platform.Contracts.Api.Onboarding;
using Aonik.Platform.Contracts.Api.Registrations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Models.Onboarding;
using ApplicationRegistrationRequest = Aonik.Platform.Contracts.Models.Registration.IndividualRegistrationRequest;
using Aonik.Platform.Contracts.Services.Registration;
using Aonik.Platform.Contracts.Models.Configuration;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Aonik.Platform.Endpoints.Registrations;

internal class IndividualRegistrationEndpoint : Endpoint<IndividualRegistrationRequest, IndividualRegistrationResponse>
{
    private readonly IRegistrationService _registrationService;
    private readonly ITenantContext _tenantContext;
    private readonly PlatformDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public IndividualRegistrationEndpoint(
        IRegistrationService registrationService,
        ITenantContext tenantContext,
        PlatformDbContext dbContext,
        IConfiguration configuration)
    {
        _registrationService = registrationService;
        _tenantContext = tenantContext;
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public override void Configure()
    {
        Post("/v1/registrations/individual");
        AllowAnonymous();
    }

    public override async Task HandleAsync(IndividualRegistrationRequest req, CancellationToken ct)
    {
        var tenantId = await ResolveTenantIdAsync(req, ct);
        if (!tenantId.HasValue)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "TenantId is required for registration." }, ct);
            return;
        }

        _tenantContext.TenantId = tenantId.Value;
        _tenantContext.ResolutionSource = "Registration";

        var result = await _registrationService.RegisterIndividualAsync(
            new ApplicationRegistrationRequest(
                tenantId,
                req.RegistrationCountry,
                req.Title,
                req.FirstName,
                req.LastName,
                req.Email,
                req.Phone,
                req.Password),
            ct);

        var response = new IndividualRegistrationResponse(
            result.UserId,
            result.PartyId,
            MapSnapshot(result.Onboarding));

        await Send.OkAsync(response, ct);
    }

    private async Task<Guid?> ResolveTenantIdAsync(IndividualRegistrationRequest request, CancellationToken ct)
    {
        if (request.TenantId.HasValue && request.TenantId.Value != Guid.Empty)
        {
            return request.TenantId.Value;
        }

        var mode = _configuration.GetValue<TenantRoutingMode>("Auth:TenantRouting");
        return mode switch
        {
            TenantRoutingMode.Subdomain => await ResolveFromSubdomainAsync(ct),
            TenantRoutingMode.Header => ResolveFromHeader(),
            _ => null
        };
    }

    private async Task<Guid?> ResolveFromSubdomainAsync(CancellationToken ct)
    {
        var host = HttpContext.Request.Host.Host;
        var parts = host.Split('.');
        if (parts.Length < 3)
        {
            return null;
        }

        var subdomain = parts[0];
        return await _dbContext.Tenants
            .Where(t => t.Subdomain == subdomain && t.Status == "Active")
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);
    }

    private Guid? ResolveFromHeader()
    {
        var header = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        return Guid.TryParse(header, out var tenantId) ? tenantId : null;
    }

    private static OnboardingSnapshotResponse MapSnapshot(OnboardingSnapshot snapshot)
    {
        return new OnboardingSnapshotResponse(
            snapshot.UserId,
            snapshot.PartyId,
            snapshot.Gates.Select(MapGate).ToList(),
            snapshot.NextActions.ToList());
    }

    private static OnboardingGateStatusResponse MapGate(OnboardingGateStatus gate)
    {
        return new OnboardingGateStatusResponse(
            gate.Gate.ToString(),
            gate.IsSatisfied,
            gate.IsRequired,
            gate.RequiredActions.ToList());
    }
}
