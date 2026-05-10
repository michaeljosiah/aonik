using Voxa.Frames;

namespace Aonik.Voice.Frames;

/// <summary>
/// AONIK-only frame emitted once per WebSocket session, after the first
/// <c>EnsureThreadAsync</c> call inside <c>AonikVoiceAgent</c>'s <c>BuildMessages</c> closure.
/// Carries the persisted <see cref="ChatThreadId"/> to mobile via the <c>threadReady</c> envelope,
/// which is serialized by <see cref="ThreadReadyFrameSerializer.Serialize"/> through Voxa's
/// <c>WebSocketAudioSink</c> custom-serializer hook.
///
/// <para>
/// Mobile uses the value as <c>hello.chatThreadId</c> on reconnect so the session resumes the
/// same thread instead of starting a new one. Mirrors the AGUI <c>RUN_STARTED</c> thread-id
/// signal.
/// </para>
/// <para>
/// See <c>docs/specifications/022.aonik-voice-realtime.md</c> "AONIK extension: threadReady".
/// </para>
/// </summary>
public sealed record ThreadReadyFrame(string ChatThreadId, bool IsNew) : DataFrame;
