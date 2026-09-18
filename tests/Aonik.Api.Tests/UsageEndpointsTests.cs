using System.Net;
using System.Net.Http.Json;

using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Endpoints.Usage;

using FluentAssertions;

using static Aonik.Api.Tests.WorkspaceTestSeeding;

namespace Aonik.Api.Tests;

/// <summary>
/// Metered usage over HTTP (Spec 087 §7; aonik#326 for ArkeKidz#9): a product reserves part of a
/// family's allowance before paid work, commits what the work used, releases what it did not — and
/// a lost response retried does not charge the family twice.
/// </summary>
public class UsageEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UsageEndpointsTests(CustomWebApplicationFactory factory) => _factory = factory;

    private const string Drafts = "chapter-drafts";

    private async Task<Person> SubscribedAsync(Guid tenantId, string name, decimal drafts = 3)
    {
        var person = await SignInAsync(_factory, tenantId, name, withPlan: false);
        await SubscribeAsync(_factory, tenantId, person.UserId, new SubscriberRef(SubscriberKinds.Party, person.PartyId), $"drafts-{drafts}",
            [(Drafts, "Chapter drafts", MeterKinds.Counter, "drafts", drafts, ResetPolicies.Never)]);
        return person;
    }

    private static SubscriberModel Party(Person person) => new(SubscriberKinds.Party, person.PartyId);

    [Fact]
    public async Task Reserve_Commit_And_Retry_Should_ChargeOnce()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SubscribedAsync(tenantId, "Maya");

        var reserved = await maya.Client.PostAsJsonAsync("/usage/reservations", new ReserveUsageRequest(Party(maya), Drafts, 1, "draft:op-1"));
        reserved.StatusCode.Should().Be(HttpStatusCode.OK, await reserved.Content.ReadAsStringAsync());
        var reservation = (await reserved.Content.ReadFromJsonAsync<UsageReservationResponse>())!;
        reservation.Status.Should().Be(UsageReservationStatuses.Held);
        reservation.Quantity.Should().Be(1);

        // The same key again is the same reservation, not a second hold.
        var again = await maya.Client.PostAsJsonAsync("/usage/reservations", new ReserveUsageRequest(Party(maya), Drafts, 1, "draft:op-1"));
        (await again.Content.ReadFromJsonAsync<UsageReservationResponse>())!.ReservationId.Should().Be(reservation.ReservationId);

        var held = await AllowanceAsync(maya);
        held.Held.Should().Be(1);
        held.Remaining.Should().Be(2);
        (await maya.Client.GetFromJsonAsync<UsageReservationResponse>($"/usage/reservations/{reservation.ReservationId}"))!.Status.Should().Be(UsageReservationStatuses.Held);

        // Commit for what was used; commit again after a lost response, and the answer is the earlier commit.
        var commit = new CommitUsageRequest(1, "chapter-draft", Guid.NewGuid());
        var committed = await maya.Client.PostAsJsonAsync($"/usage/reservations/{reservation.ReservationId}/commit", commit);
        committed.StatusCode.Should().Be(HttpStatusCode.OK, await committed.Content.ReadAsStringAsync());
        var first = (await committed.Content.ReadFromJsonAsync<UsageCommitResponse>())!;
        first.Replayed.Should().BeFalse();
        first.UsageRecordId.Should().NotBeNull();
        first.QuantityCommitted.Should().Be(1);

        var replayed = await maya.Client.PostAsJsonAsync($"/usage/reservations/{reservation.ReservationId}/commit", commit);
        replayed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replayed.Content.ReadFromJsonAsync<UsageCommitResponse>())!.Should().BeEquivalentTo(new { Replayed = true, Status = UsageReservationStatuses.Committed });

        var after = await AllowanceAsync(maya);
        after.Consumed.Should().Be(1, "one draft, however many times the commit was asked for");
        after.Held.Should().Be(0);
        after.Remaining.Should().Be(2);

        // Releasing a committed reservation changes nothing and says what it is.
        var release = await maya.Client.PostAsync($"/usage/reservations/{reservation.ReservationId}/release", null);
        release.StatusCode.Should().Be(HttpStatusCode.OK);
        (await release.Content.ReadFromJsonAsync<UsageReleaseResponse>())!.Status.Should().Be(UsageReservationStatuses.Committed);
        (await AllowanceAsync(maya)).Consumed.Should().Be(1);
    }

    [Fact]
    public async Task Release_Should_ReturnTheHold_And_TheCeiling_Should_Refuse()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SubscribedAsync(tenantId, "Maya", drafts: 2);

        var first = (await (await maya.Client.PostAsJsonAsync("/usage/reservations", new ReserveUsageRequest(Party(maya), Drafts, 1, "draft:op-a"))).Content.ReadFromJsonAsync<UsageReservationResponse>())!;
        var second = (await (await maya.Client.PostAsJsonAsync("/usage/reservations", new ReserveUsageRequest(Party(maya), Drafts, 1, "draft:op-b"))).Content.ReadFromJsonAsync<UsageReservationResponse>())!;
        (await AllowanceAsync(maya)).Remaining.Should().Be(0);

        var refused = await maya.Client.PostAsJsonAsync("/usage/reservations", new ReserveUsageRequest(Party(maya), Drafts, 1, "draft:op-c"));
        refused.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await refused.Content.ReadFromJsonAsync<UsageProblem>())!.Code.Should().Be("allowance-exceeded");

        // The work did not happen: the hold goes back, and the next one fits.
        var released = await maya.Client.PostAsync($"/usage/reservations/{first.ReservationId}/release", null);
        released.StatusCode.Should().Be(HttpStatusCode.OK);
        (await released.Content.ReadFromJsonAsync<UsageReleaseResponse>())!.Status.Should().Be(UsageReservationStatuses.Released);
        (await maya.Client.PostAsync($"/usage/reservations/{first.ReservationId}/release", null)).StatusCode.Should().Be(HttpStatusCode.OK, "releasing twice is nothing");
        (await AllowanceAsync(maya)).Remaining.Should().Be(1);
        (await maya.Client.PostAsJsonAsync("/usage/reservations", new ReserveUsageRequest(Party(maya), Drafts, 1, "draft:op-c"))).StatusCode.Should().Be(HttpStatusCode.OK);

        // A released reservation cannot be committed, and a commit above the hold is refused.
        var late = await maya.Client.PostAsJsonAsync($"/usage/reservations/{first.ReservationId}/commit", new CommitUsageRequest(1, "chapter-draft", Guid.NewGuid()));
        late.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await late.Content.ReadFromJsonAsync<UsageProblem>())!.Code.Should().Be("invalid-state");
        (await maya.Client.PostAsJsonAsync($"/usage/reservations/{second.ReservationId}/commit", new CommitUsageRequest(5, "chapter-draft", Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AnotherPerson_Should_SeeNoReservation_AndNoAllowance()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SubscribedAsync(tenantId, "Maya");
        var okoro = await SignInAsync(_factory, tenantId, "Okoro", withPlan: false);
        var reservation = (await (await maya.Client.PostAsJsonAsync("/usage/reservations", new ReserveUsageRequest(Party(maya), Drafts, 1, "draft:op-1"))).Content.ReadFromJsonAsync<UsageReservationResponse>())!;

        (await okoro.Client.GetAsync($"/usage/reservations/{reservation.ReservationId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await okoro.Client.PostAsJsonAsync($"/usage/reservations/{reservation.ReservationId}/commit", new CommitUsageRequest(1, "chapter-draft", Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await okoro.Client.PostAsync($"/usage/reservations/{reservation.ReservationId}/release", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await okoro.Client.GetAsync($"/usage/reservations/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "absent and foreign look the same");

        (await okoro.Client.PostAsJsonAsync("/usage/reservations", new ReserveUsageRequest(Party(maya), Drafts, 1, "draft:okoro"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await okoro.Client.GetAsync($"/usage/allowance?subscriberKind=party&subscriberId={maya.PartyId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var none = await okoro.Client.GetAsync($"/usage/allowance?subscriberKind=party&subscriberId={okoro.PartyId}");
        none.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await none.Content.ReadFromJsonAsync<UsageProblem>())!.Code.Should().Be("no-subscription");

        (await AllowanceAsync(maya)).Held.Should().Be(1, "nothing Okoro did touched Maya's hold");
    }

    [Fact]
    public async Task AFamilyGroup_Should_BeSpentBy_EitherParent()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SignInAsync(_factory, tenantId, "Maya", withPlan: false);
        var dev = await SignInAsync(_factory, tenantId, "Dev", withPlan: false);
        var okoro = await SignInAsync(_factory, tenantId, "Okoro", withPlan: false);
        var family = await FamilyGroupAsync(_factory, tenantId, maya, "The Harpers", dev);
        await SubscribeAsync(_factory, tenantId, maya.UserId, new SubscriberRef(SubscriberKinds.Group, family), "family-drafts-4", [(Drafts, "Chapter drafts", MeterKinds.Counter, "drafts", 4, ResetPolicies.Never)]);
        var group = new SubscriberModel(SubscriberKinds.Group, family);

        var byMaya = (await (await maya.Client.PostAsJsonAsync("/usage/reservations", new ReserveUsageRequest(group, Drafts, 1, "draft:maya-1"))).Content.ReadFromJsonAsync<UsageReservationResponse>())!;
        var byDev = (await (await dev.Client.PostAsJsonAsync("/usage/reservations", new ReserveUsageRequest(group, Drafts, 1, "draft:dev-1"))).Content.ReadFromJsonAsync<UsageReservationResponse>())!;
        byMaya.Status.Should().Be(UsageReservationStatuses.Held);
        byDev.Status.Should().Be(UsageReservationStatuses.Held);

        // Either parent may finish what the other started: the family's, not the person's.
        (await dev.Client.PostAsJsonAsync($"/usage/reservations/{byMaya.ReservationId}/commit", new CommitUsageRequest(1, "chapter-draft", Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await maya.Client.PostAsync($"/usage/reservations/{byDev.ReservationId}/release", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var allowance = (await dev.Client.GetFromJsonAsync<AllowanceResponse>($"/usage/allowance?subscriberKind=group&subscriberId={family}"))!;
        allowance.Meters.Single(m => m.MeterCode == Drafts).Should().BeEquivalentTo(new { Consumed = 1m, Held = 0m, Remaining = 3m });

        (await okoro.Client.PostAsJsonAsync("/usage/reservations", new ReserveUsageRequest(group, Drafts, 1, "draft:okoro"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await okoro.Client.GetAsync($"/usage/allowance?subscriberKind=group&subscriberId={family}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Requests_Should_BeValidated()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SubscribedAsync(tenantId, "Maya");

        (await maya.Client.PostAsJsonAsync("/usage/reservations", new ReserveUsageRequest(Party(maya), Drafts, 0, "k"))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await maya.Client.PostAsJsonAsync("/usage/reservations", new ReserveUsageRequest(Party(maya), Drafts, 1, ""))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await maya.Client.PostAsJsonAsync("/usage/reservations", new ReserveUsageRequest(new SubscriberModel("club", maya.PartyId), Drafts, 1, "k"))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await maya.Client.GetAsync("/usage/allowance?subscriberKind=club&subscriberId=" + maya.PartyId)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static async Task<MeterAllowanceModel> AllowanceAsync(Person person)
    {
        var response = await person.Client.GetAsync($"/usage/allowance?subscriberKind=party&subscriberId={person.PartyId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AllowanceResponse>())!.Meters.Single(m => m.MeterCode == Drafts);
    }
}
