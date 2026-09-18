using System.Net;
using System.Net.Http.Json;

using Aonik.Groups.Endpoints;
using Aonik.IntegrationTests.Support;
using Aonik.Platform.Contracts.Api.Consent;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Groups;

using FluentAssertions;

using static Aonik.Api.Tests.WorkspaceTestSeeding;

namespace Aonik.Api.Tests;

/// <summary>
/// The identity half of the first Kidz journey over HTTP (aonik#326, #328): an operator attests a
/// guardian through the signed-form route, the guardian enrols a child and grants purposes, another
/// family sees nothing, and a family group is stood up with the child in it.
///
/// <para>
/// SQL Server lane: enrolment is one transaction (Spec 095 §12.2), which the InMemory provider
/// cannot express. Skips where LocalDB is absent.
/// </para>
/// </summary>
public sealed class ConsentAndGroupEndpointsSqlServerTests : IClassFixture<SqlLocalDbFixture>, IDisposable
{
    private readonly SqlLocalDbFixture _db;
    private readonly CustomWebApplicationFactory? _factory;

    public ConsentAndGroupEndpointsSqlServerTests(SqlLocalDbFixture db)
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

    [SkippableFact]
    public async Task AGuardian_Should_EnrolAndGrant_OnlyOnceAnOperatorHasAttestedThem()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SignInAsync(Factory, tenantId, "Maya", withPlan: false);
        var operatorClient = await Factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithTenant(tenantId).WithRoles("TenantAdmin").WithPermissions("Consent.Attest"));

        // Nothing stands yet, and nothing can be created: no accepted route has verified Maya.
        (await maya.Client.GetFromJsonAsync<WardListResponse>("/consent/wards"))!.Wards.Should().BeEmpty();
        var unverified = await maya.Client.PostAsJsonAsync("/consent/wards", new EnrolWardRequest("Ivy", new DateOnly(2021, 5, 4), Terms, "GB"));
        unverified.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await unverified.Content.ReadFromJsonAsync<ConsentProblem>())!.Code.Should().Be("guardian-not-verified");
        (await maya.Client.GetFromJsonAsync<WardListResponse>("/consent/wards"))!.Wards.Should().BeEmpty("a refused enrolment creates nothing");

        // The guardian cannot attest themselves; an operator records the signed form.
        (await maya.Client.PostAsJsonAsync("/admin/consent/attestations", new AttestGuardianRequest(maya.PartyId))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var attested = await operatorClient.PostAsJsonAsync("/admin/consent/attestations", new AttestGuardianRequest(maya.PartyId, "case-4411", "Form matched to passport copy"));
        attested.StatusCode.Should().Be(HttpStatusCode.Created);
        var attestation = (await attested.Content.ReadFromJsonAsync<AttestGuardianResponse>())!;

        // Enrolment: child, guardian edge and service-core in one go, verified by the signed form.
        var enrolled = await maya.Client.PostAsJsonAsync("/consent/wards", new EnrolWardRequest("Ivy", new DateOnly(2021, 5, 4), Terms, "GB"));
        enrolled.StatusCode.Should().Be(HttpStatusCode.Created, await enrolled.Content.ReadAsStringAsync());
        var ivy = (await enrolled.Content.ReadFromJsonAsync<EnrolWardResponse>())!;
        ivy.VerificationMethod.Should().Be(ConsentVerificationMethods.SignedForm);
        enrolled.Headers.Location!.ToString().Should().EndWith($"/consent/wards/{ivy.ChildPartyId}");

        var ward = await maya.Client.GetFromJsonAsync<WardResponse>($"/consent/wards/{ivy.ChildPartyId}");
        ward!.DisplayName.Should().Be("Ivy");
        ward.SafetyBand.Should().Be(ivy.SafetyBand);
        ward.Purposes.Select(p => p.Purpose).Should().Equal(ConsentPurposes.ServiceCore);
        ward.Purposes.Single().VerificationMethod.Should().Be(ConsentVerificationMethods.SignedForm);
        ward.Purposes.Single().GrantedByPartyId.Should().Be(maya.PartyId);

        // A further purpose, verified again on the same route. Re-granting the same terms is idempotent.
        var granted = await maya.Client.PostAsJsonAsync($"/consent/wards/{ivy.ChildPartyId}/purposes", new GrantPurposeRequest(ConsentPurposes.GenerationDisclosure, Terms, "GB"));
        granted.StatusCode.Should().Be(HttpStatusCode.OK, await granted.Content.ReadAsStringAsync());
        (await granted.Content.ReadFromJsonAsync<GrantPurposeResponse>())!.VerificationMethod.Should().Be(ConsentVerificationMethods.SignedForm);
        (await maya.Client.PostAsJsonAsync($"/consent/wards/{ivy.ChildPartyId}/purposes", new GrantPurposeRequest(ConsentPurposes.GenerationDisclosure, Terms, "GB"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await maya.Client.GetFromJsonAsync<WardResponse>($"/consent/wards/{ivy.ChildPartyId}"))!.Purposes.Select(p => p.Purpose)
            .Should().BeEquivalentTo([ConsentPurposes.ServiceCore, ConsentPurposes.GenerationDisclosure]);

        var unknown = await maya.Client.PostAsJsonAsync($"/consent/wards/{ivy.ChildPartyId}/purposes", new GrantPurposeRequest("mind-reading", Terms));
        unknown.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Withdrawal takes effect at once.
        var withdrawn = await maya.Client.DeleteAsync($"/consent/wards/{ivy.ChildPartyId}/purposes/{ConsentPurposes.GenerationDisclosure}");
        withdrawn.StatusCode.Should().Be(HttpStatusCode.OK);
        (await maya.Client.GetFromJsonAsync<WardResponse>($"/consent/wards/{ivy.ChildPartyId}"))!.Purposes.Select(p => p.Purpose)
            .Should().Equal(ConsentPurposes.ServiceCore);

        // The attestation is revoked: grants that stand keep standing, but no new one can be made.
        var revoke = new HttpRequestMessage(HttpMethod.Delete, $"/admin/consent/attestations/{attestation.AttestationId}") { Content = JsonContent.Create(new RevokeAttestationRequest("Form found to be a photocopy")) };
        (await operatorClient.SendAsync(revoke)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterRevocation = await maya.Client.PostAsJsonAsync($"/consent/wards/{ivy.ChildPartyId}/purposes", new GrantPurposeRequest(ConsentPurposes.Voice, Terms, "GB"));
        afterRevocation.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await afterRevocation.Content.ReadFromJsonAsync<ConsentProblem>())!.Code.Should().Be("guardian-not-verified");
        (await maya.Client.GetFromJsonAsync<WardResponse>($"/consent/wards/{ivy.ChildPartyId}"))!.Purposes.Select(p => p.Purpose)
            .Should().Equal([ConsentPurposes.ServiceCore], "revoking the attestation does not revoke consent already given");
    }

    [SkippableFact]
    public async Task AnotherFamily_Should_SeeNoWard_AndChangeNothing()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SignInAsync(Factory, tenantId, "Maya", withPlan: false);
        var okoro = await SignInAsync(Factory, tenantId, "Okoro", withPlan: false);
        var ivy = await EnrolAsync(tenantId, maya, "Ivy");

        (await okoro.Client.GetFromJsonAsync<WardListResponse>("/consent/wards"))!.Wards.Should().BeEmpty();

        var foreign = await okoro.Client.GetAsync($"/consent/wards/{ivy.ChildPartyId}");
        var absent = await okoro.Client.GetAsync($"/consent/wards/{Guid.NewGuid()}");
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await foreign.Content.ReadFromJsonAsync<ConsentProblem>()).Should().BeEquivalentTo(await absent.Content.ReadFromJsonAsync<ConsentProblem>(),
            "another family's child must not be distinguishable from no child at all");

        // Okoro is attested, so verification would succeed; it is the missing guardian edge that refuses.
        await AttestAsync(tenantId, okoro.PartyId);
        (await okoro.Client.PostAsJsonAsync($"/consent/wards/{ivy.ChildPartyId}/purposes", new GrantPurposeRequest(ConsentPurposes.GenerationDisclosure, Terms, "GB")))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await okoro.Client.DeleteAsync($"/consent/wards/{ivy.ChildPartyId}/purposes/{ConsentPurposes.ServiceCore}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await maya.Client.GetFromJsonAsync<WardResponse>($"/consent/wards/{ivy.ChildPartyId}"))!.Purposes.Select(p => p.Purpose)
            .Should().Equal([ConsentPurposes.ServiceCore], "nothing Okoro did touched Ivy's consent");
    }

    [SkippableFact]
    public async Task AFamilyGroup_Should_HoldTheParentAndTheChild_AndBeInvisibleToOthers()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SignInAsync(Factory, tenantId, "Maya", withPlan: false);
        var okoro = await SignInAsync(Factory, tenantId, "Okoro", withPlan: false);
        var ivy = await EnrolAsync(tenantId, maya, "Ivy");

        var created = await maya.Client.PostAsJsonAsync("/groups", new CreateGroupRequest("family", "The Harpers"));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var family = (await created.Content.ReadFromJsonAsync<GroupDto>())!;
        family.Kind.Should().Be(GroupKinds.Family);
        family.Members.Should().ContainSingle(m => m.PartyId == maya.PartyId && m.Role == GroupRoles.Owner);

        // The child has no login, so they are added rather than invited.
        var added = await maya.Client.PostAsJsonAsync($"/groups/{family.Id}/members", new AddGroupMemberRequest(ivy.ChildPartyId, "viewer"));
        added.StatusCode.Should().Be(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());
        (await added.Content.ReadFromJsonAsync<GroupMemberDto>())!.Role.Should().Be(GroupRoles.Viewer);

        var fetched = await maya.Client.GetFromJsonAsync<GroupDto>($"/groups/{family.Id}");
        fetched!.Members.Select(m => m.PartyId).Should().BeEquivalentTo([maya.PartyId, ivy.ChildPartyId]);
        (await maya.Client.GetFromJsonAsync<GroupListResponse>("/groups/mine"))!.Groups.Should().ContainSingle(g => g.Id == family.Id);

        // Adding the same child twice, or a party who has a login, is refused with a reason.
        (await maya.Client.PostAsJsonAsync($"/groups/{family.Id}/members", new AddGroupMemberRequest(ivy.ChildPartyId, "viewer"))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await maya.Client.PostAsJsonAsync($"/groups/{family.Id}/members", new AddGroupMemberRequest(okoro.PartyId, "manager"))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Another family: the group is not visible, and cannot be changed.
        (await okoro.Client.GetFromJsonAsync<GroupListResponse>("/groups/mine"))!.Groups.Should().BeEmpty();
        (await okoro.Client.GetAsync($"/groups/{family.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await okoro.Client.PostAsJsonAsync($"/groups/{family.Id}/members", new AddGroupMemberRequest(Guid.NewGuid(), "viewer"))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await maya.Client.PostAsJsonAsync("/groups", new CreateGroupRequest("club", "Not a kind"))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<EnrolWardResponse> EnrolAsync(Guid tenantId, Person guardian, string childName)
    {
        await AttestAsync(tenantId, guardian.PartyId);
        var response = await guardian.Client.PostAsJsonAsync("/consent/wards", new EnrolWardRequest(childName, new DateOnly(2021, 5, 4), Terms, "GB"));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<EnrolWardResponse>())!;
    }

    private async Task AttestAsync(Guid tenantId, Guid guardianPartyId)
    {
        var operatorClient = await Factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithTenant(tenantId).WithRoles("TenantAdmin").WithPermissions("Consent.Attest"));
        var response = await operatorClient.PostAsJsonAsync("/admin/consent/attestations", new AttestGuardianRequest(guardianPartyId, "case-1"));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }
}
