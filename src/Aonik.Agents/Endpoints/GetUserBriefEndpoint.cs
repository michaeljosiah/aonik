using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FastEndpoints;

namespace Aonik.Agents.Endpoints;

internal sealed class GetUserBriefEndpoint : EndpointWithoutRequest<UserBrief>
{
    private readonly IUserBriefProjector _projector;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetUserBriefEndpoint(
        IUserBriefProjector projector,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _projector = projector;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public override void Configure()
    {
        Get("/ai/user-brief");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (userId is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var brief = await _projector.ProjectAsync(tenantId, userId.Value, null, ct);
        await Send.OkAsync(brief, ct);
    }
}
