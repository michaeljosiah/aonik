using System.Net;
using System.Net.Http.Json;

using Aonik.IntegrationTests.Support;
using Aonik.Platform.Contracts.Api.Consent;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Workspaces.Endpoints;

using FluentAssertions;

using static Aonik.Api.Tests.WorkspaceTestSeeding;

namespace Aonik.Api.Tests;

/// <summary>
/// Spec 095 §12 over HTTP: the child owns the world, and the people who act on it are the child's
/// guardians — each of them, without a share grant, and only while the child's consent stands.
///
/// <para>
/// SQL Server lane because enrolment is one transaction and the commit path is a compare-and-swap,
/// neither of which the InMemory provider can express. Skips where LocalDB is absent.
/// </para>
/// </summary>
public sealed class ChildOwnedWorkspaceSqlServerTests : IClassFixture<SqlLocalDbFixture>, IDisposable
{
    private readonly SqlLocalDbFixture _db;
    private readonly CustomWebApplicationFactory? _factory;

    public ChildOwnedWorkspaceSqlServerTests(SqlLocalDbFixture db)
    {
        _db = db;
        _factory = db.IsAvailable ? new SqlServerWebApplicationFactory(db.ConnectionString) : null;
    }

    public void Dispose() => _factory?.Dispose();

    private CustomWebApplicationFactory Factory
    {
        get
        {
            Skip.If(!_db.IsAvailable, _db.SkipReason);
            return _factory!;
        }
    }

    private const string Terms = "2026-09";
    private static readonly byte[] WorldJson = Utf8("{\"worldId\":\"01J8F3K2QW9VZX4N7M0RTYB6HC\",\"slug\":\"pip-s-harbour\",\"name\":\"Pip's Harbour\"}");
    private static readonly byte[] Character = Utf8("# Pip\n\nA small harbour fox with one white ear.\n");

    private static ManifestEntryModel Entry(string path, byte[] bytes) => new(path, Sha256Hex(bytes), bytes.Length);

    [SkippableFact]
    public async Task AChildsWorld_Should_BeTheChilds_AndEveryGuardians_WhileConsentStands()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SignInAsync(Factory, tenantId, "Maya");
        var dev = await SignInAsync(Factory, tenantId, "Dev", withPlan: false);
        var okoro = await SignInAsync(Factory, tenantId, "Okoro");
        var ivy = await EnrolAsync(tenantId, maya, "Ivy");

        // The family's plan pays, so that whichever parent acts can spend (Spec 087): the payer and the
        // owner are different questions, and a child holds no plan at all.
        var family = await FamilyGroupAsync(Factory, tenantId, maya, "The Harpers", dev);
        await SubscribeAsync(Factory, tenantId, maya.UserId, new SubscriberRef(SubscriberKinds.Group, family));
        var billing = new SubscriberModel(SubscriberKinds.Group, family);

        // The world is Ivy's.
        var created = await maya.Client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest("Pip's Harbour", OwnerPartyId: ivy, BillingSubscriber: billing));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var workspace = (await created.Content.ReadFromJsonAsync<WorkspaceResponse>())!;
        workspace.OwnerPartyId.Should().Be(ivy);

        // Maya acts as the owner: she uploads, commits and reads back, with no grant in sight.
        foreach (var bytes in new[] { WorldJson, Character })
        {
            (await UploadAsync(maya.Client, workspace.Id, bytes)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        var committed = await CommitAsync(maya, workspace.Id, new CommitRequest(Guid.NewGuid(), null, [Entry("world.json", WorldJson), Entry("characters/pip.md", Character)]));
        committed.Outcome.Should().Be("fastForward");
        (await maya.Client.GetFromJsonAsync<WorkspaceResponse>($"/workspaces/{workspace.Id}"))!.HeadRevisionId.Should().Be(committed.RevisionId);

        // Her list shows her own worlds and her ward's; Ivy's world is there without being hers.
        var mine = await maya.Client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest("Maya's notes"));
        var mayaOwn = (await mine.Content.ReadFromJsonAsync<WorkspaceResponse>())!;
        (await maya.Client.GetFromJsonAsync<WorkspaceListResponse>("/workspaces"))!.Workspaces.Select(w => w.Id)
            .Should().Equal([mayaOwn.Id, workspace.Id], "the caller's own first, then the wards'");

        // A second parent: Dev is attested and added as Ivy's guardian by Maya. Dev then acts on the
        // same world with the same authority — the point of Spec 095 §12.
        (await dev.Client.GetAsync($"/workspaces/{workspace.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "a guardian edge is the only thing that opens a child's world");
        await AttestAsync(tenantId, dev.PartyId);
        var added = await maya.Client.PostAsJsonAsync($"/consent/wards/{ivy}/guardians", new AddWardGuardianRequest(dev.PartyId, "GB"));
        added.StatusCode.Should().Be(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());
        (await dev.Client.GetFromJsonAsync<WardListResponse>("/consent/wards"))!.Wards.Should().ContainSingle(w => w.ChildPartyId == ivy);

        (await dev.Client.GetAsync($"/workspaces/{workspace.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await dev.Client.GetFromJsonAsync<WorkspaceListResponse>("/workspaces"))!.Workspaces.Should().ContainSingle(w => w.Id == workspace.Id);
        var byDev = await CommitAsync(dev, workspace.Id, new CommitRequest(Guid.NewGuid(), committed.RevisionId, [Entry("world.json", WorldJson)], "Dev tidies up"));
        byDev.Outcome.Should().Be("fastForward");
        (await maya.Client.GetFromJsonAsync<WorkspaceResponse>($"/workspaces/{workspace.Id}"))!.HeadRevisionId.Should().Be(byDev.RevisionId, "one world, whichever parent is holding the tablet");

        // Withdrawing service-core leaves both parents able to see what is there and to change nothing.
        (await dev.Client.DeleteAsync($"/consent/wards/{ivy}/purposes/{ConsentPurposes.ServiceCore}")).StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var parent in new[] { maya, dev })
        {
            (await parent.Client.GetAsync($"/workspaces/{workspace.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await parent.Client.GetAsync($"/workspaces/{workspace.Id}/blobs/{Sha256Hex(WorldJson)}")).StatusCode.Should().Be(HttpStatusCode.OK);
            var refused = await parent.Client.PostAsJsonAsync($"/workspaces/{workspace.Id}/commits", new CommitRequest(Guid.NewGuid(), byDev.RevisionId, [Entry("world.json", WorldJson), Entry("characters/pip.md", Character)]));
            refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await refused.Content.ReadFromJsonAsync<WorkspaceProblem>())!.Code.Should().Be("insufficient-access");
        }

        // Nor can a new world be made for a child whose consent does not stand.
        var afterWithdrawal = await maya.Client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest("Not now", OwnerPartyId: ivy, BillingSubscriber: billing));
        afterWithdrawal.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await afterWithdrawal.Content.ReadFromJsonAsync<WorkspaceProblem>())!.Code.Should().Be("consent-required");

        // Re-granting restores the authority on the next call, with nothing to re-share.
        (await maya.Client.PostAsJsonAsync($"/consent/wards/{ivy}/purposes", new GrantPurposeRequest(ConsentPurposes.ServiceCore, Terms, "GB"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await CommitAsync(maya, workspace.Id, new CommitRequest(Guid.NewGuid(), byDev.RevisionId, [Entry("world.json", WorldJson), Entry("characters/pip.md", Character)]))).Outcome.Should().Be("fastForward");

        // Another family, throughout: nothing to see, nothing to make.
        (await okoro.Client.GetAsync($"/workspaces/{workspace.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var notTheirs = await okoro.Client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest("Not yours", OwnerPartyId: ivy, BillingSubscriber: billing));
        notTheirs.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await notTheirs.Content.ReadFromJsonAsync<WorkspaceProblem>())!.Code.Should().Be("ward-not-found");
        (await okoro.Client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest("Nobody's", OwnerPartyId: Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await okoro.Client.PostAsJsonAsync($"/consent/wards/{ivy}/guardians", new AddWardGuardianRequest(dev.PartyId))).StatusCode.Should().Be(HttpStatusCode.NotFound, "only a guardian can authorise another");
        (await okoro.Client.GetFromJsonAsync<WorkspaceListResponse>("/workspaces"))!.Workspaces.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task ASecondGuardian_Should_BeVerified_AndNeverSelfAppointed()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SignInAsync(Factory, tenantId, "Maya", withPlan: false);
        var dev = await SignInAsync(Factory, tenantId, "Dev", withPlan: false);
        var ivy = await EnrolAsync(tenantId, maya, "Ivy");

        // Unverified, Dev cannot be given authority: the second guardian is held to the first's standard.
        var unverified = await maya.Client.PostAsJsonAsync($"/consent/wards/{ivy}/guardians", new AddWardGuardianRequest(dev.PartyId, "GB"));
        unverified.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await unverified.Content.ReadFromJsonAsync<ConsentProblem>())!.Code.Should().Be("guardian-not-verified");
        (await dev.Client.GetFromJsonAsync<WardListResponse>("/consent/wards"))!.Wards.Should().BeEmpty();

        // Nobody authorises their own addition.
        (await maya.Client.PostAsJsonAsync($"/consent/wards/{ivy}/guardians", new AddWardGuardianRequest(maya.PartyId, "GB"))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await AttestAsync(tenantId, dev.PartyId);
        (await maya.Client.PostAsJsonAsync($"/consent/wards/{ivy}/guardians", new AddWardGuardianRequest(dev.PartyId, "GB"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await maya.Client.PostAsJsonAsync($"/consent/wards/{ivy}/guardians", new AddWardGuardianRequest(dev.PartyId, "GB"))).StatusCode.Should().Be(HttpStatusCode.OK, "already a guardian is not an error");

        // Each guardian acts independently: Dev grants a purpose Maya never touched, and sees the ward.
        var granted = await dev.Client.PostAsJsonAsync($"/consent/wards/{ivy}/purposes", new GrantPurposeRequest(ConsentPurposes.GenerationDisclosure, Terms, "GB"));
        granted.StatusCode.Should().Be(HttpStatusCode.OK, await granted.Content.ReadAsStringAsync());
        (await maya.Client.GetFromJsonAsync<WardResponse>($"/consent/wards/{ivy}"))!.Purposes.Select(p => p.Purpose)
            .Should().BeEquivalentTo([ConsentPurposes.ServiceCore, ConsentPurposes.GenerationDisclosure]);
    }

    [SkippableFact]
    public async Task AGuardian_Should_ShareAChildsWorld_OnTheChildsBehalf()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SignInAsync(Factory, tenantId, "Maya");
        var okoro = await SignInAsync(Factory, tenantId, "Okoro");
        var ivy = await EnrolAsync(tenantId, maya, "Ivy");

        // No group here: with no billing subscriber named, the child's world is paid for by the caller.
        var created = await maya.Client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest("Pip's Harbour", OwnerPartyId: ivy));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var workspace = (await created.Content.ReadFromJsonAsync<WorkspaceResponse>())!;
        (await UploadAsync(maya.Client, workspace.Id, WorldJson)).StatusCode.Should().Be(HttpStatusCode.OK);
        await CommitAsync(maya, workspace.Id, new CommitRequest(Guid.NewGuid(), null, [Entry("world.json", WorldJson)]));

        // The grant is created as Maya; the share resolver resolves Ivy's world for Ivy's guardian.
        (await okoro.Client.GetAsync($"/workspaces/{workspace.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await GrantAsync(Factory, tenantId, maya, workspace.Id, okoro.PartyId, ShareAccessLevels.Read);

        (await okoro.Client.GetAsync($"/workspaces/{workspace.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await okoro.Client.GetAsync($"/workspaces/{workspace.Id}/blobs/{Sha256Hex(WorldJson)}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await UploadAsync(okoro.Client, workspace.Id, Character)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "read is read");
    }

    private async Task<Guid> EnrolAsync(Guid tenantId, Person guardian, string childName)
    {
        await AttestAsync(tenantId, guardian.PartyId);
        var response = await guardian.Client.PostAsJsonAsync("/consent/wards", new EnrolWardRequest(childName, new DateOnly(2021, 5, 4), Terms, "GB"));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<EnrolWardResponse>())!.ChildPartyId;
    }

    private async Task AttestAsync(Guid tenantId, Guid guardianPartyId)
    {
        var operatorClient = await Factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithTenant(tenantId).WithRoles("TenantAdmin").WithPermissions("Consent.Attest"));
        var response = await operatorClient.PostAsJsonAsync("/admin/consent/attestations", new AttestGuardianRequest(guardianPartyId, "case-1"));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private static async Task<CommitResponse> CommitAsync(Person person, Guid workspaceId, CommitRequest request)
    {
        var response = await person.Client.PostAsJsonAsync($"/workspaces/{workspaceId}/commits", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CommitResponse>())!;
    }
}
