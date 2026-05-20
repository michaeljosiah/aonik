// ignore_for_file: public_member_api_docs

import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:record/record.dart';

import '../../app/auth/auth_session_store.dart';
import '../../app/environment/app_environment.dart';
import '../../app/environment/environment_provider.dart';
import 'voice_pcm_player.dart';
import 'voxa_voice_client.dart';

/// Combines [VoxaVoiceClient] with a 16 kHz PCM mic recorder + 24 kHz PCM
/// streaming player. One [start]/[stop] cycle per conversation; the underlying
/// client is single-use so we build a fresh one on each [start].
///
/// Android playback uses an app-owned AudioTrack bridge because SoLoud's
/// miniaudio/AAudio stream can wedge after callback-driven stop/start cycles.
/// Other platforms keep the SoLoud fallback behind [VoicePcmPlayer]. Echo
/// prevention remains playback-position driven: mic frames are dropped while
/// queued bot audio or the speaker latency tail is still active.
class VoxaVoiceSession {
  VoxaVoiceSession({
    required VoxaVoiceClient Function() clientFactory,
    AudioRecorder? recorder,
    VoicePcmPlayer? player,
  })  : _clientFactory = clientFactory,
        _recorder = recorder ?? AudioRecorder(),
        _player = player ?? createVoicePcmPlayer();

  final VoxaVoiceClient Function() _clientFactory;
  final AudioRecorder _recorder;
  final VoicePcmPlayer _player;

  VoxaVoiceClient? _activeClient;
  StreamSubscription<Uint8List>? _micSubscription;
  StreamSubscription<Uint8List>? _audioSubscription;
  StreamSubscription<VoxaVoiceEvent>? _eventsSubscription;
  StreamSubscription<VoxaConnectionState>? _stateSubscription;

  bool _running = false;

  /// User-driven mic gate from the voice stage's mute button. Independent of
  /// playback gating — both must be open for a mic frame to reach the WSS.
  bool _userMuted = false;

  /// Running total of audio (in seconds) we've handed to the player across this
  /// session — every bot audio frame plus every silence tail-pad. Compared
  /// against the playback head to decide whether the engine is still
  /// processing bot audio.
  double _totalPushedSeconds = 0;

  /// Monotonic time at which the engine first reported "consumed everything
  /// I was given". From this moment the speaker's DAC pipeline is still
  /// flushing — we keep the mic gate closed for [_speakerOutputLatencySeconds]
  /// after this to cover the hardware delay. Cleared whenever fresh audio
  /// is pushed and the engine has more to consume.
  final Stopwatch _clock = Stopwatch()..start();
  Duration? _engineDrainedAt;

  bool _playerActive = false;
  bool _playbackPollInFlight = false;
  Timer? _playbackPollTimer;
  int _agentResponseSequence = 0;
  _AgentPlaybackSegment? _activeAgentResponse;
  final List<_AgentPlaybackSegment> _pendingPlaybackSegments =
      <_AgentPlaybackSegment>[];

  /// Captured on start so the silence tail-pad can synthesise the right
  /// number of zero samples to push.
  int _playerSampleRate = 24000;

  /// Upper bound for queued PCM. 100 MB is roughly 34 minutes of mono s16le
  /// 24 kHz audio, far beyond a realistic voice session.
  static const int _streamMaxBufferBytes = 100 * 1024 * 1024;

  /// Initial jitter buffer. Android AudioTrack uses this as its native buffer
  /// size; SoLoud fallback uses it as its buffer-stream resume threshold.
  static const Duration _startupBuffer = Duration(milliseconds: 1500);

  /// Hardware DAC + audio HAL latency between the player reporting a sample as
  /// "consumed" and that sample physically exiting the speaker. Empirical
  /// value for Android (AAudio / OpenSL ES); iOS typically sees lower.
  /// Without this margin the mic gate opens slightly before the speaker's
  /// last word actually finishes, and Whisper re-transcribes it as user
  /// input. 150 ms is wide enough to cover the hardware delay on every
  /// device we've tested, narrow enough that it doesn't perceptibly slow
  /// the user's turn.
  static const double _speakerOutputLatencySeconds = 0.15;

  /// Silence pushed to the stream on every bot `speaking:false` so the buffer
  /// never goes empty exactly at the boundary between sentences — covers the
  /// 200–500 ms TTS request latency for the next sentence on the chained
  /// pipeline so playback flows continuously instead of pause-and-rebuffer.
  /// This also keeps playback gating active through the sentence boundary,
  /// so the mic gate stays closed across the gap (no echo from the trailing
  /// edge leaking in).
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
    // Starting the recorder first puts the OS into voice-processing mode,
    // which engages platform AEC before playback begins.
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
          // applies platform-level dynamic-range processing; AGC on top
          // amplifies any speaker bleed that slips past AEC up to Whisper-
          // audible level. Off → residual bleed stays small.
          autoGain: false,
          androidConfig: const AndroidRecordConfig(
            audioSource: AndroidAudioSource.voiceCommunication,
            audioManagerMode: AudioManagerMode.modeInCommunication,
            speakerphone: true,
          ),
        ),
      );
    } catch (err) {
      rethrow;
    }

    // ── Step 2: PCM player ──
    _playerSampleRate = botSampleRate;
    try {
      await _player.start(
        sampleRate: botSampleRate,
        volume: _playerVolume,
        maxBufferBytes: _streamMaxBufferBytes,
        startupBuffer: _startupBuffer,
      );
      _totalPushedSeconds = 0;
      _engineDrainedAt = null;
      _playerActive = false;
      _agentResponseSequence = 0;
      _activeAgentResponse = null;
      _pendingPlaybackSegments.clear();
      _startPlaybackPoller();
    } catch (err) {
      await _disposeStream();
      await _stopRecorder();
      throw StateError('Failed to start PCM player: $err');
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
      // The only thing the speaking event drives in the session is the
      // sentence-boundary silence pad — the mic gate is now playback-state
      // driven, so we don't care about wire `speaking:true`/`speaking:false`
      // for gating purposes. Pad on `speaking:false` keeps playback fed across
      // the chained TTS sentence gap and keeps the mic gate closed.
      if (event is BotTextEvent) {
        final _AgentPlaybackSegment segment = _ensureAgentResponse('bot text');
        segment.textChunks += 1;
        segment.textChars += event.text.length;
      }
      if (event is SpeakingEvent && event.who == 'bot') {
        if (event.started) {
          final _AgentPlaybackSegment segment =
              _ensureAgentResponse('bot speaking started');
          segment.speakingStartedCount += 1;
          _log('agent response #${segment.id} speaking started');
        } else {
          final _AgentPlaybackSegment segment =
              _ensureAgentResponse('bot speaking stopped');
          segment.speakingStopped = true;
          _log(
            'agent response #${segment.id} speaking stopped; '
            'textChunks=${segment.textChunks} textChars=${segment.textChars} '
            'audioFrames=${segment.audioFrames} audioBytes=${segment.audioBytes} '
            'queued=${segment.queuedDurationSeconds.toStringAsFixed(3)}s',
          );
          if (segment.audioBytes == 0) {
            _log(
                'agent response #${segment.id} has text/speaking but no PCM frames yet');
          }
          _padPlayerTail(segment);
        }
      }
      // Barge-in is intentionally not supported in v1 — forward the event
      // for UI consumption but take no session-side action. The mic gate
      // stays closed until playback actually finishes.
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
        if (_playerActive) return;
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
  /// PCM player. Safe to call repeatedly.
  Future<void> stop() async {
    _running = false;
    _userMuted = false;
    _totalPushedSeconds = 0;
    _engineDrainedAt = null;
    _playerActive = false;
    _agentResponseSequence = 0;
    _activeAgentResponse = null;
    _pendingPlaybackSegments.clear();
    _stopPlaybackPoller();

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

  /// One-shot release; the session can't be reused after this.
  Future<void> dispose() async {
    await stop();
    await _recorder.dispose();
    await _eventsController.close();
    await _stateController.close();
  }

  // ── Internals ────────────────────────────────────────────────────────────

  /// Push 500 ms of silent samples to the stream at the end of each bot turn
  /// (or each TTS sentence in the chained pipeline, where `speaking:false`
  /// fires per sentence). Two purposes:
  ///   1. Bridges the next-sentence TTS request latency so playback flows
  ///      continuously instead of having to pause-and-rebuffer.
  ///   2. Keeps the mic gate closed through the gap (the silence counts
  ///      towards `_totalPushedSeconds` so the engine has audio to consume
  ///      while the next sentence is being synthesised).
  void _padPlayerTail(_AgentPlaybackSegment segment) {
    // bytes = sampleRate × seconds × 2 bytes per s16 sample, mono.
    final int byteCount = (_playerSampleRate * _tailPadSeconds * 2).round();
    if (byteCount <= 0) return;
    _queuePlayerAudio(
      Uint8List(byteCount),
      debugName: 'tail pad',
      segment: segment,
      isTailPad: true,
    );
  }

  void _onBotAudioFrame(Uint8List pcmBytes) {
    if (pcmBytes.isEmpty) return;
    final _AgentPlaybackSegment segment = _ensureAgentResponse('bot audio');
    segment.audioFrames += 1;
    segment.audioBytes += pcmBytes.length;
    _queuePlayerAudio(pcmBytes, debugName: 'bot audio', segment: segment);
  }

  void _queuePlayerAudio(
    Uint8List pcmBytes, {
    required String debugName,
    required _AgentPlaybackSegment segment,
    bool isTailPad = false,
  }) {
    if (!_player.isStarted || pcmBytes.isEmpty) return;

    final double seconds = pcmBytes.length / (_playerSampleRate * 2);
    segment.queuedStartSeconds ??= _totalPushedSeconds;
    segment.queuedEndSeconds = _totalPushedSeconds + seconds;
    if (isTailPad) {
      segment.tailBytes += pcmBytes.length;
    }
    _totalPushedSeconds += seconds;
    _engineDrainedAt = null;
    _playerActive = true;

    unawaited(_player.push(pcmBytes).catchError((Object err) {
      _totalPushedSeconds -= seconds;
      if (segment.queuedEndSeconds > seconds) {
        segment.queuedEndSeconds -= seconds;
      }
      _eventsController.add(VoxaVoiceEvent.error(
        message: 'Failed to enqueue $debugName frame: $err',
        code: 'audio-enqueue',
      ));
    }));
  }

  void _startPlaybackPoller() {
    _playbackPollTimer?.cancel();
    _playbackPollTimer = Timer.periodic(const Duration(milliseconds: 50), (_) {
      unawaited(_refreshPlaybackState());
    });
    unawaited(_refreshPlaybackState());
  }

  void _stopPlaybackPoller() {
    _playbackPollTimer?.cancel();
    _playbackPollTimer = null;
    _playbackPollInFlight = false;
  }

  Future<void> _refreshPlaybackState() async {
    if (_playbackPollInFlight) return;
    if (!_player.isStarted) {
      _playerActive = false;
      return;
    }

    _playbackPollInFlight = true;
    try {
      final Duration played = await _player.getPosition();
      final double playedSec = played.inMicroseconds / 1e6;
      _logPlayedBackAgentResponses(playedSec);
      final double remainingSec = _totalPushedSeconds - playedSec;
      if (remainingSec > 0.001) {
        _engineDrainedAt = null;
        _playerActive = true;
        return;
      }

      _engineDrainedAt ??= _clock.elapsed;
      _playerActive = _clock.elapsed - _engineDrainedAt! <
          Duration(
            milliseconds: (_speakerOutputLatencySeconds * 1000).round(),
          );
    } catch (_) {
      // Fail closed while the native player is starting or stopping.
      _playerActive = true;
    } finally {
      _playbackPollInFlight = false;
    }
  }

  Future<void> _disposeStream() async {
    try {
      await _player.dispose();
    } catch (_) {
      // Best-effort — native player may already be tearing down.
    }
  }

  Future<void> _teardownAfterFailure() async {
    await _audioSubscription?.cancel();
    _audioSubscription = null;
    await _eventsSubscription?.cancel();
    _eventsSubscription = null;
    await _stateSubscription?.cancel();
    _stateSubscription = null;

    _stopPlaybackPoller();
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

  _AgentPlaybackSegment _ensureAgentResponse(String reason) {
    final _AgentPlaybackSegment? current = _activeAgentResponse;
    if (current != null && !current.speakingStopped) {
      return current;
    }

    final _AgentPlaybackSegment next = _AgentPlaybackSegment(
      id: ++_agentResponseSequence,
    );
    _activeAgentResponse = next;
    _pendingPlaybackSegments.add(next);
    _log('agent response #${next.id} opened by $reason');
    return next;
  }

  void _logPlayedBackAgentResponses(double playedSec) {
    while (_pendingPlaybackSegments.isNotEmpty) {
      final _AgentPlaybackSegment segment = _pendingPlaybackSegments.first;
      if (segment.queuedEndSeconds <= 0) {
        return;
      }
      if (playedSec + 0.02 < segment.queuedEndSeconds) {
        return;
      }

      _pendingPlaybackSegments.removeAt(0);
      _log(
        'agent response #${segment.id} playback consumed; '
        'textChunks=${segment.textChunks} textChars=${segment.textChars} '
        'audioFrames=${segment.audioFrames} audioBytes=${segment.audioBytes} '
        'tailBytes=${segment.tailBytes} '
        'queued=${segment.queuedDurationSeconds.toStringAsFixed(3)}s '
        'played=${playedSec.toStringAsFixed(3)}s',
      );
    }
  }

  void _log(String message) {
    if (!kDebugMode) return;
    debugPrint('[VoxaVoiceSession] $message');
  }
}

class _AgentPlaybackSegment {
  _AgentPlaybackSegment({required this.id});

  final int id;
  int textChunks = 0;
  int textChars = 0;
  int audioFrames = 0;
  int audioBytes = 0;
  int tailBytes = 0;
  int speakingStartedCount = 0;
  bool speakingStopped = false;
  double? queuedStartSeconds;
  double queuedEndSeconds = 0;

  double get queuedDurationSeconds {
    final double? start = queuedStartSeconds;
    if (start == null) return 0;
    return queuedEndSeconds - start;
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
