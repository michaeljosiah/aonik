using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Groups;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Groups.Endpoints;

// The first HTTP surface of the Groups module (Spec 086, aonik#326): the calls a consumer product
// needs to stand a family up — create it, see it, add a child who has no login of their own. Every
// call acts as the caller's party; a group the caller is not an accepted member of answers 404.

/// <param name="Kind">One of <c>family</c> or <c>household</c>.</param>
public sealed record CreateGroupRequest(string Kind, string Name);

public sealed record GroupListResponse(IReadOnlyList<GroupDto> Groups);

/// <param name="PartyId">A party with no user — a child, enrolled through consent. A person with a login is invited instead.</param>
/// <param name="Role"><c>manager</c> or <c>viewer</c>; a child is a viewer.</param>
public sealed record AddGroupMemberRequest(Guid PartyId, string Role);

public sealed record GroupProblem(int Status, string Code, string Message);

internal abstract class GroupEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
{
    protected const string Tag = "Groups";
    protected const string MemberPolicy = "UserPolicy";

    protected Task ProblemAsync(GroupProblem problem)
        => Send.ResultAsync(Results.Json(problem, statusCode: problem.Status));

    protected static GroupProblem NoSuchGroup() => new(StatusCodes.Status404NotFound, "group-not-found", "No such group is available to you.");

    protected async Task GuardedAsync(Func<Task> body)
    {
        try
        {
            await body();
        }
        catch (NotFoundException)
        {
            await ProblemAsync(NoSuchGroup());
        }
        catch (PermissionDeniedException denied)
        {
            await ProblemAsync(new GroupProblem(StatusCodes.Status403Forbidden, "forbidden", denied.Message));
        }
        catch (InvalidStateException invalid)
        {
            await ProblemAsync(new GroupProblem(StatusCodes.Status409Conflict, "invalid-state", invalid.Message));
        }
        catch (ArgumentException invalid)
        {
            await ProblemAsync(new GroupProblem(StatusCodes.Status422UnprocessableEntity, "invalid-request", invalid.Message));
        }
    }

    /// <summary>Membership is the only thing that makes a group visible; a foreign or absent id answers the same 404.</summary>
    protected async Task<GroupDto?> VisibleGroupAsync(IGroupService groups, Guid groupId, CancellationToken ct)
    {
        var mine = await groups.GetMineAsync(ct);
        return mine.FirstOrDefault(group => group.Id == groupId);
    }
}

internal sealed class CreateGroupEndpoint : GroupEndpoint<CreateGroupRequest, GroupDto>
{
    private readonly IGroupService _groups;

    public CreateGroupEndpoint(IGroupService groups) => _groups = groups;

    public override void Configure()
    {
        Post("/groups");
        Policies(MemberPolicy);
        Summary(s =>
        {
            s.Summary = "Create a group";
            s.Description = "Creates a family or household with the caller's party as its owner.";
            s.Response(201, "Group created");
            s.Response(422, "Unknown kind or missing name");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(CreateGroupRequest req, CancellationToken ct)
    {
        var kind = req.Kind.Trim().ToLowerInvariant();

        await GuardedAsync(async () =>
        {
            var created = await _groups.CreateAsync(new CreateGroupCommand(kind, req.Name.Trim()), ct);
            await Send.CreatedAtAsync<GetGroupEndpoint>(new { groupId = created.Id }, created, cancellation: ct);
        });
    }
}

internal sealed class ListMyGroupsEndpoint : GroupEndpoint<EmptyRequest, GroupListResponse>
{
    private readonly IGroupService _groups;

    public ListMyGroupsEndpoint(IGroupService groups) => _groups = groups;

    public override void Configure()
    {
        Get("/groups/mine");
        Policies(MemberPolicy);
        Summary(s =>
        {
            s.Summary = "List my groups";
            s.Description = "The groups the caller is an accepted member of, with their members and roles.";
            s.Response(200, "Groups returned");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        await GuardedAsync(async () =>
        {
            var mine = await _groups.GetMineAsync(ct);
            await Send.OkAsync(new GroupListResponse(mine), ct);
        });
    }
}

internal sealed class GetGroupEndpoint : GroupEndpoint<EmptyRequest, GroupDto>
{
    private readonly IGroupService _groups;

    public GetGroupEndpoint(IGroupService groups) => _groups = groups;

    public override void Configure()
    {
        Get("/groups/{groupId:guid}");
        Policies(MemberPolicy);
        Summary(s =>
        {
            s.Summary = "Get a group";
            s.Description = "One group the caller is an accepted member of, with its members and roles. Any other id answers 404.";
            s.Response(200, "Group returned");
            s.Response(404, "No such group is available to the caller");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        await GuardedAsync(async () =>
        {
            var group = await VisibleGroupAsync(_groups, Route<Guid>("groupId"), ct);

            if (group is null)
            {
                await ProblemAsync(NoSuchGroup());
                return;
            }

            await Send.OkAsync(group, ct);
        });
    }
}

internal sealed class AddGroupMemberEndpoint : GroupEndpoint<AddGroupMemberRequest, GroupMemberDto>
{
    private readonly IGroupService _groups;

    public AddGroupMemberEndpoint(IGroupService groups) => _groups = groups;

    public override void Configure()
    {
        Post("/groups/{groupId:guid}/members");
        Policies(MemberPolicy);
        Summary(s =>
        {
            s.Summary = "Add a member without a login";
            s.Description = "Adds a party that has no user — a child enrolled through consent — as a member of a group the caller "
                + "manages. A party with a login must be invited instead. Requires the caller to be an owner or manager.";
            s.Response(200, "Member added");
            s.Response(403, "The caller does not manage this group");
            s.Response(404, "No such group is available to the caller");
            s.Response(409, "The party has a login, or is already a member");
            s.Response(422, "Unknown role");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(AddGroupMemberRequest req, CancellationToken ct)
    {
        var groupId = Route<Guid>("groupId");

        await GuardedAsync(async () =>
        {
            if (await VisibleGroupAsync(_groups, groupId, ct) is null)
            {
                await ProblemAsync(NoSuchGroup());
                return;
            }

            var member = await _groups.AddMemberAsync(groupId, req.PartyId, req.Role, ct);
            await Send.OkAsync(member, ct);
        });
    }
}
