using System.Text.Json;
using Voxa.Frames;

namespace Aonik.Voice.Frames;

/// <summary>
/// AONIK-only frame serializer wired into <c>WebSocketAudioSink</c> via its
/// <c>customSerializer</c> hook. Returns the <c>threadReady</c> envelope JSON for
/// <see cref="ThreadReadyFrame"/>; returns <c>null</c> for everything else so Voxa's built-in
/// envelope serialization (audio, transcription, tool-call, etc.) takes over.
///
/// <para>
/// Replaces the previous <c>AonikVoiceWebSocketSink</c> subclass — Voxa's sink is sealed but
/// accepts this hook in its constructor, so AONIK no longer needs its own copy of the send
/// discipline (semaphore, binary-vs-text dispatch, close handling).
/// </para>
/// </summary>
public static class ThreadReadyFrameSerializer
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serialize a <see cref="ThreadReadyFrame"/> as the <c>threadReady</c> envelope. Returns
    /// <c>null</c> for any other frame so the sink falls through to its built-in handling.
    /// </summary>
    public static string? Serialize(Frame frame)
    {
        if (frame is not ThreadReadyFrame ready) return null;

        return JsonSerializer.Serialize(new
        {
            type = "threadReady",
            chatThreadId = ready.ChatThreadId,
            isNew = ready.IsNew,
        }, JsonOpts);
    }
}
