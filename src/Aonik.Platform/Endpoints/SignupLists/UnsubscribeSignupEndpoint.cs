using Aonik.Platform.Contracts.Services.SignupLists;

using FastEndpoints;

namespace Aonik.Platform.Endpoints.SignupLists;

internal sealed class UnsubscribeSignupEndpoint(ISignupListService signupLists) : Endpoint<UnsubscribeSignupRequest>
{
    public override void Configure()
    {
        Post("/v1/signup-lists/{listType}/{subscriptionId:guid}/unsubscribe");
        AllowAnonymous();
        Summary(s => s.Summary = "Unsubscribe using a scoped token; repeated valid requests remain successful.");
    }

    public override void OnBeforeValidate(UnsubscribeSignupRequest req)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    public override async Task HandleAsync(UnsubscribeSignupRequest req, CancellationToken ct)
    {
        if (!await signupLists.UnsubscribeAsync(req.ListType, req.SubscriptionId, req.Body?.Token, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

internal sealed class UnsubscribeSignupRequest
{
    [RouteParam]
    public string ListType { get; set; } = string.Empty;

    [RouteParam]
    public Guid SubscriptionId { get; set; }

    [FromBody]
    public UnsubscribeSignupBody? Body { get; set; }
}

internal sealed record UnsubscribeSignupBody(string? Token);
