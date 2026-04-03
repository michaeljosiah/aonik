using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.UserBrief;
using FastEndpoints;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Projects a <see cref="UserBrief"/> for an arbitrary user by ID or party ID.
/// Used by the Admin UI playground to load real user context for testing.
/// Unlike <see cref="GetUserBriefEndpoint"/>, this accepts an explicit user/party ID
/// rather than using the current authenticated user.
/// </summary>
internal sealed class ProjectUserBriefEndpoint : Endpoint<ProjectUserBriefRequest, UserBrief>
{
    private readonly IUserBriefProjector _projector;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserBriefContextDataProvider _contextDataProvider;

    public ProjectUserBriefEndpoint(
        IUserBriefProjector projector,
        ITenantProvider tenantProvider,
        IUserBriefContextDataProvider contextDataProvider)
    {
        _projector = projector;
        _tenantProvider = tenantProvider;
        _contextDataProvider = contextDataProvider;
    }

    public override void Configure()
    {
        Post("/ai/playground/user-brief");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(ProjectUserBriefRequest req, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = req.UserId;

        if (userId == Guid.Empty && req.PartyId != Guid.Empty)
        {
            var resolved = await _contextDataProvider.GetUserIdForPartyAsync(tenantId, req.PartyId, ct);
            if (resolved is null)
            {
                AddError("No user linked to this party");
                await Send.ErrorsAsync(cancellation: ct);
                return;
            }

            userId = resolved.Value;
        }

        if (userId == Guid.Empty)
        {
            AddError("Either UserId or PartyId is required");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var brief = await _projector.ProjectAsync(tenantId, userId, null, ct);
        await Send.OkAsync(brief, ct);
    }
}

/// <summary>
/// Request DTO for projecting a user brief for the playground.
/// Accepts either a UserId directly or a PartyId (resolved to a user).
/// </summary>
public sealed record ProjectUserBriefRequest(Guid UserId = default, Guid PartyId = default);
