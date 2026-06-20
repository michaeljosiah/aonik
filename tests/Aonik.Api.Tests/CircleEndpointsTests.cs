using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Api.Tests;

/// <summary>
/// HTTP-level E2E for the Spec 048 Circle. Includes the headline SECURITY
/// assertion: logged in as a docsOnly member, the shared-entity response carries
/// no amount-bearing field. Real FastEndpoints pipeline + UserPolicy + InMemory.
/// </summary>
public class CircleEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CircleEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateUserAsync(Guid tenantId)
    {
        var options = TestAuthOptions.Create().WithTenant(tenantId).WithRoles("PersonalUser");
        var client = await _factory.CreateAuthenticatedClientAsync(options);
        return (client, options.UserId);
    }

    private static async Task<Guid> CreateEntityAsync(HttpClient client, string name = "Surulere flat")
    {
        var response = await client.PostAsJsonAsync(
            "/personal-finance/care-entities",
            new CreateCareEntityRequest("asset", "property", name, "NG", null, null, null, null));
        var created = await response.Content.ReadFromJsonAsync<CareEntityResponse>();
        return created!.Id;
    }

    private static async Task PostExpenseAsync(HttpClient client, Guid entityId, decimal amount, string currency, DateTime date)
    {
        var response = await client.PostAsJsonAsync(
            "/personal-finance/payment-logs",
            new CreatePaymentLogRequest(entityId, null, null, amount, currency, null, date, "bank", "manual", null, null));
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DocsOnlyMember_SeesNoAmounts_TheSecurityAssertion()
    {
        var tenant = Guid.NewGuid();
        var (ownerClient, ownerId) = await CreateUserAsync(tenant);
        var (memberClient, memberId) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient);

        var grantResponse = await ownerClient.PostAsJsonAsync(
            "/personal-finance/circle/grants",
            new CreateCircleGrantRequest(memberId, "docsOnly", new[] { entityId }, true));
        grantResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var viewResponse = await memberClient.GetAsync($"/personal-finance/circle/shared/{ownerId}/care-entities/{entityId}");
        viewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Field-by-field security assertion: no amount-bearing field anywhere in the payload.
        var rawJson = (await viewResponse.Content.ReadAsStringAsync()).ToLowerInvariant();
        rawJson.Should().NotContain("amount");
        rawJson.Should().NotContain("total");
        rawJson.Should().NotContain("yeartotals");
        rawJson.Should().NotContain("recentlogs");

        var result = await viewResponse.Content.ReadFromJsonAsync<CircleSharedEntityResult>();
        result!.Scope.Should().Be("docsOnly");
        result.Full.Should().BeNull();
        result.DocsOnly.Should().NotBeNull();
        result.DocsOnly!.CareEntityId.Should().Be(entityId);
    }

    [Fact]
    public async Task EntityScopeMember_SeesFullView()
    {
        var tenant = Guid.NewGuid();
        var (ownerClient, ownerId) = await CreateUserAsync(tenant);
        var (memberClient, memberId) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient);

        await ownerClient.PostAsJsonAsync(
            "/personal-finance/circle/grants",
            new CreateCircleGrantRequest(memberId, "entities", new[] { entityId }, false));

        var result = await (await memberClient.GetAsync($"/personal-finance/circle/shared/{ownerId}/care-entities/{entityId}"))
            .Content.ReadFromJsonAsync<CircleSharedEntityResult>();

        result!.Scope.Should().Be("entities");
        result.Full.Should().NotBeNull();
        result.Full!.Entity.Id.Should().Be(entityId);
    }

    [Fact]
    public async Task NonMember_Gets404()
    {
        var tenant = Guid.NewGuid();
        var (ownerClient, ownerId) = await CreateUserAsync(tenant);
        var (strangerClient, _) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient);

        var response = await strangerClient.GetAsync($"/personal-finance/circle/shared/{ownerId}/care-entities/{entityId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Revoke_DeniesMemberImmediately()
    {
        var tenant = Guid.NewGuid();
        var (ownerClient, ownerId) = await CreateUserAsync(tenant);
        var (memberClient, memberId) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient);
        var grant = await (await ownerClient.PostAsJsonAsync(
            "/personal-finance/circle/grants",
            new CreateCircleGrantRequest(memberId, "entities", new[] { entityId }, false)))
            .Content.ReadFromJsonAsync<CircleGrantResponse>();

        var beforeRevoke = await memberClient.GetAsync($"/personal-finance/circle/shared/{ownerId}/care-entities/{entityId}");
        beforeRevoke.StatusCode.Should().Be(HttpStatusCode.OK);

        var revoke = await ownerClient.DeleteAsync($"/personal-finance/circle/grants/{grant!.Id}");
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterRevoke = await memberClient.GetAsync($"/personal-finance/circle/shared/{ownerId}/care-entities/{entityId}");
        afterRevoke.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invite_Create_Then_Accept_BindsGrant()
    {
        var tenant = Guid.NewGuid();
        var (ownerClient, _) = await CreateUserAsync(tenant);
        var (memberClient, memberId) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient);

        var invite = await (await ownerClient.PostAsJsonAsync(
            "/personal-finance/circle/invites",
            new CreateCircleInviteRequest("entities", new[] { entityId }, false, "link")))
            .Content.ReadFromJsonAsync<CircleInviteResponse>();

        var acceptResponse = await memberClient.PostAsJsonAsync(
            "/personal-finance/circle/invites/accept", new { token = invite!.Token });
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var grant = await acceptResponse.Content.ReadFromJsonAsync<CircleGrantResponse>();
        grant!.MemberUserId.Should().Be(memberId);
        grant.Status.Should().Be("active");
    }

    [Fact]
    public async Task SupportStatement_ComposesForOwner()
    {
        var tenant = Guid.NewGuid();
        var (ownerClient, _) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient, "Mum");

        var response = await ownerClient.GetAsync(
            $"/personal-finance/care-entities/{entityId}/statement?from=2026-01-01&to=2026-12-31&preparedFor=HMRC");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var statement = await response.Content.ReadFromJsonAsync<StatementData>();
        statement!.Entity.Id.Should().Be(entityId);
        statement.PreparedFor.Should().Be("HMRC");
        statement.VerificationCode.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task EntityScopeMember_ListsSharedExpenses_PagedWithStatus()
    {
        var tenant = Guid.NewGuid();
        var (ownerClient, ownerId) = await CreateUserAsync(tenant);
        var (memberClient, memberId) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient);
        await PostExpenseAsync(ownerClient, entityId, 120m, "GBP", new DateTime(2026, 3, 1));
        await PostExpenseAsync(ownerClient, entityId, 340m, "NGN", new DateTime(2026, 4, 1));

        await ownerClient.PostAsJsonAsync(
            "/personal-finance/circle/grants",
            new CreateCircleGrantRequest(memberId, "entities", new[] { entityId }, false));

        var response = await memberClient.GetAsync(
            $"/personal-finance/circle/shared/{ownerId}/care-entities/{entityId}/payment-logs?page=1&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CircleSharedPaymentLogsResult>();
        result!.Items.Should().HaveCount(2);                                 // the full list, not the recent-10 preview
        result.HasMore.Should().BeFalse();
        result.Items.Should().OnlyContain(i => i.CorroborationStatus == "none"); // status surfaced per expense
    }

    [Fact]
    public async Task DocsOnlyMember_SharedExpenses_Returns404_TheNoAmountsAssertion()
    {
        var tenant = Guid.NewGuid();
        var (ownerClient, ownerId) = await CreateUserAsync(tenant);
        var (memberClient, memberId) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient);
        await PostExpenseAsync(ownerClient, entityId, 120m, "GBP", new DateTime(2026, 3, 1));

        await ownerClient.PostAsJsonAsync(
            "/personal-finance/circle/grants",
            new CreateCircleGrantRequest(memberId, "docsOnly", new[] { entityId }, true));

        // A docsOnly member can open the entity view but must never reach its expense lines.
        var response = await memberClient.GetAsync(
            $"/personal-finance/circle/shared/{ownerId}/care-entities/{entityId}/payment-logs");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/personal-finance/circle/grants");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
