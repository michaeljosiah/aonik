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

/// High-level voice session that combines [VoxaVoiceClient] with a raw-PCM mic recorder
/// and a streaming PCM player. The session is the unit a UI talks to — `start` /
/// `stop` / observe streams — without caring about the mic plugin or audio output plumbing
/// directly.
///
/// **Audio contract (spec 024 chained pipeline):**
/// * Mic capture: 16-bit signed LE PCM, 16 kHz, mono. Matches `record.AudioRecorder`
///   with `AudioEncoder.pcm16bits` and the AONIK WSS pipeline's STT expectation.
/// * Bot output: 16-bit signed LE PCM, 24 kHz, mono (OpenAI / Azure / ElevenLabs /
///   Mistral default sink rate). Converted to Float32 in `[-1, 1]` for the
///   `mp_audio_stream` player which accepts normalized floats.
///
/// **Lifecycle:** one `start()` per conversation; call `stop()` to tear down. Re-using a
/// session after `stop()` works but the underlying [VoxaVoiceClient] is single-use — the
/// session lazily constructs a fresh one on each `start`.
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

  final StreamController<VoxaVoiceEvent> _eventsController =
      StreamController<VoxaVoiceEvent>.broadcast();
  final StreamController<VoxaConnectionState> _stateController =
      StreamController<VoxaConnectionState>.broadcast();

  /// Typed events from the underlying client, surfaced through the session so widgets
  /// only have to subscribe to one stream.
  Stream<VoxaVoiceEvent> get events => _eventsController.stream;

  /// Connection state changes from the underlying client.
  Stream<VoxaConnectionState> get stateChanges => _stateController.stream;

  /// True while [start] has succeeded and [stop] hasn't yet been called.
  bool get isRunning => _running;

  /// Connection-level state. Idle when no session is active or the last one ended.
  VoxaConnectionState get connectionState =>
      _activeClient?.state ?? VoxaConnectionState.idle;

  /// Start a new voice session. Throws if mic permission is denied or the WebSocket
  /// fails to open.
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

    // Initialise the player BEFORE connecting so the first inbound bot frame doesn't
    // get dropped while we're still setting up. `init` returns 0 on success per the
    // mp_audio_stream contract; non-zero indicates platform-level failure (e.g. no
    // audio device).
    final int initResult = _player.init(
      channels: 1,
      sampleRate: botSampleRate,
      bufferMilliSec: 3000,
      waitingBufferMilliSec: 100,
    );
    _playerInitialised = initResult == 0;
    if (!_playerInitialised) {
      throw StateError(
          'Failed to initialise PCM audio player (mp_audio_stream init returned $initResult).');
    }
    _player.resume();

    // Construct a fresh client per session — VoxaVoiceClient.connect() is single-shot.
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
    _eventsSubscription = client.events.listen(_eventsController.add);
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

    // Now that the WS is up, start mic capture. The recorder's stream emits chunks at
    // its own native cadence — typically 20-50 ms — which the WebSocket happily
    // forwards as binary frames of arbitrary size.
    final Stream<Uint8List> micStream;
    try {
      micStream = await _recorder.startStream(
        RecordConfig(
          encoder: AudioEncoder.pcm16bits,
          sampleRate: micSampleRate,
          numChannels: 1,
          // Echo cancellation + noise suppression mirror the chat_voice_service defaults.
          // Without echo cancellation the recorder picks up the bot's own playback,
          // causing a feedback loop that confuses Whisper.
          echoCancel: true,
          noiseSuppress: true,
          autoGain: true,
        ),
      );
    } catch (err) {
      await _teardownAfterFailure();
      rethrow;
    }

    _micSubscription = micStream.listen(
      (Uint8List pcm) {
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

  /// Stop the session. Sends `{type:"end"}` to the server, closes the WebSocket, stops
  /// the recorder, and uninits the player. Safe to call repeatedly.
  Future<void> stop() async {
    _running = false;

    await _micSubscription?.cancel();
    _micSubscription = null;

    try {
      await _recorder.stop();
    } catch (_) {
      // Best-effort — recorder may already be stopped on permission revoke etc.
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

  /// Release all resources. After this the session can't be reused — construct a new
  /// one via the provider.
  Future<void> dispose() async {
    await stop();
    await _recorder.dispose();
    await _eventsController.close();
    await _stateController.close();
  }

  // ── Internals ────────────────────────────────────────────────────────────────────

  void _onBotAudioFrame(Uint8List pcmBytes) {
    if (pcmBytes.isEmpty) return;
    if (!_playerInitialised) return;

    // Convert Int16 PCM bytes → Float32 in [-1, 1] for the mp_audio_stream player.
    // pcmBytes is 16-bit signed LE PCM (mono). We use a ByteData view to avoid
    // depending on the platform's host endianness — Whisper / Azure / Mistral all
    // emit little-endian and so do we.
    final int sampleCount = pcmBytes.length ~/ 2;
    if (sampleCount == 0) return;

    final Float32List floats = Float32List(sampleCount);
    final ByteData view = ByteData.sublistView(pcmBytes);
    for (int i = 0; i < sampleCount; i++) {
      final int sample = view.getInt16(i * 2, Endian.little);
      floats[i] = sample / 0x8000;
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

/// Per-screen [VoxaVoiceSession] factory. Each `ref.watch` returns a fresh session;
/// the autoDispose contract tears the session (and its underlying recorder / player /
/// WS client) down when no widget is listening, so navigating away mid-conversation
/// stops the mic immediately.
///
/// The factory constructs a *fresh* [VoxaVoiceClient] on each `session.start()` —
/// VoxaVoiceClient is single-use (its broadcast controllers and channel state don't
/// reset across connect cycles), so reusing one instance across multiple sessions
/// would mix old subscribers with new sessions. We resolve config + auth from
/// Riverpod up-front so the factory closure stays pure.
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
  // Fire-and-forget — the session's own dispose is fully async and can wait on the
  // recorder shutting down; the provider can't await async work in onDispose. The
  // ignore is for the lint that would otherwise prefer the tearoff form — we use a
  // lambda here so the comment above stays adjacent to the call.
  // ignore: unnecessary_lambdas
  ref.onDispose(() {
    // ignore: discarded_futures
    session.dispose();
  });
  return session;
});
