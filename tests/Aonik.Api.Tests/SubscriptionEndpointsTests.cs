using System.Net;
using System.Net.Http.Json;

using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Contracts.Models;
using Aonik.Subscriptions.Endpoints.Subscriptions;
using Aonik.Subscriptions.Endpoints.Usage;

using FluentAssertions;

using static Aonik.Api.Tests.WorkspaceTestSeeding;

namespace Aonik.Api.Tests;

/// <summary>
/// A subscription over HTTP (Spec 087 §6, §11): an operator builds the plan by route, a parent puts
/// their own party or their family group on it, spends from it, and cannot put anyone else on
/// anything — so a family in an environment with no storefront has a plan to spend from, and only
/// its own.
/// </summary>
public class SubscriptionEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SubscriptionEndpointsTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string Plan = "kidz-family";

    private async Task<HttpClient> OperatorWithPlanAsync(Guid tenantId)
    {
        var operatorClient = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles("TenantAdmin"));
        // The catalogue, by the routes an operator has: a counter for drafts, a plan, a free version, publication.
        (await operatorClient.PostAsJsonAsync("/subscriptions/admin/meters", new CreateMeterRequest("chapter-drafts", "Chapter drafts", MeterKinds.Counter, "drafts"))).StatusCode.Should().Be(HttpStatusCode.Created);
        var plan = (await (await operatorClient.PostAsJsonAsync("/subscriptions/admin/plans", new CreatePlanRequest(Plan, "Kidz family", BillingIntervals.None))).Content.ReadFromJsonAsync<PlanResponse>())!;
        var version = (await (await operatorClient.PostAsJsonAsync($"/subscriptions/admin/plans/{plan.Id}/versions", new CreatePlanVersionRequest(0m, "GBP"))).Content.ReadFromJsonAsync<PlanVersionResponse>())!;
        (await operatorClient.PutAsJsonAsync($"/subscriptions/admin/plan-versions/{version.Id}/entitlements", new SetEntitlementsRequest([new PlanEntitlementSpec("chapter-drafts", 20, ResetPolicies.Never)]))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await operatorClient.PostAsync($"/subscriptions/admin/plan-versions/{version.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        return operatorClient;
    }

    [Fact]
    public async Task AParent_Should_PutTheirFamilyOnAPublishedPlan_Once_AndSpendFromIt()
    {
        var tenantId = Guid.NewGuid();
        await OperatorWithPlanAsync(tenantId);
        var maya = await SignInAsync(_factory, tenantId, "Maya", withPlan: false);
        var familyId = await FamilyGroupAsync(_factory, tenantId, maya, "The Harpers");
        var family = new SubscriberModel(SubscriberKinds.Group, familyId);

        (await maya.Client.GetAsync($"/subscriptions?subscriberKind=group&subscriberId={familyId}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "nobody is subscribed yet");

        var subscribed = await maya.Client.PostAsJsonAsync("/subscriptions", new CreateSubscriptionRequest(family, Plan));
        subscribed.StatusCode.Should().Be(HttpStatusCode.Created, await subscribed.Content.ReadAsStringAsync());
        var subscription = (await subscribed.Content.ReadFromJsonAsync<SubscriptionDto>())!;
        subscription.PlanCode.Should().Be(Plan);
        subscription.Subscriber.Should().Be(new SubscriberRef(SubscriberKinds.Group, familyId));

        (await maya.Client.GetFromJsonAsync<SubscriptionDto>($"/subscriptions?subscriberKind=group&subscriberId={familyId}"))!.Id.Should().Be(subscription.Id);
        (await maya.Client.GetFromJsonAsync<AllowanceResponse>($"/usage/allowance?subscriberKind=group&subscriberId={familyId}"))!
            .Meters.Should().ContainSingle(m => m.MeterCode == "chapter-drafts" && m.Remaining == 20);

        // One active subscription per subscriber.
        var again = await maya.Client.PostAsJsonAsync("/subscriptions", new CreateSubscriptionRequest(family, Plan));
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadFromJsonAsync<UsageProblem>())!.Code.Should().Be("invalid-state");

        // Her own party is hers to subscribe as well.
        (await maya.Client.PostAsJsonAsync("/subscriptions", new CreateSubscriptionRequest(new SubscriberModel(SubscriberKinds.Party, maya.PartyId), Plan))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task NobodyElse_Should_SubscribeOrReadAFamily_AndAnUnknownPlan_Should_Be404()
    {
        var tenantId = Guid.NewGuid();
        var operatorClient = await OperatorWithPlanAsync(tenantId);
        var maya = await SignInAsync(_factory, tenantId, "Maya", withPlan: false);
        var okoro = await SignInAsync(_factory, tenantId, "Okoro", withPlan: false);
        var familyId = await FamilyGroupAsync(_factory, tenantId, maya, "The Harpers");
        var family = new SubscriberModel(SubscriberKinds.Group, familyId);

        // Not a member: the family's plan is the family's to buy, and its subscription is theirs to read.
        var refused = await okoro.Client.PostAsJsonAsync("/subscriptions", new CreateSubscriptionRequest(family, Plan));
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadFromJsonAsync<UsageProblem>())!.Code.Should().Be("forbidden");
        (await okoro.Client.GetAsync($"/subscriptions?subscriberKind=group&subscriberId={familyId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // Not even an operator: the route is a user's, and an operator holds no user role.
        (await operatorClient.PostAsJsonAsync("/subscriptions", new CreateSubscriptionRequest(family, Plan))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await okoro.Client.PostAsJsonAsync("/subscriptions", new CreateSubscriptionRequest(new SubscriberModel(SubscriberKinds.Party, maya.PartyId), Plan))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var missing = await maya.Client.PostAsJsonAsync("/subscriptions", new CreateSubscriptionRequest(family, "no-such-plan"));
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await missing.Content.ReadFromJsonAsync<UsageProblem>())!.Code.Should().Be("plan-not-found");

        (await maya.Client.PostAsJsonAsync("/subscriptions", new CreateSubscriptionRequest(new SubscriberModel("family", familyId), Plan))).StatusCode.Should().Be((HttpStatusCode)422);
    }
}
