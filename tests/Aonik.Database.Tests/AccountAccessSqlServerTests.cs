using System.Text.Json;

using Aonik.IntegrationTests.Support;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Identity;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Events.Integration;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Aonik.Database.Tests;

public class AccountAccessSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();
    private readonly TestClock _clock = new(DateTime.UtcNow);

    [SkippableFact]
    public async Task ConcurrentComplete_Should_ConsumeOnce_AndCommitOnlyOneCanonicalCustomerAndClaim()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var seed = await SeedAsync(tenantId);
        await using var first = Context(tenantId);
        await using var second = Context(tenantId);

        var outcomes = await Task.WhenAll(
            Service(first, tenantId).CompleteAsync(seed.Token, Proof(tenantId, "subject-one"), AccountAccessPurposes.PaidSetup),
            Service(second, tenantId).CompleteAsync(seed.Token, Proof(tenantId, "subject-two"), AccountAccessPurposes.PaidSetup));

        outcomes.Should().ContainSingle(x => x).And.ContainSingle(x => !x);
        await using var verify = Context(tenantId);
        var action = await verify.AccountAccessActions.SingleAsync();
        action.Status.Should().Be("Consumed");
        action.RowVersion.Should().HaveCount(8);
        var user = await verify.Users.SingleAsync();
        action.ConsumedByUserId.Should().Be(user.Id);
        (await verify.UserParties.CountAsync()).Should().Be(1);
        (await verify.Parties.CountAsync()).Should().Be(1);
        (await verify.Set<Aonik.SharedKernel.Events.Outbox.OutboxMessage>().CountAsync(x => x.TenantId == tenantId
            && x.EventType == typeof(AccountAccessVerifiedEvent).FullName)).Should().Be(1);
    }

    [SkippableFact]
    public async Task TwoPaidLinksForOneSubject_Should_ReuseOneCanonicalParty_UnderContention()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var firstSeed = await SeedAsync(tenantId);
        var secondSeed = await SeedAsync(tenantId, addTenant: false);
        await using var first = Context(tenantId);
        await using var second = Context(tenantId);
        var proof = Proof(tenantId, "same-subject");

        var outcomes = await Task.WhenAll(
            Service(first, tenantId).CompleteAsync(firstSeed.Token, proof, AccountAccessPurposes.PaidSetup),
            Service(second, tenantId).CompleteAsync(secondSeed.Token, proof, AccountAccessPurposes.PaidSetup));

        outcomes.Should().OnlyContain(x => x);
        await using var verify = Context(tenantId);
        (await verify.Users.CountAsync()).Should().Be(1);
        var party = await verify.UserParties.SingleAsync();
        (await verify.Parties.CountAsync()).Should().Be(1);
        (await verify.PartyRoleAssignments.CountAsync()).Should().Be(1);
        var claims = await verify.Set<Aonik.SharedKernel.Events.Outbox.OutboxMessage>().Where(x => x.TenantId == tenantId
            && x.EventType == typeof(AccountAccessVerifiedEvent).FullName).ToListAsync();
        claims.Should().HaveCount(2);
        claims.Should().OnlyContain(x => x.Payload.Contains(party.PartyId.ToString()));
    }

    [SkippableFact]
    public async Task ConsumeRacingResend_Should_NeverPublishAClaimForAnInvalidatedGeneration()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var seed = await SeedAsync(tenantId);
        await using var consume = Context(tenantId);
        await using var resend = Context(tenantId);
        var completion = Service(consume, tenantId).CompleteAsync(seed.Token, Proof(tenantId, "subject"), AccountAccessPurposes.PaidSetup);
        await Task.WhenAll(completion, Service(resend, tenantId).ResendAsync(seed.Token));

        await using var verify = Context(tenantId);
        var row = await verify.AccountAccessActions.SingleAsync();
        var claims = await verify.Set<Aonik.SharedKernel.Events.Outbox.OutboxMessage>().CountAsync(x => x.TenantId == tenantId
            && x.EventType == typeof(AccountAccessVerifiedEvent).FullName);
        if (await completion)
        {
            row.Status.Should().Be("Consumed");
            row.Generation.Should().Be(seed.Generation);
            claims.Should().Be(1);
        }
        else
        {
            row.Status.Should().Be("Pending");
            row.Generation.Should().NotBe(seed.Generation);
            claims.Should().Be(0);
            (await verify.Users.CountAsync()).Should().Be(0);
        }
        (await Service(verify, tenantId).ResolveAsync(seed.Token)).Should().BeFalse();
    }

    [SkippableFact]
    public async Task ConcurrentResends_Should_RespectSharedMailboxBudgetAcrossDifferentPaidActions()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var one = await SeedAsync(tenantId);
        var two = await SeedAsync(tenantId, addTenant: false);
        var three = await SeedAsync(tenantId, addTenant: false);
        await using (var seed = Context(tenantId))
        {
            var rows = await seed.AccountAccessActions.ToListAsync();
            foreach (var row in rows) row.SendCount = 3; // One remaining shared mailbox send.
            await seed.SaveChangesAsync();
        }
        await using var first = Context(tenantId);
        await using var second = Context(tenantId);
        await using var third = Context(tenantId);

        await Task.WhenAll(Service(first, tenantId).ResendAsync(one.Token),
            Service(second, tenantId).ResendAsync(two.Token), Service(third, tenantId).ResendAsync(three.Token));

        await using var verify = Context(tenantId);
        (await verify.AccountAccessActions.SumAsync(x => x.SendCount)).Should().Be(10);
        var generations = new[] { one.Generation, two.Generation, three.Generation };
        (await verify.AccountAccessActions.CountAsync(x => !generations.Contains(x.Generation))).Should().Be(1);
    }

    private async Task<(string Token, Guid Generation)> SeedAsync(Guid tenantId, bool addTenant = true)
    {
        await using var context = Context(tenantId);
        if (addTenant)
        {
            context.Tenants.Add(new Tenant { Id = tenantId, Name = "Account action SQL tests", Status = "Active" });
            await context.SaveChangesAsync();
        }
        var emails = new CapturingEmailSender();
        var service = Service(context, tenantId, emails);
        var paymentId = Guid.NewGuid();
        await service.IssueAsync(new PaidAccountAccessRequest(tenantId, Guid.NewGuid(), Guid.NewGuid(), paymentId,
            Guid.NewGuid(), "paid@example.test"));
        var row = await context.AccountAccessActions.SingleAsync(x => x.PaymentIntentId == paymentId);
        await service.DeliverAsync(row.Id, row.Generation);
        var uri = new Uri((string)emails.Message!.Model["action_url"]!);
        var query = QueryHelpers.ParseQuery(string.IsNullOrEmpty(uri.Fragment) ? uri.Query : uri.Fragment[1..]);
        return (query["token"].ToString(), row.Generation);
    }

    private AccountAccessService Service(PlatformDbContext context, Guid tenantId, CapturingEmailSender? email = null)
    {
        var tenantSettings = new Mock<ITenantSettingStore>();
        tenantSettings.Setup(x => x.GetTenantValueAsync(AccountAccessSettingNames.Configuration, tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new AccountAccessConfiguration(true, "https://store.example.test",
                "/account/access", "/account/email-change"), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var audit = Mock.Of<IAuditLogWriter>();
        var correlation = Mock.Of<ICorrelationContext>();
        var identity = new UserIdentityService(context, NullLogger<UserIdentityService>.Instance, audit, correlation);
        var provision = new UserProvisioningService(context, identity, audit, _clock,
            new TestCurrentUserProvider(), correlation);
        return new AccountAccessService(context, new TestTenantProvider(tenantId), _clock, _protection,
            tenantSettings.Object, email ?? new CapturingEmailSender(), provision,
            Mock.Of<IIdpAccountServiceFactory>(), Mock.Of<ISettingProvider>(), Mock.Of<IUserSessionBlocklist>());
    }

    private PlatformDbContext Context(Guid tenantId) => new(database.CreateOptions<PlatformDbContext>(),
        new TestTenantProvider(tenantId), new TestCurrentUserProvider());

    private AccountAccessIdentityProof Proof(Guid tenantId, string subject) => new(tenantId,
        "https://identity.example.test/", subject, "paid@example.test", _clock.UtcNow, _clock.UtcNow, null);

    private void RequireSqlServer() => Skip.IfNot(database.IsAvailable, database.SkipReason);
    private sealed class TestClock(DateTime now) : IClock { public DateTime UtcNow => now; }
    private sealed class CapturingEmailSender : ITemplatedEmailSender
    {
        public TemplatedEmailMessage? Message { get; private set; }
        public Task SendAsync(TemplatedEmailMessage message, CancellationToken cancellationToken = default)
        { Message = message; return Task.CompletedTask; }
    }
}
