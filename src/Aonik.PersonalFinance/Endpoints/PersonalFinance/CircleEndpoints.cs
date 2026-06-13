using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

// The Circle (Spec 048): entity-scoped sharing + the Support Statement.
// Customer-scoped (UserPolicy), under /personal-finance/circle and the existing
// care-entities prefix for the statement.

// ── Create grant ────────────────────────────────────────────────────

internal sealed class CreateCircleGrantEndpoint : Endpoint<CreateCircleGrantRequest, CircleGrantResponse>
{
    private readonly ICircleService _service;
    public CreateCircleGrantEndpoint(ICircleService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/circle/grants");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Create a circle grant";
            s.Description = "Shares a scoped slice of the owner's records with a member (all | entities | docsOnly).";
            s.Response(201, "Grant created");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CreateCircleGrantRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _service.CreateGrantAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

// ── List "shared with" (owner's grants) ─────────────────────────────

internal sealed class ListCircleGrantsEndpoint : EndpointWithoutRequest<IReadOnlyList<CircleGrantResponse>>
{
    private readonly ICircleService _service;
    public ListCircleGrantsEndpoint(ICircleService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/circle/grants");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List grants you have shared (Shared with)";
            s.Response(200, "Grants returned");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _service.ListGrantsForOwnerAsync(ct), ct);
}

// ── List "can see" (grants where I'm the member) ────────────────────

internal sealed class ListCircleSharedWithMeEndpoint : EndpointWithoutRequest<IReadOnlyList<CircleGrantResponse>>
{
    private readonly ICircleService _service;
    public ListCircleSharedWithMeEndpoint(ICircleService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/circle/shared");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List grants shared with you (Can see)";
            s.Response(200, "Grants returned");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _service.ListGrantsForMemberAsync(ct), ct);
}

// ── Revoke grant ────────────────────────────────────────────────────

internal sealed class RevokeCircleGrantEndpoint : EndpointWithoutRequest
{
    private readonly ICircleService _service;
    public RevokeCircleGrantEndpoint(ICircleService service) => _service = service;

    public override void Configure()
    {
        Delete("/personal-finance/circle/grants/{id}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Revoke a circle grant (effective immediately)";
            s.Response(204, "Revoked");
            s.Response(401, "Not authenticated");
            s.Response(404, "Grant not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var revoked = await _service.RevokeGrantAsync(Route<Guid>("id"), ct);
        if (!revoked)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

// ── Create invite ───────────────────────────────────────────────────

internal sealed class CreateCircleInviteEndpoint : Endpoint<CreateCircleInviteRequest, CircleInviteResponse>
{
    private readonly ICircleService _service;
    public CreateCircleInviteEndpoint(ICircleService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/circle/invites");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Create a circle invite link";
            s.Description = "Mints a signed, single-use, 7-day invite token carrying the grant terms.";
            s.Response(201, "Invite created");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CreateCircleInviteRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _service.CreateInviteAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

// ── Accept invite ───────────────────────────────────────────────────

internal sealed class AcceptCircleInviteRequest
{
    public string Token { get; set; } = string.Empty;
}

internal sealed class AcceptCircleInviteRequestValidator : Validator<AcceptCircleInviteRequest>
{
    public AcceptCircleInviteRequestValidator() => RuleFor(x => x.Token).RequiredText(128);
}

internal sealed class AcceptCircleInviteEndpoint : Endpoint<AcceptCircleInviteRequest, CircleGrantResponse>
{
    private readonly ICircleService _service;
    public AcceptCircleInviteEndpoint(ICircleService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/circle/invites/accept");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Accept a circle invite";
            s.Description = "The authenticated user accepts a token, becoming the member of an active grant.";
            s.Response(200, "Invite accepted; grant returned");
            s.Response(401, "Not authenticated");
            s.Response(404, "Invite invalid, expired, or already used");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(AcceptCircleInviteRequest req, CancellationToken ct)
    {
        var grant = await _service.AcceptInviteAsync(req.Token, ct);
        if (grant is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(grant, ct);
    }
}

// ── Shared reads ────────────────────────────────────────────────────

internal sealed class ListSharedEntitiesEndpoint : EndpointWithoutRequest<IReadOnlyList<CareEntityRef>>
{
    private readonly ICircleService _service;
    public ListSharedEntitiesEndpoint(ICircleService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/circle/shared/{ownerUserId}/care-entities");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List the entities an owner shares with you";
            s.Response(200, "Entities returned");
            s.Response(401, "Not authenticated");
            s.Response(404, "No active grant");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _service.ListSharedEntitiesAsync(Route<Guid>("ownerUserId"), ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}

internal sealed class GetSharedEntityEndpoint : EndpointWithoutRequest<CircleSharedEntityResult>
{
    private readonly ICircleService _service;
    public GetSharedEntityEndpoint(ICircleService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/circle/shared/{ownerUserId}/care-entities/{careEntityId}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "View a shared entity (scoped)";
            s.Description = "Full view (amounts) for all|entities scope; amount-free docs-only view for docsOnly.";
            s.Response(200, "Scoped view returned");
            s.Response(401, "Not authenticated");
            s.Response(404, "No grant, out of scope, or not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _service.GetSharedEntityAsync(Route<Guid>("ownerUserId"), Route<Guid>("careEntityId"), ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}

// ── Support Statement ───────────────────────────────────────────────

internal sealed class GetSupportStatementEndpoint : EndpointWithoutRequest<StatementData>
{
    private readonly ISupportStatementService _service;
    public GetSupportStatementEndpoint(ISupportStatementService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/care-entities/{id}/statement");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Compose a Support Statement";
            s.Description = "Per-currency totals (never converted), corroboration column, receipt appendix, verification code. PDF is client-side.";
            s.Response(200, "Statement composed");
            s.Response(401, "Not authenticated");
            s.Response(404, "Entity not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var from = Query<DateTime?>("from", isRequired: false) ?? DateTime.UtcNow.Date.AddYears(-1);
        var to = Query<DateTime?>("to", isRequired: false) ?? DateTime.UtcNow.Date;
        var preparedFor = Query<string?>("preparedFor", isRequired: false);

        var result = await _service.ComposeAsync(id, from, to, preparedFor, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
