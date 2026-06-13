using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentAssertions;

using Aonik.Documents.Contracts;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;

namespace Aonik.Api.Tests;

/// <summary>
/// HTTP-level E2E for the Spec 046 Vault document-linking surface — link CRUD on
/// /documents/{id}/links and the careEntity/type filter on the document list.
/// Driven as a tenant-wide staff caller (Operations) to exercise the link
/// mechanics without per-user party-resolution setup; owner-scoping is covered
/// by DocumentLinkServiceTests. Real FastEndpoints pipeline + InMemory store.
/// </summary>
public class DocumentLinkEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DocumentLinkEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // DocumentDto / DocumentListItem carry enums the API serializes as strings.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private Task<HttpClient> CreateStaffClientAsync()
        => _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("Operations"));

    private static async Task<Guid> CreateReceiptAsync(HttpClient client, string title = "Repair invoice")
    {
        var command = new CreateDocumentCommand(
            OwnerPartyId: Guid.NewGuid(),
            DocumentType: "receipt",
            Title: title);
        var response = await client.PostAsJsonAsync("/documents", command);
        response.IsSuccessStatusCode.Should().BeTrue();
        var dto = await response.Content.ReadFromJsonAsync<DocumentDto>(JsonOpts);
        dto!.Title.Should().Be(title); // Title round-trips
        return dto.DocumentId;
    }

    [Fact]
    public async Task LinkDocumentToEntity_AppearsInLinks_AndCareEntityFilter()
    {
        var client = await CreateStaffClientAsync();
        var docId = await CreateReceiptAsync(client);
        var entityId = Guid.NewGuid();

        var addResponse = await client.PostAsJsonAsync(
            $"/documents/{docId}/links",
            new AddDocumentLinkRequest("careEntity", entityId));
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var link = await addResponse.Content.ReadFromJsonAsync<DocumentLinkDto>();
        link!.TargetId.Should().Be(entityId);

        var listResponse = await client.GetAsync($"/documents/{docId}/links");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var links = await listResponse.Content.ReadFromJsonAsync<List<DocumentLinkDto>>();
        links!.Should().ContainSingle(l => l.TargetId == entityId);

        var filterResponse = await client.GetAsync($"/documents?careEntityId={entityId}&documentType=receipt");
        filterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await filterResponse.Content.ReadFromJsonAsync<PagedResult<DocumentListItem>>(JsonOpts);
        page!.Items.Should().Contain(d => d.DocumentId == docId);
    }

    [Fact]
    public async Task RemoveLink_Returns204_AndClearsIt()
    {
        var client = await CreateStaffClientAsync();
        var docId = await CreateReceiptAsync(client);
        var link = await (await client.PostAsJsonAsync(
            $"/documents/{docId}/links", new AddDocumentLinkRequest("paymentLog", Guid.NewGuid())))
            .Content.ReadFromJsonAsync<DocumentLinkDto>();

        var removeResponse = await client.DeleteAsync($"/documents/{docId}/links/{link!.Id}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var links = await (await client.GetAsync($"/documents/{docId}/links"))
            .Content.ReadFromJsonAsync<List<DocumentLinkDto>>();
        links!.Should().BeEmpty();
    }

    [Fact]
    public async Task AddLink_WithBadTargetType_Returns422()
    {
        var client = await CreateStaffClientAsync();
        var docId = await CreateReceiptAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/documents/{docId}/links",
            new AddDocumentLinkRequest("spaceship", Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AddLink_ToUnknownDocument_Returns404()
    {
        var client = await CreateStaffClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/documents/{Guid.NewGuid()}/links",
            new AddDocumentLinkRequest("careEntity", Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/documents/{Guid.NewGuid()}/links");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
