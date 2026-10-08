using Aonik.Platform.Contracts.Models.SignupLists;
using Aonik.Platform.Contracts.Services.SignupLists;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.SignupLists;

internal sealed class CaptureSignupEndpoint(ISignupListService signupLists) : Endpoint<CaptureSignupRequest>
{
    public override void Configure()
    {
        Post("/v1/signup-lists/{listType}");
        AllowAnonymous();
        Summary(s => s.Summary = "Join a published signup list using its current consent version.");
    }

    public override void OnBeforeValidate(CaptureSignupRequest req)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    public override async Task HandleAsync(CaptureSignupRequest req, CancellationToken ct)
    {
        await signupLists.CaptureAsync(req.ListType, req.Capture!, ct);
        await Send.StatusCodeAsync(StatusCodes.Status202Accepted, ct);
    }
}

internal sealed class CaptureSignupRequest
{
    [RouteParam]
    public string ListType { get; set; } = string.Empty;

    [FromBody]
    public SignupCaptureRequest? Capture { get; set; }
}
