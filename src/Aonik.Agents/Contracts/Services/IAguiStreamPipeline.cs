using System.Diagnostics;
using Aonik.Agents.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Translates the agent's framework-level streaming output into AG-UI
/// SSE events. The endpoint hands the pipeline a started agent run,
/// the SSE writer, and the active voice coordinator (if any); the pipeline
/// owns the entire <c>RUN_STARTED</c>-to-<c>speech.render</c> body of the
/// stream and returns aggregate metrics for the endpoint to flush as
/// <c>RUN_FINISHED</c>.
/// </summary>
/// <remarks>
/// The endpoint deliberately does not implement protocol translation
/// itself — it stays a thin orchestrator. All TEXT_MESSAGE / TOOL_CALL /
/// REASONING / speech.chunk / speech.render handling lives here.
/// Marked <c>internal</c> because the signature uses internal types
/// (<see cref="AguiResponseWriter"/>, <see cref="VoiceSynthCoordinator"/>);
/// keeping those internal stays consistent with the existing layering.
/// </remarks>
internal interface IAguiStreamPipeline
{
    /// <summary>
    /// Pump the agent's streaming output through the AG-UI protocol
    /// adapter. Writes events through <paramref name="writer"/> and (in
    /// voice mode) starts per-chunk synthesis through
    /// <paramref name="voiceCoordinator"/>. Returns aggregate stats the
    /// endpoint needs for RUN_FINISHED + post-stream persistence.
    /// </summary>
    Task<AguiStreamPipelineResult> StreamAsync(
        AguiStreamPipelineInput input,
        CancellationToken cancellationToken);
}

/// <summary>
/// Inputs to <see cref="IAguiStreamPipeline.StreamAsync"/>.
/// </summary>
/// <param name="Agent">The resolved domain agent (already contextualised).</param>
/// <param name="ChatMessages">History + user-brief preamble + new user turn.</param>
/// <param name="RunOptions">Optional per-run options (tools + model override). May be null.</param>
/// <param name="Writer">Prioritised SSE writer (control writes never preempted by audio).</param>
/// <param name="VoiceCoordinator">Voice-mode synth coordinator, or null for text-only runs.</param>
/// <param name="ThreadId">String form of the persisted thread id (also flows to baggage).</param>
/// <param name="RequestStopwatch">Started stopwatch from the endpoint — used for first-token timestamps.</param>
/// <param name="ChatActivity">Activity to tag with stream-discovered metrics (first-token, synth metrics).</param>
internal sealed record AguiStreamPipelineInput(
    AIAgent Agent,
    IReadOnlyList<ChatMessage> ChatMessages,
    ChatClientAgentRunOptions? RunOptions,
    AguiResponseWriter Writer,
    VoiceSynthCoordinator? VoiceCoordinator,
    string ThreadId,
    Stopwatch RequestStopwatch,
    Activity? ChatActivity);

/// <summary>
/// Output of <see cref="IAguiStreamPipeline.StreamAsync"/>. Aggregated
/// over the full stream so the endpoint can write RUN_FINISHED metrics
/// and queue post-stream persistence.
/// </summary>
internal sealed record AguiStreamPipelineResult(
    string AssistantText,
    string MessageId,
    long InputTokens,
    long OutputTokens,
    long? TimeToFirstTokenMs,
    long? RequestToFirstTokenSseMs,
    bool RequiresVisualAttention,
    bool RequiresApproval);
