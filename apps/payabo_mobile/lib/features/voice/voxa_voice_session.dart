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
/// Playback uses [SoLoud]'s buffer-stream API in [BufferingType.preserved]
/// mode. (We initially used [BufferingType.released], but that variant has
/// an internal "stream ended" state that gets set when the play head catches
/// up to the write head — between turns the bot audio is fully drained and
/// the source quietly stops accepting new data, which produces the
/// "last turn was silent" symptom in a multi-turn session.) Preserved
/// avoids that gotcha at the cost of growing memory for the duration of
/// the session, bounded by `maxBufferSizeBytes` (100 MB ≈ 34 min of mono
/// s16le 24 kHz audio — comfortably more than any realistic voice call,
/// and freed on [stop] when we tear down the engine).
///
/// Echo prevention: the mic listener drops frames whenever there's still
/// queued bot audio yet to be played. We track [_totalPushedSeconds] (a
/// running total of what we've pushed, including the silence tail-pad) and
/// compare against `getPosition(handle)` on every mic frame. When the
/// playback head reaches everything we've pushed, the gate opens — driven
/// by the engine's actual playback position, not wire-event timing. A small
/// [_speakerOutputLatencySeconds] grace covers the hardware DAC delay
/// between SoLoud reporting the position and the speaker physically
/// finishing.
///
/// We deliberately do NOT use `setBufferStream(onBuffering: ...)`. That
/// callback's FFI bridge is invoked one last time during stream teardown,
/// after Dart has already finalised the closure — fatal abort with
/// "Callback invoked after it has been deleted". Polling `getPosition`
/// from the mic listener avoids the callback lifecycle hazard entirely.
///
/// We also fully [deinit] the SoLoud engine on every [stop] and re-init on
/// the next [start]. Without this, SoLoud + AAudio accumulate state across
/// sessions ("AAudioStream already started" warnings, "stream cannot be
/// stopped from a callback" errors) which can starve later turns of audio.
/// Init costs ~100 ms — invisible against the user explicitly opening
/// voice mode.
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

  /// The active SoLoud stream source + handle for this session. Created in
  /// [start], torn down in [stop]. We fully deinit the engine on [stop] —
  /// see class doc for why.
  AudioSource? _streamSource;
  SoundHandle? _streamHandle;

  bool _running = false;

  /// User-driven mic gate from the voice stage's mute button. Independent of
  /// [_isPlayerActive] — both must be false for a mic frame to reach the WSS.
  bool _userMuted = false;

  /// Running total of audio (in seconds) we've handed to SoLoud across this
  /// session — every bot audio frame plus every silence tail-pad. Compared
  /// against `getStreamTimeConsumed` to decide whether the engine is still
  /// processing bot audio.
  double _totalPushedSeconds = 0;

  /// Wall-clock time at which the engine first reported "consumed everything
  /// I was given". From this moment the speaker's DAC pipeline is still
  /// flushing — we keep the mic gate closed for [_speakerOutputLatencySeconds]
  /// after this to cover the hardware delay. Cleared whenever fresh audio
  /// is pushed and the engine has more to consume.
  DateTime? _engineDrainedAt;

  /// Captured on start so the silence tail-pad can synthesise the right
  /// number of zero samples to push.
  int _playerSampleRate = 24000;

  /// SoLoud's [setBufferStream.maxBufferSizeBytes] cap. With
  /// [BufferingType.preserved] this is also the upper bound on RAM the
  /// stream holds at any instant, since played samples aren't freed.
  /// 100 MB ≈ 34 min of continuous s16le 24 kHz mono audio — comfortably
  /// more than any realistic voice session and freed entirely on [stop]
  /// when the engine is deinitialised.
  static const int _streamMaxBufferBytes = 100 * 1024 * 1024;

  /// Seconds of buffered audio SoLoud needs accumulated before resuming
  /// playback after an underrun. Doubles as the *minimum window* the mic
  /// gate stays open between sentences — if the buffer empties for less
  /// than this, SoLoud waits to refill so playback stays smooth and the
  /// gate doesn't flap open/closed.
  static const double _bufferingTimeNeedsSeconds = 0.08;

  /// Hardware DAC + audio HAL latency between SoLoud reporting a sample as
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
  /// This also keeps `_isPlayerActive` true through the sentence boundary,
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
    // Starting the recorder flips the OS audio session into voice-processing
    // mode (AVAudioSession `playAndRecord` + voice-chat on iOS;
    // `MODE_IN_COMMUNICATION` on Android), which is what engages hardware
    // AEC. Doing this before SoLoud opens its output stream ensures the
    // platform AEC pipeline is active when SoLoud's audio reaches the
    // speaker — without this the bleed leaks straight into STT.
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
          ),
        ),
      );
    } catch (err) {
      rethrow;
    }

    // ── Step 2: SoLoud player ──
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
      // No `onBuffering` callback — see class doc. We poll `getPosition`
      // instead. BufferingType.preserved avoids the "stream auto-ends on
      // drain" gotcha that broke later turns with released mode.
      _streamSource = _player.setBufferStream(
        maxBufferSizeBytes: _streamMaxBufferBytes,
        sampleRate: botSampleRate,
        channels: Channels.mono,
        format: BufferType.s16le,
        bufferingType: BufferingType.preserved,
        bufferingTimeNeeds: _bufferingTimeNeedsSeconds,
      );
      _streamHandle = await _player.play(_streamSource!, volume: _playerVolume);
      _totalPushedSeconds = 0;
      _engineDrainedAt = null;
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
      // The only thing the speaking event drives in the session is the
      // sentence-boundary silence pad — the mic gate is now playback-state
      // driven, so we don't care about wire `speaking:true`/`speaking:false`
      // for gating purposes. Pad on `speaking:false` keeps the SoLoud queue
      // fed across the chained TTS sentence gap so playback stays continuous
      // (and `_isPlayerActive` stays true through the boundary).
      if (event is SpeakingEvent && event.who == 'bot' && !event.started) {
        _padPlayerTail();
      }
      // Barge-in is intentionally not supported in v1 — forward the event
      // for UI consumption but take no session-side action. The mic gate
      // stays closed until SoLoud actually finishes playing.
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
        if (_isPlayerActive) return;
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

  /// Whether SoLoud is currently still outputting bot audio (or has audio
  /// queued and pending). Two-stage logic:
  ///
  ///   1. Playback head is behind what we've pushed → gate closed.
  ///   2. Playback head has caught up, but the speaker's DAC pipeline is
  ///      still flushing the last [_speakerOutputLatencySeconds] of audio
  ///      → gate stays closed until that latency window elapses.
  ///
  /// Called on every mic frame; `getPosition` is an FFI hop of ~microseconds
  /// — much cheaper than the wire-event gates it replaces. The state mutation
  /// (recording / clearing `_engineDrainedAt`) is idempotent and stays inside
  /// this single isolate, so making it a getter is a tiny lie but works fine
  /// in practice.
  bool get _isPlayerActive {
    final SoundHandle? handle = _streamHandle;
    if (handle == null) return false;
    if (!_player.isInitialized) return false;
    try {
      final Duration played = _player.getPosition(handle);
      final double playedSec = played.inMicroseconds / 1e6;
      final double remainingSec = _totalPushedSeconds - playedSec;
      if (remainingSec > 0.001) {
        // Engine still has audio to play. Reset the drain marker — we'll
        // re-stamp it once playback catches up.
        _engineDrainedAt = null;
        return true;
      }
      // Playback head has caught up. The speaker's DAC is still flushing
      // for ~150 ms — keep the gate closed until then.
      _engineDrainedAt ??= DateTime.now();
      final Duration sinceDrain = DateTime.now().difference(_engineDrainedAt!);
      const double latencyWindowMs = _speakerOutputLatencySeconds * 1000;
      return sinceDrain.inMilliseconds < latencyWindowMs;
    } catch (_) {
      // Engine in a transitional state (init/teardown). Fail closed — keep
      // the gate closed rather than risk leaking bot audio through.
      return true;
    }
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
  /// SoLoud stream and deinit the engine. Safe to call repeatedly.
  Future<void> stop() async {
    _running = false;
    _userMuted = false;
    _totalPushedSeconds = 0;
    _engineDrainedAt = null;

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

    // Deinit the engine between sessions for clean state. Without this,
    // AAudio + SoLoud state from previous sessions accumulates and starves
    // later turns of audio. Re-init on next [start] takes ~100 ms — invisible
    // against the user explicitly opening voice mode.
    if (_player.isInitialized) {
      try {
        _player.deinit();
      } catch (_) {
        // Engine may already be in a teardown state — ignore.
      }
    }
  }

  /// One-shot release; the session can't be reused after this. [stop] above
  /// also deinitialises the SoLoud engine, so there's nothing extra to free
  /// at the engine level.
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
  void _padPlayerTail() {
    final AudioSource? source = _streamSource;
    if (source == null) return;
    // bytes = sampleRate × seconds × 2 bytes per s16 sample, mono.
    final int byteCount = (_playerSampleRate * _tailPadSeconds * 2).round();
    if (byteCount <= 0) return;
    final Uint8List silence = Uint8List(byteCount);
    try {
      _player.addAudioDataStream(source, silence);
      _totalPushedSeconds += byteCount / (_playerSampleRate * 2);
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
    try {
      // SoLoud accepts raw s16le PCM directly — no Float32 conversion needed.
      _player.addAudioDataStream(source, pcmBytes);
      _totalPushedSeconds += pcmBytes.length / (_playerSampleRate * 2);
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
  /// times. Doesn't touch the engine itself — that's done in [stop].
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
