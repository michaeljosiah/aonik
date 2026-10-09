using System.Text.Json;

using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Identity;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Identity;

using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Aonik.Application.Tests.Identity;

public class AccountAccessServiceTests
{
    [Fact]
    public async Task Issue_Should_BeIdempotentAndStoreOnlyReferencesInOutbox_WithoutCreatingAccount()
    {
        using var h = new Harness();
        var request = h.Request();
        await h.Service.IssueAsync(request);
        await h.Service.IssueAsync(request);

        (await h.Db.AccountAccessActions.CountAsync()).Should().Be(1);
        (await h.Db.Users.CountAsync()).Should().Be(0);
        var message = await h.Db.Set<OutboxMessage>().SingleAsync();
        message.Payload.Should().NotContain(request.Email).And.NotContain("token").And.NotContain("action_url");
        message.EventType.Should().EndWith(nameof(AccountAccessDeliveryRequestedEvent));
        h.Email.Messages.Should().BeEmpty();
        await h.Service.Awaiting(x => x.IssueAsync(request with { OrderId = Guid.NewGuid() }))
            .Should().ThrowAsync<InvalidStateException>();
    }

    [Fact]
    public async Task Complete_Should_RequireMatchingVerifiedIdentity_ThenConsumeAndProvisionOnce()
    {
        using var h = new Harness();
        var token = await h.IssueAndDeliverAsync();
        (await h.Service.CompleteAsync(token, h.Proof() with { VerifiedEmail = "other@example.test" }, AccountAccessPurposes.PaidSetup))
            .Should().BeFalse();
        (await h.Db.Users.CountAsync()).Should().Be(0);
        (await h.Service.CompleteAsync(token, h.Proof(), AccountAccessPurposes.EmailChange)).Should().BeFalse();

        (await h.Service.CompleteAsync(token, h.Proof(), AccountAccessPurposes.PaidSetup)).Should().BeTrue();
        (await h.Service.CompleteAsync(token, h.Proof(), AccountAccessPurposes.PaidSetup)).Should().BeFalse();

        var user = await h.Db.Users.SingleAsync();
        user.ExternalSubject.Should().Be("auth0|customer");
        user.Email.Should().Be("paid@example.test");
        (await h.Db.UserParties.CountAsync()).Should().Be(1);
        (await h.Db.Parties.CountAsync()).Should().Be(1);
        (await h.Db.AccountAccessActions.SingleAsync()).ConsumedByUserId.Should().Be(user.Id);
        (await h.Db.Set<OutboxMessage>().CountAsync(x => x.EventType.EndsWith("AccountAccessVerifiedEvent"))).Should().Be(1);
    }

    [Fact]
    public async Task Complete_Should_NotLinkByEmailOrAcrossTenant()
    {
        using var h = new Harness();
        var token = await h.IssueAndDeliverAsync();
        h.Db.Users.Add(new User
        {
            TenantId = h.Tenant.GetCurrentTenantId(), ExternalIssuer = "https://other.example.test/",
            ExternalSubject = "another-user", Email = "paid@example.test"
        });
        await h.Db.SaveChangesAsync();
        (await h.Service.CompleteAsync(token, h.Proof(), AccountAccessPurposes.PaidSetup)).Should().BeFalse();
        (await h.Db.UserParties.CountAsync()).Should().Be(0);
        var otherTenant = Guid.NewGuid();
        h.Tenant.Id = otherTenant;
        (await h.Service.ResolveAsync(token)).Should().BeFalse();
        (await h.Service.CompleteAsync(token, h.Proof(), AccountAccessPurposes.PaidSetup)).Should().BeFalse();
    }

    [Fact]
    public async Task Resend_Should_RotateGeneration_InvalidateOldLink_AndRemainNeutralAfterConsumption()
    {
        using var h = new Harness();
        var original = await h.IssueAndDeliverAsync();
        h.Clock.UtcNow = h.Clock.UtcNow.AddMinutes(11);
        (await h.Service.ResolveAsync(original)).Should().BeFalse();
        await h.Service.ResendAsync(original);
        var action = await h.Db.AccountAccessActions.AsNoTracking().SingleAsync();
        action.SendCount.Should().Be(2);
        action.DeliveryStartedAtUtc.Should().BeNull();
        (await h.Service.ResolveAsync(original)).Should().BeFalse();
        await h.Service.DeliverAsync(action.Id, action.Generation);
        var replacement = h.Email.Token;
        (await h.Service.ResolveAsync(replacement)).Should().BeTrue();
        (await h.Service.CompleteAsync(replacement, h.Proof(), AccountAccessPurposes.PaidSetup)).Should().BeTrue();
        await h.Service.ResendAsync(replacement);
        await h.Service.ResendAsync("nonsense");
        (await h.Db.AccountAccessActions.AsNoTracking().SingleAsync()).SendCount.Should().Be(2);
    }

    [Fact]
    public async Task Resend_Should_EnforceAggregateTargetBudgetAcrossIndependentActions()
    {
        using var h = new Harness();
        var first = await h.IssueAndDeliverAsync();
        for (var i = 0; i < 9; i++) await h.Service.IssueAsync(h.Request());
        var original = await h.Db.AccountAccessActions.AsNoTracking().OrderBy(x => x.CreatedAt).FirstAsync();
        await h.Service.ResendAsync(first);

        (await h.Db.AccountAccessActions.SumAsync(x => x.SendCount)).Should().Be(10);
        (await h.Db.Set<OutboxMessage>().CountAsync()).Should().Be(10);
        (await h.Service.ResolveAsync(first)).Should().BeTrue();
        (await h.Db.AccountAccessActions.AsNoTracking().SingleAsync(x => x.Id == original.Id)).Generation.Should().Be(original.Generation);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("{\"isEnabled\":true,\"storefrontOrigin\":\"http://store.test\",\"setupPath\":\"/access\",\"emailChangePath\":\"/change\"}")]
    public async Task Deliver_Should_RetainUnstartedActionUntilConfigured_ThenStartExpiryAtDelivery(string? configuration)
    {
        using var h = new Harness { Configuration = configuration };
        await h.Service.IssueAsync(h.Request());
        var action = await h.Db.AccountAccessActions.AsNoTracking().SingleAsync();
        await h.Service.Awaiting(x => x.DeliverAsync(action.Id, action.Generation)).Should().ThrowAsync<InvalidStateException>();
        (await h.Db.AccountAccessActions.AsNoTracking().SingleAsync()).DeliveryStartedAtUtc.Should().BeNull();
        h.Email.Messages.Should().BeEmpty();
        h.Clock.UtcNow = h.Clock.UtcNow.AddDays(1);
        h.Configuration = Harness.ValidConfiguration;

        await h.Service.DeliverAsync(action.Id, action.Generation);

        var delivered = await h.Db.AccountAccessActions.AsNoTracking().SingleAsync();
        delivered.DeliveryStartedAtUtc.Should().Be(h.Clock.UtcNow);
        delivered.ExpiresAtUtc.Should().Be(h.Clock.UtcNow.AddMinutes(10));
        (await h.Service.ResolveAsync(h.Email.Token)).Should().BeTrue();
        new Uri((string)h.Email.Messages.Single().Model["action_url"]!).Query.Should().BeEmpty();
    }

    [Fact]
    public async Task Deliver_Should_NotExtendAnUncertainSendGeneration()
    {
        using var h = new Harness();
        await h.IssueAndDeliverAsync();
        var action = await h.Db.AccountAccessActions.AsNoTracking().SingleAsync();
        h.Clock.UtcNow = h.Clock.UtcNow.AddMinutes(11);

        await h.Service.Awaiting(x => x.DeliverAsync(action.Id, action.Generation)).Should().ThrowAsync<InvalidStateException>();

        var after = await h.Db.AccountAccessActions.AsNoTracking().SingleAsync();
        after.Generation.Should().Be(action.Generation);
        after.ExpiresAtUtc.Should().Be(action.ExpiresAtUtc);
        h.Email.Messages.Should().HaveCount(1);
    }

    [Fact]
    public async Task EmailChange_Should_KeepOldAccountUntilTargetConfirmation_ThenRevokeSessions()
    {
        using var h = new Harness();
        var proof = await h.CreateKnownUserAsync();
        await h.Service.RequestEmailChangeAsync("new@example.test", proof);
        var action = await h.Db.AccountAccessActions.AsNoTracking().SingleAsync(x => x.Purpose == AccountAccessPurposes.EmailChange);
        (await h.Db.Users.AsNoTracking().SingleAsync()).Email.Should().Be("paid@example.test");
        h.Provider.Verify(x => x.ConfirmVerifiedEmailAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        await h.Service.DeliverAsync(action.Id, action.Generation);

        (await h.Service.CompleteAsync(h.Email.Token, proof, AccountAccessPurposes.EmailChange)).Should().BeTrue();

        var user = await h.Db.Users.AsNoTracking().SingleAsync();
        user.Email.Should().Be("new@example.test");
        user.IdentityRevision.Should().Be(1);
        (await h.Db.PartyContacts.SingleAsync()).Value.Should().Be("new@example.test");
        h.Provider.Verify(x => x.ConfirmVerifiedEmailAsync(It.Is<User>(u => u.Id == user.Id), "paid@example.test",
            "new@example.test", It.IsAny<CancellationToken>()), Times.Once);
        h.Blocklist.Verify(x => x.RevokeAsync(proof.TenantId, user.Id, user.Id, "Confirmed email change", It.IsAny<CancellationToken>()), Times.Once);
        h.Blocklist.Verify(x => x.InvalidateAsync(proof.TenantId, user.Id, It.IsAny<CancellationToken>()), Times.Once);
        (await h.Service.CompleteAsync(h.Email.Token, proof, AccountAccessPurposes.EmailChange)).Should().BeFalse();
    }

    [Fact]
    public async Task EmailChange_Should_ResumeExactApplyingTargetAfterUnknownProviderOutcome()
    {
        using var h = new Harness();
        var proof = await h.CreateKnownUserAsync();
        await h.Service.RequestEmailChangeAsync("new@example.test", proof);
        var action = await h.Db.AccountAccessActions.AsNoTracking().SingleAsync(x => x.Purpose == AccountAccessPurposes.EmailChange);
        await h.Service.DeliverAsync(action.Id, action.Generation);
        var token = h.Email.Token;
        h.Provider.SetupSequence(x => x.ConfirmVerifiedEmailAsync(It.IsAny<User>(), "paid@example.test", "new@example.test", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Uncertain provider result"))
            .Returns(Task.CompletedTask);
        await h.Service.Awaiting(x => x.CompleteAsync(token, proof, AccountAccessPurposes.EmailChange)).Should().ThrowAsync<HttpRequestException>();
        (await h.Db.AccountAccessActions.AsNoTracking().SingleAsync(x => x.Id == action.Id)).Status.Should().Be("Applying");
        (await h.Db.Users.AsNoTracking().SingleAsync()).Email.Should().Be("paid@example.test");
        await h.Service.RequestEmailChangeAsync("different@example.test", proof);
        (await h.Db.AccountAccessActions.CountAsync(x => x.Purpose == AccountAccessPurposes.EmailChange)).Should().Be(1);
        h.Clock.UtcNow = h.Clock.UtcNow.AddHours(1);
        proof = proof with { AuthenticationTimeUtc = h.Clock.UtcNow, IssuedAtUtc = h.Clock.UtcNow };
        (await h.Service.ResolveAsync(token)).Should().BeTrue();
        (await h.Service.CompleteAsync(token, proof, AccountAccessPurposes.EmailChange)).Should().BeTrue();
        (await h.Db.Users.AsNoTracking().SingleAsync()).IdentityRevision.Should().Be(1);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("revision")]
    [InlineData("unsupported")]
    [InlineData("duplicate")]
    public async Task EmailChange_Should_NotIssueWhenProofOrTargetIsUnavailable(string reason)
    {
        using var h = new Harness();
        var proof = await h.CreateKnownUserAsync();
        if (reason == "stale") proof = proof with { AuthenticationTimeUtc = h.Clock.UtcNow.AddMinutes(-11) };
        if (reason == "revision") proof = proof with { IdentityRevision = 9 };
        if (reason == "unsupported") h.ProviderName = "Keycloak";
        if (reason == "duplicate")
        {
            h.Db.Users.Add(new User { TenantId = proof.TenantId, ExternalIssuer = proof.Issuer, ExternalSubject = "auth0|other", Email = "new@example.test" });
            await h.Db.SaveChangesAsync();
        }

        await h.Service.RequestEmailChangeAsync("new@example.test", proof);

        (await h.Db.AccountAccessActions.CountAsync(x => x.Purpose == AccountAccessPurposes.EmailChange)).Should().Be(0);
    }

    private sealed class Harness : IDisposable
    {
        public static string ValidConfiguration => JsonSerializer.Serialize(new AccountAccessConfiguration(true,
            "https://store.example.test", "/account/access", "/account/email-change"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        public string? Configuration { get; set; } = ValidConfiguration;
        public string ProviderName { get; set; } = "Auth0";
        public TenantProvider Tenant { get; } = new();
        public MutableClock Clock { get; } = new();
        public PlatformDbContext Db { get; }
        public CapturingEmailSender Email { get; } = new();
        public Mock<IIdpAccountService> Provider { get; } = new();
        public Mock<IUserSessionBlocklist> Blocklist { get; } = new();
        public AccountAccessService Service { get; }

        public Harness()
        {
            Db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase("AccountAccess_" + Guid.NewGuid()).Options, Tenant, new TestCurrentUserProvider(), Clock);
            var tenantSettings = new Mock<ITenantSettingStore>();
            tenantSettings.Setup(x => x.GetTenantValueAsync(AccountAccessSettingNames.Configuration, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Configuration);
            var settingProvider = new Mock<ISettingProvider>();
            settingProvider.Setup(x => x.GetAsync(AuthSettingNames.Provider, It.IsAny<CancellationToken>())).ReturnsAsync(() => ProviderName);
            var factory = new Mock<IIdpAccountServiceFactory>();
            factory.Setup(x => x.GetService("Auth0")).Returns(Provider.Object);
            var audit = Mock.Of<IAuditLogWriter>();
            var correlation = Mock.Of<ICorrelationContext>();
            var identities = new UserIdentityService(Db, NullLogger<UserIdentityService>.Instance, audit, correlation);
            var provision = new UserProvisioningService(Db, identities, audit, Clock, new TestCurrentUserProvider(), correlation);
            Service = new AccountAccessService(Db, Tenant, Clock, new EphemeralDataProtectionProvider(), tenantSettings.Object,
                Email, provision, factory.Object, settingProvider.Object, Blocklist.Object);
        }

        public PaidAccountAccessRequest Request() => new(Tenant.Id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "paid@example.test");
        public AccountAccessIdentityProof Proof() => new(Tenant.Id, "https://identity.example.test/", "auth0|customer",
            "paid@example.test", Clock.UtcNow, Clock.UtcNow, null);
        public async Task<string> IssueAndDeliverAsync()
        {
            var request = Request();
            await Service.IssueAsync(request);
            var row = await Db.AccountAccessActions.AsNoTracking().SingleAsync(x => x.PaymentIntentId == request.PaymentIntentId);
            await Service.DeliverAsync(row.Id, row.Generation);
            return Email.Token;
        }
        public async Task<AccountAccessIdentityProof> CreateKnownUserAsync()
        {
            var token = await IssueAndDeliverAsync();
            (await Service.CompleteAsync(token, Proof(), AccountAccessPurposes.PaidSetup)).Should().BeTrue();
            var user = await Db.Users.AsNoTracking().SingleAsync();
            return Proof() with { ExistingUserId = user.Id, IdentityRevision = user.IdentityRevision };
        }
        public void Dispose() => Db.Dispose();
    }

    private sealed class TenantProvider : ITenantProvider
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GetCurrentTenantId() => Id;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = Id; return true; }
    }
    private sealed class MutableClock : IClock { public DateTime UtcNow { get; set; } = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class CapturingEmailSender : ITemplatedEmailSender
    {
        public List<TemplatedEmailMessage> Messages { get; } = [];
        public string Token => QueryHelpers.ParseQuery(new Uri((string)Messages.Last().Model["action_url"]!).Fragment[1..])["token"].ToString();
        public Task SendAsync(TemplatedEmailMessage message, CancellationToken cancellationToken = default)
        { Messages.Add(message); return Task.CompletedTask; }
    }
}
