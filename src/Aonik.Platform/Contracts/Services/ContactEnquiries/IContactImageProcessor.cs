using Aonik.Platform.Contracts.Models.ContactEnquiries;

namespace Aonik.Platform.Contracts.Services.ContactEnquiries;

public interface IContactImageProcessor
{
    Task<ProcessedContactImage> ProcessAsync(ContactImageUpload image, CancellationToken cancellationToken = default);
}
