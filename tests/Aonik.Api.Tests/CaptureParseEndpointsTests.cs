using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;

using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Aonik.Api.Tests;

/// <summary>
/// HTTP E2E for <c>POST /ai/capture/parse</c> (Spec 047) over the real pipeline
/// (auth, validation, DI, DB). The default stub <c>IChatClient</c> returns prose,
/// which exercises the <c>unparseable</c> fallback plus the two structural
/// acceptance criteria — nothing persisted, and an audited <c>AiRun</c>. A
/// per-test canned client covers the parsed-draft path.
/// </summary>
public class CaptureParseEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CaptureParseEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ParseCapture_Should_ReturnUnparseable_AndPersistNothing_AndAudit_WithStubProvider()
    {
        // Arrange — the stub provider returns non-JSON prose.
        var options = TestAuthOptions.Create().WithRoles("PersonalUser");
        var client = await _factory.CreateAuthenticatedClientAsync(options);
        var request = new CaptureParseRequest(
            CaptureInputTypes.Text,
            "Sent £200 to Mum via Wise yesterday, ref P2046-XK",
            new CaptureHints([new CaptureEntityHint("ce_1", "Mum")], null));

        // Act
        var response = await client.PostAsJsonAsync("/ai/capture/parse", request);

        // Assert — 200 with an unparseable proposal (capture never dead-ends).
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CaptureParseResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(CaptureParseStatuses.Unparseable);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();

        // Nothing persisted — a parse writes no PaymentLog (AcrossTenants: the assertion
        // scope has no tenant, so look past the tenant filter to assert a genuine zero).
        (await db.Set<PaymentLog>().AcrossTenants().CountAsync()).Should().Be(0);

        // Audited — AiRunWriter persists to AiDbContext (a separate InMemory store from
        // AonikDbContext — EF InMemory does not share roots across contexts).
        var aiDb = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        var runs = await aiDb.AiRuns.AcrossTenants()
            .Where(r => r.UseCase == "capture_parse").ToListAsync();
        runs.Should().NotBeEmpty();
        // The input SHAPE is recorded; the payload (e.g. "Mum") is never stored.
        runs.Should().Contain(r => r.InputRefsJson.Contains("inputType"));
        runs.Should().NotContain(r => r.InputRefsJson.Contains("Mum"));
    }

    [Fact]
    public async Task ParseCapture_Should_ReturnStructuredDraft_AndPersistNothing_WhenModelReturnsJson()
    {
        // Arrange — swap in a canned model that returns a valid capture draft.
        const string draftJson =
            """
            {"status":"parsed","draft":{"kind":"paymentLog","entityMatch":{"id":"ce_1","confidence":0.93},
            "amount":{"value":200.00,"currency":"GBP"},"date":"2026-06-13","channel":"wise",
            "note":"Wise transfer ref P2046-XK","fieldConfidence":{"amount":0.98,"date":0.95,"entity":0.93}}}
            """;

        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatClient>();
                services.AddScoped<IChatClient>(_ => new CannedChatClient(draftJson));
            }));

        var client = await CreateSeededRoleClientAsync(factory, "PersonalUser");
        var request = new CaptureParseRequest(
            CaptureInputTypes.Text,
            "Sent £200 to Mum via Wise, ref P2046-XK",
            new CaptureHints([new CaptureEntityHint("ce_1", "Mum")], null));

        // Act
        var response = await client.PostAsJsonAsync("/ai/capture/parse", request);

        // Assert
        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "response body was: {0}", raw);
        var body = await response.Content.ReadFromJsonAsync<CaptureParseResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be(CaptureParseStatuses.Parsed);
        body.Draft.Should().NotBeNull();
        body.Draft!.Amount!.Value.Should().Be(200.00m);
        body.Draft.Amount.Currency.Should().Be("GBP");
        body.Draft.EntityMatch!.Id.Should().Be("ce_1");
        body.AiRunId.Should().NotBeNull(); // the audited run id rides back on the proposal

        // Still nothing persisted — the draft is a proposal.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        (await db.Set<PaymentLog>().CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("bogusType", "some text")]
    [InlineData("text", "")]
    public async Task ParseCapture_Should_Return422_When_RequestIsInvalid(string inputType, string payload)
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser"));
        var request = new CaptureParseRequest(inputType, payload, null);

        // Act
        var response = await client.PostAsJsonAsync("/ai/capture/parse", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ParseCapture_Should_Return401_When_Unauthenticated()
    {
        var client = _factory.CreateClient();
        var request = new CaptureParseRequest(CaptureInputTypes.Text, "anything", null);

        var response = await client.PostAsJsonAsync("/ai/capture/parse", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // The derived (canned-client) factory is a separate host with its own InMemory
    // store, so seed a tenant there before the request (the tenant-resolution
    // middleware 404s an unknown tenant). Role headers alone satisfy AdminUserPolicy.
    private static async Task<HttpClient> CreateSeededRoleClientAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string role)
    {
        var tenantId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Capture Test Tenant",
                Environment = Environments.Development,
                DefaultCurrency = "GBP",
                SupportedCountriesJson = "[]",
                Status = TenantStatus.Active,
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantIdHeader, tenantId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }

    private sealed class CannedChatClient : IChatClient
    {
        private readonly string _responseText;

        public CannedChatClient(string responseText) => _responseText = responseText;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, _responseText)]));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
