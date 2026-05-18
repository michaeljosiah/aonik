using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.UserBrief;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Projects a <see cref="UserBrief"/> for an arbitrary user by ID or party ID.
/// Used by the Admin UI playground to load real user context for testing.
/// Unlike <see cref="GetUserBriefEndpoint"/>, this accepts an explicit user/party ID
/// rather than using the current authenticated user.
/// </summary>
internal sealed class ProjectUserBriefEndpoint : Endpoint<ProjectUserBriefRequest, ProjectUserBriefResponse>
{
    private readonly IUserBriefProjector _projector;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserBriefContextDataProvider _contextDataProvider;
    private readonly IPersonalFinancePartyResolver? _personalFinancePartyResolver;

    public ProjectUserBriefEndpoint(
        IUserBriefProjector projector,
        ITenantProvider tenantProvider,
        IUserBriefContextDataProvider contextDataProvider,
        IPersonalFinancePartyResolver? personalFinancePartyResolver = null)
    {
        _projector = projector;
        _tenantProvider = tenantProvider;
        _contextDataProvider = contextDataProvider;
        _personalFinancePartyResolver = personalFinancePartyResolver;
    }

    public override void Configure()
    {
        Post("/ai/playground/user-brief");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Project user brief by ID";
            s.Description = "Projects a user brief for an arbitrary user by user ID or party ID. Returns the resolved user id alongside the brief so the playground can pass it as ImpersonateUserId on subsequent /ai/playground/run calls.";
            s.Response(200, "Success");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(ProjectUserBriefRequest req, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = req.UserId;

        if (userId == Guid.Empty && req.PartyId != Guid.Empty)
        {
            // First try the Platform-side UserParty link.
            var resolved = await _contextDataProvider.GetUserIdForPartyAsync(tenantId, req.PartyId, ct);

            // Fall back to PersonalProfile.PartyId so demo personas (e.g. the
            // seeded Seamus / Mark Keane personas — which have a synthetic
            // UserId in PersonalProfile but no real UserParty link) resolve
            // cleanly. The playground User Brief picker would otherwise 400
            // with "no user linked to this party" for every seeded persona.
            if (resolved is null && _personalFinancePartyResolver is not null)
            {
                resolved = await _personalFinancePartyResolver.GetUserIdForPartyAsync(tenantId, req.PartyId, ct);
            }

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
        await Send.OkAsync(new ProjectUserBriefResponse(userId, brief), ct);
    }
}

/// <summary>
/// Wraps the projected user brief with the resolved user id so the Admin UI
/// can use it as <c>ImpersonateUserId</c> when sending the brief into a
/// playground run — needed so personal-finance sub-agents target the briefed
/// user's data instead of the calling admin's.
/// </summary>
public sealed record ProjectUserBriefResponse(
    Guid UserId,
    UserBrief Brief);

/// <summary>
/// Request DTO for projecting a user brief for the playground.
/// Accepts either a UserId directly or a PartyId (resolved to a user).
/// </summary>
public sealed class ProjectUserBriefRequest
{
    public Guid UserId { get; set; }
    public Guid PartyId { get; set; }
}
