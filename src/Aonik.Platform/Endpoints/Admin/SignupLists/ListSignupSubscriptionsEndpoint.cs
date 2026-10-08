using Aonik.Platform.Contracts.Models.SignupLists;
using Aonik.Platform.Contracts.Services.SignupLists;
using Aonik.SharedKernel.Abstractions;

using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.SignupLists;

internal sealed class ListSignupSubscriptionsEndpoint(ISignupListService signupLists)
    : Endpoint<ListSignupSubscriptionsRequest, PagedResult<SignupSubscriptionDto>>
{
    public override void Configure()
    {
        Get("/admin/signup-lists/{listType}");
        Policies("AdminPolicy");
        Summary(s => s.Summary = "List tenant signup subscriptions; active subscriptions are the default.");
    }

    public override void OnBeforeValidate(ListSignupSubscriptionsRequest req)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
    }

    public override async Task HandleAsync(ListSignupSubscriptionsRequest req, CancellationToken ct)
    {
        await Send.OkAsync(await signupLists.ListAsync(req.ListType, req.IncludeUnsubscribed,
            req.PageNumber, req.PageSize, ct), ct);
    }
}

internal sealed class ListSignupSubscriptionsRequest
{
    [RouteParam]
    public string ListType { get; set; } = string.Empty;

    [QueryParam]
    public bool IncludeUnsubscribed { get; set; }

    [QueryParam]
    public int PageNumber { get; set; } = 1;

    [QueryParam]
    public int PageSize { get; set; } = 50;
}
