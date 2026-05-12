// ignore_for_file: public_member_api_docs

import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_soloud/flutter_soloud.dart';
import 'package:record/record.dart';

import '../../app/auth/auth_session_store.dart';
import '../../app/environment/app_environment.dart';
import '../../app/environment/environment_provider.dart';
import 'voxa_voice_client.dart';

/// Combines [VoxaVoiceClient] with a 16 kHz PCM mic recorder + 24 kHz PCM
/// streaming player. One [start]/[stop] cycle per conversation; the underlying
/// client is single-use so we build a fresh one on each [start].
///
/// Playback uses [SoLoud]'s buffer-stream API. Unlike the previous
/// `mp_audio_stream` ring buffer (which silently dropped samples on overflow
/// — the root cause of the "last word missing" symptom), SoLoud's stream:
///   * Accepts raw `s16le` PCM directly (no Float32 conversion needed).
///   * Throws on out-of-memory rather than dropping silently, so producer
///     errors are observable.
///   * Frees memory of already-played samples ([BufferingType.released]) so
///     long sessions don't grow unbounded.
///   * Pauses-and-resumes around underrun based on a configurable buffering
///     time, matching what the web client gets from Web Audio scheduling.
class VoxaVoiceSession {
  VoxaVoiceSession({
    required VoxaVoiceClient Function() clientFactory,
    AudioRecorder? recorder,
    SoLoud? player,
  })  : _clientFactory = clientFactory,
        _recorder = recorder ?? AudioRecorder(),
        _player = player ?? SoLoud.instance;

  final VoxaVoiceClient Function() _clientFactory;
  final AudioRecorder _recorder;
  final SoLoud _player;

  VoxaVoiceClient? _activeClient;
  StreamSubscription<Uint8List>? _micSubscription;
  StreamSubscription<Uint8List>? _audioSubscription;
  StreamSubscription<VoxaVoiceEvent>? _eventsSubscription;
  StreamSubscription<VoxaConnectionState>? _stateSubscription;

  /// The active SoLoud stream source for this session. Created in [start],
  /// torn down in [stop]. We don't deinit the SoLoud engine itself between
  /// sessions — `init()` is idempotent and the engine sleeps when nothing
  /// is playing, so leaving it warm makes the next session start faster.
  AudioSource? _streamSource;
  SoundHandle? _streamHandle;

  bool _running = false;

  /// User-driven mic gate from the voice stage's mute button. Independent of
  /// [_botSpeaking] (which is server-driven) — both must be false for a mic
  /// frame to reach the WSS.
  bool _userMuted = false;

  // Captured on start so the silence tail-pad can synthesise the right number
  // of zero samples to push.
  int _playerSampleRate = 24000;

  // Mic gate: speakers bleed into the mic; without it Whisper transcribes the
  // bot's own voice as a new user turn. Trade-off: no barge-in in v1.
  bool _botSpeaking = false;
  Timer? _botStoppedHangoverTimer;

  // Tail guard uses a monotonic Stopwatch (immune to wall-clock jumps) so we
  // can drop mic frames during the real speaker tail, independent of the
  // server's `speaking` events. See [_micSubscription] below.
  final Stopwatch _clock = Stopwatch()..start();
  Duration? _lastBotAudioAt;

  // Mic stays muted this long after the server's `speaking:false` to let the
  // local PCM queue drain. Tuned upward from the mp_audio_stream era (3.3 s)
  // because SoLoud's buffered stream model can hold ~1–2 s of bursted-ahead
  // TTS audio when `speaking:false` fires — that audio is still played out
  // at realtime after the wire event, and the mic gate has to cover it or
  // the speaker bleed leaks into STT.
  static const Duration _postBotStopHangover = Duration(milliseconds: 5000);

  // Drop mic frames within this window of the last inbound bot audio frame —
  // catches the late tail that the wire-event gate misses (e.g. when the
  // SoLoud queue still has buffered samples after `speaking:false` fired).
  // Tuned upward from the mp_audio_stream era (1.2 s) for the same reason
  // as [_postBotStopHangover] above.
  static const Duration _audioTailGuard = Duration(milliseconds: 2500);

  /// SoLoud's [setBufferStream.maxBufferSizeBytes] cap. With
  /// [BufferingType.released] this is the total cumulative bytes the stream
  /// will accept across its lifetime, not memory at any instant; played
  /// samples are freed. 100 MB ≈ 17 min of continuous s16le 24 kHz mono
  /// audio, comfortably more than any realistic voice session.
  static const int _streamMaxBufferBytes = 100 * 1024 * 1024;

  /// Seconds of buffered audio SoLoud needs accumulated before resuming
  /// playback after an underrun. Equivalent to the old
  /// `_playerWaitingBufferMilliSec` knob — 80 ms is short enough to be
  /// imperceptible as a between-sentence gap, long enough to absorb single-
  /// frame network jitter.
  static const double _bufferingTimeNeedsSeconds = 0.08;

  /// Silence pushed to the stream on every bot `speaking:false` so the buffer
  /// never goes empty exactly at the boundary between sentences — covers the
  /// 200–500 ms TTS request latency for the next sentence on the chained
  /// pipeline so playback never has to pause-and-rebuffer.
  static const double _tailPadSeconds = 0.5;

  /// Output volume for the bot's audio. Slightly under full so that on Android
  /// devices where `AcousticEchoCanceler` is software-implemented (mid-range
  /// hardware that lacks HAL AEC) the residual speaker bleed is below
  /// Whisper's pickup threshold. Full volume + imperfect AEC = the bot
  /// occasionally transcribes itself; ~0.85 is a measurable safety margin
  /// without making playback feel quiet.
  static const double _playerVolume = 0.85;

  final StreamController<VoxaVoiceEvent> _eventsController =
      StreamController<VoxaVoiceEvent>.broadcast();
  final StreamController<VoxaConnectionState> _stateController =
      StreamController<VoxaConnectionState>.broadcast();

  Stream<VoxaVoiceEvent> get events => _eventsController.stream;
  Stream<VoxaConnectionState> get stateChanges => _stateController.stream;
  bool get isRunning => _running;
  VoxaConnectionState get connectionState =>
      _activeClient?.state ?? VoxaConnectionState.idle;

  /// Open the mic + player + WSS. Throws on permission denial or handshake failure.
  Future<void> start({
    required String agentId,
    String? chatThreadId,
    int micSampleRate = 16000,
    int botSampleRate = 24000,
    List<String> frontendTools = const <String>[],
  }) async {
    if (_running) {
      throw StateError('VoxaVoiceSession is already running.');
    }

    final bool hasPermission = await _recorder.hasPermission();
    if (!hasPermission) {
      throw StateError(
          'Microphone permission denied. Open device settings to grant Payabo access.');
    }

    // ── Step 1: recorder first ──
    // Starting the recorder is what flips the OS audio session into voice-
    // processing / in-communication mode (AVAudioSession `playAndRecord` +
    // voice-chat on iOS; `MODE_IN_COMMUNICATION` on Android). That mode is
    // what engages hardware AEC. If we let SoLoud open its output stream
    // first (as we did originally) iOS commits to `.playback` and the
    // recorder's later switch may not promote it back to voice-processing,
    // so the speaker bleed leaks straight into STT. Start the mic capture
    // first to lock in the right session, THEN bring the player up.
    //
    // The captured PCM stream isn't yet wired to a listener — that happens
    // after we have the client + audio gate state ready. So no mic frames
    // reach the server during the window between this call and the listener
    // being attached below; they just buffer inside the OS-level capture
    // pipeline harmlessly.
    final Stream<Uint8List> micStream;
    try {
      micStream = await _recorder.startStream(
        RecordConfig(
          encoder: AudioEncoder.pcm16bits,
          sampleRate: micSampleRate,
          numChannels: 1,
          echoCancel: true,
          noiseSuppress: true,
          // AGC OFF on purpose. Android's voice-communication source already
          // applies platform-level dynamic-range processing, and AGC on top
          // amplifies any speaker bleed that slips past AEC to be Whisper-
          // audible. With AGC off, residual bleed stays well under Whisper's
          // pickup threshold and genuine user speech is still well-levelled
          // by the platform pre-processing.
          autoGain: false,
          androidConfig: const AndroidRecordConfig(
            audioSource: AndroidAudioSource.voiceCommunication,
            audioManagerMode: AudioManagerMode.modeInCommunication,
          ),
        ),
      );
    } catch (err) {
      rethrow;
    }

    // ── Step 2: SoLoud player ──
    // With the OS session already in voice-processing mode, SoLoud's output
    // stream attaches into that pipeline so its audio is reference-fed to the
    // platform AEC.
    _playerSampleRate = botSampleRate;
    if (!_player.isInitialized) {
      try {
        await _player.init();
      } catch (err) {
        await _stopRecorder();
        throw StateError('Failed to initialise SoLoud engine: $err');
      }
    }
    try {
      _streamSource = _player.setBufferStream(
        maxBufferSizeBytes: _streamMaxBufferBytes,
        sampleRate: botSampleRate,
        channels: Channels.mono,
        format: BufferType.s16le,
        bufferingType: BufferingType.released,
        bufferingTimeNeeds: _bufferingTimeNeedsSeconds,
      );
      _streamHandle = await _player.play(_streamSource!, volume: _playerVolume);
    } catch (err) {
      await _disposeStream();
      await _stopRecorder();
      throw StateError('Failed to start SoLoud buffer stream: $err');
    }

    final VoxaVoiceClient client = _clientFactory();
    _activeClient = client;

    _audioSubscription = client.audioFrames.listen(
      _onBotAudioFrame,
      onError: (Object err, StackTrace _) {
        _eventsController.add(VoxaVoiceEvent.error(
          message: 'Audio frame stream error: $err',
          code: 'audio-stream',
        ));
      },
    );
    _eventsSubscription = client.events.listen((VoxaVoiceEvent event) {
      if (event is SpeakingEvent && event.who == 'bot') {
        _botStoppedHangoverTimer?.cancel();
        if (event.started) {
          _botSpeaking = true;
          _botStoppedHangoverTimer = null;
        } else {
          // Pad the stream with silence so the buffer never goes empty at the
          // boundary between sentences. Covers the TTS request latency for
          // the next sentence so SoLoud doesn't have to pause-and-rebuffer.
          _padPlayerTail();
          _botStoppedHangoverTimer = Timer(_postBotStopHangover, () {
            _botSpeaking = false;
          });
        }
      }
      // Barge-in is intentionally not supported in v1: an InterruptionEvent
      // from the server's VAD is *forwarded* (so the controller can render
      // any UI it wants) but the session takes no action. We deliberately do
      // NOT clear `_botSpeaking` / `_lastBotAudioAt` here — doing so would
      // open the mic mid-playback and feed the bot's own audio back into STT.
      // The bot finishes its current TTS naturally; if the user wants to
      // continue they wait for the natural turn boundary.
      _eventsController.add(event);
    });
    _stateSubscription = client.stateChanges.listen(_stateController.add);

    try {
      await client.connect(
        agentId: agentId,
        chatThreadId: chatThreadId,
        frontendTools: frontendTools,
      );
    } catch (err) {
      await _teardownAfterFailure();
      rethrow;
    }

    _micSubscription = micStream.listen(
      (Uint8List pcm) {
        if (_userMuted) return;
        if (_botSpeaking) return;
        final Duration? lastBotAudio = _lastBotAudioAt;
        if (lastBotAudio != null &&
            _clock.elapsed - lastBotAudio < _audioTailGuard) {
          return;
        }
        _activeClient?.sendPcm(pcm);
      },
      onError: (Object err, StackTrace _) {
        _eventsController.add(VoxaVoiceEvent.error(
          message: 'Mic stream error: $err',
          code: 'mic-stream',
        ));
      },
    );

    _running = true;
  }

  /// Stops the OS-level recorder. Used in failure paths during [start] when
  /// we've already opened the capture pipeline but a later step failed and
  /// we need to release the mic before throwing.
  Future<void> _stopRecorder() async {
    try {
      await _recorder.stop();
    } catch (_) {
      // Best-effort — recorder may already be stopped.
    }
  }

  /// Gate the mic from the UI without tearing the session down. Bot audio
  /// keeps flowing so the user can still hear the assistant talk; only the
  /// upstream mic frames are suppressed.
  void setMuted(bool muted) {
    _userMuted = muted;
  }

  /// Send `{type:"end"}`, close the WS, stop the recorder, tear down the
  /// SoLoud stream. The engine itself stays initialised for fast restart.
  /// Safe to call repeatedly.
  Future<void> stop() async {
    _running = false;
    _botSpeaking = false;
    _userMuted = false;
    _botStoppedHangoverTimer?.cancel();
    _botStoppedHangoverTimer = null;
    _lastBotAudioAt = null;

    await _micSubscription?.cancel();
    _micSubscription = null;

    try {
      await _recorder.stop();
    } catch (_) {
      // Best-effort — recorder may already be stopped (permission revoke etc).
    }

    final VoxaVoiceClient? client = _activeClient;
    _activeClient = null;
    if (client != null) {
      await client.close();
    }

    await _audioSubscription?.cancel();
    _audioSubscription = null;
    await _eventsSubscription?.cancel();
    _eventsSubscription = null;
    await _stateSubscription?.cancel();
    _stateSubscription = null;

    await _disposeStream();
  }

  /// One-shot release; the session can't be reused after this. SoLoud itself
  /// is a process-wide singleton so we don't deinit it — it'll be shut down
  /// when the app exits.
  Future<void> dispose() async {
    await stop();
    await _recorder.dispose();
    await _eventsController.close();
    await _stateController.close();
  }

  // ── Internals ────────────────────────────────────────────────────────────

  /// Push 500 ms of silent samples to the stream at the end of each bot turn
  /// (or each TTS sentence in the chained pipeline, where `speaking:false`
  /// fires per sentence). Bridges the next-sentence TTS request latency so
  /// playback flows continuously instead of having to pause-and-rebuffer.
  void _padPlayerTail() {
    final AudioSource? source = _streamSource;
    if (source == null) return;
    // bytes = sampleRate × seconds × 2 bytes per s16 sample, mono.
    final int byteCount = (_playerSampleRate * _tailPadSeconds * 2).round();
    if (byteCount <= 0) return;
    final Uint8List silence = Uint8List(byteCount);
    try {
      _player.addAudioDataStream(source, silence);
    } catch (err) {
      // Best-effort tail pad — if the stream is mid-teardown or the buffer is
      // unexpectedly full it's harmless to skip the silence pad.
      if (kDebugMode) {
        debugPrint('VoxaVoiceSession._padPlayerTail: $err');
      }
    }
  }

  void _onBotAudioFrame(Uint8List pcmBytes) {
    if (pcmBytes.isEmpty) return;
    final AudioSource? source = _streamSource;
    if (source == null) return;
    _lastBotAudioAt = _clock.elapsed;
    try {
      // SoLoud accepts raw s16le PCM directly — no Float32 conversion needed.
      _player.addAudioDataStream(source, pcmBytes);
    } catch (err) {
      // The stream's max buffer can be reached on very long sessions; surface
      // the error rather than silently dropping (which is what the previous
      // mp_audio_stream player did and caused the "last word missing" bug).
      _eventsController.add(VoxaVoiceEvent.error(
        message: 'Failed to enqueue bot audio frame: $err',
        code: 'audio-enqueue',
      ));
    }
  }

  /// Stop + dispose the active SoLoud stream source. Safe to call multiple
  /// times. Doesn't touch the engine itself — that's a process-wide
  /// singleton we keep warm.
  Future<void> _disposeStream() async {
    final SoundHandle? handle = _streamHandle;
    _streamHandle = null;
    final AudioSource? source = _streamSource;
    _streamSource = null;
    if (!_player.isInitialized) return;
    if (handle != null) {
      try {
        await _player.stop(handle);
      } catch (_) {
        // Already stopped / handle invalid — ignore.
      }
    }
    if (source != null) {
      try {
        // Marking the stream ended lets SoLoud finalise any internal state
        // even though we're about to dispose it.
        _player.setDataIsEnded(source);
      } catch (_) {
        // Source might already be ended.
      }
      try {
        await _player.disposeSource(source);
      } catch (_) {
        // Already disposed.
      }
    }
  }

  Future<void> _teardownAfterFailure() async {
    await _audioSubscription?.cancel();
    _audioSubscription = null;
    await _eventsSubscription?.cancel();
    _eventsSubscription = null;
    await _stateSubscription?.cancel();
    _stateSubscription = null;

    final VoxaVoiceClient? client = _activeClient;
    _activeClient = null;
    if (client != null) {
      await client.close();
    }

    await _disposeStream();
    // With the new init order the recorder is opened before the WSS client.
    // If the WSS handshake fails, we still need to release the mic capture.
    await _stopRecorder();
  }
}

/// Per-screen session factory. AutoDispose so navigating away mid-conversation
/// tears the mic + player + WS down. The controller holds a keep-alive across
/// its own start/stop cycle (see [RealtimeVoiceController]).
final Provider<VoxaVoiceSession> voxaVoiceSessionProvider =
    Provider.autoDispose<VoxaVoiceSession>((Ref ref) {
  final AppEnvironment environment = ref.watch(appEnvironmentProvider);
  final AuthSessionStore authStore = ref.watch(authSessionStoreProvider);

  Future<String?> getAccessToken() async {
    final AuthSession? session = await authStore.read();
    if (session == null || !session.hasAccessToken || session.isExpired) {
      return null;
    }
    return session.accessToken;
  }

  final VoxaVoiceSession session = VoxaVoiceSession(
    clientFactory: () => VoxaVoiceClient(
      apiBaseUrl: environment.runtimeApiBaseUrl,
      tenantId: environment.tenantId,
      getAccessToken: getAccessToken,
    ),
  );
  // dispose is async; provider can't await it. The lambda keeps the
  // discarded_futures ignore visually attached to the call site.
  // ignore: unnecessary_lambdas
  ref.onDispose(() {
    // ignore: discarded_futures
    session.dispose();
  });
  return session;
});
