using Aonik.Platform.Contracts.Services.ContactEnquiries;

using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.ContactEnquiries;

public sealed class GetContactEnquiryImageEndpoint(IContactEnquiryService enquiries) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/v1/admin/contact-enquiries/{id:guid}/images/{imageId:guid}");
        Policies("AdminReadPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await enquiries.OpenImageAsync(Route<Guid>("id"), Route<Guid>("imageId"), ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await using var content = result.Content;
        await Send.StreamAsync(content, result.FileName, contentType: result.ContentType,
            enableRangeProcessing: false, cancellation: ct);
    }
}
