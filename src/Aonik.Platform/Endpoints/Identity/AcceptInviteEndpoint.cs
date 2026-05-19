using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Aonik.Platform.Endpoints.Identity;

/// <summary>
/// Spec 026 Part 1 — <c>POST /identity/invite/accept</c>. The caller
/// is authenticated against the IdP (Auth0 / Entra) but does not yet
/// have an active platform identity. The endpoint consumes the
/// one-shot invite token, links the IdP identity onto the matching
/// placeholder, and (transparently) lets the next request authenticate
/// normally as an active user.
/// </summary>
public class AcceptInviteEndpoint : Endpoint<AcceptInviteRequest, AcceptInviteResponse>
{
    private readonly IInviteAcceptanceService _inviteAcceptanceService;
    private readonly ICurrentUserContext _currentUserContext;

    public AcceptInviteEndpoint(
        IInviteAcceptanceService inviteAcceptanceService,
        ICurrentUserContext currentUserContext)
    {
        _inviteAcceptanceService = inviteAcceptanceService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("/identity/invite/accept");
        // The endpoint relies on the standard JWT validation pipeline
        // having already run, so the claims principal is trustworthy.
        // We don't pin to a specific scheme — the platform policy
        // scheme ("Aonik") picks Auth0 vs AzureAd based on the issuer
        // in the token, exactly as for every other authenticated
        // endpoint.
        Summary(s =>
        {
            s.Summary = "Accept an invitation by consuming the one-shot invite token.";
            s.Description = "Spec 026 Part 1. Requires (a) a valid IdP bearer token in the Authorization header and (b) the one-shot invite token in the body. On success the placeholder row is linked to the IdP identity and the user is logged in.";
            s.Response(200, "Invite accepted");
            s.Response(401, "Bearer token missing or invalid");
            s.Response(400, "Invite token missing/invalid/expired or email mismatch");
        });
        Options(x => x.WithTags("Identity"));
    }

    public override async Task HandleAsync(AcceptInviteRequest req, CancellationToken ct)
    {
        // The JWT pipeline at AonikAuthenticationSetup populates these
        // already; we re-derive directly from claims to keep the
        // service-layer call signature small.
        var issuer = HttpContext.User.FindFirstValue("iss") ?? _currentUserContext.ExternalIssuer ?? string.Empty;
        var subject = HttpContext.User.FindFirstValue("oid")
                      ?? HttpContext.User.FindFirstValue("sub")
                      ?? _currentUserContext.ExternalSubject
                      ?? string.Empty;
        var tid = HttpContext.User.FindFirstValue("tid");
        var email = HttpContext.User.FindFirstValue("email")
                    ?? HttpContext.User.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new AcceptInviteResponse(
                Guid.Empty, Guid.Empty, string.Empty, false, "missing_idp_claims"), ct);
            return;
        }

        var result = await _inviteAcceptanceService.AcceptInviteAsync(
            req,
            issuer,
            subject,
            tid,
            email,
            ct);

        if (!result.Accepted)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
        await HttpContext.Response.WriteAsJsonAsync(result, ct);
    }
}
