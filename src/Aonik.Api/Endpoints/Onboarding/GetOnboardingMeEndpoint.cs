using System.Text.Json;
using FastEndpoints;

using Aonik.Api.Contracts.Onboarding;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Models.Onboarding;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Onboarding;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Api.Endpoints.Onboarding;

public class GetOnboardingMeEndpoint : EndpointWithoutRequest<OnboardingSnapshotResponse>
{
    private readonly IOnboardingPolicyEvaluator _onboardingPolicyEvaluator;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;

    public GetOnboardingMeEndpoint(
        IOnboardingPolicyEvaluator onboardingPolicyEvaluator,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext)
    {
        _onboardingPolicyEvaluator = onboardingPolicyEvaluator;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
    }

    public override void Configure()
    {
        Get("/v1/onboarding/me");
        Policies("Users.Read");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Authentication required." }, ct);
            return;
        }

        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Tenant context missing." }, ct);
            return;
        }

        var snapshot = await _onboardingPolicyEvaluator.EvaluateAsync(userId, ct);

        await _auditLogWriter.LogAsync(
            AuditEventNames.OnboardingActivated,
            "User",
            userId,
            tenantId,
            userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { userId, tenantId }),
            ct);

        await Send.OkAsync(MapResponse(snapshot), ct);
    }

    private static OnboardingSnapshotResponse MapResponse(OnboardingSnapshot snapshot)
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
