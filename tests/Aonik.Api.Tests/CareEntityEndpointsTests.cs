using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Api.Tests;

/// <summary>
/// HTTP-level E2E for the Spec 043 care-entity endpoints — the exact surface
/// the Aonik CLI's <c>care-entities</c> command group drives. Runs the real
/// FastEndpoints pipeline (routing, UserPolicy authorization, FluentValidation,
/// serialization) in-process via <see cref="CustomWebApplicationFactory"/>,
/// which swaps Auth0 for a test scheme and SQL Server for InMemory. This is the
/// faithful substitute for the live-CLI E2E, which is blocked locally by the
/// bare API's startup hang and Auth0's lack of a local password-grant path.
/// </summary>
public class CareEntityEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CareEntityEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private Task<HttpClient> CreateUserClientAsync(Guid? tenantId = null)
    {
        var options = TestAuthOptions.Create().WithRoles("PersonalUser");
        if (tenantId.HasValue)
        {
            options.WithTenant(tenantId.Value);
        }

        return _factory.CreateAuthenticatedClientAsync(options);
    }

    [Fact]
    public async Task CreatePerson_ThenGetAndList_RoundTrips()
    {
        var client = await CreateUserClientAsync();
        var request = new CreateCareEntityRequest("person", null, "Mum", "NG", "mother", "👩🏾", null, null);

        var createResponse = await client.PostAsJsonAsync("/personal-finance/care-entities", request);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CareEntityResponse>();
        created.Should().NotBeNull();
        created!.Kind.Should().Be("person");
        created.AssetType.Should().BeNull();
        created.Name.Should().Be("Mum");
        created.CountryCode.Should().Be("NG");
        created.Relationship.Should().Be("mother");

        var getResponse = await client.GetAsync($"/personal-finance/care-entities/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<CareEntityResponse>();
        fetched!.Id.Should().Be(created.Id);

        var listResponse = await client.GetAsync("/personal-finance/care-entities");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<List<CareEntityResponse>>();
        list!.Should().ContainSingle(e => e.Id == created.Id);
    }

    [Fact]
    public async Task CreateAsset_WithAttributes_RoundTrips_AndProfileHasEmptyDependents()
    {
        var client = await CreateUserClientAsync();
        var attributes = new Dictionary<string, string>
        {
            ["address"] = "12 Bode Thomas",
            ["titleNumber"] = "LA-99",
        };
        var request = new CreateCareEntityRequest("asset", "property", "Surulere flat", "ng", null, "🏠", null, attributes);

        var createResponse = await client.PostAsJsonAsync("/personal-finance/care-entities", request);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CareEntityResponse>();
        created!.Kind.Should().Be("asset");
        created.AssetType.Should().Be("property");
        created.CountryCode.Should().Be("NG"); // normalised to upper
        created.Attributes.Should().ContainKey("address");
        created.Attributes["address"].Should().Be("12 Bode Thomas");

        var profileResponse = await client.GetAsync($"/personal-finance/care-entities/{created.Id}/profile");
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await profileResponse.Content.ReadFromJsonAsync<CareEntityProfileResponse>();
        profile!.Entity.Id.Should().Be(created.Id);
        profile.YearTotals.Should().BeEmpty();
        profile.Commitments.Should().BeEmpty();
        profile.RecentLogs.Should().BeEmpty();
        profile.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsset_WithoutAssetType_Returns422()
    {
        var client = await CreateUserClientAsync();
        var request = new CreateCareEntityRequest("asset", null, "Mystery", "NG", null, null, null, null);

        var response = await client.PostAsJsonAsync("/personal-finance/care-entities", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreatePerson_WithAssetType_Returns422()
    {
        var client = await CreateUserClientAsync();
        var request = new CreateCareEntityRequest("person", "property", "Mum", "NG", null, null, null, null);

        var response = await client.PostAsJsonAsync("/personal-finance/care-entities", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Update_ChangesMutableFields_Returns200()
    {
        var client = await CreateUserClientAsync();
        var create = await client.PostAsJsonAsync(
            "/personal-finance/care-entities",
            new CreateCareEntityRequest("person", null, "Mum", "NG", "mother", null, null, null));
        var created = await create.Content.ReadFromJsonAsync<CareEntityResponse>();

        var update = new UpdateCareEntityRequest("Mama", null, "GB", "mother-in-law", "👵🏾", null, null);
        var updateResponse = await client.PutAsJsonAsync($"/personal-finance/care-entities/{created!.Id}", update);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CareEntityResponse>();
        updated!.Name.Should().Be("Mama");
        updated.CountryCode.Should().Be("GB");
        updated.Relationship.Should().Be("mother-in-law");
        updated.Kind.Should().Be("person");
    }

    [Fact]
    public async Task Archive_Returns204_AndExcludedFromDefaultList()
    {
        var client = await CreateUserClientAsync();
        var create = await client.PostAsJsonAsync(
            "/personal-finance/care-entities",
            new CreateCareEntityRequest("asset", "vehicle", "The Hilux", "NG", null, null, null, null));
        var created = await create.Content.ReadFromJsonAsync<CareEntityResponse>();

        var archiveResponse = await client.PostAsJsonAsync(
            $"/personal-finance/care-entities/{created!.Id}/archive", new { });
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var defaultList = await (await client.GetAsync("/personal-finance/care-entities"))
            .Content.ReadFromJsonAsync<List<CareEntityResponse>>();
        defaultList!.Should().NotContain(e => e.Id == created.Id);

        var withArchived = await (await client.GetAsync("/personal-finance/care-entities?includeArchived=true"))
            .Content.ReadFromJsonAsync<List<CareEntityResponse>>();
        withArchived!.Should().Contain(e => e.Id == created.Id && e.Archived);
    }

    [Fact]
    public async Task Isolation_OtherUserInSameTenantCannotRead_Returns404()
    {
        var tenant = Guid.NewGuid();
        var ownerClient = await CreateUserClientAsync(tenant);
        var strangerClient = await CreateUserClientAsync(tenant);

        var create = await ownerClient.PostAsJsonAsync(
            "/personal-finance/care-entities",
            new CreateCareEntityRequest("person", null, "Mum", "NG", null, null, null, null));
        var created = await create.Content.ReadFromJsonAsync<CareEntityResponse>();

        var strangerGet = await strangerClient.GetAsync($"/personal-finance/care-entities/{created!.Id}");

        strangerGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/personal-finance/care-entities");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
