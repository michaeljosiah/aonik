using System.Net;
using System.Net.Http.Json;

using Aonik.Workspaces.Endpoints;

using FluentAssertions;

using static Aonik.Api.Tests.WorkspaceTestSeeding;

namespace Aonik.Api.Tests;

/// <summary>
/// The workspace endpoints' contract for everything the InMemory provider can express (aonik#326): who may
/// call, what a caller may see, and how bytes move. Commits, replay and divergence need the SQL lane and live
/// in <see cref="WorkspaceSyncEndpointsSqlServerTests"/>.
/// </summary>
public class WorkspaceEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public WorkspaceEndpointsTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Workspaces_Should_RequireASession()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/workspaces");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Workspaces_Should_RefuseAUserWithNoParty()
    {
        var tenantId = Guid.NewGuid();
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithTenant(tenantId).WithRoles("PersonalUser").WithPermissions("Workspace.Use"));

        var response = await client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest("Nobody's"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<WorkspaceProblem>();
        problem!.Code.Should().Be("no-party");
    }

    [Fact]
    public async Task Create_Should_ClaimASlotBeforeTheWorkspaceExists_When_ThePartyHasNoPlan()
    {
        var tenantId = Guid.NewGuid();
        var person = await SignInAsync(_factory, tenantId, "Unsubscribed", withPlan: false);

        var response = await person.Client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest("No plan"));

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        var problem = await response.Content.ReadFromJsonAsync<WorkspaceProblem>();
        problem!.Code.Should().Be("allowance-exceeded");

        var mine = await person.Client.GetFromJsonAsync<WorkspaceListResponse>("/workspaces");
        mine!.Workspaces.Should().BeEmpty("a refused claim must leave no workspace behind");
    }

    [Fact]
    public async Task Create_Get_And_List_Should_ReturnTheCallersWorkspace()
    {
        var tenantId = Guid.NewGuid();
        var person = await SignInAsync(_factory, tenantId, "Maya");

        var created = await person.Client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest("Pip's Harbour"));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var workspace = await created.Content.ReadFromJsonAsync<WorkspaceResponse>();
        workspace!.OwnerPartyId.Should().Be(person.PartyId);
        workspace.Kind.Should().Be("world");
        workspace.HeadRevisionId.Should().BeNull();
        created.Headers.Location!.ToString().Should().EndWith($"/workspaces/{workspace.Id}");

        var fetched = await person.Client.GetFromJsonAsync<WorkspaceResponse>($"/workspaces/{workspace.Id}");
        fetched.Should().BeEquivalentTo(workspace);

        var mine = await person.Client.GetFromJsonAsync<WorkspaceListResponse>("/workspaces");
        mine!.Workspaces.Should().ContainSingle(w => w.Id == workspace.Id);

        var manifest = await person.Client.GetFromJsonAsync<ManifestResponse>($"/workspaces/{workspace.Id}/manifest");
        manifest!.RevisionId.Should().BeNull();
        manifest.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_Should_RefuseAnUnknownKind()
    {
        var tenantId = Guid.NewGuid();
        var person = await SignInAsync(_factory, tenantId, "Maya");

        var response = await person.Client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest("Odd", Kind: "spreadsheet"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AnotherFamily_Should_SeeTheSameNotFound_ForAForeignAndAnAbsentWorkspace()
    {
        var tenantId = Guid.NewGuid();
        var harper = await SignInAsync(_factory, tenantId, "Harper");
        var okoro = await SignInAsync(_factory, tenantId, "Okoro");
        var created = await harper.Client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest("Harpers' world"));
        var workspace = (await created.Content.ReadFromJsonAsync<WorkspaceResponse>())!;

        var foreign = await okoro.Client.GetAsync($"/workspaces/{workspace.Id}");
        var absent = await okoro.Client.GetAsync($"/workspaces/{Guid.NewGuid()}");

        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        absent.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var foreignProblem = await foreign.Content.ReadFromJsonAsync<WorkspaceProblem>();
        var absentProblem = await absent.Content.ReadFromJsonAsync<WorkspaceProblem>();
        foreignProblem.Should().BeEquivalentTo(absentProblem, "no existence oracle for another family's workspaces");

        (await okoro.Client.GetFromJsonAsync<WorkspaceListResponse>("/workspaces"))!.Workspaces.Should().BeEmpty();
        (await okoro.Client.GetAsync($"/workspaces/{workspace.Id}/manifest")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await okoro.Client.GetAsync($"/workspaces/{workspace.Id}/revisions")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await okoro.Client.PostAsJsonAsync($"/workspaces/{workspace.Id}/negotiate", new NegotiateRequest([Sha256Hex(Utf8("x"))])))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await UploadAsync(okoro.Client, workspace.Id, Utf8("intruder"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Negotiate_Upload_And_Negotiate_Should_CloseTheLoop()
    {
        var tenantId = Guid.NewGuid();
        var person = await SignInAsync(_factory, tenantId, "Maya");
        var workspace = (await (await person.Client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest("Pip's Harbour")))
            .Content.ReadFromJsonAsync<WorkspaceResponse>())!;
        var bytes = Utf8("{\"worldId\":\"01J\",\"slug\":\"pip-s-harbour\"}");
        var hash = Sha256Hex(bytes);

        var before = await person.Client.PostAsJsonAsync($"/workspaces/{workspace.Id}/negotiate", new NegotiateRequest([hash]));
        (await before.Content.ReadFromJsonAsync<NegotiateResponse>())!.Missing.Should().Equal(hash);

        var upload = await UploadAsync(person.Client, workspace.Id, bytes);
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await upload.Content.ReadFromJsonAsync<BlobUploadResponse>();
        stored!.ContentHash.Should().Be(hash);
        stored.SizeBytes.Should().Be(bytes.Length);
        stored.AlreadyPresent.Should().BeFalse();

        var after = await person.Client.PostAsJsonAsync($"/workspaces/{workspace.Id}/negotiate", new NegotiateRequest([hash]));
        (await after.Content.ReadFromJsonAsync<NegotiateResponse>())!.Missing.Should().BeEmpty();

        // Uploading the same bytes again costs nothing and is not an error.
        var again = await UploadAsync(person.Client, workspace.Id, bytes);
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        (await again.Content.ReadFromJsonAsync<BlobUploadResponse>())!.AlreadyPresent.Should().BeTrue();

        // Uploaded is not committed: no revision names it, so it cannot be read back yet.
        var download = await person.Client.GetAsync($"/workspaces/{workspace.Id}/blobs/{hash}");
        download.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await download.Content.ReadFromJsonAsync<WorkspaceProblem>())!.Code.Should().Be("content-not-found");
    }

    [Fact]
    public async Task Upload_Should_DiscardBytesThatDoNotMatchTheirDeclaration()
    {
        var tenantId = Guid.NewGuid();
        var person = await SignInAsync(_factory, tenantId, "Maya");
        var workspace = (await (await person.Client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest("Pip's Harbour")))
            .Content.ReadFromJsonAsync<WorkspaceResponse>())!;
        var bytes = Utf8("actual bytes");
        var declared = Sha256Hex(Utf8("declared bytes"));

        var mismatch = await UploadAsync(person.Client, workspace.Id, bytes, declaredHash: declared);

        mismatch.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await mismatch.Content.ReadFromJsonAsync<WorkspaceProblem>())!.Code.Should().Be("content-hash-mismatch");

        // Neither the declared nor the actual hash became possessed: the staged object was discarded.
        var negotiate = await person.Client.PostAsJsonAsync($"/workspaces/{workspace.Id}/negotiate",
            new NegotiateRequest([declared, Sha256Hex(bytes)]));
        (await negotiate.Content.ReadFromJsonAsync<NegotiateResponse>())!.Missing.Should().BeEquivalentTo([declared, Sha256Hex(bytes)]);

        // "We will find out how big it is" is not compatible with claiming quota first (Spec 089 §12).
        using var chunkedRequest = new HttpRequestMessage(HttpMethod.Put, $"/workspaces/{workspace.Id}/blobs/{Sha256Hex(bytes)}")
        {
            Content = new StreamContent(new MemoryStream(bytes)),
        };
        chunkedRequest.Content.Headers.ContentType = new("application/octet-stream");
        chunkedRequest.Headers.TransferEncodingChunked = true;
        var chunked = await person.Client.SendAsync(chunkedRequest);
        chunked.StatusCode.Should().Be(HttpStatusCode.LengthRequired);
        (await chunked.Content.ReadFromJsonAsync<WorkspaceProblem>())!.Code.Should().Be("length-required");

        var octets = new ByteArrayContent(bytes);
        octets.Headers.ContentType = new("application/octet-stream");
        var badHash = await person.Client.PutAsync($"/workspaces/{workspace.Id}/blobs/not-a-hash", octets);
        badHash.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // The body is raw bytes; anything else is refused before it is read.
        var json = new ByteArrayContent(bytes);
        json.Headers.ContentType = new("application/json");
        (await person.Client.PutAsync($"/workspaces/{workspace.Id}/blobs/{Sha256Hex(bytes)}", json))
            .StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }
}
