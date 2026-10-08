using Aonik.Platform.Contracts.Models.SignupLists;
using Aonik.Platform.Contracts.Services.SignupLists;

using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.SignupLists;

internal sealed class GetSignupAreaDemandEndpoint(ISignupListService signupLists)
    : EndpointWithoutRequest<List<SignupAreaDemandDto>>
{
    public override void Configure()
    {
        Get("/admin/signup-lists/delivery-availability/areas");
        Policies("AdminPolicy");
        Summary(s => s.Summary = "Count active delivery-availability subscriptions by postcode area.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(await signupLists.GetAreaDemandAsync(ct), ct);
    }
}
