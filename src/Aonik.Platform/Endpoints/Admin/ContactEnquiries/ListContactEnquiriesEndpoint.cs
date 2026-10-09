using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.Platform.Contracts.Services.ContactEnquiries;
using Aonik.SharedKernel.Abstractions;

using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.ContactEnquiries;

public sealed class ListContactEnquiriesEndpoint(IContactEnquiryService enquiries)
    : Endpoint<ListContactEnquiriesRequest, PagedResult<ContactEnquirySummaryDto>>
{
    public override void Configure()
    {
        Get("/v1/admin/contact-enquiries");
        Policies("AdminReadPolicy");
    }

    public override async Task HandleAsync(ListContactEnquiriesRequest req, CancellationToken ct)
        => await Send.OkAsync(await enquiries.ListAsync(req.Page, req.PageSize, req.Topic, ct), ct);
}

public sealed class ListContactEnquiriesRequest
{
    [QueryParam] public int Page { get; set; } = 1;
    [QueryParam] public int PageSize { get; set; } = 20;
    [QueryParam] public string? Topic { get; set; }
}
