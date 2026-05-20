import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:flutter_soloud/flutter_soloud.dart';

/// Minimal PCM sink used by voice mode.
abstract interface class VoicePcmPlayer {
  bool get isStarted;

  Future<void> start({
    required int sampleRate,
    required double volume,
    required int maxBufferBytes,
    required Duration startupBuffer,
  });

  Future<void> push(Uint8List pcmBytes);

  Future<Duration> getPosition();

  Future<void> dispose();
}

VoicePcmPlayer createVoicePcmPlayer() {
  if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) {
    if (kDebugMode) {
      debugPrint('[VoicePcmPlayer] using Android AudioTrack bridge');
    }
    return const AndroidAudioTrackPcmPlayer();
  }

  if (kDebugMode) {
    debugPrint('[VoicePcmPlayer] using SoLoud fallback');
  }
  return SoLoudPcmPlayer(SoLoud.instance);
}

/// Android uses a tiny app-owned AudioTrack bridge instead of SoLoud.
///
/// AudioTrack MODE_STREAM is the platform-native fit for app-supplied PCM:
/// a dedicated native writer thread blocks in AudioTrack.write() when the
/// hardware buffer is full, so playback gets back-pressure without AAudio
/// stop/start calls from an audio callback.
class AndroidAudioTrackPcmPlayer implements VoicePcmPlayer {
  const AndroidAudioTrackPcmPlayer();

  static const MethodChannel _channel =
      MethodChannel('payabo/voice_pcm_player');

  static bool _started = false;

  @override
  bool get isStarted => _started;

  @override
  Future<void> start({
    required int sampleRate,
    required double volume,
    required int maxBufferBytes,
    required Duration startupBuffer,
  }) async {
    if (kDebugMode) {
      debugPrint(
        '[VoicePcmPlayer] Android start sampleRate=$sampleRate '
        'bufferMs=${startupBuffer.inMilliseconds}',
      );
    }
    await _channel.invokeMethod<void>('start', <String, Object>{
      'sampleRate': sampleRate,
      'volume': volume,
      'maxBufferBytes': maxBufferBytes,
      'bufferMs': startupBuffer.inMilliseconds,
    });
    _started = true;
  }

  @override
  Future<void> push(Uint8List pcmBytes) async {
    if (!_started || pcmBytes.isEmpty) return;
    final bool accepted = await _channel.invokeMethod<bool>(
          'write',
          <String, Object>{'data': pcmBytes},
        ) ??
        false;
    if (!accepted) {
      throw StateError('Android AudioTrack queue is full.');
    }
  }

  @override
  Future<Duration> getPosition() async {
    if (!_started) return Duration.zero;
    final double seconds = await _channel.invokeMethod<double>('position') ?? 0;
    return Duration(microseconds: (seconds * 1000000).round());
  }

  @override
  Future<void> dispose() async {
    if (!_started) return;
    _started = false;
    if (kDebugMode) {
      debugPrint('[VoicePcmPlayer] Android stop');
    }
    await _channel.invokeMethod<void>('stop');
  }
}

/// Non-Android fallback. Keeps existing platform coverage while Android uses
/// AudioTrack for the problematic continuous PCM path.
class SoLoudPcmPlayer implements VoicePcmPlayer {
  SoLoudPcmPlayer(this._player);

  final SoLoud _player;
  AudioSource? _source;
  SoundHandle? _handle;

  @override
  bool get isStarted => _source != null && _handle != null;

  @override
  Future<void> start({
    required int sampleRate,
    required double volume,
    required int maxBufferBytes,
    required Duration startupBuffer,
  }) async {
    if (!_player.isInitialized) {
      await _player.init(sampleRate: sampleRate);
    }

    _source = _player.setBufferStream(
      maxBufferSizeBytes: maxBufferBytes,
      sampleRate: sampleRate,
      channels: Channels.mono,
      format: BufferType.s16le,
      bufferingType: BufferingType.preserved,
      bufferingTimeNeeds: startupBuffer.inMilliseconds / 1000,
    );
    _handle = await _player.play(_source!, volume: volume);
  }

  @override
  Future<void> push(Uint8List pcmBytes) async {
    final AudioSource? source = _source;
    if (source == null || pcmBytes.isEmpty) return;
    _player.addAudioDataStream(source, pcmBytes);
  }

  @override
  Future<Duration> getPosition() async {
    final SoundHandle? handle = _handle;
    if (handle == null || !_player.isInitialized) return Duration.zero;
    return _player.getPosition(handle);
  }

  @override
  Future<void> dispose() async {
    _handle = null;
    _source = null;
    if (!_player.isInitialized) return;
    try {
      await _player.disposeAllSources();
    } finally {
      _player.deinit();
    }
  }
}
