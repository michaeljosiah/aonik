using System.Security.Cryptography;
using System.Text;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Services;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Aonik.PersonalFinance.Endpoints;

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
            s.Description = "The authenticated user accepts a token, becoming the member of an active grant. "
                + "Idempotent — a repeat accept by the same user returns their existing grant (200).";
            s.Response(200, "Invite accepted (or already accepted by this user); grant returned");
            s.Response(401, "Not authenticated");
            s.Response(404, "Invite invalid, expired, or already used by another member");
            s.Response(409, "You cannot accept your own invite");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(AcceptCircleInviteRequest req, CancellationToken ct)
    {
        var result = await _service.AcceptInviteAsync(req.Token, ct);
        switch (result.Status)
        {
            case AcceptInviteStatus.Accepted:
                await Send.OkAsync(result.Grant!, ct);
                return;

            case AcceptInviteStatus.SelfAccept:
                // A state conflict, distinct from an invalid token (404) — you cannot join your own circle.
                HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                await HttpContext.Response.WriteAsJsonAsync(new { error = "You cannot accept your own invite." }, ct);
                return;

            default: // Invalid — fail-closed 404 for invalid / expired / consumed-by-another
                await Send.NotFoundAsync(ct);
                return;
        }
    }
}

// ── Preview invite (anonymous, Spec 061 §5) ─────────────────────────

internal sealed class GetInvitePreviewEndpoint : EndpointWithoutRequest<InvitePreviewResponse>
{
    private readonly ICircleService _service;
    private readonly IInvitePreviewRateLimiter _rateLimiter;
    private readonly ILogger<GetInvitePreviewEndpoint> _logger;

    public GetInvitePreviewEndpoint(
        ICircleService service,
        IInvitePreviewRateLimiter rateLimiter,
        ILogger<GetInvitePreviewEndpoint> logger)
    {
        _service = service;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/personal-finance/circle/invites/{token}/preview");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Preview a circle invite (anonymous)";
            s.Description = "Fail-closed, rate-limited, amount-free headline of an invite — owner name, scope, "
                + "shared entity names, expiry — so a signed-out recipient sees what they're joining before "
                + "signing up. One 404 for any invalid / expired / consumed / revoked token (no oracle).";
            s.Response(200, "Preview returned");
            s.Response(404, "Invite invalid, expired, consumed, or revoked");
            s.Response(429, "Too many preview requests");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var token = Route<string>("token") ?? string.Empty;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var tokenRef = TokenRef(token);

        if (!_rateLimiter.ShouldAllow(clientIp, token))
        {
            _logger.LogWarning("Circle invite preview rate-limited: tokenRef={TokenRef} ip={Ip}", tokenRef, clientIp);
            HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Too many requests." }, ct);
            return;
        }

        var preview = await _service.PreviewInviteAsync(token, ct);

        // Audit the read by a non-reversible token reference (never the raw token) + outcome (§10).
        _logger.LogInformation(
            "Circle invite preview: outcome={Outcome} tokenRef={TokenRef} ip={Ip}",
            preview is null ? "not_found" : "ok", tokenRef, clientIp);

        if (preview is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(preview, ct);
    }

    /// <summary>A short, non-reversible reference to the token for audit correlation — never the raw secret.</summary>
    private static string TokenRef(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return "empty";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(hash)[..12];
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

internal sealed class GetSharedPaymentLogsEndpoint : EndpointWithoutRequest<CircleSharedPaymentLogsResult>
{
    private readonly ICircleService _service;
    public GetSharedPaymentLogsEndpoint(ICircleService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/circle/shared/{ownerUserId}/care-entities/{careEntityId}/payment-logs");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List a shared entity's expenses (paged)";
            s.Description = "The full expense list behind the entity view's recent-log preview — newest first, "
                + "each row carrying its corroboration status. 404 for a docsOnly / no-amounts member, so the "
                + "no-amounts property holds.";
            s.Response(200, "Expenses returned");
            s.Response(401, "Not authenticated");
            s.Response(404, "No grant, out of scope, amounts not permitted, or not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var page = Query<int?>("page", isRequired: false) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: false) ?? 20;
        var result = await _service.GetSharedPaymentLogsAsync(
            Route<Guid>("ownerUserId"), Route<Guid>("careEntityId"), page, pageSize, ct);
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
