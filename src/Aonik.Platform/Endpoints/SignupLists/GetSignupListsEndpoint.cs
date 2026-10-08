using Aonik.Platform.Contracts.Models.SignupLists;
using Aonik.Platform.Contracts.Services.SignupLists;

using FastEndpoints;

namespace Aonik.Platform.Endpoints.SignupLists;

internal sealed class GetSignupListsEndpoint(ISignupListService signupLists)
    : EndpointWithoutRequest<SignupListsConfigurationDto>
{
    public override void Configure()
    {
        Get("/v1/signup-lists");
        AllowAnonymous();
        Summary(s => s.Summary = "Get the tenant's published signup lists and consent wording.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(await signupLists.GetConfigurationAsync(ct), ct);
    }
}
