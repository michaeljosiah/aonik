using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Contracts.Services.ContactEnquiries;

public interface IContactEnquiryService
{
    Task<ContactEnquiryReceipt> SubmitAsync(ContactEnquirySubmission command, CancellationToken cancellationToken = default);
    Task<PagedResult<ContactEnquirySummaryDto>> ListAsync(int page = 1, int pageSize = 20, string? topic = null, CancellationToken cancellationToken = default);
    Task<ContactEnquiryDetailDto?> GetAsync(Guid enquiryId, CancellationToken cancellationToken = default);
    Task<ContactEnquiryImageDownload?> OpenImageAsync(Guid enquiryId, Guid imageId, CancellationToken cancellationToken = default);
    Task DeliverAsync(Guid enquiryId, string audience, CancellationToken cancellationToken = default);
}
