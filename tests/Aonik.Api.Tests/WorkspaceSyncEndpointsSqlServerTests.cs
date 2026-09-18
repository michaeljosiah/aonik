using System.Net;
using System.Net.Http.Json;

using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.Workspaces.Endpoints;

using FluentAssertions;

using static Aonik.Api.Tests.WorkspaceTestSeeding;

namespace Aonik.Api.Tests;

/// <summary>
/// The commit half of the workspace contract (aonik#326), over HTTP against SQL Server LocalDB.
///
/// <para>
/// This lane, not InMemory, because the head advance is an <c>ExecuteUpdateAsync</c> compare-and-swap the
/// InMemory provider cannot issue, and the idempotency the Node adapter depends on is a unique index it does
/// not enforce. Skips where LocalDB is absent, exactly like <c>tests/Aonik.Database.Tests</c>.
/// </para>
/// </summary>
public sealed class WorkspaceSyncEndpointsSqlServerTests : IClassFixture<SqlLocalDbFixture>, IDisposable
{
    private readonly SqlLocalDbFixture _db;
    private readonly CustomWebApplicationFactory? _factory;

    public WorkspaceSyncEndpointsSqlServerTests(SqlLocalDbFixture db)
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

    private static readonly byte[] WorldJson = Utf8("{\"worldId\":\"01J8F3K2QW9VZX4N7M0RTYB6HC\",\"slug\":\"pip-s-harbour\",\"name\":\"Pip's Harbour\"}");
    private static readonly byte[] Character = Utf8("# Pip\n\nA small harbour fox with one white ear.\n");
    private static readonly byte[] Chapter = Utf8("Pip would not go near the water.\n");

    private static ManifestEntryModel Entry(string path, byte[] bytes, string? contentType = null)
        => new(path, Sha256Hex(bytes), bytes.Length, contentType);

    [SkippableFact]
    public async Task Save_And_Reopen_Should_RoundTripTheExactTree()
    {
        var tenantId = Guid.NewGuid();
        var parent = await SignInAsync(Factory, tenantId, "Maya");
        var workspace = await CreateAsync(parent, "Pip's Harbour");

        foreach (var bytes in new[] { WorldJson, Character })
        {
            (await UploadAsync(parent.Client, workspace.Id, bytes)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var commitId = Guid.NewGuid();
        var manifest = new[] { Entry("world.json", WorldJson, "application/json"), Entry("characters/pip.md", Character, "text/markdown") };
        var committed = await CommitAsync(parent, workspace.Id, new CommitRequest(commitId, null, manifest, "Pip is born"));

        committed.Outcome.Should().Be("fastForward");
        committed.HeadRevisionId.Should().Be(committed.RevisionId);
        committed.Sequence.Should().Be(1);

        // Reopen: the summary names the head, the manifest is complete, and every byte reads back.
        var reopened = await parent.Client.GetFromJsonAsync<WorkspaceResponse>($"/workspaces/{workspace.Id}");
        reopened!.HeadRevisionId.Should().Be(committed.RevisionId);
        reopened.FileCount.Should().Be(2);
        reopened.TotalBytes.Should().Be(WorldJson.Length + Character.Length);

        var head = await parent.Client.GetFromJsonAsync<ManifestResponse>($"/workspaces/{workspace.Id}/manifest");
        head!.RevisionId.Should().Be(committed.RevisionId);
        head.Entries.Should().BeEquivalentTo(manifest);

        var byId = await parent.Client.GetFromJsonAsync<ManifestResponse>($"/workspaces/{workspace.Id}/manifest?revisionId={committed.RevisionId}");
        byId!.Entries.Should().BeEquivalentTo(manifest);

        var download = await parent.Client.GetAsync($"/workspaces/{workspace.Id}/blobs/{Sha256Hex(Character)}");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("text/markdown");
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(Character);

        var history = await parent.Client.GetFromJsonAsync<RevisionListResponse>($"/workspaces/{workspace.Id}/revisions");
        history!.Revisions.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Id = committed.RevisionId, Sequence = 1L, ParentRevisionId = (Guid?)null, AuthorPartyId = parent.PartyId, Message = "Pip is born", State = "fast-forward" });
    }

    [SkippableFact]
    public async Task ACommitId_Should_ReplayForTheSameTree_And_RefuseADifferentOne()
    {
        var tenantId = Guid.NewGuid();
        var parent = await SignInAsync(Factory, tenantId, "Maya");
        var workspace = await CreateAsync(parent, "Pip's Harbour");
        await UploadAllAsync(parent, workspace.Id, WorldJson, Character);

        var commitId = Guid.NewGuid();
        var manifest = new[] { Entry("world.json", WorldJson), Entry("characters/pip.md", Character) };
        var first = await CommitAsync(parent, workspace.Id, new CommitRequest(commitId, null, manifest));

        // The response was lost; the host asks again with the same id and the same tree.
        var retried = await CommitAsync(parent, workspace.Id, new CommitRequest(commitId, null, manifest));
        retried.Outcome.Should().Be("replayed");
        retried.RevisionId.Should().Be(first.RevisionId);
        retried.Sequence.Should().Be(first.Sequence);
        (await parent.Client.GetFromJsonAsync<RevisionListResponse>($"/workspaces/{workspace.Id}/revisions"))!.Revisions.Should().HaveCount(1);

        // The same id for a different tree is a client bug, and it is refused loudly rather than replayed.
        var different = new[] { Entry("world.json", WorldJson) };
        var reused = await parent.Client.PostAsJsonAsync($"/workspaces/{workspace.Id}/commits", new CommitRequest(commitId, null, different));
        reused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await reused.Content.ReadFromJsonAsync<WorkspaceProblem>())!.Code.Should().Be("commit-id-reused");
    }

    [SkippableFact]
    public async Task AStaleParent_Should_BeStoredAsDiverged_And_AcceptingIt_Should_AdvanceThroughANewRevision()
    {
        var tenantId = Guid.NewGuid();
        var parent = await SignInAsync(Factory, tenantId, "Maya");
        var workspace = await CreateAsync(parent, "Pip's Harbour");
        await UploadAllAsync(parent, workspace.Id, WorldJson, Character, Chapter);

        var first = await CommitAsync(parent, workspace.Id, new CommitRequest(Guid.NewGuid(), null, [Entry("world.json", WorldJson)]));
        var second = await CommitAsync(parent, workspace.Id,
            new CommitRequest(Guid.NewGuid(), first.RevisionId, [Entry("world.json", WorldJson), Entry("characters/pip.md", Character)]));
        second.Outcome.Should().Be("fastForward");

        // A writer that still believes the first revision is the head: stored, sequenced, head untouched.
        var stale = await CommitAsync(parent, workspace.Id,
            new CommitRequest(Guid.NewGuid(), first.RevisionId, [Entry("world.json", WorldJson), Entry("productions/story/chapter-1.md", Chapter)]));
        stale.Outcome.Should().Be("diverged");
        stale.HeadRevisionId.Should().Be(second.RevisionId);
        stale.Sequence.Should().Be(3);

        var summary = await parent.Client.GetFromJsonAsync<WorkspaceResponse>($"/workspaces/{workspace.Id}");
        summary!.HeadRevisionId.Should().Be(second.RevisionId);
        var history = await parent.Client.GetFromJsonAsync<RevisionListResponse>($"/workspaces/{workspace.Id}/revisions");
        history!.Revisions.Select(r => r.State).Should().Equal("diverged", "fast-forward", "fast-forward");

        // The divergent tree's bytes are readable through this workspace while the person decides.
        (await parent.Client.GetAsync($"/workspaces/{workspace.Id}/blobs/{Sha256Hex(Chapter)}")).StatusCode.Should().Be(HttpStatusCode.OK);

        // A person accepts it: the head advances through a NEW revision parented on the current head.
        var resolve = await parent.Client.PostAsJsonAsync($"/workspaces/{workspace.Id}/revisions/{stale.RevisionId}/resolve", new ResolveRevisionRequest("accept"));
        resolve.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resolve.Content.ReadFromJsonAsync<ResolveRevisionResponse>())!.Resolved.Should().BeTrue();

        var after = await parent.Client.GetFromJsonAsync<RevisionListResponse>($"/workspaces/{workspace.Id}/revisions");
        after!.Revisions.Should().HaveCount(4);
        after.Revisions[0].ParentRevisionId.Should().Be(second.RevisionId);
        after.Revisions[0].State.Should().Be("fast-forward");
        after.Revisions.Single(r => r.Id == stale.RevisionId).State.Should().Be("accepted");

        var head = await parent.Client.GetFromJsonAsync<ManifestResponse>($"/workspaces/{workspace.Id}/manifest");
        head!.RevisionId.Should().Be(after.Revisions[0].Id);
        head.Entries.Select(e => e.Path).Should().Equal("productions/story/chapter-1.md", "world.json");

        // Resolving it twice does nothing the second time.
        var again = await parent.Client.PostAsJsonAsync($"/workspaces/{workspace.Id}/revisions/{stale.RevisionId}/resolve", new ResolveRevisionRequest("accept"));
        (await again.Content.ReadFromJsonAsync<ResolveRevisionResponse>())!.Resolved.Should().BeFalse();

        var unknown = await parent.Client.PostAsJsonAsync($"/workspaces/{workspace.Id}/revisions/{stale.RevisionId}/resolve", new ResolveRevisionRequest("merge"));
        unknown.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [SkippableFact]
    public async Task ACommit_Should_RefuseContentTheCallerHasNotUploaded_AsMissing()
    {
        var tenantId = Guid.NewGuid();
        var parent = await SignInAsync(Factory, tenantId, "Maya");
        var workspace = await CreateAsync(parent, "Pip's Harbour");
        await UploadAllAsync(parent, workspace.Id, WorldJson);

        var response = await parent.Client.PostAsJsonAsync($"/workspaces/{workspace.Id}/commits",
            new CommitRequest(Guid.NewGuid(), null, [Entry("world.json", WorldJson), Entry("characters/pip.md", Character)]));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<WorkspaceProblem>();
        problem!.Code.Should().Be("missing-content");
        problem.MissingHashes.Should().Equal(Sha256Hex(Character));
        (await parent.Client.GetFromJsonAsync<RevisionListResponse>($"/workspaces/{workspace.Id}/revisions"))!.Revisions.Should().BeEmpty("no revision is created for a refused commit");

        var malformed = await parent.Client.PostAsJsonAsync($"/workspaces/{workspace.Id}/commits",
            new CommitRequest(Guid.NewGuid(), null, [Entry("../escape.json", WorldJson)]));
        malformed.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [SkippableFact]
    public async Task TwoFamilies_Should_BeIsolated_And_AReadGrant_Should_NotAllowWriting()
    {
        var tenantId = Guid.NewGuid();
        var harper = await SignInAsync(Factory, tenantId, "Harper");
        var okoro = await SignInAsync(Factory, tenantId, "Okoro");
        var workspace = await CreateAsync(harper, "Harpers' world");
        await UploadAllAsync(harper, workspace.Id, WorldJson, Character);
        var committed = await CommitAsync(harper, workspace.Id, new CommitRequest(Guid.NewGuid(), null, [Entry("world.json", WorldJson)]));

        // Okoro uploads identical bytes to their own world; that possession does not reach into Harper's.
        var theirs = await CreateAsync(okoro, "Okoros' world");
        await UploadAllAsync(okoro, theirs.Id, Character);

        (await okoro.Client.GetAsync($"/workspaces/{workspace.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await okoro.Client.GetAsync($"/workspaces/{workspace.Id}/blobs/{Sha256Hex(WorldJson)}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await okoro.Client.PostAsJsonAsync($"/workspaces/{workspace.Id}/commits",
            new CommitRequest(Guid.NewGuid(), committed.RevisionId, [Entry("world.json", WorldJson)]))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await okoro.Client.PostAsJsonAsync($"/workspaces/{workspace.Id}/revisions/{committed.RevisionId}/resolve",
            new ResolveRevisionRequest("reject"))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Harper shares read access. Okoro can now open and read, and still cannot write or spend.
        await GrantAsync(Factory, tenantId, harper, workspace.Id, okoro.PartyId, ShareAccessLevels.Read);

        (await okoro.Client.GetAsync($"/workspaces/{workspace.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await okoro.Client.GetAsync($"/workspaces/{workspace.Id}/manifest")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await okoro.Client.GetAsync($"/workspaces/{workspace.Id}/blobs/{Sha256Hex(WorldJson)}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var upload = await UploadAsync(okoro.Client, workspace.Id, Chapter);
        upload.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await upload.Content.ReadFromJsonAsync<WorkspaceProblem>())!.Code.Should().Be("insufficient-access");

        var commit = await okoro.Client.PostAsJsonAsync($"/workspaces/{workspace.Id}/commits",
            new CommitRequest(Guid.NewGuid(), committed.RevisionId, [Entry("world.json", WorldJson), Entry("characters/pip.md", Character)]));
        commit.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await commit.Content.ReadFromJsonAsync<WorkspaceProblem>())!.Code.Should().Be("insufficient-access");

        // A grantee's own list is still their own workspaces; grants are a separate question.
        (await okoro.Client.GetFromJsonAsync<WorkspaceListResponse>("/workspaces"))!.Workspaces.Should().ContainSingle(w => w.Id == theirs.Id);
    }

    private static async Task<WorkspaceResponse> CreateAsync(Person person, string name)
    {
        var response = await person.Client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest(name));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<WorkspaceResponse>())!;
    }

    private static async Task UploadAllAsync(Person person, Guid workspaceId, params byte[][] blobs)
    {
        foreach (var bytes in blobs)
        {
            var response = await UploadAsync(person.Client, workspaceId, bytes);
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }
    }

    private static async Task<CommitResponse> CommitAsync(Person person, Guid workspaceId, CommitRequest request)
    {
        var response = await person.Client.PostAsJsonAsync($"/workspaces/{workspaceId}/commits", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CommitResponse>())!;
    }
}
