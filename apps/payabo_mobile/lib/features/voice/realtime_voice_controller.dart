// ignore_for_file: public_member_api_docs

import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';

import '../../data/repositories/repository_providers.dart';
import '../chat/domain/chat_controller.dart';
import 'voxa_voice_client.dart';
import 'voxa_voice_session.dart';

/// Controller for the realtime (Voxa WSS) voice mode used by the chat screen.
///
/// Owns one [VoxaVoiceSession] per voice-mode activation, translates the
/// session's typed event stream into [ChatController] writes (user turn → bot
/// turn), and exposes a slim 4-phase state machine the orb widget binds to.
///
/// **Why a separate controller (and not a wrapper around `ChatVoiceService`):**
/// the legacy turn-based service has a `startListening`/`stopListening`/`speak`
/// interface that maps poorly onto duplex WSS audio. Forcing the duplex client
/// into that shape loses the benefits (continuous mic, server VAD, barge-in,
/// no thinking gap). Instead we keep the controllers parallel and gate the
/// chat-screen orb on [voxaVoiceModeEnabledProvider].
///
/// **Lifecycle:** one [start] per voice activation; [stop] tears down.
/// Re-using the controller for a second activation works — the underlying
/// session is constructed fresh from [voxaVoiceSessionProvider] each time.
class RealtimeVoiceController extends StateNotifier<RealtimeVoiceState> {
  RealtimeVoiceController({
    required Ref ref,
    required ChatController chatController,
  })  : _ref = ref,
        _chatController = chatController,
        super(const RealtimeVoiceState());

  final Ref _ref;
  final ChatController _chatController;

  VoxaVoiceSession? _session;
  StreamSubscription<VoxaVoiceEvent>? _eventsSub;
  StreamSubscription<VoxaConnectionState>? _stateSub;

  /// Subscription that keeps [voxaVoiceSessionProvider] alive across the
  /// async hops in [start]. Without it the autoDispose microtask runs
  /// during `session.start()`'s `await`s (mic permission, WS connect) and
  /// tears the session down — including uniniting the player — half-way
  /// through the connect. The subscription's callback is empty: we only
  /// hold it for its lifetime side-effect, not for change notifications.
  /// Closed in [_teardown] so the session auto-disposes once stop() runs.
  ProviderSubscription<VoxaVoiceSession>? _sessionKeepAlive;

  /// True when a streaming assistant turn is in flight in ChatController.
  /// We track it locally so [TranscriptionEvent], [BotTextEvent], and
  /// [SpeakingEvent] can collaborate on when to finalize the assistant
  /// message in [ChatController.finishRealtimeAssistantTurn].
  bool _assistantTurnActive = false;

  /// Start a realtime voice session for [agentId]. Reuses the chat thread
  /// id from [ChatController.state] so the conversation continues whichever
  /// pipeline (SSE or WSS) opened it.
  ///
  /// Errors from mic permission or the WS handshake surface in
  /// [RealtimeVoiceState.errorMessage] and the phase flips to
  /// [RealtimeVoicePhase.error]. The caller doesn't need to catch — the
  /// state machine is the source of truth.
  Future<void> start({
    String agentId = 'personal-finance-agent',
  }) async {
    if (state.phase == RealtimeVoicePhase.connecting ||
        state.phase == RealtimeVoicePhase.live) {
      return;
    }

    state = const RealtimeVoiceState(phase: RealtimeVoicePhase.connecting);

    // Acquire a keep-alive subscription on the autoDispose voxa session
    // BEFORE reading its value. `ref.read` alone is transient — the
    // dispose microtask would fire during this method's awaits and
    // tear the recorder + player down mid-connect. The subscription
    // holds the provider alive until [_teardown] closes it.
    _sessionKeepAlive ??= _ref.listen<VoxaVoiceSession>(
      voxaVoiceSessionProvider,
      (_, __) {},
    );

    final VoxaVoiceSession session = _ref.read(voxaVoiceSessionProvider);
    _session = session;

    await _eventsSub?.cancel();
    _eventsSub = session.events.listen(_onEvent);

    await _stateSub?.cancel();
    _stateSub = session.stateChanges.listen(_onConnectionState);

    try {
      await session.start(
        agentId: agentId,
        chatThreadId: _chatController.state.threadId,
      );
      if (!mounted) return;
      if (state.phase != RealtimeVoicePhase.error) {
        state = state.copyWith(phase: RealtimeVoicePhase.live);
      }
    } catch (err) {
      final String message = err.toString();
      if (mounted) {
        state = state.copyWith(
          phase: RealtimeVoicePhase.error,
          errorMessage: message,
        );
        _chatController.markRealtimeError(message);
      }
      await _teardown();
    }
  }

  /// Stop the active session. Finalizes any in-flight assistant text into
  /// chat history, closes the WebSocket, and returns to [RealtimeVoicePhase.idle].
  /// Safe to call repeatedly.
  Future<void> stop() async {
    if (state.phase == RealtimeVoicePhase.idle &&
        _session == null &&
        _eventsSub == null) {
      return;
    }

    if (_assistantTurnActive) {
      _chatController.finishRealtimeAssistantTurn();
      _assistantTurnActive = false;
    }
    _chatController.endRealtimeSession();

    await _teardown();

    // Keep an error phase visible so the user sees the failure reason; any
    // subsequent [start] will clear it.
    if (state.phase != RealtimeVoicePhase.error) {
      state = const RealtimeVoiceState();
    }
  }

  // ── Internals ─────────────────────────────────────────────────────────

  void _onEvent(VoxaVoiceEvent event) {
    switch (event) {
      case TranscriptionEvent(:final String text, :final bool isFinal):
        final String trimmed = text.trim();
        if (isFinal) {
          if (trimmed.isNotEmpty) {
            _chatController.addRealtimeUserTurn(trimmed);
          }
          state = state.copyWith(livePartialTranscript: '');
        } else {
          state = state.copyWith(livePartialTranscript: trimmed);
        }

      case BotTextEvent(:final String text):
        if (!_assistantTurnActive) {
          _chatController.beginRealtimeAssistantTurn();
          _assistantTurnActive = true;
        }
        _chatController.appendRealtimeAssistantText(text);

      case SpeakingEvent(:final String who, :final bool started):
        final RealtimeSpeaker speaker;
        if (!started) {
          speaker = RealtimeSpeaker.none;
        } else if (who == 'bot') {
          speaker = RealtimeSpeaker.bot;
        } else {
          speaker = RealtimeSpeaker.user;
        }
        // Bot finished speaking → materialize its message in chat history.
        if (!started && who == 'bot' && _assistantTurnActive) {
          _chatController.finishRealtimeAssistantTurn();
          _assistantTurnActive = false;
        }
        state = state.copyWith(whoIsSpeaking: speaker);

      case InterruptionEvent():
        // User barged in mid-bot-speech. Preserve the partial reply in chat
        // history; the next BotTextEvent (if any) will open a new turn.
        if (_assistantTurnActive) {
          _chatController.markRealtimeInterruption();
          _assistantTurnActive = false;
        }
        state = state.copyWith(whoIsSpeaking: RealtimeSpeaker.user);

      case StatusEvent():
        // Informational only — no state update.
        break;

      case ErrorEvent(:final String message):
        state = state.copyWith(
          phase: RealtimeVoicePhase.error,
          errorMessage: message,
        );
        _chatController.markRealtimeError(message);
        unawaited(_teardown());

      case EndedEvent():
        if (_assistantTurnActive) {
          _chatController.finishRealtimeAssistantTurn();
          _assistantTurnActive = false;
        }
        _chatController.endRealtimeSession();
        unawaited(_teardown());
        if (state.phase != RealtimeVoicePhase.error) {
          state = const RealtimeVoiceState();
        }

      case ThreadReadyEvent(:final String chatThreadId):
        _chatController.setRealtimeThreadId(chatThreadId);

      case ToolCallEvent():
        // Tool calls flow through the existing ChatController approval flow;
        // surfacing them in voice mode is a future polish task (Phase F /
        // composite-recipe follow-up). No-op for v1.
        break;
    }
  }

  void _onConnectionState(VoxaConnectionState s) {
    switch (s) {
      case VoxaConnectionState.idle:
        break;
      case VoxaConnectionState.connecting:
        if (state.phase != RealtimeVoicePhase.error) {
          state = state.copyWith(phase: RealtimeVoicePhase.connecting);
        }
      case VoxaConnectionState.connected:
        if (state.phase != RealtimeVoicePhase.error) {
          state = state.copyWith(phase: RealtimeVoicePhase.live);
        }
      case VoxaConnectionState.closed:
        unawaited(_teardown());
        if (state.phase != RealtimeVoicePhase.error) {
          state = const RealtimeVoiceState();
        }
      case VoxaConnectionState.error:
        // We may have already populated [errorMessage] via an ErrorEvent;
        // only set a generic fallback if it's empty.
        state = state.copyWith(
          phase: RealtimeVoicePhase.error,
          errorMessage:
              state.errorMessage ?? 'Voice connection lost. Tap to try again.',
        );
        _chatController.markRealtimeError(
            state.errorMessage ?? 'Voice connection lost.');
        unawaited(_teardown());
    }
  }

  Future<void> _teardown() async {
    final StreamSubscription<VoxaVoiceEvent>? eSub = _eventsSub;
    _eventsSub = null;
    if (eSub != null) {
      try {
        await eSub.cancel();
      } catch (_) {
        // already cancelled
      }
    }

    final StreamSubscription<VoxaConnectionState>? sSub = _stateSub;
    _stateSub = null;
    if (sSub != null) {
      try {
        await sSub.cancel();
      } catch (_) {
        // already cancelled
      }
    }

    final VoxaVoiceSession? session = _session;
    _session = null;
    if (session != null) {
      try {
        await session.stop();
      } catch (_) {
        // best effort
      }
    }

    // Drop the keep-alive so the voxa session provider can auto-dispose
    // its recorder + player. Next [start] will reacquire and read a
    // fresh session.
    final ProviderSubscription<VoxaVoiceSession>? keepAlive =
        _sessionKeepAlive;
    _sessionKeepAlive = null;
    keepAlive?.close();

    _assistantTurnActive = false;
  }

  @override
  void dispose() {
    unawaited(_teardown());
    super.dispose();
  }
}

/// Phases of the realtime voice UI. Deliberately fewer than the legacy
/// turn-based state machine (`idle | listening | thinking | speaking | ready`)
/// because server-side VAD owns turn detection, so the client doesn't need
/// to distinguish "listening" from "speaking" or surface a "thinking" gap.
///
/// Pulse / who-is-speaking visuals are driven by
/// [RealtimeVoiceState.whoIsSpeaking] independently of this phase.
enum RealtimeVoicePhase {
  /// Voice mode is off.
  idle,

  /// WebSocket handshake or mic permission check in flight.
  connecting,

  /// Duplex audio is flowing.
  live,

  /// Last session failed. Tap to retry.
  error,
}

/// Who is currently producing audio. Driven by server-emitted SpeakingEvents
/// rather than client-side inference — flips back to [none] between turns.
enum RealtimeSpeaker { none, user, bot }

class RealtimeVoiceState {
  const RealtimeVoiceState({
    this.phase = RealtimeVoicePhase.idle,
    this.whoIsSpeaking = RealtimeSpeaker.none,
    this.livePartialTranscript = '',
    this.errorMessage,
  });

  final RealtimeVoicePhase phase;
  final RealtimeSpeaker whoIsSpeaking;

  /// Latest partial transcript from the server. Cleared on each final.
  /// The orb / transcript chip surfaces this so the user has continuous
  /// feedback that the mic is working.
  final String livePartialTranscript;

  /// Populated when [phase] is [RealtimeVoicePhase.error]. Cleared on the
  /// next successful [RealtimeVoiceController.start].
  final String? errorMessage;

  bool get isLive => phase == RealtimeVoicePhase.live;
  bool get isConnecting => phase == RealtimeVoicePhase.connecting;
  bool get isActive => isLive || isConnecting;

  RealtimeVoiceState copyWith({
    RealtimeVoicePhase? phase,
    RealtimeSpeaker? whoIsSpeaking,
    String? livePartialTranscript,
    Object? errorMessage = _sentinel,
  }) {
    return RealtimeVoiceState(
      phase: phase ?? this.phase,
      whoIsSpeaking: whoIsSpeaking ?? this.whoIsSpeaking,
      livePartialTranscript:
          livePartialTranscript ?? this.livePartialTranscript,
      errorMessage:
          errorMessage == _sentinel ? this.errorMessage : errorMessage as String?,
    );
  }
}

const Object _sentinel = Object();

/// App-scoped [RealtimeVoiceController] — NOT autoDispose.
///
/// Earlier this was `autoDispose` so the WSS session would be torn down
/// when no widget was listening. That broke voice mode: tapping the mic
/// races with the autoDispose microtask. The flow is —
///
///   1. `_handleRealtimeVoiceTap` calls `ref.read(notifier)` (transient sub)
///   2. `setState(_showVoiceStage = true)` schedules a rebuild
///   3. `await notifier.start()` yields control to the event loop
///   4. Riverpod's dispose microtask fires *before* the rebuild mounts
///      `RealtimeVoiceStage` (which would have kept the controller alive
///      via its own `ref.watch`)
///   5. The controller is disposed mid-`session.start()`, which tears the
///      recorder + player down half-way through the connect handshake
///
/// Resource cost of keeping the controller alive is negligible — it's a
/// thin orchestrator that owns stream subscriptions and a few flags. The
/// heavy native handles (mic recorder, AAudio player, WS channel) live
/// inside [VoxaVoiceSession] which remains autoDispose; the controller
/// acquires/releases that via a [ProviderSubscription] inside [start]
/// and [_teardown] so we still get prompt resource release between
/// voice calls.
final StateNotifierProvider<RealtimeVoiceController, RealtimeVoiceState>
    realtimeVoiceControllerProvider =
    StateNotifierProvider<RealtimeVoiceController, RealtimeVoiceState>(
  (Ref ref) {
    final ChatController chatController =
        ref.read(chatControllerProvider.notifier);
    return RealtimeVoiceController(
      ref: ref,
      chatController: chatController,
    );
  },
);
