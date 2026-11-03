using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Notifications;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Aonik.Application.Tests.Notifications;

public sealed class CustomerReceiptContactReaderTests
{
    [Fact]
    public async Task Read_Should_ReturnOnlyValidSavedEmailsForTheCurrentTenantCustomer()
    {
        var tenant = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var other = Guid.NewGuid();
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"ReceiptContact_{Guid.NewGuid()}").Options, new TestTenantProvider(tenant));
        db.Parties.AddRange(new Party { Id = customer, TenantId = tenant, DisplayName = "Nadia" },
            new Party { Id = other, TenantId = Guid.NewGuid(), DisplayName = "Other tenant" });
        db.PartyContacts.AddRange(
            new PartyContact { PartyId = customer, Type = "Email", Value = " secondary@example.test " },
            new PartyContact { PartyId = customer, Type = "Email", Value = "primary@example.test", IsPrimary = true },
            new PartyContact { PartyId = customer, Type = "Email", Value = "not-an-email" },
            new PartyContact { PartyId = customer, Type = "Email", Value = "Name <address@example.test>" },
            new PartyContact { PartyId = customer, Type = "Phone", Value = "phone@example.test" },
            new PartyContact { PartyId = other, Type = "Email", Value = "foreign@example.test" });
        await db.SaveChangesAsync();
        var profile = new Mock<IPublicBusinessProfileService>();
        profile.Setup(p => p.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PublicBusinessProfileDto("Meridian Remit"));
        var reader = new CustomerReceiptContactReader(db, new TestTenantProvider(tenant), profile.Object);

        var result = await reader.GetAsync(customer);

        result!.CustomerName.Should().Be("Nadia");
        result.BusinessName.Should().Be("Meridian Remit");
        result.Contacts.Select(c => c.Email).Should().Equal("primary@example.test", "secondary@example.test");
        (await reader.GetAsync(other)).Should().BeNull();
    }
}
