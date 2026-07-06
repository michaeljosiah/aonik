using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;

using Aonik.Agents.Endpoints;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Ai;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Aonik.Api.Tests;

/// <summary>
/// HTTP E2E for <c>POST /ai/agents/improve-prompt</c> (#216). No AiRoutePolicy/AiTask
/// is seeded for "prompt-improvement" in the test DB, so this exercises the
/// fallback path: <see cref="IAiTaskProfileResolver"/> resolves to the endpoint's
/// literal default model and inline system prompt, while still stamping the
/// "prompt-improvement" use-case for telemetry — mirroring the convention
/// <c>ChatThreadTitleGenerator</c> established.
/// </summary>
public class ImprovePromptEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ImprovePromptEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ImprovePrompt_Should_ResolveDefaultModel_AndStampUseCase_WhenNoRoutePolicyConfigured()
    {
        // Arrange — swap in a client that captures the ChatOptions it receives.
        var capturingClient = new OptionsCapturingChatClient("An improved prompt.");

        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatClient>();
                services.AddScoped<IChatClient>(_ => capturingClient);
            }));

        var client = await CreateSeededRoleClientAsync(factory, "Operations");
        var request = new ImprovePromptRequest(
            CurrentPrompt: "You are a helpful assistant.",
            UserIntent: "Make it more concise.");

        // Act
        var response = await client.PostAsJsonAsync("/ai/agents/improve-prompt", request);

        // Assert
        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "response body was: {0}", raw);
        var body = await response.Content.ReadFromJsonAsync<ImprovePromptResponse>();
        body.Should().NotBeNull();
        body!.ImprovedPrompt.Should().Be("An improved prompt.");

        capturingClient.LastOptions.Should().NotBeNull();
        capturingClient.LastOptions!.ModelId.Should().Be(
            "gpt-5-mini", "no route policy is configured, so it must fall back to the endpoint's literal default");

        capturingClient.LastOptions.AdditionalProperties.Should().NotBeNull();
        capturingClient.LastOptions.AdditionalProperties!
            .TryGetValue(AiTelemetry.UseCaseAttribute, out var stamped)
            .Should().BeTrue();
        stamped.Should().Be(
            "prompt-improvement",
            because: "the call must be tagged with a semantic use_case so the trace listing doesn't leak the model id");
    }

    [Fact]
    public async Task ImprovePrompt_Should_Return401_When_Unauthenticated()
    {
        var client = _factory.CreateClient();
        var request = new ImprovePromptRequest("Existing prompt", "Improve it");

        var response = await client.PostAsJsonAsync("/ai/agents/improve-prompt", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // The derived (capturing-client) factory is a separate host with its own InMemory
    // store, so seed a tenant there before the request (the tenant-resolution
    // middleware 404s an unknown tenant). Role headers alone satisfy AdminWritePolicy.
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
                Name = "Improve Prompt Test Tenant",
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

    private sealed class OptionsCapturingChatClient : IChatClient
    {
        private readonly string _responseText;

        public OptionsCapturingChatClient(string responseText) => _responseText = responseText;

        public ChatOptions? LastOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, _responseText)]));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
