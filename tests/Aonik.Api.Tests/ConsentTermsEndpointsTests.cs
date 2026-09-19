using System.Net;
using System.Net.Http.Json;

using Aonik.IntegrationTests.Support;
using Aonik.Platform.Contracts.Api.Consent;

using FluentAssertions;

using static Aonik.Api.Tests.WorkspaceTestSeeding;

namespace Aonik.Api.Tests;

/// <summary>
/// The operator's terms over HTTP (Spec 095 §10.2, Spec 096 §16): a version is published naming the
/// processors it discloses, becomes the tenant's current terms, can be read back, and is nobody
/// else's to write.
/// </summary>
public class ConsentTermsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ConsentTermsEndpointsTests(CustomWebApplicationFactory factory) => _factory = factory;

    private Task<HttpClient> OperatorAsync(Guid tenantId)
        => _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles("TenantAdmin"));

    [Fact]
    public async Task AnOperator_Should_PublishTerms_NamingProviders_AndReadThemBack()
    {
        var tenantId = Guid.NewGuid();
        var operatorClient = await OperatorAsync(tenantId);

        var published = await operatorClient.PostAsJsonAsync("/admin/consent/terms", new PublishConsentTermsRequest("kidz-2026-09", ["OpenAI"]));
        published.StatusCode.Should().Be(HttpStatusCode.Created, await published.Content.ReadAsStringAsync());
        var first = (await published.Content.ReadFromJsonAsync<PublishConsentTermsResponse>())!;
        first.Should().BeEquivalentTo(new { Version = "kidz-2026-09", NamedProviders = new[] { "openai" }, IsCurrent = true, RevokedGrants = 0 });

        // A second version supersedes the first as current; the first stays on record for the grants that name it.
        var next = (await (await operatorClient.PostAsJsonAsync("/admin/consent/terms", new PublishConsentTermsRequest("kidz-2026-10", ["openai", "anthropic"], ["service-core"])))
            .Content.ReadFromJsonAsync<PublishConsentTermsResponse>())!;
        next.IsCurrent.Should().BeTrue();
        next.NamedProviders.Should().BeEquivalentTo(["openai", "anthropic"]);

        var listed = (await operatorClient.GetFromJsonAsync<ConsentTermsVersionsResponse>("/admin/consent/terms"))!;
        listed.Versions.Select(v => (v.Version, v.IsCurrent)).Should().BeEquivalentTo([("kidz-2026-10", true), ("kidz-2026-09", false)]);

        // Publishing a version again updates what it names and does not create a second row.
        (await operatorClient.PostAsJsonAsync("/admin/consent/terms", new PublishConsentTermsRequest("kidz-2026-09", ["openai"]))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await operatorClient.GetFromJsonAsync<ConsentTermsVersionsResponse>("/admin/consent/terms"))!.Versions.Should().HaveCount(2);
    }

    [Fact]
    public async Task AParent_Should_NotPublishTerms_AndNoProvider_Should_BeRefused()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SignInAsync(_factory, tenantId, "Maya", withPlan: false);
        (await maya.Client.PostAsJsonAsync("/admin/consent/terms", new PublishConsentTermsRequest("v1", ["openai"]))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // Reading them is any signed-in user's: what a family consents to is not a secret from them.
        (await maya.Client.GetAsync("/admin/consent/terms")).StatusCode.Should().Be(HttpStatusCode.OK);

        var operatorClient = await OperatorAsync(tenantId);
        (await operatorClient.PostAsJsonAsync("/admin/consent/terms", new PublishConsentTermsRequest("v1", []))).StatusCode.Should().Be((HttpStatusCode)422);
        (await operatorClient.PostAsJsonAsync("/admin/consent/terms", new PublishConsentTermsRequest("", ["openai"]))).StatusCode.Should().Be((HttpStatusCode)422);
    }
}
