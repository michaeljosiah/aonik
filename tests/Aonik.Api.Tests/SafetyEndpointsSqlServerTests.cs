using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

using Aonik.Ai.Endpoints.Safety;
using Aonik.IntegrationTests.Support;
using Aonik.Platform.Contracts.Api.Consent;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Safety;

using FluentAssertions;

using static Aonik.Api.Tests.SafetyTestSeeding;
using static Aonik.Api.Tests.WorkspaceTestSeeding;

namespace Aonik.Api.Tests;

/// <summary>
/// The content-safety boundary over HTTP (Spec 096; aonik#323 for ArkeKidz#10): a guardian's product
/// screens what their child typed and what a model produced, the decision is recorded with the hash
/// of exactly what was judged, the youngest band's output is held for the guardian's own review, and
/// nobody else's child is anybody's business.
///
/// <para>
/// SQL Server lane: enrolment is one transaction, and the route resolves through real catalogue and
/// consent rows. The classifier is a keyword double in place of the OpenAI moderation adapter; the
/// adapter itself is unit-tested against its wire shape.
/// </para>
/// </summary>
public sealed class SafetyEndpointsSqlServerTests : IClassFixture<SqlLocalDbFixture>, IDisposable
{
    private readonly SqlLocalDbFixture _db;
    private readonly CustomWebApplicationFactory? _factory;

    public SafetyEndpointsSqlServerTests(SqlLocalDbFixture db)
    {
        _db = db;
        _factory = db.IsAvailable ? new SafetyWebApplicationFactory(db.ConnectionString) : null;
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

    private static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    [SkippableFact]
    public async Task AGuardian_Should_ScreenInputAndOutput_AndReviewWhatIsHeld()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SignInAsync(Factory, tenantId, "Maya", withPlan: false);
        var dev = await SignInAsync(Factory, tenantId, "Dev", withPlan: false);
        await SeedClassificationRouteAsync(Factory, tenantId, maya.UserId);
        await SeedTermsAsync(Factory, tenantId, maya.UserId, Terms, KeywordClassificationProvider.Name);
        var ivy = await EnrolAsync(tenantId, maya, "Ivy", ConsentPurposes.SafetyClassification);

        // What the child typed, before a model sees it: fine, and not fine.
        var fine = await ScreenAsync(maya, ivy, "input", "Pip finds a little boat under the harbour wall.");
        fine.Should().BeEquivalentTo(new { Allowed = true, Outcome = "allowed", Categories = Array.Empty<string>(), ContentHash = Sha256("Pip finds a little boat under the harbour wall."), SafetyBand = SafetyBandNames.Under6 });
        fine.PendingReviewId.Should().BeNull();

        var blocked = await ScreenAsync(maya, ivy, "input", "There was blood everywhere.");
        blocked.Should().BeEquivalentTo(new { Allowed = false, Outcome = "blocked", Categories = new[] { SafetyCategories.GraphicViolence } });

        // What a model produced, before the child sees it: the youngest band holds it for the guardian.
        var body = "Pip crept along the harbour wall, one white ear turned to the sea.";
        var held = await ScreenAsync(maya, ivy, "output", body);
        held.Should().BeEquivalentTo(new { Allowed = false, Outcome = "held-for-review", ContentHash = Sha256(body) });
        held.PendingReviewId.Should().NotBeNull();

        var recorded = await maya.Client.GetFromJsonAsync<SafetyDecisionResponse>($"/safety/decisions/{held.DecisionId}");
        recorded!.Should().BeEquivalentTo(new { SubjectPartyId = ivy, Modality = "text", Layer = "L4", Outcome = "held-for-review", ContentHash = Sha256(body) });
        recorded.Review!.State.Should().Be("pending");

        // Not Dev's child: the decision is not visible to him, and not his to review.
        (await dev.Client.GetAsync($"/safety/decisions/{held.DecisionId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await dev.Client.PostAsJsonAsync($"/safety/decisions/{held.DecisionId}/review", new ReviewDecisionRequest("approve"))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The guardian's own review is what lets the product deliver.
        var approved = await maya.Client.PostAsJsonAsync($"/safety/decisions/{held.DecisionId}/review", new ReviewDecisionRequest("approve"));
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        (await approved.Content.ReadFromJsonAsync<ReviewDecisionResponse>())!.Should().BeEquivalentTo(new { Outcome = "approved", ContentHash = Sha256(body) });
        (await maya.Client.GetFromJsonAsync<SafetyDecisionResponse>($"/safety/decisions/{held.DecisionId}"))!.Review!.State.Should().Be("approved");
        (await (await maya.Client.PostAsJsonAsync($"/safety/decisions/{held.DecisionId}/review", new ReviewDecisionRequest("approve"))).Content.ReadFromJsonAsync<ReviewDecisionResponse>())!.Outcome
            .Should().Be("not-available", "a review is decided once");

        // An image is judged as its bytes: the decision's hash is the file's own.
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
        var image = await ScreenAsync(maya, ivy, "output", "data:image/png;base64," + Convert.ToBase64String(png), modality: "image");
        image.Should().BeEquivalentTo(new { Outcome = "held-for-review", ContentHash = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant() });
        (await maya.Client.GetFromJsonAsync<SafetyDecisionResponse>($"/safety/decisions/{image.DecisionId}"))!.Modality.Should().Be("image");

        // With pre-review off for this child, an output is allowed outright — and blocked is still blocked.
        await SetPreReviewAsync(Factory, tenantId, maya.UserId, maya.PartyId, ivy, enabled: false);
        (await ScreenAsync(maya, ivy, "output", body)).Should().BeEquivalentTo(new { Allowed = true, Outcome = "allowed", ContentHash = Sha256(body) });
        (await ScreenAsync(maya, ivy, "output", "Blood on the harbour wall.")).Should().BeEquivalentTo(new { Allowed = false, Outcome = "blocked", Categories = new[] { SafetyCategories.GraphicViolence } });
    }

    [SkippableFact]
    public async Task Screening_Should_Refuse_WithoutClassificationConsent_OrAuthority()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SignInAsync(Factory, tenantId, "Maya", withPlan: false);
        var okoro = await SignInAsync(Factory, tenantId, "Okoro", withPlan: false);
        await SeedClassificationRouteAsync(Factory, tenantId, maya.UserId);
        await SeedTermsAsync(Factory, tenantId, maya.UserId, Terms, KeywordClassificationProvider.Name);
        var ivy = await EnrolAsync(tenantId, maya, "Ivy");

        // service-core alone is not consent to send the child's words to a classifier.
        var refused = await maya.Client.PostAsJsonAsync("/safety/screen", new ScreenContentRequest(ivy, "text", "input", "Pip."));
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadFromJsonAsync<SafetyProblem>())!.Code.Should().Be("consent-required");

        (await maya.Client.PostAsJsonAsync($"/consent/wards/{ivy}/purposes", new GrantPurposeRequest(ConsentPurposes.SafetyClassification, Terms, "GB"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ScreenAsync(maya, ivy, "input", "Pip.")).Outcome.Should().Be("allowed");

        // Another family: the child is not theirs to screen for, and looks like no child at all.
        var foreign = await okoro.Client.PostAsJsonAsync("/safety/screen", new ScreenContentRequest(ivy, "text", "input", "Pip."));
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await foreign.Content.ReadFromJsonAsync<SafetyProblem>())!.Code.Should().Be("ward-not-found");
        (await okoro.Client.PostAsJsonAsync("/safety/screen", new ScreenContentRequest(Guid.NewGuid(), "text", "input", "Pip."))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // An image travels inline as its bytes, as an output; anything else about it is malformed.
        (await maya.Client.PostAsJsonAsync("/safety/screen", new ScreenContentRequest(ivy, "image", "output", "https://example/x.png"))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await maya.Client.PostAsJsonAsync("/safety/screen", new ScreenContentRequest(ivy, "image", "input", "data:image/png;base64,AAAA"))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await maya.Client.PostAsJsonAsync("/safety/screen", new ScreenContentRequest(ivy, "text", "sideways", "Pip."))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [SkippableFact]
    public async Task ARouteTheTermsDoNotName_Should_RefuseAsUnavailable_NeverPassThrough()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SignInAsync(Factory, tenantId, "Maya", withPlan: false);
        await SeedClassificationRouteAsync(Factory, tenantId, maya.UserId);
        // The terms the guardian consented to name a different classifier than the one the route resolves.
        await SeedTermsAsync(Factory, tenantId, maya.UserId, Terms, "some-other-classifier");
        var ivy = await EnrolAsync(tenantId, maya, "Ivy", ConsentPurposes.SafetyClassification);

        var verdict = await ScreenAsync(maya, ivy, "input", "Pip finds a boat.");
        verdict.Should().BeEquivalentTo(new { Allowed = false, Outcome = "check-unavailable" });
        (await maya.Client.GetFromJsonAsync<SafetyDecisionResponse>($"/safety/decisions/{verdict.DecisionId}"))!.Outcome.Should().Be("check-unavailable", "the refusal is on the record");
    }

    private async Task<SafetyVerdictResponse> ScreenAsync(Person guardian, Guid child, string layer, string content, string modality = "text")
    {
        var response = await guardian.Client.PostAsJsonAsync("/safety/screen", new ScreenContentRequest(child, modality, layer, content));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<SafetyVerdictResponse>())!;
    }

    private async Task<Guid> EnrolAsync(Guid tenantId, Person guardian, string childName, params string[] purposes)
    {
        var operatorClient = await Factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithTenant(tenantId).WithRoles("TenantAdmin").WithPermissions("Consent.Attest"));
        (await operatorClient.PostAsJsonAsync("/admin/consent/attestations", new AttestGuardianRequest(guardian.PartyId, "case-1"))).StatusCode.Should().Be(HttpStatusCode.Created);
        var response = await guardian.Client.PostAsJsonAsync("/consent/wards", new EnrolWardRequest(childName, new DateOnly(2021, 5, 4), Terms, "GB", purposes));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<EnrolWardResponse>())!.ChildPartyId;
    }
}
