using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FastEndpoints;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Projects a <see cref="UserBrief"/> for an arbitrary user by ID.
/// Used by the Admin UI playground to load real user context for testing.
/// Unlike <see cref="GetUserBriefEndpoint"/>, this accepts an explicit user ID
/// rather than using the current authenticated user.
/// </summary>
internal sealed class ProjectUserBriefEndpoint : Endpoint<ProjectUserBriefRequest, UserBrief>
{
    private readonly IUserBriefProjector _projector;
    private readonly ITenantProvider _tenantProvider;

    public ProjectUserBriefEndpoint(
        IUserBriefProjector projector,
        ITenantProvider tenantProvider)
    {
        _projector = projector;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Post("/ai/playground/user-brief");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(ProjectUserBriefRequest req, CancellationToken ct)
    {
        if (req.UserId == Guid.Empty)
        {
            AddError("UserId is required");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var brief = await _projector.ProjectAsync(tenantId, req.UserId, null, ct);
        await Send.OkAsync(brief, ct);
    }
}

/// <summary>
/// Request DTO for projecting a user brief for the playground.
/// </summary>
public sealed record ProjectUserBriefRequest(Guid UserId);
