using System.Text.Json;

using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using Aonik.IntegrationTests.Support;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests;

public class AccountEmailChangeSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    private const string OriginalEmail = "original@example.test";
    private const string Issuer = "https://identity.example.test/";
    private const string Subject = "auth0|email-change-subject";
    private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();
    private readonly TestClock _clock = new();

    [SkippableFact]
    public async Task CompetingRequests_Should_LeaveOnePendingChange_WithoutChangingEitherMailbox()
    {
        RequireSqlServer();
        var seed = await SeedUserAsync();
        var gateA = new BeforeRequestSave();
        var gateB = new BeforeRequestSave();
        await using var firstContext = Context(seed.TenantId, gateA);
        await using var secondContext = Context(seed.TenantId, gateB);
        firstContext.Database.CreateExecutionStrategy().RetriesOnFailure.Should().BeTrue();
        var idp = new Mock<IIdpAccountService>(MockBehavior.Strict);
        var first = Service(firstContext, seed.TenantId, idp.Object)
            .RequestEmailChangeAsync("one@example.test", Proof(seed));
        try
        {
            await gateA.WaitUntilReachedAsync(first);
            var second = Service(secondContext, seed.TenantId, idp.Object)
                .RequestEmailChangeAsync("two@example.test", Proof(seed));
            await gateB.WaitUntilReachedAsync(second);
            // Both serializable requests hold their reads. Let SQL choose/retry the winner;
            // waiting for either commit while the other barrier is held would deadlock the test.
            gateA.Release.TrySetResult();
            gateB.Release.TrySetResult();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(60));
            await firstContext.SaveChangesAsync();
            await secondContext.SaveChangesAsync();

            await using var verify = Context(seed.TenantId);
            var actions = await verify.AccountAccessActions.AsNoTracking().ToListAsync();
            var pending = actions.Should().ContainSingle(action => action.Status == "Pending").Subject;
            actions.Should().OnlyContain(action => action.Status == "Pending" || action.Status == "Revoked");
            actions.Should().HaveCountLessThanOrEqualTo(2);
            pending.RowVersion.Should().HaveCount(8);
            new[] { "one@example.test", "two@example.test" }.Should().Contain(pending.Email);
            (await verify.Users.SingleAsync()).Email.Should().Be(OriginalEmail);
            (await verify.Users.SingleAsync()).IdentityRevision.Should().Be(seed.IdentityRevision);
            (await verify.PartyContacts.SingleAsync(contact => contact.PartyId == seed.PartyId && contact.Type == "Email"))
                .Value.Should().Be(OriginalEmail);
            (await verify.Set<Aonik.SharedKernel.Events.Outbox.OutboxMessage>().CountAsync(message => message.TenantId == seed.TenantId
                && message.EventType == typeof(AccountAccessDeliveryRequestedEvent).FullName)).Should().Be(actions.Count);
            var emails = new CapturingEmailSender();
            var delivery = Service(verify, seed.TenantId, idp.Object, emails);
            foreach (var action in actions) await delivery.DeliverAsync(action.Id, action.Generation);
            emails.Messages.Should().ContainSingle().Which.To.Should().Be(pending.Email);
            idp.VerifyNoOtherCalls();
        }
        finally
        {
            gateA.Release.TrySetResult();
            gateB.Release.TrySetResult();
        }
    }

    [SkippableFact]
    public async Task UncertainProviderChange_Should_RemainApplying_AndReconcileTheSameActionOnceOutsideSqlTransactions()
    {
        RequireSqlServer();
        var seed = await SeedUserAsync();
        const string target = "confirmed@example.test";
        var emails = new CapturingEmailSender();
        var idp = new Mock<IIdpAccountService>(MockBehavior.Strict);
        var blocklist = new Mock<IUserSessionBlocklist>();
        blocklist.Setup(service => service.RevokeAsync(seed.TenantId, seed.UserId, seed.UserId,
                "Confirmed email change", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSessionRevocation(seed.TenantId, seed.UserId, _clock.UtcNow,
                _clock.UtcNow.AddDays(1), seed.UserId, "Confirmed email change"));
        Guid actionId;
        Guid generation;
        string token;
        await using (var request = Context(seed.TenantId))
        {
            var service = Service(request, seed.TenantId, idp.Object, emails, blocklist.Object);
            await service.RequestEmailChangeAsync(target, Proof(seed));
            var action = await request.AccountAccessActions.SingleAsync();
            actionId = action.Id;
            generation = action.Generation;
            action.Status.Should().Be("Pending");
            action.OriginalEmail.Should().Be(OriginalEmail);
            await service.DeliverAsync(action.Id, action.Generation);
            var sent = emails.Messages.Should().ContainSingle().Subject;
            sent.TemplateName.Should().Be(TransactionalEmailTemplateNames.EmailChangeConfirmation);
            sent.To.Should().Be(target);
            var actionUri = new Uri((string)sent.Model["action_url"]!);
            token = QueryHelpers.ParseQuery(actionUri.Fragment[1..])["token"].ToString();
            (await request.Users.AsNoTracking().SingleAsync()).Email.Should().Be(OriginalEmail);
            idp.VerifyNoOtherCalls();
        }

        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerEmail = OriginalEmail;
        await using var applyingContext = Context(seed.TenantId);
        idp.Setup(service => service.ConfirmVerifiedEmailAsync(It.Is<User>(user => user.Id == seed.UserId
                    && user.ExternalSubject == Subject && user.ExternalIssuer == Issuer), OriginalEmail, target,
                It.IsAny<CancellationToken>()))
            .Returns(async (User _, string _, string destination, CancellationToken cancellationToken) =>
            {
                applyingContext.Database.CurrentTransaction.Should().BeNull();
                System.Transactions.Transaction.Current.Should().BeNull();
                reached.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                providerEmail = destination;
                throw new HttpRequestException("Provider response was lost after applying the change.");
            });
        var completion = Service(applyingContext, seed.TenantId, idp.Object, emails, blocklist.Object)
            .CompleteAsync(token, Proof(seed), AccountAccessPurposes.EmailChange);
        try
        {
            await AwaitReachedAsync(reached.Task, completion);
            providerEmail.Should().Be(OriginalEmail);
            await AssertLocalPendingAsync(seed, actionId);
            await using (var another = Context(seed.TenantId))
                await Service(another, seed.TenantId, idp.Object).RequestEmailChangeAsync("replacement@example.test", Proof(seed));
            release.TrySetResult();
            var uncertain = () => completion;
            await uncertain.Should().ThrowAsync<HttpRequestException>();
        }
        finally { release.TrySetResult(); }
        providerEmail.Should().Be(target, "a lost response does not prove the provider rejected the update");
        await AssertLocalPendingAsync(seed, actionId);
        await using (var blocked = Context(seed.TenantId))
        {
            await Service(blocked, seed.TenantId, idp.Object).RequestEmailChangeAsync("another@example.test", Proof(seed));
            (await blocked.AccountAccessActions.CountAsync()).Should().Be(1);
            (await blocked.Set<Aonik.SharedKernel.Events.Outbox.OutboxMessage>().CountAsync(message => message.TenantId == seed.TenantId
                && message.EventType == typeof(AccountAccessDeliveryRequestedEvent).FullName)).Should().Be(1);
        }

        _clock.UtcNow = _clock.UtcNow.AddMinutes(20);
        await using var retryContext = Context(seed.TenantId);
        var retryProof = Proof(seed) with { VerifiedEmail = target };
        idp.Setup(service => service.ConfirmVerifiedEmailAsync(It.Is<User>(user => user.Id == seed.UserId
                    && user.ExternalSubject == Subject && user.ExternalIssuer == Issuer), OriginalEmail, target,
                It.IsAny<CancellationToken>()))
            .Returns((User _, string _, string destination, CancellationToken _) =>
            {
                retryContext.Database.CurrentTransaction.Should().BeNull();
                System.Transactions.Transaction.Current.Should().BeNull();
                providerEmail.Should().Be(destination, "retry reconciles the same target already written externally");
                return Task.CompletedTask;
            });
        var retry = Service(retryContext, seed.TenantId, idp.Object, emails, blocklist.Object);

        (await retry.CompleteAsync(token, retryProof, AccountAccessPurposes.EmailChange)).Should().BeTrue();
        (await retry.CompleteAsync(token, retryProof with { IdentityRevision = seed.IdentityRevision + 1 },
            AccountAccessPurposes.EmailChange)).Should().BeFalse();

        await using var verify = Context(seed.TenantId);
        var consumed = await verify.AccountAccessActions.SingleAsync();
        consumed.Id.Should().Be(actionId);
        consumed.Generation.Should().Be(generation);
        consumed.Status.Should().Be("Consumed");
        consumed.ConsumedByUserId.Should().Be(seed.UserId);
        consumed.ConsumedAtUtc.Should().Be(_clock.UtcNow);
        consumed.RowVersion.Should().HaveCount(8);
        var user = await verify.Users.SingleAsync();
        user.Email.Should().Be(target);
        user.IdentityRevision.Should().Be(seed.IdentityRevision + 1);
        user.RowVersion.Should().HaveCount(8);
        (await verify.UserParties.SingleAsync()).PartyId.Should().Be(seed.PartyId);
        (await verify.PartyContacts.SingleAsync(contact => contact.PartyId == seed.PartyId && contact.Type == "Email"))
            .Value.Should().Be(target);
        (await verify.Set<Aonik.SharedKernel.Events.Outbox.OutboxMessage>().CountAsync(message => message.TenantId == seed.TenantId
            && message.EventType == typeof(AccountAccessDeliveryRequestedEvent).FullName)).Should().Be(1);
        idp.Verify(service => service.ConfirmVerifiedEmailAsync(It.IsAny<User>(), OriginalEmail, target,
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        blocklist.Verify(service => service.RevokeAsync(seed.TenantId, seed.UserId, seed.UserId,
            "Confirmed email change", It.IsAny<CancellationToken>()), Times.Once);
        blocklist.Verify(service => service.InvalidateAsync(seed.TenantId, seed.UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private async Task AssertLocalPendingAsync(Seed seed, Guid actionId)
    {
        await using var verify = Context(seed.TenantId);
        var action = await verify.AccountAccessActions.SingleAsync();
        action.Id.Should().Be(actionId);
        action.Status.Should().Be("Applying");
        action.ConsumedAtUtc.Should().BeNull();
        (await verify.Users.SingleAsync()).Email.Should().Be(OriginalEmail);
        (await verify.Users.SingleAsync()).IdentityRevision.Should().Be(seed.IdentityRevision);
        (await verify.PartyContacts.SingleAsync(contact => contact.PartyId == seed.PartyId && contact.Type == "Email"))
            .Value.Should().Be(OriginalEmail);
    }

    private async Task<Seed> SeedUserAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var context = Context(tenantId);
        context.Tenants.Add(new Tenant { Id = tenantId, Name = $"Email change SQL tests {tenantId}", Status = "Active" });
        var user = new User
        {
            TenantId = tenantId, ExternalIssuer = Issuer, ExternalSubject = Subject, Email = OriginalEmail, Status = "Active"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var provisioned = await Provisioning(context).EnsureUserAndCustomerAsync(new ExternalIdentity(tenantId));
        return new(tenantId, user.Id, provisioned.PartyId, user.IdentityRevision);
    }

    private AccountAccessService Service(PlatformDbContext context, Guid tenantId, IIdpAccountService idp,
        CapturingEmailSender? emails = null, IUserSessionBlocklist? blocklist = null)
    {
        var tenantSettings = new Mock<ITenantSettingStore>();
        tenantSettings.Setup(service => service.GetTenantValueAsync(AccountAccessSettingNames.Configuration, tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new AccountAccessConfiguration(true, "https://store.example.test",
                "/account/access", "/account/email-change"), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var settings = new Mock<ISettingProvider>();
        settings.Setup(service => service.GetAsync(AuthSettingNames.Provider, It.IsAny<CancellationToken>())).ReturnsAsync("Auth0");
        var factory = new Mock<IIdpAccountServiceFactory>(MockBehavior.Strict);
        factory.Setup(service => service.GetService("Auth0")).Returns(idp);
        return new(context, new TestTenantProvider(tenantId), _clock, _protection, tenantSettings.Object,
            emails ?? new CapturingEmailSender(), Provisioning(context), factory.Object, settings.Object,
            blocklist ?? Mock.Of<IUserSessionBlocklist>());
    }

    private UserProvisioningService Provisioning(PlatformDbContext context)
    {
        var audit = Mock.Of<IAuditLogWriter>();
        var correlation = Mock.Of<ICorrelationContext>();
        var identity = new UserIdentityService(context, NullLogger<UserIdentityService>.Instance, audit, correlation);
        return new(context, identity, audit, _clock, new TestCurrentUserProvider(), correlation);
    }

    private PlatformDbContext Context(Guid tenantId, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>(database.CreateOptions<PlatformDbContext>());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, new TestTenantProvider(tenantId), new TestCurrentUserProvider());
    }

    private AccountAccessIdentityProof Proof(Seed seed) => new(seed.TenantId, Issuer, Subject, OriginalEmail,
        _clock.UtcNow, _clock.UtcNow, seed.UserId, seed.IdentityRevision);

    private void RequireSqlServer() => Skip.IfNot(database.IsAvailable, database.SkipReason);

    private static async Task AwaitReachedAsync(Task reached, Task operation)
    {
        var completed = await Task.WhenAny(reached, operation).WaitAsync(TimeSpan.FromSeconds(30));
        if (completed == operation)
        {
            await operation;
            throw new InvalidOperationException("Operation completed before the test barrier.");
        }
    }

    private sealed record Seed(Guid TenantId, Guid UserId, Guid PartyId, long IdentityRevision);
    private sealed record ExternalIdentity(Guid TenantId) : IExternalIdentity
    {
        public string ExternalIssuer => Issuer;
        public string ExternalSubject => Subject;
        public string? ExternalTenantId => null;
        public string? Email => OriginalEmail;
    }
    private sealed class TestClock : IClock { public DateTime UtcNow { get; set; } = DateTime.UtcNow; }
    private sealed class CapturingEmailSender : ITemplatedEmailSender
    {
        public List<TemplatedEmailMessage> Messages { get; } = [];
        public Task SendAsync(TemplatedEmailMessage message, CancellationToken cancellationToken = default)
        { Messages.Add(message); return Task.CompletedTask; }
    }

    private sealed class BeforeRequestSave : SaveChangesInterceptor
    {
        private int _paused;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task WaitUntilReachedAsync(Task operation) => AwaitReachedAsync(Reached.Task, operation);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var newRequest = eventData.Context!.ChangeTracker.Entries<AccountAccessAction>()
                .Any(entry => entry.State == EntityState.Added && entry.Entity.Purpose == AccountAccessPurposes.EmailChange);
            if (newRequest && Interlocked.CompareExchange(ref _paused, 1, 0) == 0)
            {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
