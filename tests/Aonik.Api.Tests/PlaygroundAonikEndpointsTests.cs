using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Ai.Entities;
using Aonik.Ai.Middleware;
using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Api.Tests;

public sealed class PlaygroundAonikEndpointsTests
{
    private const string Agent = IMasterOrchestratorService.AgentName;
    private const string Model = "fixture-orchestrator-model";
    private const string Slug = "playground-invoice-rehearsal";
    private const string DraftName = "Playground invoice routing";
    private const string Tool = "aonik_create_workflow";

    [Fact]
    public async Task Playground_Should_UseAoniksRoutedToolsAndRequireTheAuthenticatedOperatorToDecide()
    {
        using var factory = new PlaygroundFactory();
        Guid tenantId = Guid.NewGuid();
        TestAuthOptions user = TestAuthOptions.Create().WithTenant(tenantId).WithRoles("Admin", "PlatformAdmin")
            .WithPermissions("Workflow.Read", "Workflow.Write");
        using HttpClient client = await factory.CreateAuthenticatedClientAsync(user);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString("D"));
        var messages = new[] { new { role = "user", content = "Prepare an invoice routing draft." } };
        string workflowPath = $"/ai/workflows/{Slug}";

        using HttpResponseMessage refused = await client.PostAsJsonAsync("/ai/playground/run",
            new { agentName = Agent, messages, impersonateUserId = Guid.NewGuid() });
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Run Aonik under your own account");

        JsonElement[] events = await RunAsync();
        JsonElement approval = events.Where(IsApprovalEvent).Should().ContainSingle().Which.GetProperty("value");
        approval.GetProperty("tool").GetString().Should().Be(Tool);
        approval.GetProperty("preview").GetRawText().Should().Contain(DraftName);
        Guid requestId = approval.GetProperty("approvalRequestId").GetGuid();
        using HttpResponseMessage before = await client.GetAsync(workflowPath);
        before.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using HttpResponseMessage decision = await client.PostAsJsonAsync($"/ai/tool-approvals/{requestId:D}/decide",
            new { decision = "Approve" });
        decision.StatusCode.Should().Be(HttpStatusCode.OK);
        (await RunAsync()).Where(IsApprovalEvent).Should().BeEmpty();
        (await client.GetFromJsonAsync<WorkflowGraphResponse>(workflowPath))!.Name.Should().Be(DraftName);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var database = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        ToolApprovalRequest stored = await database.Set<ToolApprovalRequest>().SingleAsync(item => item.Id == requestId);
        stored.RequestingUserId.Should().Be(user.UserId);
        stored.DecidedByUserId.Should().Be(user.UserId);
        stored.ConsumedAt.Should().NotBeNull();
        AiRun audit = await database.Set<AiRun>().SingleAsync(item => item.Id == stored.AiRunId);
        audit.UseCase.Should().Be("playground");
        audit.UserId.Should().Be(user.UserId);

        async Task<JsonElement[]> RunAsync()
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync("/ai/playground/run", new { agentName = Agent, messages });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            string body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("\"type\":\"RUN_FINISHED\"").And.NotContain("\"type\":\"RUN_ERROR\"");
            return body.Split('\n', StringSplitOptions.TrimEntries)
                .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
                .Select(line => JsonSerializer.Deserialize<JsonElement>(line[5..])).ToArray();
        }
    }

    private static bool IsApprovalEvent(JsonElement item)
        => item.TryGetProperty("name", out JsonElement name) && name.GetString() == "tool.approval.required";

    private sealed class PlaygroundFactory : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAiModelResolver>();
                services.AddSingleton<IAiModelResolver>(new FixtureModelResolver());
                services.RemoveAll<IChatClient>();
                services.AddScoped<IChatClient>(provider => new AuditMiddleware(
                    new WorkflowChatClient(), provider.GetRequiredService<IAiRunWriter>(),
                    provider.GetRequiredService<ILogger<AuditMiddleware>>()));
            });
        }
    }

    private sealed class FixtureModelResolver : IAiModelResolver
    {
        public Task<string?> ResolveModelNameAsync(string useCase, CancellationToken cancellationToken = default)
            => Task.FromResult(useCase == "orchestrator" ? Model : null);

        public Task<string?> ResolveModelNameByIdAsync(Guid modelId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class WorkflowChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            options!.ModelId.Should().Be(Model);
            options.Tools.Should().Contain(tool => tool.Name == Tool);
            if (messages.SelectMany(message => message.Contents).Any(content => content is FunctionResultContent))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, "Review the workflow result.");
                yield break;
            }

            var draft = new WorkflowSaveRequest(Slug, DraftName, "Synthetic workflow", "Draft", "v0.1", false, "", null, [],
                [new("start", "Trigger", "Start", "", "", 0, 0, "{}"), new("end", "End", "Done", "", "", 200, 0, "{}")],
                [new("start", "end", 0, "")], null, SchemaVersion: 2);
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new FunctionCallContent(Guid.NewGuid().ToString("N"), Tool,
                    new Dictionary<string, object?> { ["draft"] = draft })],
            };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
