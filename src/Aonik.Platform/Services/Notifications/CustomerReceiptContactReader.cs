using System.Net.Mail;

using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Platform.Services.Notifications;

internal sealed class CustomerReceiptContactReader(PlatformDbContext db, ITenantProvider tenantProvider, IPublicBusinessProfileService businessProfile)
    : ICustomerReceiptContactReader
{
    public async Task<CustomerReceiptContacts?> GetAsync(Guid customerPartyId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var customer = await db.Parties.AsNoTracking().SingleOrDefaultAsync(party => party.Id == customerPartyId && party.TenantId == tenantId, cancellationToken);
        if (customer is null) return null;
        var contacts = await db.PartyContacts.AsNoTracking().Where(contact => contact.PartyId == customerPartyId && contact.Type == "Email")
            .OrderByDescending(contact => contact.IsPrimary).ThenBy(contact => contact.Id).ToListAsync(cancellationToken);
        var profile = await businessProfile.GetCurrentAsync(cancellationToken);
        return new CustomerReceiptContacts(customer.DisplayName, profile?.DisplayName ?? "AONIK", contacts
            .Where(contact => MailAddress.TryCreate(contact.Value.Trim(), out var address) && address.Address == contact.Value.Trim())
            .Select(contact => new ReceiptEmailContact(contact.Id, contact.Value.Trim(), contact.IsPrimary)).ToList());
    }
}
