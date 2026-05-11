// ignore_for_file: public_member_api_docs

import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';

import '../../data/repositories/repository_providers.dart';
import '../chat/domain/chat_controller.dart';
import 'voxa_voice_client.dart';
import 'voxa_voice_session.dart';

/// Owns the realtime (Voxa WSS) voice session lifecycle. The chat screen only
/// toggles UI visibility — this controller owns busy state, the re-entrancy
/// guard, the watchdog timer, error surface, and the bridge into
/// [ChatController] for transcript writes.
class RealtimeVoiceController extends StateNotifier<RealtimeVoiceState> {
  RealtimeVoiceController({
    required Ref ref,
    required ChatController chatController,
  })  : _ref = ref,
        _chatController = chatController,
        super(const RealtimeVoiceState());

  /// Watchdog deadline for [toggle]. A platform call that hangs without this
  /// would leave the busy flag stuck on forever and every subsequent tap would
  /// be silently ignored.
  static const Duration _busyMaxDuration = Duration(seconds: 8);

  static const String _logPrefix = '[RealtimeVoice]';

  final Ref _ref;
  final ChatController _chatController;

  VoxaVoiceSession? _session;
  StreamSubscription<VoxaVoiceEvent>? _eventsSub;
  StreamSubscription<VoxaConnectionState>? _stateSub;

  // Keeps the autoDispose [voxaVoiceSessionProvider] alive across the awaits
  // in [_start] — without it the dispose microtask runs mid-connect and tears
  // the recorder + player down. Closed in [_teardown] so the session can
  // auto-dispose between voice calls.
  ProviderSubscription<VoxaVoiceSession>? _sessionKeepAlive;

  // Tracks whether ChatController has an open assistant turn so transcription
  // / text / speaking events can collaborate on finalisation.
  bool _assistantTurnActive = false;

  Timer? _busyWatchdog;

  /// Idle / error → start; connecting / live → stop. The chat screen calls
  /// this from the orb tap and reads the resulting [state.phase] to decide
  /// whether to show the stage.
  Future<void> toggle({String agentId = 'personal-finance-agent'}) async {
    if (state.busy) {
      _log('tap IGNORED — busy');
      return;
    }
    switch (state.phase) {
      case RealtimeVoicePhase.idle:
      case RealtimeVoicePhase.error:
        await _runBusy('start', () => _start(agentId));
      case RealtimeVoicePhase.connecting:
      case RealtimeVoicePhase.live:
        await _runBusy('stop', _stop);
    }
  }

  /// Imperative tear-down — used when the user navigates away or pivots to a
  /// different surface. Idempotent. Doesn't go through [_runBusy] because the
  /// caller is already tearing the UI down.
  Future<void> dismiss() async {
    if (state.phase == RealtimeVoicePhase.idle &&
        _session == null &&
        _eventsSub == null) {
      return;
    }
    await _stop();
  }

  /// Flip the mic gate. The session keeps consuming bot audio (so the user can
  /// still hear the assistant), but mic frames stop reaching the server until
  /// the next call. Auto-resets to unmuted on the next [_start].
  void toggleMute() {
    final bool next = !state.micMuted;
    _session?.setMuted(next);
    state = state.copyWith(micMuted: next);
  }

  // ── Internals ────────────────────────────────────────────────────────────

  Future<void> _start(String agentId) async {
    // Always start unmuted — carrying mute state across sessions would silently
    // dead-air the next call from the user's perspective.
    state = const RealtimeVoiceState(phase: RealtimeVoicePhase.connecting, busy: true);

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

  Future<void> _stop() async {
    if (_assistantTurnActive) {
      _chatController.finishRealtimeAssistantTurn();
      _assistantTurnActive = false;
    }
    _chatController.endRealtimeSession();

    await _teardown();

    // Preserve error state so the user sees the failure reason; the next
    // [toggle] clears it.
    if (state.phase != RealtimeVoicePhase.error) {
      state = const RealtimeVoiceState();
    }
  }

  /// Holds [state.busy] true for the duration of [op], with a watchdog that
  /// surfaces a timeout error if the underlying platform call hangs.
  Future<void> _runBusy(String label, Future<void> Function() op) async {
    state = state.copyWith(busy: true);
    _busyWatchdog?.cancel();
    _busyWatchdog = Timer(_busyMaxDuration, () {
      if (!state.busy) return;
      _log('watchdog FIRED for $label after ${_busyMaxDuration.inSeconds}s');
      state = state.copyWith(
        phase: RealtimeVoicePhase.error,
        errorMessage: 'Voice took too long to respond. Tap Talk to try again.',
        busy: false,
      );
    });
    try {
      await op();
    } finally {
      _busyWatchdog?.cancel();
      _busyWatchdog = null;
      if (mounted) {
        state = state.copyWith(busy: false);
      }
    }
  }

  void _onEvent(VoxaVoiceEvent event) {
    switch (event) {
      case TranscriptionEvent(:final String text, :final bool isFinal):
        final String trimmed = text.trim();
        if (isFinal) {
          if (trimmed.isNotEmpty) {
            _chatController.addRealtimeUserTurn(trimmed);
          }
          state = state.copyWith(
            livePartialTranscript: '',
            hasInteracted: trimmed.isNotEmpty ? true : null,
          );
        } else {
          state = state.copyWith(
            livePartialTranscript: trimmed,
            // First partial = user is mid-speech; that counts as interaction
            // so the "Speak whenever you're ready" placeholder retires even
            // before the final transcript lands.
            hasInteracted: trimmed.isNotEmpty ? true : null,
          );
        }

      case BotTextEvent(:final String text):
        if (!_assistantTurnActive) {
          _chatController.beginRealtimeAssistantTurn();
          _assistantTurnActive = true;
          // New assistant turn → reset the on-stage typewriter target.
          state = state.copyWith(liveAssistantText: '');
        }
        _chatController.appendRealtimeAssistantText(text);
        // Parallel display copy for the stage's typewriter; chat history
        // already gets the same text via ChatController above.
        state = state.copyWith(
          liveAssistantText: state.liveAssistantText + text,
          hasInteracted: true,
        );

      case SpeakingEvent(:final String who, :final bool started):
        final RealtimeSpeaker speaker;
        if (!started) {
          speaker = RealtimeSpeaker.none;
        } else if (who == 'bot') {
          speaker = RealtimeSpeaker.bot;
        } else {
          speaker = RealtimeSpeaker.user;
        }
        // The chained pipeline emits `speaking:false` between EACH TTS sentence
        // (audio gap while the next sentence is being synthesised). Closing
        // the chat bubble here would fragment one reply into N bubbles — one
        // per sentence. The real turn boundary is the user speaking again, an
        // interruption, the session ending, or an explicit dismiss. Those are
        // handled in their own cases below.
        //
        // We also intentionally do NOT clear `liveAssistantText` on
        // `speaking:false` — the on-stage typewriter would flicker between
        // sentences. It's cleared when the user starts speaking (real next
        // turn), on interruption, or when a fresh BotTextEvent opens a new
        // turn after a real boundary.
        if (started && who == 'user' && _assistantTurnActive) {
          // User starting their next turn = real boundary. Materialise the
          // assistant's reply in chat history and clear the stage typewriter.
          _chatController.finishRealtimeAssistantTurn();
          _assistantTurnActive = false;
          state = state.copyWith(
            whoIsSpeaking: speaker,
            liveAssistantText: '',
          );
        } else {
          state = state.copyWith(whoIsSpeaking: speaker);
        }

      case InterruptionEvent():
        // Barge-in is not supported in v1. The server's VAD may still emit
        // InterruptionEvent on background noise or genuine user-over-bot, but
        // we ignore it client-side: the bot's current TTS plays to its natural
        // end and the user waits for the turn to finish. Don't close the
        // assistant turn, don't clear the typewriter, don't reshape the
        // speaker indicator — just drop the event.
        break;

      case StatusEvent():
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
        // Tool calls flow through ChatController's existing approval surface;
        // voice-mode rendering is a future polish task. No-op for now.
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

    final ProviderSubscription<VoxaVoiceSession>? keepAlive = _sessionKeepAlive;
    _sessionKeepAlive = null;
    keepAlive?.close();

    _assistantTurnActive = false;
  }

  void _log(String message) {
    if (!kDebugMode) return;
    debugPrint('$_logPrefix $message');
  }

  @override
  void dispose() {
    _busyWatchdog?.cancel();
    unawaited(_teardown());
    super.dispose();
  }
}

/// Phases of the realtime voice UI. Server VAD owns turn detection so we don't
/// need separate `listening`/`thinking`/`speaking` phases — who-is-speaking is
/// surfaced separately via [RealtimeVoiceState.whoIsSpeaking].
enum RealtimeVoicePhase { idle, connecting, live, error }

enum RealtimeSpeaker { none, user, bot }

class RealtimeVoiceState {
  const RealtimeVoiceState({
    this.phase = RealtimeVoicePhase.idle,
    this.whoIsSpeaking = RealtimeSpeaker.none,
    this.livePartialTranscript = '',
    this.liveAssistantText = '',
    this.errorMessage,
    this.busy = false,
    this.micMuted = false,
    this.hasInteracted = false,
  });

  final RealtimeVoicePhase phase;
  final RealtimeSpeaker whoIsSpeaking;

  /// Latest partial transcript from the server. Cleared on each final.
  final String livePartialTranscript;

  /// Assistant text accumulated for the current bot turn — parallel display
  /// copy of what's also being streamed into chat history. Cleared on bot
  /// `speaking:false` or on interruption.
  final String liveAssistantText;

  /// Populated when [phase] is [RealtimeVoicePhase.error]. Cleared on the next
  /// successful [RealtimeVoiceController.toggle].
  final String? errorMessage;

  /// True while [RealtimeVoiceController.toggle] is in flight. The chat screen
  /// can read this to decide whether the orb should appear pending.
  final bool busy;

  /// User has muted the mic via the voice stage action bar. Resets to false on
  /// every new session.
  final bool micMuted;

  /// True once the user has spoken or the bot has emitted any text in this
  /// session. Gates the "Speak whenever you're ready" placeholder so it only
  /// appears at the very start — after the first turn the stage is quiet
  /// when no one's currently speaking. Resets to false on every new session.
  final bool hasInteracted;

  bool get isLive => phase == RealtimeVoicePhase.live;
  bool get isConnecting => phase == RealtimeVoicePhase.connecting;
  bool get isActive => isLive || isConnecting;

  RealtimeVoiceState copyWith({
    RealtimeVoicePhase? phase,
    RealtimeSpeaker? whoIsSpeaking,
    String? livePartialTranscript,
    String? liveAssistantText,
    Object? errorMessage = _sentinel,
    bool? busy,
    bool? micMuted,
    bool? hasInteracted,
  }) {
    return RealtimeVoiceState(
      phase: phase ?? this.phase,
      whoIsSpeaking: whoIsSpeaking ?? this.whoIsSpeaking,
      livePartialTranscript:
          livePartialTranscript ?? this.livePartialTranscript,
      liveAssistantText: liveAssistantText ?? this.liveAssistantText,
      errorMessage:
          errorMessage == _sentinel ? this.errorMessage : errorMessage as String?,
      busy: busy ?? this.busy,
      micMuted: micMuted ?? this.micMuted,
      hasInteracted: hasInteracted ?? this.hasInteracted,
    );
  }
}

const Object _sentinel = Object();

/// App-scoped — NOT autoDispose. Riverpod's dispose microtask fires *before*
/// the rebuild that mounts the orb, so an autoDispose controller would be torn
/// down mid-`session.start()` (right after the chat screen's `ref.read` + the
/// `setState` that adds the stage to the tree). Resource cost of keeping the
/// controller alive is negligible — the heavy native handles live in
/// [VoxaVoiceSession] which IS autoDispose, acquired via a
/// [ProviderSubscription] inside [_start] and released in [_teardown].
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
