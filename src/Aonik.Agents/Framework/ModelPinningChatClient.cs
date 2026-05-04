using Microsoft.Extensions.AI;

namespace Aonik.Agents.Framework;

/// <summary>
/// <see cref="DelegatingChatClient"/> that stamps a fixed
/// <see cref="ChatOptions.ModelId"/> on every call, regardless of what
/// the caller provided.
/// <para>
/// Used by <c>MasterOrchestratorService.BuildAllToolsAsync</c> to enforce
/// a per-sub-agent model override. The orchestrator runs with its own
/// <see cref="ChatClientAgentRunOptions"/>, but when it dispatches a
/// tool call into a sub-agent the framework calls the sub-agent's
/// <c>RunAsync</c> internally — without our run-time options, so the
/// sub-agent inherits whatever the parent context decided. Wrapping
/// the sub-agent's <see cref="IChatClient"/> with this decorator pins
/// the model at construction time so the sub-agent's LLM calls always
/// hit the configured model (e.g. <c>gpt-4o</c>) instead of falling
/// through to the global default.
/// </para>
/// </summary>
internal sealed class ModelPinningChatClient : DelegatingChatClient
{
    private readonly string _modelId;

    public ModelPinningChatClient(IChatClient innerClient, string modelId)
        : base(innerClient)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model id cannot be null or empty.", nameof(modelId));

        _modelId = modelId;
    }

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => base.GetResponseAsync(messages, WithPinnedModel(options), cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => base.GetStreamingResponseAsync(messages, WithPinnedModel(options), cancellationToken);

    /// <summary>
    /// Returns a <see cref="ChatOptions"/> with <see cref="ChatOptions.ModelId"/>
    /// set to the pinned value, preserving every other field. Always
    /// returns a NEW instance so we don't mutate caller-owned state —
    /// some callers (e.g. AGUI streaming) reuse a single ChatOptions
    /// object across multiple calls.
    /// </summary>
    private ChatOptions WithPinnedModel(ChatOptions? options)
    {
        if (options is null)
        {
            return new ChatOptions { ModelId = _modelId };
        }

        // Defensive clone — we copy every field by serialising back through
        // the public surface. ChatOptions.Clone() is the canonical way to
        // duplicate without rebuilding by hand.
        var cloned = options.Clone();
        cloned.ModelId = _modelId;
        return cloned;
    }
}
