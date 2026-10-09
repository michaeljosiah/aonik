using Aonik.Platform.Contracts.Services.ContactEnquiries;
using Aonik.SharedKernel.Events;

namespace Aonik.Platform.Services.ContactEnquiries;

public sealed record ContactEnquiryDeliveryRequestedEvent(Guid TenantId, Guid EnquiryId, string Audience) : ITenantScopedEvent;

internal sealed class ContactEnquiryDeliveryRequestedHandler(IContactEnquiryService service)
    : IEventHandler<ContactEnquiryDeliveryRequestedEvent>
{
    public Task HandleAsync(ContactEnquiryDeliveryRequestedEvent @event, CancellationToken cancellationToken = default)
        => service.DeliverAsync(@event.EnquiryId, @event.Audience, cancellationToken);
}
