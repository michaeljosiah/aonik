using System.Net;
using System.Text;
using System.Text.Json;
using Aonik.Finance.Agents.CodeAct;
using Aonik.SharedKernel.Abstractions.Agents;
using Azure.Core;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aonik.Application.Tests.Agents.CodeAct;

public class AcaSessionsCodeActSandboxProviderTests
{
    private const string TestSigningKey = "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20";
    private const string PoolEndpoint = "https://uksouth.dynamicsessions.io/subscriptions/sub/resourceGroups/rg/sessionPools/pool";
    private const string CallbackBaseUrl = "https://aonik-dev-api.example.azurecontainerapps.io";

    private static (AcaSessionsCodeActSandboxProvider provider, CapturingHandler handler) CreateProvider(
        Action<AcaSessionsOptions>? configure = null)
    {
        var options = new AcaSessionsOptions
        {
            PoolManagementEndpoint = PoolEndpoint,
            CallbackBaseUrl = CallbackBaseUrl,
            NonceTtlSeconds = 600,
            MaxCallbacksPerNonce = 30,
            DataPlaneApiVersion = "2025-10-02-preview",
        };
        configure?.Invoke(options);

        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        if (Uri.TryCreate(options.PoolManagementEndpoint, UriKind.Absolute, out var baseUri))
        {
            httpClient.BaseAddress = baseUri;
        }
        var client = new AcaSessionsClient(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<AcaSessionsClient>.Instance,
            credentialOverride: new StaticTokenCredential("fake-access-token"));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:CodeAct:NonceSigningKey"] = TestSigningKey,
            })
            .Build();
        var nonceService = new CodeActCallbackNonceService(config, NullLogger<CodeActCallbackNonceService>.Instance);

        var provider = new AcaSessionsCodeActSandboxProvider(
            client,
            Microsoft.Extensions.Options.Options.Create(options),
            nonceService,
            NullLogger<AcaSessionsCodeActSandboxProvider>.Instance,
            config);

        return (provider, handler);
    }

    private static CodeActSandboxContext CreateContext(string subAgent = "pf-insights") => new(
        SubAgentName: subAgent,
        RunId: "run-abc-123",
        TenantId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        CurrentUserId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

    [Fact]
    public void TryBuildExecuteCodeTool_Should_ReturnNull_When_PoolEndpointEmpty()
    {
        var (provider, _) = CreateProvider(opts => opts.PoolManagementEndpoint = "");
        var result = provider.TryBuildExecuteCodeTool(CreateContext(), Array.Empty<AIFunction>());
        result.Should().BeNull();
    }

    [Fact]
    public void TryBuildExecuteCodeTool_Should_ReturnNull_When_CallbackBaseUrlEmpty()
    {
        var (provider, _) = CreateProvider(opts => opts.CallbackBaseUrl = "");
        var result = provider.TryBuildExecuteCodeTool(CreateContext(), Array.Empty<AIFunction>());
        result.Should().BeNull();
    }

    [Fact]
    public void TryBuildExecuteCodeTool_Should_ReturnExecuteCodeTool_When_Configured()
    {
        var (provider, _) = CreateProvider();
        var result = provider.TryBuildExecuteCodeTool(CreateContext(), Array.Empty<AIFunction>());
        result.Should().NotBeNull();
        result!.Name.Should().Be("execute_code");
    }

    [Fact]
    public async Task ExecuteCode_Should_PostToCorrectUrl_When_Invoked()
    {
        var (provider, handler) = CreateProvider();
        handler.Response = BuildExecutionResponseJson(status: "Succeeded", stdout: "hello");
        var tool = provider.TryBuildExecuteCodeTool(CreateContext(), Array.Empty<AIFunction>())!;

        await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["code"] = "print('hello')" }), CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Contain("/executions");
        handler.LastRequest.RequestUri.Query.Should().Contain("api-version=2025-10-02-preview");
        handler.LastRequest.RequestUri.Query.Should().Contain("identifier=");
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("fake-access-token");
    }

    [Fact]
    public async Task ExecuteCode_Should_BakeUrlAndNonceIntoPreamble_When_Invoked()
    {
        var (provider, handler) = CreateProvider();
        handler.Response = BuildExecutionResponseJson(status: "Succeeded");
        var tool = provider.TryBuildExecuteCodeTool(CreateContext(), Array.Empty<AIFunction>())!;

        await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["code"] = "x = 1" }), CancellationToken.None);

        handler.LastRequestBody.Should().Contain("/ai/codeact/call-tool/");
        handler.LastRequestBody.Should().Contain("nonce_v1.");
        handler.LastRequestBody.Should().Contain("def call_tool(name, **kwargs):");
        handler.LastRequestBody.Should().Contain("urllib.request");
        handler.LastRequestBody.Should().Contain("# === BEGIN LLM CODE ===");
        handler.LastRequestBody.Should().Contain("x = 1");
    }

    [Fact]
    public async Task ExecuteCode_Should_ProduceStableSessionIdentifier_When_SameContextTwice()
    {
        var (provider, handler) = CreateProvider();
        handler.Response = BuildExecutionResponseJson(status: "Succeeded");
        var ctx = CreateContext();
        var tool = provider.TryBuildExecuteCodeTool(ctx, Array.Empty<AIFunction>())!;

        await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["code"] = "1" }), CancellationToken.None);
        var firstIdentifier = ExtractSessionIdentifier(handler.LastRequest!);

        await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["code"] = "2" }), CancellationToken.None);
        var secondIdentifier = ExtractSessionIdentifier(handler.LastRequest!);

        firstIdentifier.Should().Be(secondIdentifier, "same (RunId, SubAgentName) must hit the same warm session");
    }

    [Fact]
    public async Task ExecuteCode_Should_ProduceDifferentSessionIdentifier_When_DifferentRunIds()
    {
        var (provider, handler) = CreateProvider();
        handler.Response = BuildExecutionResponseJson(status: "Succeeded");

        var tool1 = provider.TryBuildExecuteCodeTool(CreateContext() with { RunId = "run-1" }, Array.Empty<AIFunction>())!;
        await tool1.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["code"] = "1" }), CancellationToken.None);
        var firstIdentifier = ExtractSessionIdentifier(handler.LastRequest!);

        var tool2 = provider.TryBuildExecuteCodeTool(CreateContext() with { RunId = "run-2" }, Array.Empty<AIFunction>())!;
        await tool2.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["code"] = "1" }), CancellationToken.None);
        var secondIdentifier = ExtractSessionIdentifier(handler.LastRequest!);

        firstIdentifier.Should().NotBe(secondIdentifier);
    }

    [Fact]
    public async Task ExecuteCode_Should_RejectCodeRedefiningCallTool_When_AssignmentDetected()
    {
        var (provider, handler) = CreateProvider();
        var tool = provider.TryBuildExecuteCodeTool(CreateContext(), Array.Empty<AIFunction>())!;

        var result = await tool.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?> { ["code"] = "call_tool = lambda x: 'evil'" }),
            CancellationToken.None);

        handler.LastRequest.Should().BeNull("we must reject before HTTP");
        var resultStr = result?.ToString() ?? "";
        resultStr.Should().Contain("call_tool_shadowed");
    }

    [Fact]
    public async Task ExecuteCode_Should_RejectCodeRedefiningCallTool_When_DefDetected()
    {
        var (provider, handler) = CreateProvider();
        var tool = provider.TryBuildExecuteCodeTool(CreateContext(), Array.Empty<AIFunction>())!;

        var result = await tool.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?> { ["code"] = "def call_tool(x): return x" }),
            CancellationToken.None);

        handler.LastRequest.Should().BeNull();
        (result?.ToString() ?? "").Should().Contain("call_tool_shadowed");
    }

    [Fact]
    public async Task ExecuteCode_Should_ReturnErrorEnvelope_When_AcaReturns500()
    {
        var (provider, handler) = CreateProvider();
        handler.StatusCode = HttpStatusCode.InternalServerError;
        handler.Response = "boom";
        var tool = provider.TryBuildExecuteCodeTool(CreateContext(), Array.Empty<AIFunction>())!;

        var result = await tool.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?> { ["code"] = "1" }),
            CancellationToken.None);

        (result?.ToString() ?? "").Should().Contain("aca_sessions_http_error");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string BuildExecutionResponseJson(string status, string? stdout = null) => JsonSerializer.Serialize(new
    {
        properties = new
        {
            status,
            stdout,
            stderr = (string?)null,
            executionTimeInMilliseconds = 12L,
            result = (object?)null,
        },
    });

    private static string ExtractSessionIdentifier(HttpRequestMessage req)
    {
        var query = req.RequestUri!.Query;
        var idx = query.IndexOf("identifier=", StringComparison.Ordinal);
        return query[(idx + "identifier=".Length)..];
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string LastRequestBody { get; private set; } = string.Empty;
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public string Response { get; set; } = "{}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(Response, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class StaticTokenCredential : TokenCredential
    {
        private readonly string _token;
        public StaticTokenCredential(string token) => _token = token;
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new(_token, DateTimeOffset.UtcNow.AddHours(1));
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new(GetToken(requestContext, cancellationToken));
    }
}
