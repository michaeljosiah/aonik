using Aonik.Platform.Entities.Party;
using Aonik.Platform.Entities.Compliance;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Services.Notifications;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Aonik.Application.Tests.Notifications;

public sealed class CustomerReceiptContactEditorTests
{
    [Fact]
    public async Task Update_Should_PersistTheSavedContactAndPrivateAudit_WithoutChangingPrimaryStatus()
    {
        await using var h = new Harness();
        var requestId = Guid.NewGuid();
        var result = await h.Editor.UpdateEmailAsync(h.Customer.Id, h.Contact.Id, "original@example.test", " corrected@example.test ", requestId);

        result.Email.Should().Be("corrected@example.test");
        result.ContactId.Should().Be(h.Contact.Id);
        result.IsPrimary.Should().BeTrue();
        (await h.Db.PartyContacts.SingleAsync(c => c.Id == h.Contact.Id)).Value.Should().Be("corrected@example.test");
        var audit = await h.Db.AuditLogs.SingleAsync();
        audit.Action.Should().Be(AuditEventNames.CustomerEmailUpdated);
        audit.ActorId.Should().Be(h.ActorId);
        audit.TenantId.Should().Be(h.TenantId);
        audit.DetailsJson.Should().Contain(requestId.ToString()).And.Contain("BouncedReceiptReview").And.NotContain("@example.test");
    }

    [Theory]
    [InlineData("stale@example.test", "corrected@example.test", false)]
    [InlineData("original@example.test", "invalid email", true)]
    [InlineData("original@example.test", "Name <corrected@example.test>", true)]
    [InlineData("original@example.test", "secondary@example.test", false)]
    public async Task Update_Should_RejectStaleInvalidAndDuplicateAddresses(string expected, string updated, bool invalidFormat)
    {
        await using var h = new Harness();
        var act = () => h.Editor.UpdateEmailAsync(h.Customer.Id, h.Contact.Id, expected, updated, Guid.NewGuid());
        if (invalidFormat) await act.Should().ThrowAsync<ArgumentException>();
        else await act.Should().ThrowAsync<InvalidStateException>();
        h.Contact.Value.Should().Be("original@example.test");
        (await h.Db.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Update_Should_PreserveAnUnchangedEmailWithoutCreatingAMutationAudit()
    {
        await using var h = new Harness();
        var saved = await h.Editor.UpdateEmailAsync(h.Customer.Id, h.Contact.Id, "original@example.test", " ORIGINAL@example.test ", Guid.NewGuid());
        saved.Email.Should().Be("original@example.test");
        (await h.Db.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Update_Should_DiscardPendingContactAndAuditWritesAfterAConcurrencyFailure()
    {
        await using var h = new Harness(failSave: true);
        var act = () => h.Editor.UpdateEmailAsync(h.Customer.Id, h.Contact.Id, "original@example.test", "corrected@example.test", Guid.NewGuid());
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*Reload and review*");
        await h.Db.SaveChangesAsync();
        (await h.Db.PartyContacts.AsNoTracking().SingleAsync(c => c.Id == h.Contact.Id)).Value.Should().Be("original@example.test");
        (await h.Db.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Update_Should_EnforceCustomerOwnershipTenantAndWritePermission()
    {
        await using var h = new Harness();
        var foreign = new Party { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), DisplayName = "Foreign customer" };
        var other = new Party { Id = Guid.NewGuid(), TenantId = h.TenantId, DisplayName = "Another customer" };
        h.Db.Parties.AddRange(foreign, other);
        await h.Db.SaveChangesAsync();
        foreach (var partyId in new[] { foreign.Id, other.Id })
        {
            var act = () => h.Editor.UpdateEmailAsync(partyId, h.Contact.Id, "original@example.test", "corrected@example.test", Guid.NewGuid());
            await act.Should().ThrowAsync<NotFoundException>();
        }
        h.Permissions.Setup(p => p.HasPermissionAsync(h.ActorId, "Customers.Write", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var denied = () => h.Editor.UpdateEmailAsync(h.Customer.Id, h.Contact.Id, "original@example.test", "corrected@example.test", Guid.NewGuid());
        await denied.Should().ThrowAsync<PermissionDeniedException>();
        h.Contact.Value.Should().Be("original@example.test");
        (await h.Db.AuditLogs.CountAsync()).Should().Be(0);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public PlatformDbContext Db { get; }
        public Party Customer { get; }
        public PartyContact Contact { get; }
        public CustomerReceiptContactEditor Editor { get; }
        public Mock<IPermissionService> Permissions { get; } = new();

        public Harness(bool failSave = false)
        {
            var tenant = new TestTenantProvider(TenantId);
            var actor = new Mock<ICurrentUserProvider>();actor.Setup(a => a.GetCurrentUserId()).Returns(ActorId);
            var clock = new Mock<IClock>();clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));
            Db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>().UseInMemoryDatabase($"ReceiptEditor_{Guid.NewGuid()}").Options, tenant, actor.Object, clock.Object);
            Customer = new Party { Id = Guid.NewGuid(), TenantId = TenantId, DisplayName = "Nadia" };
            Contact = new PartyContact { Id = Guid.NewGuid(), PartyId = Customer.Id, Type = "Email", Value = "original@example.test", IsPrimary = true };
            Db.Parties.Add(Customer);
            Db.PartyContacts.AddRange(Contact, new PartyContact { PartyId = Customer.Id, Type = "Email", Value = "secondary@example.test" });
            Db.SaveChanges();
            Permissions.Setup(p => p.HasPermissionAsync(ActorId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            IAuditLogWriter audit = new AuditLogWriter(Db, tenant, actor.Object, new Mock<ICorrelationContext>().Object, clock.Object);
            if (failSave)
            {
                var failingAudit = new Mock<IAuditLogWriter>();
                failingAudit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                    .Callback(() => Db.AuditLogs.Add(new AuditLog { TenantId = TenantId, Action = AuditEventNames.CustomerEmailUpdated }))
                    .ThrowsAsync(new DbUpdateConcurrencyException("Simulated competing contact update"));
                audit = failingAudit.Object;
            }
            Editor = new(Db, tenant, actor.Object, Permissions.Object, audit, clock.Object);
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
