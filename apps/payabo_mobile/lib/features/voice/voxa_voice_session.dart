// ignore_for_file: public_member_api_docs

import 'dart:async';
import 'dart:typed_data';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:mp_audio_stream/mp_audio_stream.dart';
import 'package:record/record.dart';

import '../../app/auth/auth_session_store.dart';
import '../../app/environment/app_environment.dart';
import '../../app/environment/environment_provider.dart';
import 'voxa_voice_client.dart';

/// Combines [VoxaVoiceClient] with a 16 kHz PCM mic recorder + 24 kHz PCM
/// streaming player. One [start]/[stop] cycle per conversation; the underlying
/// client is single-use so we build a fresh one on each [start].
class VoxaVoiceSession {
  VoxaVoiceSession({
    required VoxaVoiceClient Function() clientFactory,
    AudioRecorder? recorder,
    AudioStream? player,
  })  : _clientFactory = clientFactory,
        _recorder = recorder ?? AudioRecorder(),
        _player = player ?? getAudioStream();

  final VoxaVoiceClient Function() _clientFactory;
  final AudioRecorder _recorder;
  final AudioStream _player;

  VoxaVoiceClient? _activeClient;
  StreamSubscription<Uint8List>? _micSubscription;
  StreamSubscription<Uint8List>? _audioSubscription;
  StreamSubscription<VoxaVoiceEvent>? _eventsSubscription;
  StreamSubscription<VoxaConnectionState>? _stateSubscription;

  bool _playerInitialised = false;
  bool _running = false;

  // Captured on start so [_resetPlayerBuffer] can re-init with the same shape.
  int _playerChannels = 1;
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
  // local PCM queue drain. Must exceed [_playerBufferMilliSec] +
  // [_playerWaitingBufferMilliSec] or the bot's tail leaks back into STT.
  static const Duration _postBotStopHangover = Duration(milliseconds: 3300);

  // Drop mic frames within this window of the last inbound bot audio frame —
  // catches both pre-`speaking:true` audio and post-`speaking:false` queue
  // playback that the event gate misses.
  static const Duration _audioTailGuard = Duration(milliseconds: 1200);

  // Cloud TTS bursts faster than realtime; an undersized ring buffer drops
  // samples and speeds the voice up. 300 ms jitter buffer avoids underrun on
  // bursty network paths at the cost of a small start-of-utterance delay.
  static const int _playerBufferMilliSec = 3000;
  static const int _playerWaitingBufferMilliSec = 300;

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

    // Player first so the first inbound bot frame isn't dropped during setup.
    _playerChannels = 1;
    _playerSampleRate = botSampleRate;
    final int initResult = _player.init(
      channels: _playerChannels,
      sampleRate: _playerSampleRate,
      bufferMilliSec: _playerBufferMilliSec,
      waitingBufferMilliSec: _playerWaitingBufferMilliSec,
    );
    _playerInitialised = initResult == 0;
    if (!_playerInitialised) {
      throw StateError(
          'Failed to initialise PCM audio player (mp_audio_stream init returned $initResult).');
    }
    _player.resume();

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
          _botStoppedHangoverTimer = Timer(_postBotStopHangover, () {
            _botSpeaking = false;
          });
        }
      } else if (event is InterruptionEvent) {
        // Server-side VAD detected user barge-in. Flush queued audio so the
        // bot stops mid-word, and drop the tail guard so the user's next
        // utterance flows immediately.
        _resetPlayerBuffer();
        _botStoppedHangoverTimer?.cancel();
        _botStoppedHangoverTimer = null;
        _botSpeaking = false;
        _lastBotAudioAt = null;
      }
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

    // VoIP audio source + communication mode engage Android's hardware AEC.
    // `echoCancel: true` alone is just a hint — without these the platform
    // routes through the generic mic path with no echo cancellation and the
    // speaker bleeds straight back into STT. iOS handles AEC via AVAudioSession
    // automatically when `echoCancel: true` is set.
    final Stream<Uint8List> micStream;
    try {
      micStream = await _recorder.startStream(
        RecordConfig(
          encoder: AudioEncoder.pcm16bits,
          sampleRate: micSampleRate,
          numChannels: 1,
          echoCancel: true,
          noiseSuppress: true,
          autoGain: true,
          androidConfig: const AndroidRecordConfig(
            audioSource: AndroidAudioSource.voiceCommunication,
            audioManagerMode: AudioManagerMode.modeInCommunication,
          ),
        ),
      );
    } catch (err) {
      await _teardownAfterFailure();
      rethrow;
    }

    _micSubscription = micStream.listen(
      (Uint8List pcm) {
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

  /// Send `{type:"end"}`, close the WS, stop the recorder, uninit the player.
  /// Safe to call repeatedly.
  Future<void> stop() async {
    _running = false;
    _botSpeaking = false;
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

    if (_playerInitialised) {
      try {
        _player.uninit();
      } catch (_) {
        // already torn down
      }
      _playerInitialised = false;
    }
  }

  /// One-shot release; the session can't be reused after this.
  Future<void> dispose() async {
    await stop();
    await _recorder.dispose();
    await _eventsController.close();
    await _stateController.close();
  }

  // ── Internals ────────────────────────────────────────────────────────────

  // mp_audio_stream has no flush API; the package docs say a second `init()`
  // call uninits the previous device. Re-init with the same params lets the
  // next inbound frame play with no audible click on Android.
  void _resetPlayerBuffer() {
    if (!_playerInitialised) return;
    try {
      final int initResult = _player.init(
        channels: _playerChannels,
        sampleRate: _playerSampleRate,
        bufferMilliSec: _playerBufferMilliSec,
        waitingBufferMilliSec: _playerWaitingBufferMilliSec,
      );
      if (initResult != 0) {
        _playerInitialised = false;
        return;
      }
      _player.resume();
    } catch (_) {
      // Best-effort flush; next stop() will tear things down cleanly.
    }
  }

  void _onBotAudioFrame(Uint8List pcmBytes) {
    if (pcmBytes.isEmpty) return;
    if (!_playerInitialised) return;
    _lastBotAudioAt = _clock.elapsed;

    // Int16 LE PCM → Float32 [-1, 1] for mp_audio_stream. ByteData view keeps
    // us independent of host endianness.
    final int sampleCount = pcmBytes.length ~/ 2;
    if (sampleCount == 0) return;
    final Float32List floats = Float32List(sampleCount);
    final ByteData view = ByteData.sublistView(pcmBytes);
    for (int i = 0; i < sampleCount; i++) {
      floats[i] = view.getInt16(i * 2, Endian.little) / 0x8000;
    }
    _player.push(floats);
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

    if (_playerInitialised) {
      try {
        _player.uninit();
      } catch (_) {
        // already torn down
      }
      _playerInitialised = false;
    }
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
