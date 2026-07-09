using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Aonik.PersonalFinance.Contracts.Models;

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

    // ── Spec 061: anonymous invite preview + accept hardening ───────────

    private static async Task<string> CreateInviteAsync(HttpClient ownerClient, string scope, Guid entityId, bool noAmounts)
    {
        var invite = await (await ownerClient.PostAsJsonAsync(
            "/personal-finance/circle/invites",
            new CreateCircleInviteRequest(scope, new[] { entityId }, noAmounts, "link")))
            .Content.ReadFromJsonAsync<CircleInviteResponse>();
        return invite!.Token;
    }

    [Fact]
    public async Task Preview_Anonymous_ValidInvite_Returns200_WithScopeAndEntityNames_NoMoney()
    {
        // The whole point of Spec 061: a SIGNED-OUT recipient (no JWT, no tenant header) can
        // preview an invite. This exercises the TenantValidationMiddleware whitelist + the
        // tenant-from-token resolution end-to-end through the real pipeline.
        var tenant = Guid.NewGuid();
        var (ownerClient, _) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient, "Surulere flat");
        var token = await CreateInviteAsync(ownerClient, "docsOnly", entityId, noAmounts: true);

        var anon = _factory.CreateClient(); // no Authorization header, no X-Tenant-Id
        var response = await anon.GetAsync($"/personal-finance/circle/invites/{token}/preview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // No amount-bearing field may appear in an anonymous preview payload (§5).
        var rawJson = (await response.Content.ReadAsStringAsync()).ToLowerInvariant();
        rawJson.Should().NotContain("total");
        rawJson.Should().NotContain("balance");
        rawJson.Should().NotContain("currency");
        rawJson.Should().NotContain("recentlogs");

        var preview = await response.Content.ReadFromJsonAsync<InvitePreviewResponse>();
        preview!.Scope.Should().Be("docsOnly");
        preview.ScopeLabel.Should().Be("Documents only");
        preview.EntityNames.Should().ContainSingle().Which.Should().Be("Surulere flat");
        preview.EntityCount.Should().Be(1);
        preview.NoAmounts.Should().BeTrue();
    }

    [Fact]
    public async Task Preview_ScopeAll_OmitsEntityNames_AndLabelsEverything()
    {
        var tenant = Guid.NewGuid();
        var (ownerClient, _) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient);
        var token = await CreateInviteAsync(ownerClient, "all", entityId, noAmounts: false);

        var preview = await (await _factory.CreateClient().GetAsync($"/personal-finance/circle/invites/{token}/preview"))
            .Content.ReadFromJsonAsync<InvitePreviewResponse>();

        preview!.Scope.Should().Be("all");
        preview.ScopeLabel.Should().Be("Everything they look after");
        preview.EntityNames.Should().BeEmpty(); // scope=all shares everything — no specific list to name
    }

    [Fact]
    public async Task Preview_UnknownToken_Returns404_FailClosed()
    {
        var anon = _factory.CreateClient();

        var response = await anon.GetAsync("/personal-finance/circle/invites/this-is-not-a-real-token/preview");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Preview_OverLengthToken_Returns404_FailClosed()
    {
        // A real token is ~43 chars; anything over the 128 cap is not one we issued and is rejected
        // as an indistinguishable 404 before it can become a per-token rate-limiter cache key.
        var anon = _factory.CreateClient();
        var overLong = new string('A', 200);

        var response = await anon.GetAsync($"/personal-finance/circle/invites/{overLong}/preview");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Preview_ConsumedToken_Returns404_FailClosed()
    {
        // Fail-closed indistinguishability: once accepted, the same token previews as a plain 404 — no oracle.
        var tenant = Guid.NewGuid();
        var (ownerClient, _) = await CreateUserAsync(tenant);
        var (memberClient, _) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient);
        var token = await CreateInviteAsync(ownerClient, "entities", entityId, noAmounts: false);

        var accept = await memberClient.PostAsJsonAsync("/personal-finance/circle/invites/accept", new { token });
        accept.StatusCode.Should().Be(HttpStatusCode.OK);

        var preview = await _factory.CreateClient().GetAsync($"/personal-finance/circle/invites/{token}/preview");
        preview.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Accept_ByOwner_Returns409_SelfAccept()
    {
        var tenant = Guid.NewGuid();
        var (ownerClient, _) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient);
        var token = await CreateInviteAsync(ownerClient, "entities", entityId, noAmounts: false);

        // The owner taps their own link: a conflict (409), never their own grant and never a silent 404.
        var response = await ownerClient.PostAsJsonAsync("/personal-finance/circle/invites/accept", new { token });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Accept_IsIdempotentForSameUser_ButSingleUseAcrossUsers()
    {
        var tenant = Guid.NewGuid();
        var (ownerClient, _) = await CreateUserAsync(tenant);
        var (memberClient, memberId) = await CreateUserAsync(tenant);
        var (otherClient, _) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient);
        var token = await CreateInviteAsync(ownerClient, "entities", entityId, noAmounts: false);

        // First accept by the member → their active grant.
        var first = await memberClient.PostAsJsonAsync("/personal-finance/circle/invites/accept", new { token });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstGrant = await first.Content.ReadFromJsonAsync<CircleGrantResponse>();

        // Same member replays the token (Simi cold-start / warm-link) → the SAME grant, not a second one.
        var replay = await memberClient.PostAsJsonAsync("/personal-finance/circle/invites/accept", new { token });
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        var replayGrant = await replay.Content.ReadFromJsonAsync<CircleGrantResponse>();
        replayGrant!.Id.Should().Be(firstGrant!.Id);
        replayGrant.MemberUserId.Should().Be(memberId);

        // A DIFFERENT user reaching the spent token is fail-closed (single-use) → 404.
        var other = await otherClient.PostAsJsonAsync("/personal-finance/circle/invites/accept", new { token });
        other.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<CircleInviteResponse> CreateInviteFullAsync(HttpClient ownerClient, string scope, Guid entityId)
        => (await (await ownerClient.PostAsJsonAsync(
            "/personal-finance/circle/invites",
            new CreateCircleInviteRequest(scope, new[] { entityId }, false, "link")))
            .Content.ReadFromJsonAsync<CircleInviteResponse>())!;

    [Fact]
    public async Task RevokeInvite_ByOwner_Returns204_AndPreviewThen404()
    {
        var tenant = Guid.NewGuid();
        var (ownerClient, _) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient);
        var invite = await CreateInviteFullAsync(ownerClient, "entities", entityId);

        var revoke = await ownerClient.DeleteAsync($"/personal-finance/circle/invites/{invite.Id}");
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The rescinded token now previews as a plain fail-closed 404 for a signed-out recipient.
        var preview = await _factory.CreateClient().GetAsync($"/personal-finance/circle/invites/{invite.Token}/preview");
        preview.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeInvite_AlreadyAccepted_Returns422()
    {
        var tenant = Guid.NewGuid();
        var (ownerClient, _) = await CreateUserAsync(tenant);
        var (memberClient, _) = await CreateUserAsync(tenant);
        var entityId = await CreateEntityAsync(ownerClient);
        var invite = await CreateInviteFullAsync(ownerClient, "entities", entityId);
        (await memberClient.PostAsJsonAsync("/personal-finance/circle/invites/accept", new { token = invite.Token }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // An accepted invite is a spent token — rescinding it as pending is a 422; revoke the grant instead.
        var revoke = await ownerClient.DeleteAsync($"/personal-finance/circle/invites/{invite.Id}");
        revoke.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RevokeInvite_CrossTenant_IsNotFound()
    {
        var (ownerClient, _) = await CreateUserAsync(Guid.NewGuid());
        var (otherTenantClient, _) = await CreateUserAsync(Guid.NewGuid());
        var entityId = await CreateEntityAsync(ownerClient);
        var invite = await CreateInviteFullAsync(ownerClient, "entities", entityId);

        // A user in a DIFFERENT tenant cannot revoke this invite — tenant isolation → 404, untouched.
        var crossTenant = await otherTenantClient.DeleteAsync($"/personal-finance/circle/invites/{invite.Id}");
        crossTenant.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Proof it was never touched: the real owner can still revoke it (204).
        var ownerRevoke = await ownerClient.DeleteAsync($"/personal-finance/circle/invites/{invite.Id}");
        ownerRevoke.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
