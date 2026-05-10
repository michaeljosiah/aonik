using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions;
using Aonik.Voice.Pipeline;
using Aonik.Voice.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Voxa.Pipelines;

namespace Aonik.Voice.Endpoints;

/// <summary>
/// <c>WSS /ai/voice</c> handler. Owns WebSocket lifecycle, hello validation,
/// and per-connection setup; delegates the actual voice pipeline to
/// <see cref="IAonikVoicePipelineFactory"/>.
///
/// <para>
/// See <c>docs/specifications/022.aonik-voice-realtime.md</c> "Endpoint Lifecycle".
/// </para>
/// </summary>
internal static class VoiceWebSocketEndpoint
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static async Task HandleAsync(HttpContext context)
    {
        var cancellationToken = context.RequestAborted;
        var services = context.RequestServices;
        var logger = services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Aonik.Voice.VoiceWebSocketEndpoint");

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new { error = "WebSocket upgrade required." },
                cancellationToken);
            return;
        }

        // Authentication has already validated the JWT and OnTokenValidated
        // populated ITenantContext + ICurrentUserContext on the request scope.
        // The MobileVoicePolicy authorization check fired via the route's
        // RequireAuthorization metadata before we reached this handler.

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        logger.LogInformation("Voice WebSocket accepted");

        try
        {
            HelloEnvelope hello;
            try
            {
                hello = await HelloEnvelopeReader.ReadAsync(socket, cancellationToken);
            }
            catch (HelloParseException ex)
            {
                logger.LogWarning(ex, "Voice WebSocket: rejected hello envelope");
                await SendErrorAsync(socket, "hello-invalid", ex.Message, cancellationToken);
                await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Invalid hello envelope", cancellationToken);
                return;
            }

            // Validate frontend tools against the server-owned allowlist.
            var catalog = services.GetRequiredService<IVoiceFrontendToolCatalog>();
            var rejected = catalog.Validate(hello.FrontendTools ?? new List<string>());
            if (rejected.Count > 0)
            {
                var message = $"Unknown frontend tool name(s): {string.Join(", ", rejected)}";
                logger.LogWarning("Voice WebSocket: {Message}", message);
                await SendErrorAsync(socket, "frontend-tool-unknown", message, cancellationToken);
                await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Unknown frontend tool(s)", cancellationToken);
                return;
            }

            // Resolve tenant voice provider configuration.
            var voiceSettings = services.GetRequiredService<ITenantVoiceProviderSettingsService>();
            var config = await voiceSettings.GetCurrentAsync(cancellationToken);
            if (!config.Enabled)
            {
                await SendErrorAsync(socket, "voice-not-configured", "Voice mode is not configured for this tenant.", cancellationToken);
                await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Voice not configured", cancellationToken);
                return;
            }

            // Resolve agent + descriptor + user-brief preamble.
            var contextualizer = services.GetRequiredService<IAgentContextualizer>();
            var domainResolver = services.GetRequiredService<IDomainAgentResolver>();
            var agentContext = await contextualizer.ResolveAsync(hello.AgentId, cancellationToken);
            var domainResolution = await domainResolver.ResolveAsync(hello.AgentId!, cancellationToken);

            // Phase 1.5: build the read-only voice variant of the resolved agent.
            // Original agent (used by AGUI) is unchanged.
            var voiceBuilder = services.GetRequiredService<IVoiceAgentBuilder>();
            var voiceAgentResult = voiceBuilder.BuildReadOnlyVariant(domainResolution.Descriptor, services);

            // Build run options. Same call shape as AguiStreamPipeline:74.
            var runOptionsBuilder = services.GetRequiredService<IAguiRunOptionsBuilder>();
            // v1: clientTools come from the server-owned canonical catalog, NOT from
            // the hello envelope. The hello list is used purely as a capability
            // negotiation (the server only declares tools the client claims it can
            // render). Once the catalog ships canonical AITool declarations
            // (Phase 3 follow-up), this will populate.
            var clientTools = catalog.ResolveCanonical(hello.FrontendTools ?? new List<string>());
            var runOptions = runOptionsBuilder.Build(clientTools, agentContext.ConfiguredModelName);

            // Stamp use_case = "voice" on ChatOptions.AdditionalProperties so
            // TelemetryChatClient tags AI run logs correctly. Mirrors the AGUI
            // path (AguiStreamingEndpoint.cs:111 sets the activity tag; here we
            // also set the property because run options carry it through to
            // TelemetryChatClient).
            runOptions ??= new Microsoft.Agents.AI.ChatClientAgentRunOptions { ChatOptions = new ChatOptions() };
            runOptions.ChatOptions ??= new ChatOptions();
            runOptions.ChatOptions.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            runOptions.ChatOptions.AdditionalProperties[AiTelemetry.UseCaseAttribute] = "voice";

            // Resolve tenant + user IDs from the request scope (set by
            // OnTokenValidated → tenantContext / currentUserContext).
            Guid? tenantId = TryGetTenantId(services);
            Guid? userId = TryGetUserId(services);

            // Compose the pipeline.
            var factory = services.GetRequiredService<IAonikVoicePipelineFactory>();
            Voxa.Pipelines.Pipeline pipeline;
            try
            {
                pipeline = factory.BuildChained(new VoicePipelineBuildRequest(
                    WebSocket: socket,
                    VoiceAgent: voiceAgentResult.Agent,
                    UserBriefPreamble: agentContext.UserBriefPreamble,
                    RunOptions: runOptions,
                    FrontendToolNames: catalog.AllowedNames,
                    InitialChatThreadId: hello.ChatThreadId,
                    AgentId: hello.AgentId,
                    TenantId: tenantId,
                    UserId: userId,
                    TenantConfig: config,
                    RequestServices: services));
            }
            catch (VoiceConfigurationException ex)
            {
                logger.LogWarning(ex, "Voice WebSocket: pipeline configuration error");
                await SendErrorAsync(socket, "voice-config-invalid", ex.Message, cancellationToken);
                await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Voice configuration error", cancellationToken);
                return;
            }

            await using var runner = new PipelineRunner(pipeline, cancellationToken);
            await runner.StartAsync(ct: cancellationToken);

            try
            {
                // PipelineRunner.WaitAsync completes when the sink observes EndFrame
                // (graceful shutdown) or an upstream ErrorFrame surfaces. Cancellation
                // happens via the runner's external token (set in the constructor),
                // so we just await — Task.WaitAsync wraps the cancellation timeout
                // for safety.
                await runner.WaitAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Client disconnected — normal.
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected — normal.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Voice WebSocket: unhandled exception");
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await SendErrorAsync(socket, "server-error", "Server error", default);
                    await CloseAsync(socket, WebSocketCloseStatus.InternalServerError, "Server error", default);
                }
                catch
                {
                    // Best-effort.
                }
            }
        }
    }

    private static Guid? TryGetTenantId(IServiceProvider services)
    {
        var tenantContext = services.GetService<ITenantContext>();
        if (tenantContext?.TenantId is { } id && id != Guid.Empty) return id;
        var tenantProvider = services.GetService<ITenantProvider>();
        if (tenantProvider is not null && tenantProvider.TryGetCurrentTenantId(out var providerId))
            return providerId;
        return null;
    }

    private static Guid? TryGetUserId(IServiceProvider services)
    {
        var userContext = services.GetService<ICurrentUserContext>();
        if (userContext?.UserId is { } id && id != Guid.Empty) return id;
        var currentUserProvider = services.GetService<ICurrentUserProvider>();
        if (currentUserProvider is not null && currentUserProvider.TryGetCurrentUserId(out var providerId))
            return providerId;
        return null;
    }

    private static Task SendErrorAsync(System.Net.WebSockets.WebSocket socket, string code, string message, CancellationToken ct)
        => SendEnvelopeAsync(socket, new { type = "error", code, message }, ct);

    private static async Task SendEnvelopeAsync(System.Net.WebSockets.WebSocket socket, object envelope, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open) return;

        var json = JsonSerializer.Serialize(envelope, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }

    private static async Task CloseAsync(System.Net.WebSockets.WebSocket socket, WebSocketCloseStatus status, string description, CancellationToken ct)
    {
        if (socket.State == WebSocketState.Open)
        {
            await socket.CloseAsync(status, description, ct).ConfigureAwait(false);
        }
    }
}
