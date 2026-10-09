using System.Net;
using System.Net.Http.Json;

using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Loyalty;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public sealed class CommerceLoyaltyEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private const string Path = "/commerce/storefront/loyalty";
    private const string AdminPath = "/commerce/admin/loyalty/adjustments";

    [Fact]
    public async Task CustomerReads_Should_UseOnlyPrincipalOwner_AndNeverMarkMilestonesAsSeen()
    {
        var own = await CustomerAsync(1250);
        var other = await CustomerAsync(9900, own.TenantId);
        using var ownClient = own.Client;
        using var otherClient = other.Client;

        using var balanceResponse = await ownClient.GetAsync(Path + "?partyId=" + other.PartyId);
        balanceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        Private(balanceResponse);
        var balance = (await balanceResponse.Content.ReadFromJsonAsync<LoyaltyBalance>())!;
        balance.BalancePoints.Should().Be(1250);
        balance.Value.Should().Be(12.50m);
        balance.HighestFivePoundMarkSeen.Should().Be(0);
        using var historyResponse = await ownClient.GetAsync(Path + "/history?page=1&pageSize=1&partyId=" + other.PartyId);
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        Private(historyResponse);
        var history = (await historyResponse.Content.ReadFromJsonAsync<LoyaltyActivityPage>())!;
        history.TotalCount.Should().Be(1);
        history.Items.Should().ContainSingle().Which.Points.Should().Be(1250);
        history.Items[0].RunningBalancePoints.Should().Be(1250);
        (await ownClient.GetFromJsonAsync<LoyaltyBalance>(Path))!.HighestFivePoundMarkSeen.Should().Be(0);

        using var marked = await ownClient.PostAsJsonAsync(Path + "/seen", new { mark = 2L });
        marked.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Private(marked);
        using var older = await ownClient.PostAsJsonAsync(Path + "/seen", new { mark = 1L });
        older.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ownClient.GetFromJsonAsync<LoyaltyBalance>(Path))!.HighestFivePoundMarkSeen.Should().Be(2);
        (await otherClient.GetFromJsonAsync<LoyaltyBalance>(Path))!.HighestFivePoundMarkSeen.Should().Be(0);
    }

    [Theory]
    [InlineData("/history?page=0")]
    [InlineData("/history?pageSize=101")]
    [InlineData("/history?page=2147483647&pageSize=100")]
    public async Task History_Should_RejectInvalidPaging(string suffix)
    {
        var own = await CustomerAsync(0);
        using var client = own.Client;
        using var response = await client.GetAsync(Path + suffix);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Private(response);
    }

    [Fact]
    public async Task Seen_Should_RejectOwnerSelectorsAndInvalidMark_WithoutChangingBalance()
    {
        var own = await CustomerAsync(1000);
        using var client = own.Client;
        using var spoofed = await client.PostAsJsonAsync(Path + "/seen", new { mark = 1, partyId = Guid.NewGuid() });
        spoofed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Private(spoofed);
        using var negative = await client.PostAsJsonAsync(Path + "/seen", new { mark = -1 });
        negative.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Private(negative);
        var balance = (await client.GetFromJsonAsync<LoyaltyBalance>(Path))!;
        balance.HighestFivePoundMarkSeen.Should().Be(0);
        balance.BalancePoints.Should().Be(1000);
    }

    [Fact]
    public async Task Loyalty_Should_RequireAuthenticationAndExistingProfile_AndNotCreateAccountsForEmptyReads()
    {
        var tenant = Guid.NewGuid();
        using var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.ToString());
        using var rejected = await anonymous.GetAsync(Path);
        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Private(rejected);
        using var noProfile = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenant)
            .WithRoles("PersonalUser").WithPermissions("UserInfo.Read"));
        using var missing = await noProfile.GetAsync(Path);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Private(missing);
        var own = await CustomerAsync(null, tenant);
        using var client = own.Client;
        (await client.GetFromJsonAsync<LoyaltyBalance>(Path))!.Should().Be(new LoyaltyBalance(0, 0, 0, 0, 0));
        using var marked = await client.PostAsJsonAsync(Path + "/seen", new { mark = 1 });
        marked.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenant;
        (await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().LoyaltyAccounts
            .AnyAsync(x => x.TenantId == tenant && x.PartyId == own.PartyId)).Should().BeFalse();
    }

    [Fact]
    public async Task ManualAdjustments_Should_RequireBothStaffPolicyAndLedgerWritePermission()
    {
        var own = await CustomerAsync(0);
        using var customer = own.Client;
        using var staffWithoutPermission = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create()
            .WithTenant(own.TenantId).WithRoles("Operations").WithPermissions("UserInfo.Read"));
        using var staffWithPermission = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create()
            .WithTenant(own.TenantId).WithRoles("Operations").WithPermissions("Ledger.Write"));
        var request = new { partyId = own.PartyId, adjustmentId = Guid.NewGuid(), points = 50, reason = "Approved service correction" };
        foreach (var deniedClient in new[] { customer, staffWithoutPermission })
        {
            using var response = await deniedClient.PostAsJsonAsync(AdminPath, request);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            Private(response);
        }
        using var permitted = await staffWithPermission.PostAsJsonAsync(AdminPath, request);
        permitted.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "permission does not enable an unconfigured loyalty policy");
        Private(permitted);
        (await customer.GetFromJsonAsync<LoyaltyBalance>(Path))!.BalancePoints.Should().Be(0);
    }

    private async Task<Customer> CustomerAsync(long? points, Guid? tenantId = null)
    {
        var tenant = tenantId ?? Guid.NewGuid();
        var auth = TestAuthOptions.Create().WithTenant(tenant).WithRoles("PersonalUser").WithPermissions("UserInfo.Read");
        var client = await factory.CreateAuthenticatedClientAsync(auth);
        var party = await WorkspaceTestSeeding.SeedPartyAsync(factory, tenant, auth.UserId, "Loyalty customer");
        if (points is not null)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenant;
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            var ledger = new Ledger { TenantId = tenant, BaseCurrency = "GBP" };
            var liability = new LedgerAccount { TenantId = tenant, LedgerId = ledger.Id, AccountType = "Liability", Code = "LOYALTY", Name = "Loyalty liability" };
            var expense = new LedgerAccount { TenantId = tenant, LedgerId = ledger.Id, AccountType = "Expense", Code = "EARN", Name = "Loyalty expense" };
            var account = new LoyaltyAccount { TenantId = tenant, PartyId = party, LedgerId = ledger.Id, LiabilityAccountId = liability.Id };
            db.Ledgers.Add(ledger); db.LedgerAccounts.AddRange(liability, expense); db.LoyaltyAccounts.Add(account);
            if (points > 0)
            {
                var entry = new JournalEntry { TenantId = tenant, LedgerId = ledger.Id, Status = "Posted", Timestamp = DateTime.UtcNow,
                    SourceType = "LoyaltyEarn", SourceId = Guid.NewGuid() };
                var credit = new JournalEntryLine { TenantId = tenant, JournalEntryId = entry.Id, LedgerAccountId = liability.Id,
                    Direction = "Credit", Amount = points.Value / 100m, Currency = "GBP" };
                entry.Lines.Add(credit);
                entry.Lines.Add(new JournalEntryLine { TenantId = tenant, JournalEntryId = entry.Id, LedgerAccountId = expense.Id,
                    Direction = "Debit", Amount = credit.Amount, Currency = "GBP" });
                db.JournalEntries.Add(entry);
                db.LoyaltyOperations.Add(new LoyaltyOperation { TenantId = tenant, AccountId = account.Id, Kind = "Earn", SourceId = entry.SourceId,
                    JournalEntryId = entry.Id, JournalEntryLineId = credit.Id, Points = points.Value, OccurredAtUtc = entry.Timestamp });
            }
            await db.SaveChangesAsync();
        }
        return new(tenant, party, client);
    }

    private static void Private(HttpResponseMessage response)
        => response.Headers.CacheControl!.NoStore.Should().BeTrue();

    private sealed record Customer(Guid TenantId, Guid PartyId, HttpClient Client);
}
