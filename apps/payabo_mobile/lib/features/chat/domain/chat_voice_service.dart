import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_tts/flutter_tts.dart';
import 'package:speech_to_text/speech_to_text.dart';

typedef ChatVoiceResultCallback = void Function(String text, bool isFinal);
typedef ChatVoiceStatusCallback = void Function(String status);
typedef ChatVoiceErrorCallback = void Function(String message);
typedef ChatVoiceProgressCallback = void Function(
    String text, int startOffset, int endOffset, String word);

final Provider<ChatVoiceService> chatVoiceServiceProvider =
    Provider<ChatVoiceService>((Ref ref) {
  return DeviceChatVoiceService();
});

abstract class ChatVoiceService {
  Future<bool> initialize();

  Future<void> startThinkingLoop();

  Future<void> stopThinkingLoop();

  Future<bool> startListening({
    required ChatVoiceResultCallback onResult,
    ChatVoiceStatusCallback? onStatus,
    ChatVoiceErrorCallback? onError,
    String? localeTag,
  });

  Future<void> stopListening();

  Future<void> speak(
    String text, {
    VoidCallback? onStart,
    VoidCallback? onComplete,
    VoidCallback? onCancel,
    ChatVoiceProgressCallback? onProgress,
    ChatVoiceErrorCallback? onError,
    String? localeTag,
  });

  Future<void> stopSpeaking();

  Future<void> dispose();
}

class DeviceChatVoiceService implements ChatVoiceService {
  static const Duration _postTtsListenDelay = Duration(milliseconds: 450);
  static const Duration _recognizerResetDelay = Duration(milliseconds: 250);
  static const Duration _listenActivationWindow = Duration(milliseconds: 900);
  static const String _logPrefix = '[VoiceService]';
  static const Duration _thinkingLoopFadeStep = Duration(milliseconds: 45);
  static const int _thinkingLoopFadeSteps = 6;
  static const double _thinkingLoopTargetVolume = 0.45;

  final SpeechToText _speechToText = SpeechToText();
  final FlutterTts _flutterTts = FlutterTts();
  final AudioPlayer _thinkingLoopPlayer = AudioPlayer();

  ChatVoiceStatusCallback? _activeSpeechStatusCallback;
  ChatVoiceErrorCallback? _activeSpeechErrorCallback;
  ChatVoiceErrorCallback? _onTtsError;
  VoidCallback? _onSpeakStart;
  VoidCallback? _onSpeakComplete;
  VoidCallback? _onSpeakCancel;
  ChatVoiceProgressCallback? _onSpeakProgress;
  bool _initialized = false;
  bool _thinkingLoopPrepared = false;
  bool _thinkingLoopActive = false;
  bool _thinkingLoopStopping = false;
  double _thinkingLoopVolume = 0;
  int _listenSessionId = 0;
  int? _activeListenSessionId;
  int _speakSessionId = 0;
  int? _activeSpeakSessionId;
  int? _handledSpeakTerminalSessionId;
  bool _ttsStopRequested = false;

  @override
  Future<bool> initialize() async {
    if (_initialized) {
      _log('initialize skipped: already initialized');
      return true;
    }

    _log('initialize start');

    final bool speechReady = await _speechToText.initialize(
      onStatus: (String status) {
        _log('stt status: $status');
        _activeSpeechStatusCallback?.call(status);
      },
      onError: (error) {
        _log('stt error: ${error.errorMsg}');
        _activeSpeechErrorCallback?.call(error.errorMsg);
      },
      debugLogging: false,
    );

    await _flutterTts.awaitSpeakCompletion(true);
    await _flutterTts.setLanguage('en-US');
    await _flutterTts.setSpeechRate(0.44);
    await _flutterTts.setPitch(1.0);
    await _flutterTts.setVolume(1.0);

    await _thinkingLoopPlayer.setReleaseMode(ReleaseMode.loop);
    await _thinkingLoopPlayer.setVolume(0);
    _thinkingLoopVolume = 0;

    _flutterTts.setStartHandler(_handleTtsStart);
    _flutterTts.setCompletionHandler(
      () => _handleTtsTerminalEvent(source: 'completion', canceled: false),
    );
    _flutterTts.setCancelHandler(
      () => _handleTtsTerminalEvent(source: 'cancel', canceled: true),
    );
    _flutterTts
        .setProgressHandler((String text, int start, int end, String word) {
      _log('tts progress: "$word" ($start-$end)');
      _onSpeakProgress?.call(text, start, end, word);
    });
    _flutterTts.setErrorHandler((message) {
      _log('tts error: $message');
      _onTtsError?.call('$message');
    });

    _initialized = speechReady;
    _log('initialize complete: speechReady=$speechReady');
    return speechReady;
  }

  @override
  Future<void> startThinkingLoop() async {
    _log('startThinkingLoop requested');
    await initialize();
    await stopSpeaking();
    _thinkingLoopStopping = false;

    if (!_thinkingLoopPrepared) {
      await _thinkingLoopPlayer.setSource(
        AssetSource('audio/simi_thinking_loop.wav'),
      );
      _thinkingLoopPrepared = true;
    }

    await _thinkingLoopPlayer.setVolume(0);
    _thinkingLoopVolume = 0;
    await _thinkingLoopPlayer.resume();
    _thinkingLoopActive = true;
    await _fadeThinkingLoop(to: _thinkingLoopTargetVolume);
    _log('startThinkingLoop active');
  }

  @override
  Future<void> stopThinkingLoop() async {
    _log('stopThinkingLoop requested');
    if (!_thinkingLoopActive) {
      await _thinkingLoopPlayer.stop();
      return;
    }

    _thinkingLoopStopping = true;
    await _fadeThinkingLoop(to: 0);
    await _thinkingLoopPlayer.stop();
    _thinkingLoopActive = false;
    _thinkingLoopStopping = false;
    _thinkingLoopVolume = 0;
  }

  @override
  Future<bool> startListening({
    required ChatVoiceResultCallback onResult,
    ChatVoiceStatusCallback? onStatus,
    ChatVoiceErrorCallback? onError,
    String? localeTag,
  }) async {
    final int sessionId = ++_listenSessionId;
    final String localeId = _speechLocaleId(localeTag);
    _log('startListening requested: session=$sessionId locale=$localeId');

    final bool ready = await initialize();
    if (!ready) {
      _log('startListening aborted: initialize returned false');
      return false;
    }

    await stopThinkingLoop();
    _log('startListening stopping TTS before STT handoff');
    await stopSpeaking();
    _log(
        'startListening post-TTS delay ${_postTtsListenDelay.inMilliseconds}ms');
    await Future<void>.delayed(_postTtsListenDelay);

    if (_speechToText.isListening) {
      _log('startListening canceling active recognizer session');
      await _speechToText.cancel();
    }

    _log('startListening forcing recognizer reset');
    _activeListenSessionId = null;
    _activeSpeechStatusCallback = null;
    _activeSpeechErrorCallback = null;
    await _speechToText.cancel();
    _log(
        'startListening recognizer reset delay ${_recognizerResetDelay.inMilliseconds}ms');
    await Future<void>.delayed(_recognizerResetDelay);

    _log('startListening invoking speech recognizer');
    _activeListenSessionId = sessionId;
    _activeSpeechStatusCallback = (String status) {
      if (_activeListenSessionId != sessionId) {
        _log('ignore stale stt status for session=$sessionId: $status');
        return;
      }
      onStatus?.call(status);
    };
    _activeSpeechErrorCallback = (String message) {
      if (_activeListenSessionId != sessionId) {
        _log('ignore stale stt error for session=$sessionId: $message');
        return;
      }
      onError?.call(message);
    };

    await _speechToText.listen(
      onResult: (result) {
        if (_activeListenSessionId != sessionId) {
          _log(
            'ignore stale stt result for session=$sessionId words="${result.recognizedWords}"',
          );
          return;
        }

        _log(
          'stt result: final=${result.finalResult} words="${result.recognizedWords}"',
        );
        onResult(result.recognizedWords, result.finalResult);
      },
      listenFor: const Duration(seconds: 120),
      pauseFor: const Duration(seconds: 16),
      localeId: localeId,
      listenOptions: SpeechListenOptions(
        partialResults: true,
        cancelOnError: true,
      ),
    );

    final bool active = await _waitForRecognizerActivation();
    _log('startListening active=$active');
    if (!active && _activeListenSessionId == sessionId) {
      _activeListenSessionId = null;
      _activeSpeechStatusCallback = null;
      _activeSpeechErrorCallback = null;
    }

    return active;
  }

  @override
  Future<void> stopListening() async {
    _log('stopListening requested: active=${_speechToText.isListening}');
    _activeListenSessionId = null;
    _activeSpeechStatusCallback = null;
    _activeSpeechErrorCallback = null;
    if (_speechToText.isListening) {
      await _speechToText.stop();
      _log('stopListening completed');
    }
  }

  @override
  Future<void> speak(
    String text, {
    VoidCallback? onStart,
    VoidCallback? onComplete,
    VoidCallback? onCancel,
    ChatVoiceProgressCallback? onProgress,
    ChatVoiceErrorCallback? onError,
    String? localeTag,
  }) async {
    final int sessionId = ++_speakSessionId;
    _log(
        'speak requested: session=$sessionId locale=${localeTag ?? 'default'} length=${text.length}');
    await initialize();

    await stopThinkingLoop();
    await stopSpeaking();

    _activeSpeakSessionId = sessionId;
    _handledSpeakTerminalSessionId = null;
    _ttsStopRequested = false;
    _onSpeakStart = () {
      if (_activeSpeakSessionId != sessionId) {
        return;
      }
      onStart?.call();
    };
    _onSpeakComplete = () {
      if (_activeSpeakSessionId != null && _activeSpeakSessionId != sessionId) {
        return;
      }
      onComplete?.call();
    };
    _onSpeakCancel = () {
      if (_activeSpeakSessionId != null && _activeSpeakSessionId != sessionId) {
        return;
      }
      onCancel?.call();
    };
    _onSpeakProgress = (String spokenText, int start, int end, String word) {
      if (_activeSpeakSessionId != sessionId) {
        return;
      }
      onProgress?.call(spokenText, start, end, word);
    };
    _onTtsError = (String message) {
      if (_activeSpeakSessionId != sessionId) {
        return;
      }
      onError?.call(message);
    };

    await _setTtsLocale(localeTag);
    _log('speak stopping any active TTS before speaking');
    _log('speak start');
    await _flutterTts.speak(text);
  }

  @override
  Future<void> stopSpeaking() async {
    _log('stopSpeaking requested');
    _ttsStopRequested = true;
    await _flutterTts.stop();
    _log('stopSpeaking completed');
  }

  @override
  Future<void> dispose() async {
    _log('dispose start');
    _activeListenSessionId = null;
    _activeSpeechStatusCallback = null;
    _activeSpeechErrorCallback = null;
    _activeSpeakSessionId = null;
    await _speechToText.cancel();
    _thinkingLoopActive = false;
    _thinkingLoopStopping = false;
    _thinkingLoopVolume = 0;
    await _thinkingLoopPlayer.dispose();
    await _flutterTts.stop();
    _log('dispose complete');
  }

  Future<void> _fadeThinkingLoop({required double to}) async {
    final double from = _thinkingLoopVolume;
    final double delta = (to - from) / _thinkingLoopFadeSteps;

    for (int step = 1; step <= _thinkingLoopFadeSteps; step += 1) {
      if (!_thinkingLoopActive && to > 0) {
        return;
      }

      if (!_thinkingLoopStopping && to == 0 && step > 1) {
        return;
      }

      final double nextVolume = (from + (delta * step)).clamp(0, 1);
      await _thinkingLoopPlayer.setVolume(nextVolume);
      _thinkingLoopVolume = nextVolume;
      await Future<void>.delayed(_thinkingLoopFadeStep);
    }
  }

  Future<bool> _waitForRecognizerActivation() async {
    final Stopwatch stopwatch = Stopwatch()..start();
    while (stopwatch.elapsed < _listenActivationWindow) {
      if (_speechToText.isListening) {
        return true;
      }
      await Future<void>.delayed(const Duration(milliseconds: 75));
    }

    return _speechToText.isListening;
  }

  String _speechLocaleId(String? localeTag) {
    if (localeTag == null || localeTag.trim().isEmpty) {
      return 'en_US';
    }

    return localeTag.replaceAll('-', '_');
  }

  Future<void> _setTtsLocale(String? localeTag) async {
    final String normalized = (localeTag == null || localeTag.trim().isEmpty)
        ? 'en-US'
        : localeTag.replaceAll('_', '-');

    try {
      _log('tts locale set: $normalized');
      await _flutterTts.setLanguage(normalized);
    } catch (_) {
      _log('tts locale fallback to en-US from $normalized');
      await _flutterTts.setLanguage('en-US');
    }
  }

  void _handleTtsStart() {
    final int? sessionId = _activeSpeakSessionId;
    if (sessionId == null) {
      _log('tts start ignored: no active session');
      return;
    }

    _log('tts start: session=$sessionId');
    _onSpeakStart?.call();
  }

  void _handleTtsTerminalEvent({
    required String source,
    required bool canceled,
  }) {
    final int? sessionId = _activeSpeakSessionId;
    if (sessionId == null) {
      _log('tts $source ignored: no active session');
      _ttsStopRequested = false;
      return;
    }

    if (_handledSpeakTerminalSessionId == sessionId) {
      _log('tts $source ignored: session already handled=$sessionId');
      return;
    }

    _handledSpeakTerminalSessionId = sessionId;
    final bool treatAsCancel = canceled || _ttsStopRequested;
    _log(
      'tts terminal: source=$source session=$sessionId cancel=$treatAsCancel',
    );

    _ttsStopRequested = false;
    _activeSpeakSessionId = null;

    if (treatAsCancel) {
      _onSpeakCancel?.call();
      return;
    }

    _onSpeakComplete?.call();
  }

  void _log(String message) {
    if (!kDebugMode) {
      return;
    }

    debugPrint('$_logPrefix $message');
  }
}
