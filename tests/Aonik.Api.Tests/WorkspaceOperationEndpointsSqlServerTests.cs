using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

using Aonik.IntegrationTests.Support;
using Aonik.Workspaces.Endpoints;

using FluentAssertions;

using static Aonik.Api.Tests.WorkspaceTestSeeding;

namespace Aonik.Api.Tests;

/// <summary>
/// The engine's operation store held in Aonik, over HTTP (aonik#327): one row per key however many
/// hosts begin it, completed once, replayed on a lost response, refused for a different request,
/// and invisible by key to anyone the workspace is not available to.
///
/// <para>SQL Server lane: insert-if-absent is the unique index, which the InMemory provider does not enforce.</para>
/// </summary>
public sealed class WorkspaceOperationEndpointsSqlServerTests : IClassFixture<SqlLocalDbFixture>, IDisposable
{
    private readonly SqlLocalDbFixture _db;
    private readonly CustomWebApplicationFactory? _factory;

    public WorkspaceOperationEndpointsSqlServerTests(SqlLocalDbFixture db)
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

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static BeginOperationModel Begin(Guid workspaceId, string operationId, string fingerprintOf = "input-1", string? result = null) => new(
        workspaceId,
        Hash($"key:{workspaceId}:{operationId}"),
        Hash($"fingerprint:{fingerprintOf}"),
        "propose",
        """{"actorId":"maya","scopeId":"harpers","executorId":"host-1","subjectId":"ivy"}""",
        $$"""{"worldId":"{{workspaceId}}"}""",
        result);

    [SkippableFact]
    public async Task AnOperation_Should_BeginOnce_CompleteOnce_AndReplayALostResponse()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SignInAsync(Factory, tenantId, "Maya");
        var workspace = await CreateAsync(maya, "Pip's Harbour");

        // Ten hosts begin the same operation at once: one row, nine finders.
        var begun = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            var response = await maya.Client.PostAsJsonAsync("/operations", Begin(workspace.Id, "propose-pip"));
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<BeginOperationResponse>())!;
        }));
        begun.Count(b => b.Inserted).Should().Be(1, "insert-if-absent admits exactly one");
        begun.Select(b => b.Operation.Key).Distinct().Should().ContainSingle();
        begun.Should().AllSatisfy(b => b.Operation.Status.Should().Be("started"));
        var key = begun[0].Operation.Key;

        // Read back: a started row is the durable "uncertain".
        (await maya.Client.GetFromJsonAsync<OperationResponse>($"/operations/{key}"))!.Should().BeEquivalentTo(new { Status = "started", Action = "propose", Result = (string?)null });

        // Complete once; complete again with the same result after a lost response, and it is the same row.
        var result = """{"proposalId":"pr_1","summary":"Pip"}""";
        var completed = await maya.Client.PostAsJsonAsync($"/operations/{key}/complete", new CompleteOperationModel(Hash("fingerprint:input-1"), result));
        completed.StatusCode.Should().Be(HttpStatusCode.OK, await completed.Content.ReadAsStringAsync());
        var first = (await completed.Content.ReadFromJsonAsync<OperationResponse>())!;
        first.Should().BeEquivalentTo(new { Status = "completed", Result = result });
        var again = await maya.Client.PostAsJsonAsync($"/operations/{key}/complete", new CompleteOperationModel(Hash("fingerprint:input-1"), result));
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        (await again.Content.ReadFromJsonAsync<OperationResponse>())!.CompletedAt.Should().Be(first.CompletedAt, "completed once, whenever asked again");

        // Beginning it again finds the completed row, with its result: what a retried request replays.
        var replay = (await (await maya.Client.PostAsJsonAsync("/operations", Begin(workspace.Id, "propose-pip"))).Content.ReadFromJsonAsync<BeginOperationResponse>())!;
        replay.Inserted.Should().BeFalse();
        replay.Operation.Should().BeEquivalentTo(new { Status = "completed", Result = result });
    }

    [SkippableFact]
    public async Task AKeyReusedForADifferentRequest_Should_BeRefused_AndADifferentResult_Should_NotMoveTheRecord()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SignInAsync(Factory, tenantId, "Maya");
        var workspace = await CreateAsync(maya, "Pip's Harbour");
        var key = (await (await maya.Client.PostAsJsonAsync("/operations", Begin(workspace.Id, "op"))).Content.ReadFromJsonAsync<BeginOperationResponse>())!.Operation.Key;

        var reused = await maya.Client.PostAsJsonAsync("/operations", Begin(workspace.Id, "op", fingerprintOf: "input-2"));
        reused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await reused.Content.ReadFromJsonAsync<WorkspaceProblem>())!.Code.Should().Be("operation-conflict");

        (await maya.Client.PostAsJsonAsync($"/operations/{key}/complete", new CompleteOperationModel(Hash("fingerprint:input-2"), "{}"))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await maya.Client.PostAsJsonAsync($"/operations/{key}/complete", new CompleteOperationModel(Hash("fingerprint:input-1"), """{"a":1}"""))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await maya.Client.PostAsJsonAsync($"/operations/{key}/complete", new CompleteOperationModel(Hash("fingerprint:input-1"), """{"a":2}"""))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await maya.Client.GetFromJsonAsync<OperationResponse>($"/operations/{key}"))!.Result.Should().Be("""{"a":1}""");

        // A started row carries a result from the start when the engine begins a settlement with its decision.
        var settlement = (await (await maya.Client.PostAsJsonAsync("/operations", Begin(workspace.Id, "settle", result: """{"jobs":[]}"""))).Content.ReadFromJsonAsync<BeginOperationResponse>())!;
        settlement.Operation.Should().BeEquivalentTo(new { Status = "started", Result = """{"jobs":[]}""" });
    }

    [SkippableFact]
    public async Task AnotherFamily_Should_SeeNoOperation_AndBeginNone()
    {
        var tenantId = Guid.NewGuid();
        var maya = await SignInAsync(Factory, tenantId, "Maya");
        var okoro = await SignInAsync(Factory, tenantId, "Okoro");
        var workspace = await CreateAsync(maya, "Pip's Harbour");
        var key = (await (await maya.Client.PostAsJsonAsync("/operations", Begin(workspace.Id, "op"))).Content.ReadFromJsonAsync<BeginOperationResponse>())!.Operation.Key;

        (await okoro.Client.GetAsync($"/operations/{key}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await okoro.Client.GetAsync($"/operations/{Hash("nothing")}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "absent and foreign look the same");
        (await okoro.Client.PostAsJsonAsync($"/operations/{key}/complete", new CompleteOperationModel(Hash("fingerprint:input-1"), "{}"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await okoro.Client.PostAsJsonAsync("/operations", Begin(workspace.Id, "theirs"))).StatusCode.Should().Be(HttpStatusCode.NotFound, "a workspace that is not yours is not there");
        (await maya.Client.GetFromJsonAsync<OperationResponse>($"/operations/{key}"))!.Status.Should().Be("started", "nothing Okoro did touched it");
    }

    private static async Task<WorkspaceResponse> CreateAsync(Person person, string name)
    {
        var response = await person.Client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest(name));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<WorkspaceResponse>())!;
    }
}
