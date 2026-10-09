using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.Platform.Contracts.Services.ContactEnquiries;

using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.ContactEnquiries;

public sealed class GetContactEnquiryEndpoint(IContactEnquiryService enquiries)
    : EndpointWithoutRequest<ContactEnquiryDetailDto>
{
    public override void Configure()
    {
        Get("/v1/admin/contact-enquiries/{id:guid}");
        Policies("AdminReadPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await enquiries.GetAsync(Route<Guid>("id"), ct);
        if (result is null) await Send.NotFoundAsync(ct);
        else await Send.OkAsync(result, ct);
    }
}
