using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Ai;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Agents.Services;

/// <summary>
/// Owns the AG-UI SSE protocol translation: pumps the agent's framework
/// streaming output through the appropriate AG-UI events, fans out
/// per-chunk speech synthesis in voice mode, and aggregates the metrics
/// the endpoint needs for RUN_FINISHED + post-stream persistence.
/// </summary>
internal sealed class AguiStreamPipeline : IAguiStreamPipeline
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly IToolCallClassifier _classifier;
    private readonly ISpeechRenderer _speechRenderer;

    public AguiStreamPipeline(
        IToolCallClassifier classifier,
        ISpeechRenderer speechRenderer)
    {
        _classifier = classifier;
        _speechRenderer = speechRenderer;
    }

    public async Task<AguiStreamPipelineResult> StreamAsync(
        AguiStreamPipelineInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var assistantTextBuilder = new StringBuilder();
        long inputTokens = 0;
        long outputTokens = 0;
        long? timeToFirstTokenMs = null;
        long? requestToFirstTokenSseMs = null;

        var messageId = Guid.NewGuid().ToString("N");
        var messageStarted = false;
        var requiresVisualAttention = false;
        var requiresApproval = false;
        var speechBuffer = new SpeechStreamBuffer();
        var speechChunksEmittedDuringStream = 0;
        var speechChunkTailEmitted = false;

        var chatActivity = input.ChatActivity;
        var stopwatch = input.RequestStopwatch;
        var writer = input.Writer;
        var voiceCoordinator = input.VoiceCoordinator;
        var threadId = input.ThreadId;

        chatActivity?.AddEvent(new ActivityEvent(
            "aonik.chat.llm_start",
            tags: new ActivityTagsCollection
            {
                ["elapsed_ms"] = stopwatch.ElapsedMilliseconds,
                ["message_count"] = input.ChatMessages.Count,
            }));

        await foreach (var update in input.Agent.RunStreamingAsync(
            input.ChatMessages, session: null, options: input.RunOptions, cancellationToken: cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested) break;

            var chatUpdate = update.AsChatResponseUpdate();
            if (chatUpdate is null) continue;

            foreach (var content in chatUpdate.Contents ?? [])
            {
                switch (content)
                {
                    case TextContent textContent when !string.IsNullOrEmpty(textContent.Text):
                        if (!messageStarted)
                        {
                            timeToFirstTokenMs ??= stopwatch.ElapsedMilliseconds;
                            chatActivity?.SetTag("aonik.chat.time_to_first_token_ms", timeToFirstTokenMs.Value);
                            chatActivity?.AddEvent(new ActivityEvent(
                                "aonik.chat.first_token",
                                tags: new ActivityTagsCollection
                                {
                                    ["elapsed_ms"] = timeToFirstTokenMs.Value,
                                }));
                            await WriteSseEventAsync(writer, new
                            {
                                type = "TEXT_MESSAGE_START",
                                messageId,
                                role = "assistant",
                            }, cancellationToken);
                            messageStarted = true;
                        }

                        assistantTextBuilder.Append(textContent.Text);
                        speechBuffer.Append(textContent.Text);

                        await WriteSseEventAsync(writer, new
                        {
                            type = "TEXT_MESSAGE_CONTENT",
                            messageId,
                            delta = textContent.Text,
                        }, cancellationToken);
                        requestToFirstTokenSseMs ??= stopwatch.ElapsedMilliseconds;

                        // Voice mode: skip per-sentence chunk emission. We emit
                        // a single full-message chunk after the LLM stream is
                        // complete so the client plays one audio file rather
                        // than fighting a streaming MP3 decoder. Text-only
                        // consumers (e.g. AdminUI playground when no voice
                        // coordinator is wired) keep the per-sentence path.
                        if (voiceCoordinator is null)
                        {
                            while (speechBuffer.TryPopSentence(out var rawChunk))
                            {
                                if (await EmitSpeechChunkAsync(
                                    writer, voiceCoordinator, threadId, messageId, speechBuffer.NextChunkIndex - 1,
                                    rawChunk, isFinal: false, cancellationToken))
                                {
                                    speechChunksEmittedDuringStream++;
                                }
                            }
                        }
                        break;

                    case FunctionCallContent functionCall:
                        var toolCallId = _classifier.ResolveCallId(functionCall);
                        var toolName = functionCall.Name ?? string.Empty;
                        requiresVisualAttention |= _classifier.IsDisplay(toolName);
                        requiresApproval |= _classifier.RequiresApproval(toolName);

                        await WriteSseEventAsync(writer, new
                        {
                            type = "TOOL_CALL_START",
                            toolCallId,
                            toolCallName = functionCall.Name,
                            parentMessageId = messageId,
                        }, cancellationToken);

                        if (functionCall.Arguments is { Count: > 0 })
                        {
                            var argsJson = JsonSerializer.Serialize(
                                functionCall.Arguments, JsonOptions);
                            await WriteSseEventAsync(writer, new
                            {
                                type = "TOOL_CALL_ARGS",
                                toolCallId,
                                delta = argsJson,
                            }, cancellationToken);
                        }

                        await WriteSseEventAsync(writer, new
                        {
                            type = "TOOL_CALL_END",
                            toolCallId,
                        }, cancellationToken);
                        break;

                    case FunctionResultContent functionResult:
                        await WriteSseEventAsync(writer, new
                        {
                            type = "TOOL_CALL_RESULT",
                            messageId = Guid.NewGuid().ToString("N"),
                            toolCallId = functionResult.CallId,
                            content = functionResult.Result?.ToString(),
                            role = "tool",
                        }, cancellationToken);
                        break;

                    case TextReasoningContent reasoningContent
                        when !string.IsNullOrEmpty(reasoningContent.Text):

                        await WriteSseEventAsync(writer, new
                        {
                            type = "REASONING_MESSAGE_CONTENT",
                            messageId,
                            delta = reasoningContent.Text,
                        }, cancellationToken);
                        break;

                    case UsageContent usageContent:
                        inputTokens += usageContent.Details.InputTokenCount ?? 0;
                        outputTokens += usageContent.Details.OutputTokenCount ?? 0;
                        break;
                }
            }
        }

        if (messageStarted)
        {
            await WriteSseEventAsync(writer, new
            {
                type = "TEXT_MESSAGE_END",
                messageId,
            }, cancellationToken);
        }

        // Tail chunk — text-only path. Voice mode emits one consolidated
        // chunk for the full assistant message after the speech.render
        // event below; the per-sentence buffer is unused there.
        if (voiceCoordinator is null)
        {
            var tailChunk = speechBuffer.FlushRemaining();
            if (tailChunk is not null)
            {
                if (await EmitSpeechChunkAsync(
                    writer, voiceCoordinator, threadId, messageId, speechBuffer.NextChunkIndex - 1,
                    tailChunk, isFinal: true, cancellationToken))
                {
                    speechChunkTailEmitted = true;
                }
            }
        }

        if (chatActivity is not null)
        {
            var totalEmitted = speechChunksEmittedDuringStream + (speechChunkTailEmitted ? 1 : 0);
            chatActivity.SetTag("aonik.chat.speech_chunks_emitted", totalEmitted);
            chatActivity.SetTag("aonik.chat.speech_chunks_emitted_during_stream", speechChunksEmittedDuringStream);
            chatActivity.SetTag("aonik.chat.speech_chunk_tail_emitted", speechChunkTailEmitted);
        }

        // Guidance speech — a CUSTOM SSE event the client renders as the
        // post-message advisory ("Check the proposal panel.", etc.). Always
        // emitted so the client knows whether to draw attention.
        var guidanceText = _speechRenderer.RenderGuidance(requiresVisualAttention, requiresApproval);
        await WriteSseEventAsync(writer, new
        {
            type = "CUSTOM",
            name = "speech.render",
            value = new
            {
                messageId,
                speechText = guidanceText,
                requiresVisualAttention,
                requiresApproval,
                isFinal = true,
            }
        }, cancellationToken);

        // Voice-mode one-shot synth (Option A): synthesize the entire
        // assistant message in a single chunk after the LLM stream is
        // complete. The simplified mobile player accumulates all frames
        // for chunkIndex 0 and plays the result as a single MP3 file —
        // no streaming player, no proxy server, no watchdog fallback.
        // Per-sentence chunking added avoidable failure modes (just_audio
        // setAudioSource hangs on small MP3 prefixes, _proxyHandlerForSource
        // null-checks, two-player crossfade overlap). One file plays
        // cleanly through audioplayers' DeviceFileSource path.
        if (voiceCoordinator is not null)
        {
            var fullSpeechText = assistantTextBuilder.ToString();
            if (!string.IsNullOrWhiteSpace(fullSpeechText))
            {
                if (await EmitSpeechChunkAsync(
                    writer, voiceCoordinator, threadId, messageId,
                    chunkIndex: 0, rawChunk: fullSpeechText,
                    isFinal: true, cancellationToken))
                {
                    speechChunkTailEmitted = true;
                    chatActivity?.SetTag("aonik.chat.speech_chunk_tail_emitted", true);
                    chatActivity?.SetTag("aonik.chat.speech_chunks_emitted", 1);
                }
            }

            // Voice-mode drain — RUN_FINISHED MUST come after every audio
            // frame is on the wire. Wait for synth workers to finish
            // enqueueing, close the audio channel, then wait for the
            // writer's pump to drain.
            await voiceCoordinator.WaitForAllSynthesisAsync();
            writer.CompleteAudioInput();
            await writer.WaitForAudioDrainAsync();

            var synthMetrics = voiceCoordinator.GetSynthTaskMetrics();
            chatActivity?.SetTag("aonik.chat.synth_tasks_started", synthMetrics.Started);
            chatActivity?.SetTag("aonik.chat.synth_tasks_completed", synthMetrics.Completed);
            chatActivity?.SetTag("aonik.chat.synth_tasks_errored", synthMetrics.Errored);
            chatActivity?.SetTag("aonik.chat.synth_tasks_timed_out", synthMetrics.TimedOut);
            chatActivity?.SetTag("aonik.chat.synth_tasks_cancelled", synthMetrics.Cancelled);
            chatActivity?.SetTag("aonik.chat.synth_tasks_yielded_frames", synthMetrics.YieldedAtLeastOneFrame);
        }

        return new AguiStreamPipelineResult(
            AssistantText: assistantTextBuilder.ToString(),
            MessageId: messageId,
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            TimeToFirstTokenMs: timeToFirstTokenMs,
            RequestToFirstTokenSseMs: requestToFirstTokenSseMs,
            RequiresVisualAttention: requiresVisualAttention,
            RequiresApproval: requiresApproval);
    }

    private static Task WriteSseEventAsync<T>(
        AguiResponseWriter writer,
        T eventData,
        CancellationToken cancellationToken) =>
        writer.WriteControlAsync(eventData, cancellationToken);

    private async Task<bool> EmitSpeechChunkAsync(
        AguiResponseWriter writer,
        VoiceSynthCoordinator? voiceCoordinator,
        string threadId,
        string messageId,
        int chunkIndex,
        string rawChunk,
        bool isFinal,
        CancellationToken cancellationToken)
    {
        var chunkText = _speechRenderer.RenderChunk(rawChunk);
        if (string.IsNullOrWhiteSpace(chunkText))
            return false;

        await WriteSseEventAsync(writer, new
        {
            type = "CUSTOM",
            name = "speech.chunk",
            value = new
            {
                messageId,
                chunkIndex,
                speechText = chunkText,
                isFinal,
            },
        }, cancellationToken);

        // Voice mode fans out per-chunk synthesis; audio frames are
        // enqueued through the prioritised writer's audio channel and
        // flushed by its background pump while the LLM keeps emitting
        // text deltas downstream.
        voiceCoordinator?.StartChunkSynthesis(messageId, chunkIndex, chunkText, threadId, cancellationToken);
        return true;
    }
}
