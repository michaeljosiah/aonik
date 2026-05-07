// Voice-mode TTS playback (Option A): the backend emits a single
// `speech.chunk` for the full assistant message after the LLM stream
// completes, then streams its `speech.audio` frames. The client
// buffers all frames for that one chunk into memory, writes them to
// a temp MP3, and plays the file with audioplayers' DeviceFileSource.
// No streaming player, no proxy server, no watchdog fallback — every
// failure mode that came with just_audio's StreamAudioSource path
// (setAudioSource hangs, _proxyHandlerForSource null-checks, two-
// player crossfade overlap) is gone.

import 'dart:async';
import 'dart:collection';
import 'dart:developer' as developer;
import 'dart:io';
import 'dart:typed_data';

import 'package:audioplayers/audioplayers.dart';
import 'package:dio/dio.dart';
import 'package:firebase_analytics/firebase_analytics.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_tts/flutter_tts.dart';
import 'package:path_provider/path_provider.dart';
import 'package:speech_to_text/speech_to_text.dart';

import '../../../data/api/api_client.dart';

typedef ChatVoiceResultCallback = void Function(String text, bool isFinal);
typedef ChatVoiceStatusCallback = void Function(String status);
typedef ChatVoiceErrorCallback = void Function(String message);
typedef ChatVoiceProgressCallback = void Function(
    String text, int startOffset, int endOffset, String word);
typedef ChatVoiceInlineAudioMilestoneCallback = void Function({
  required int chunkIndex,
  required int queueIndex,
  required int bufferedBytes,
  required String contentType,
  required bool isClosed,
});

/// App-scoped singleton voice service.
///
/// Ownership: the provider creates the instance and is responsible for
/// disposing it when the [ProviderScope] is torn down (i.e. app exit).
/// Screens MUST NOT call `dispose()` on the returned service — doing so
/// tears down the underlying SpeechToText / FlutterTts / AudioPlayer
/// instances that this class holds as `final` fields and cannot recreate,
/// leaving the next consumer with a dead singleton whose calls hang on
/// stale platform channels.
final Provider<ChatVoiceService> chatVoiceServiceProvider =
    Provider<ChatVoiceService>((Ref ref) {
  final DeviceChatVoiceService service =
      DeviceChatVoiceService(apiClient: ref.watch(apiClientProvider));
  ref.onDispose(() {
    unawaited(service.dispose());
  });
  return service;
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

  /// Start a new sentence-chunk speaking queue. Any prior queue is canceled
  /// and its in-flight backend TTS requests are aborted. Chunks added via
  /// [enqueueSpeechChunk] are synthesized in parallel and played sequentially.
  ///
  /// [onSpeakingStart] fires each time the queue transitions from idle to
  /// playing (including when new chunks arrive after a drain).
  /// [onSpeakingIdle] fires each time the queue drains to empty.
  /// [onChunkError] fires when a non-cancellation synthesis or playback
  /// failure occurs on a chunk.
  Future<void> beginSpeechQueue({
    VoidCallback? onSpeakingStart,
    VoidCallback? onSpeakingIdle,
    ChatVoiceErrorCallback? onChunkError,
    ChatVoiceInlineAudioMilestoneCallback? onInlinePlaybackBufferReady,
    String? localeTag,
  });

  /// Append [text] to the current speech queue. No-op if no queue is active.
  /// Synthesis starts immediately so it can overlap with in-progress playback.
  ///
  /// Legacy non-voice-mode path — synthesises via the per-chunk
  /// `/mobile/text-to-speech/synthesize` HTTP endpoint. Use the
  /// [enqueueInlineSpeechChunk] / [appendSpeechAudioFrame] /
  /// [markSpeechAudioError] trio when the run was sent with
  /// `voiceMode=true` so audio bytes arrive inline on the SSE stream.
  void enqueueSpeechChunk(String text);

  /// Reserve a queue slot for the chunk identified by [chunkIndex] (the
  /// server's chunk id from the `speech.chunk` event). Subsequent
  /// [appendSpeechAudioFrame] calls for the same [chunkIndex] push
  /// bytes into an in-memory buffer; once the chunk closes (terminal
  /// frame, error, or watchdog) the drain loop writes the buffer to a
  /// temp MP3 and plays it through the shared `audioplayers` instance.
  ///
  /// The slot's mime is set by the first audio frame's `mime` (passed
  /// to [appendSpeechAudioFrame]); calling enqueue here is optional
  /// but recommended so that chunks play strictly in chunkIndex order,
  /// regardless of frame arrival order.
  void enqueueInlineSpeechChunk({required int chunkIndex});

  /// Append a decoded audio frame for the chunk identified by
  /// [chunkIndex]. The first call for a given [chunkIndex] sets the
  /// slot's [mime] (subsequent calls' `mime` is ignored — switching
  /// decoders mid-stream isn't supported). When [isFinal] is `true`,
  /// the slot's source is closed; the player will play the remaining
  /// buffered bytes and transition to the next chunk.
  void appendSpeechAudioFrame({
    required int chunkIndex,
    required List<int> data,
    required bool isFinal,
    required String mime,
  });

  /// Mark a chunk's audio synthesis as failed. The chunk's text was
  /// already delivered via [enqueueInlineSpeechChunk]; this just lets
  /// the playback queue advance past the chunk without waiting on
  /// audio that will never arrive. Same terminal effect as a final
  /// frame of zero bytes.
  void markSpeechAudioError({
    required int chunkIndex,
    required String code,
  });

  /// Cancel the active speech queue (if any): stops playback, aborts
  /// in-flight synthesis, clears pending chunks.
  Future<void> cancelSpeechQueue();

  Future<void> stopSpeaking();

  Future<void> dispose();
}

class DeviceChatVoiceService implements ChatVoiceService {
  static const Duration _postTtsListenDelay = Duration(milliseconds: 200);
  static const Duration _recognizerResetDelay = Duration(milliseconds: 250);
  static const Duration _listenActivationWindow = Duration(milliseconds: 900);
  static const String _logPrefix = '[VoiceService]';
  static const String _telemetryLogName = 'Payabo.ChatVoice';
  static const Duration _thinkingLoopFadeStep = Duration(milliseconds: 45);
  static const int _thinkingLoopFadeSteps = 6;
  static const double _thinkingLoopTargetVolume = 0.45;

  /// Watchdog ceiling for an inline voice-mode chunk to receive its
  /// terminal frame (`isFinal=true`). Aligned with the server's per-
  /// chunk synth ceiling (`VoiceSynthCoordinator.PerChunkTimeout`,
  /// 10 s) plus network grace. If the deadline elapses without the
  /// chunk closing, we surface a chunk-error so the host's loading
  /// indicator dismisses cleanly.
  static const Duration _inlineChunkCloseTimeout = Duration(seconds: 14);

  /// Watchdog ceiling for one inline chunk's complete playback. The
  /// one-shot voice-mode synth produces audio for the entire assistant
  /// message, so a longer ceiling than the old per-sentence path makes
  /// sense. 60 s comfortably covers Simi's longest natural responses
  /// while still bounding genuinely stuck players.
  static const Duration _inlineChunkPlaybackTimeout = Duration(seconds: 60);

  final Dio _apiClient;
  final SpeechToText _speechToText = SpeechToText();
  final FlutterTts _flutterTts = FlutterTts();
  final AudioPlayer _thinkingLoopPlayer = AudioPlayer();
  final AudioPlayer _speechPlayer = AudioPlayer();

  ChatVoiceStatusCallback? _activeSpeechStatusCallback;
  ChatVoiceErrorCallback? _activeSpeechErrorCallback;
  ChatVoiceErrorCallback? _onTtsError;
  VoidCallback? _onSpeakStart;
  VoidCallback? _onSpeakComplete;
  VoidCallback? _onSpeakCancel;
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
  String? _activeSpeechFilePath;
  CancelToken? _activeTtsRequestCancelToken;
  String? _lastBackendTtsAiRunId;
  String? _lastBackendTtsProvider;
  String? _lastBackendTtsVoiceId;

  int _queueSessionId = 0;
  int? _activeQueueSessionId;
  int _queueNextIndex = 0;
  String? _queueLocaleTag;
  VoidCallback? _queueOnSpeakingStart;
  VoidCallback? _queueOnSpeakingIdle;
  ChatVoiceErrorCallback? _queueOnChunkError;
  ChatVoiceInlineAudioMilestoneCallback? _queueOnInlinePlaybackBufferReady;
  bool _queueSpeakingFlag = false;
  // Tracks whether the drain loop has attempted to play (or skip) any
  // chunks in the current queue session. Decoupled from [_queueSpeakingFlag]
  // because a chunk that arrived with zero audio bytes never reaches the
  // setAudioSource step that flips speakingFlag — we still need to know
  // a chunk was processed so that the queue-empty branch can fire
  // [_queueOnSpeakingIdle] and dismiss the host's loading indicator.
  bool _queueAnyChunkAttempted = false;
  bool _queueDrainInFlight = false;
  final ListQueue<_QueuedSpeechChunk> _speechQueue =
      ListQueue<_QueuedSpeechChunk>();
  _QueuedSpeechChunk? _currentlyPlayingChunk;
  Completer<void>? _currentChunkPlaybackCompleter;
  StreamSubscription<void>? _currentChunkPlaybackSub;

  /// Per-chunkIndex byte buffers for voice-mode inline audio. Created
  /// when a `speech.chunk` arrives ([enqueueInlineSpeechChunk]) or
  /// lazily when the first `speech.audio` frame for a new chunkIndex
  /// arrives. The buffer accumulates frames until the chunk closes
  /// (terminal frame, error, or watchdog), at which point the bytes
  /// are written to a temp MP3 and the chunk's [_QueuedSpeechChunk]
  /// transitions from waiting-for-bytes to ready-to-play.
  final Map<int, _InlineChunkBuffer> _inlineChunkBuffers =
      <int, _InlineChunkBuffer>{};

  DeviceChatVoiceService({required Dio apiClient}) : _apiClient = apiClient;

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
    await _prepareThinkingLoop();

    await _speechPlayer.setReleaseMode(ReleaseMode.stop);
    _speechPlayer.onPlayerComplete.listen((_) {
      _handleTtsTerminalEvent(source: 'audio_complete', canceled: false);
    });

    _flutterTts.setStartHandler(_handleTtsStart);
    _flutterTts.setCompletionHandler(
      () => _handleTtsTerminalEvent(source: 'completion', canceled: false),
    );
    _flutterTts.setCancelHandler(
      () => _handleTtsTerminalEvent(source: 'cancel', canceled: true),
    );
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
    // Stop any prior single-shot speak() session but leave an active chunk
    // queue intact — callers (e.g. _stopVoiceStage) set up the queue before
    // starting the thinking loop, so trashing it here breaks streaming TTS.
    await _stopLegacySpeech();
    _thinkingLoopStopping = false;
    await _prepareThinkingLoop();

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
      await _safeStopThinkingLoop();
      return;
    }

    _thinkingLoopStopping = true;
    await _fadeThinkingLoop(to: 0);
    await _safeStopThinkingLoop();
    _thinkingLoopActive = false;
    _thinkingLoopStopping = false;
    _thinkingLoopVolume = 0;
  }

  Future<void> _safeStopThinkingLoop() async {
    try {
      await _thinkingLoopPlayer.stop();
    } catch (_) {
      // Player may be in Idle state (setSource not yet called) or disposed —
      // Android MediaPlayer.stop() throws IllegalStateException in that case.
    }
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
    final bool requiresPostTtsDelay = _hasActiveSpeechOutput;
    _log('startListening requested: session=$sessionId locale=$localeId');

    final bool ready = await initialize();
    if (!ready) {
      _log('startListening aborted: initialize returned false');
      return false;
    }

    await stopThinkingLoop();
    _log('startListening stopping TTS before STT handoff');
    await stopSpeaking();

    final Future<void> recognizerReset = _resetRecognizerForListening();
    if (requiresPostTtsDelay) {
      _log(
        'startListening overlapping post-TTS delay ${_postTtsListenDelay.inMilliseconds}ms with recognizer reset',
      );
      await Future.wait<void>(<Future<void>>[
        recognizerReset,
        Future<void>.delayed(_postTtsListenDelay),
      ]);
    } else {
      _log('startListening skipping post-TTS delay: no active speech output');
      await recognizerReset;
    }

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

  /// Prepares [text] for natural speech by removing constructs that cause
  /// TTS engines to produce double-read artefacts (e.g. "$79.99 USD" → "79.99
  /// dollars" rather than "79 dollars dollars 99").
  ///
  /// Rules applied (in order):
  /// 1. Currency symbol + amount + redundant code → amount + spoken name
  ///    e.g. "$79.99 USD" → "79 dollars and 99 cents"
  /// 2. Bare currency symbol + amount (no code) → amount + spoken name
  ///    e.g. "$79" → "79 dollars"
  /// 3. Amount + currency code (no symbol) → amount + spoken name
  ///    e.g. "79.99 GBP" → "79 pounds and 99 pence"
  String _sanitizeSpeechText(String text) {
    // Map of currency symbol → [code, singular spoken name, cents name]
    const Map<String, List<String>> symbolMap = {
      r'$': ['USD', 'dollar', 'cent'],
      '£': ['GBP', 'pound', 'penny'],
      '€': ['EUR', 'euro', 'cent'],
      '₦': ['NGN', 'naira', 'kobo'],
      '₵': ['GHS', 'cedi', 'pesewa'],
    };

    // Map of currency code → [singular spoken name, cents name]
    const Map<String, List<String>> codeMap = {
      'USD': ['dollar', 'cent'],
      'GBP': ['pound', 'penny'],
      'EUR': ['euro', 'cent'],
      'NGN': ['naira', 'kobo'],
      'GHS': ['cedi', 'pesewa'],
      'KES': ['shilling', 'cent'],
      'ZAR': ['rand', 'cent'],
      'UGX': ['shilling', 'cent'],
    };

    String result = text;

    // Pass 1: symbol + amount + optional code → spoken form
    for (final entry in symbolMap.entries) {
      final String sym = RegExp.escape(entry.key);
      final String code = entry.value[0];
      final String unit = entry.value[1];
      final String subunit = entry.value[2];

      // With code: e.g. $79.99 USD
      result = result.replaceAllMapped(
        RegExp(r'(?<!\w)' + sym + r'(\d[\d,]*)\.(\d{1,2})\s*' + code + r'\b',
            caseSensitive: false),
        (m) => _spokenAmount(m.group(1)!, m.group(2), unit, subunit),
      );
      // Without code: e.g. $79.99
      result = result.replaceAllMapped(
        RegExp(r'(?<!\w)' + sym + r'(\d[\d,]*)\.(\d{1,2})(?!\d|\s*[A-Z]{3})'),
        (m) => _spokenAmount(m.group(1)!, m.group(2), unit, subunit),
      );
      // Whole dollars with code: e.g. $200 USD
      result = result.replaceAllMapped(
        RegExp(r'(?<!\w)' + sym + r'(\d[\d,]*)\s*' + code + r'\b',
            caseSensitive: false),
        (m) => _spokenAmount(m.group(1)!, null, unit, subunit),
      );
      // Whole dollars no code: e.g. $200
      result = result.replaceAllMapped(
        RegExp(r'(?<!\w)' + sym + r'(\d[\d,]*)(?!\d|\.\d|\s*[A-Z]{3})'),
        (m) => _spokenAmount(m.group(1)!, null, unit, subunit),
      );
    }

    // Pass 2: amount + code (no symbol) → spoken form
    for (final entry in codeMap.entries) {
      final String code = entry.key;
      final String unit = entry.value[0];
      final String subunit = entry.value[1];

      result = result.replaceAllMapped(
        RegExp(r'(\d[\d,]*)\.(\d{1,2})\s*' + code + r'\b',
            caseSensitive: false),
        (m) => _spokenAmount(m.group(1)!, m.group(2), unit, subunit),
      );
      result = result.replaceAllMapped(
        RegExp(r'(\d[\d,]*)\s*' + code + r'\b', caseSensitive: false),
        (m) => _spokenAmount(m.group(1)!, null, unit, subunit),
      );
    }

    return result;
  }

  String _spokenAmount(
      String whole, String? cents, String unit, String subunit) {
    final int wholeNum = int.tryParse(whole.replaceAll(',', '')) ?? 0;
    final String unitWord = wholeNum == 1 ? unit : '${unit}s';

    if (cents == null || cents == '00') {
      return '$wholeNum $unitWord';
    }

    final int centsNum = int.tryParse(cents) ?? 0;
    final String subunitWord = centsNum == 1 ? subunit : '${subunit}s';
    return '$wholeNum $unitWord and $centsNum $subunitWord';
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
    final String sanitized = _sanitizeSpeechText(text);
    final int sessionId = ++_speakSessionId;
    _log(
        'speak requested: session=$sessionId locale=${localeTag ?? 'default'} length=${sanitized.length}');
    await initialize();

    await stopThinkingLoop();
    await stopSpeaking();

    _activeSpeakSessionId = sessionId;
    _handledSpeakTerminalSessionId = null;
    _ttsStopRequested = false;
    _lastBackendTtsAiRunId = null;
    _lastBackendTtsProvider = null;
    _lastBackendTtsVoiceId = null;
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
    _onTtsError = (String message) {
      if (_activeSpeakSessionId != sessionId) {
        return;
      }
      onError?.call(message);
    };

    await _setTtsLocale(localeTag);
    try {
      await _speakViaBackend(
        sessionId,
        sanitized,
        localeTag: localeTag,
        onStart: onStart,
      );
      return;
    } catch (error) {
      if (_activeSpeakSessionId != sessionId) {
        _log('ignore stale backend tts failure for session=$sessionId: $error');
        return;
      }

      if (_ttsStopRequested || _isCancellationError(error)) {
        _log('backend tts canceled before playback: $error');
        _handledSpeakTerminalSessionId = sessionId;
        _activeSpeakSessionId = null;
        _ttsStopRequested = false;
        unawaited(_deleteActiveSpeechFile());
        _onSpeakCancel?.call();
        return;
      }

      final bool shouldFallback = _shouldUseNativeFallback(error);
      if (!shouldFallback) {
        _log('backend tts rejected without fallback: $error');
        await _recordBackendTtsFailure(
          error,
          stage: 'backend_rejected',
          localeTag: localeTag,
          textLength: sanitized.length,
          includeCrashlytics: false,
        );
        _handledSpeakTerminalSessionId = sessionId;
        _activeSpeakSessionId = null;
        _ttsStopRequested = false;
        unawaited(_deleteActiveSpeechFile());
        onError?.call(_describeTtsError(error));
        return;
      }

      _log('backend tts failed, falling back to native: $error');
      await _recordBackendTtsFailure(
        error,
        stage: 'backend_delivery',
        localeTag: localeTag,
        textLength: sanitized.length,
      );
    }

    if (_activeSpeakSessionId != sessionId || _ttsStopRequested) {
      return;
    }

    _log('fallback native tts start');
    await _recordNativeFallbackActivation(
      reason: 'backend_tts_failed',
      localeTag: localeTag,
      textLength: sanitized.length,
    );

    if (_activeSpeakSessionId != sessionId || _ttsStopRequested) {
      return;
    }

    try {
      await _flutterTts.speak(sanitized);
    } catch (error) {
      _handledSpeakTerminalSessionId = sessionId;
      _activeSpeakSessionId = null;
      _ttsStopRequested = false;
      unawaited(_deleteActiveSpeechFile());
      onError?.call(_describeTtsError(error));
    }
  }

  @override
  Future<void> stopSpeaking() async {
    _log('stopSpeaking requested');
    await _cancelSpeechQueueInternal(stopPlayer: false);
    await _stopLegacySpeech();
    _log('stopSpeaking completed');
  }

  /// Stops any in-flight single-shot `speak()` session and silences the
  /// playback players without touching the chunk queue state.
  Future<void> _stopLegacySpeech() async {
    _ttsStopRequested = true;
    _cancelActiveTtsRequest();
    try {
      await _speechPlayer.stop();
    } catch (_) {
      // Player may not have been created yet — safe to ignore.
    }
    await _flutterTts.stop();
    await _deleteActiveSpeechFile();
  }

  @override
  Future<void> beginSpeechQueue({
    VoidCallback? onSpeakingStart,
    VoidCallback? onSpeakingIdle,
    ChatVoiceErrorCallback? onChunkError,
    ChatVoiceInlineAudioMilestoneCallback? onInlinePlaybackBufferReady,
    String? localeTag,
  }) async {
    _log('beginSpeechQueue locale=${localeTag ?? 'default'}');
    await _cancelSpeechQueueInternal(stopPlayer: true);
    await initialize();

    // Any leftover single-shot speak session would otherwise race with our
    // queue's use of _speechPlayer. Flush that state explicitly.
    _activeSpeakSessionId = null;
    _handledSpeakTerminalSessionId = null;
    _ttsStopRequested = false;

    final int sessionId = ++_queueSessionId;
    _activeQueueSessionId = sessionId;
    _queueNextIndex = 0;
    _queueLocaleTag = localeTag;
    _queueOnSpeakingStart = onSpeakingStart;
    _queueOnSpeakingIdle = onSpeakingIdle;
    _queueOnChunkError = onChunkError;
    _queueOnInlinePlaybackBufferReady = onInlinePlaybackBufferReady;
    _queueSpeakingFlag = false;
    _queueAnyChunkAttempted = false;
    _log('beginSpeechQueue session=$sessionId');
  }

  @override
  void enqueueSpeechChunk(String text) {
    final String sanitized = _sanitizeSpeechText(text.trim());
    if (sanitized.isEmpty) {
      return;
    }

    final int? sessionId = _activeQueueSessionId;
    if (sessionId == null) {
      _log('enqueueSpeechChunk ignored: no active queue');
      return;
    }

    final int index = _queueNextIndex++;
    final CancelToken cancelToken = CancelToken();
    final Future<String> synthesis = _synthesizeQueueChunkAudio(
      sessionId: sessionId,
      index: index,
      text: sanitized,
      localeTag: _queueLocaleTag,
      cancelToken: cancelToken,
    );
    // Swallow unhandled rejections on the synthesis future; the drain loop
    // handles errors when it awaits the same future.
    unawaited(synthesis.then<void>((_) {}, onError: (_) {}));

    _speechQueue.add(
      _QueuedSpeechChunk(
        sessionId: sessionId,
        index: index,
        text: sanitized,
        synthesisFuture: synthesis,
        cancelToken: cancelToken,
      ),
    );
    // Note: inlineAudioFuture is null on this path — falls through to
    // the legacy synthesize-and-file pipeline in [_playQueueChunk].
    _log(
      'enqueueSpeechChunk session=$sessionId index=$index length=${sanitized.length} queued=${_speechQueue.length}',
    );

    unawaited(_drainSpeechQueue());
  }

  @override
  void enqueueInlineSpeechChunk({required int chunkIndex}) {
    final int? sessionId = _activeQueueSessionId;
    if (sessionId == null) {
      _log('enqueueInlineSpeechChunk ignored: no active queue');
      return;
    }

    if (_inlineChunkBuffers.containsKey(chunkIndex)) {
      _log(
        'enqueueInlineSpeechChunk ignored: chunkIndex=$chunkIndex already buffered',
      );
      return;
    }

    final _InlineChunkBuffer buffer = _InlineChunkBuffer();
    _inlineChunkBuffers[chunkIndex] = buffer;

    final int queueIndex = _queueNextIndex++;
    _speechQueue.add(
      _QueuedSpeechChunk(
        sessionId: sessionId,
        index: queueIndex,
        text: '',
        cancelToken: CancelToken(),
        inlineBuffer: buffer,
        serverChunkIndex: chunkIndex,
      ),
    );
    _log(
      'enqueueInlineSpeechChunk session=$sessionId chunkIndex=$chunkIndex queueIndex=$queueIndex queued=${_speechQueue.length}',
    );
    unawaited(_drainSpeechQueue());
  }

  @override
  void appendSpeechAudioFrame({
    required int chunkIndex,
    required List<int> data,
    required bool isFinal,
    required String mime,
  }) {
    var buffer = _inlineChunkBuffers[chunkIndex];

    // Lazy slot creation: the server may emit `speech.audio` for a chunk
    // we never saw a `speech.chunk` for (compact / stripped wire shapes
    // in the future). Reserve a slot so playback ordering still matches
    // the chunkIndex sequence.
    if (buffer == null) {
      enqueueInlineSpeechChunk(chunkIndex: chunkIndex);
      buffer = _inlineChunkBuffers[chunkIndex];
      if (buffer == null) {
        // enqueueInline ignored the call (no active queue) — drop frame.
        return;
      }
    }

    if (buffer.isClosed) {
      _log(
        'appendSpeechAudioFrame ignored: chunkIndex=$chunkIndex buffer already closed',
      );
      return;
    }

    // First frame sets the mime. Later frames must match — if they
    // don't, keep the original; the platform decoder is keyed off the
    // first frame's type and switching mid-stream isn't supported.
    if (buffer.mime == null && mime.isNotEmpty) {
      buffer.mime = mime;
    }

    if (data.isNotEmpty) {
      buffer.append(data);
    }

    if (isFinal) {
      _inlineChunkBuffers.remove(chunkIndex);
      buffer.markClosed();
    }
  }

  @override
  void markSpeechAudioError({
    required int chunkIndex,
    required String code,
  }) {
    final buffer = _inlineChunkBuffers.remove(chunkIndex);
    if (buffer == null || buffer.isClosed) {
      return;
    }
    _log(
      'markSpeechAudioError chunkIndex=$chunkIndex code=$code — chunk will be skipped',
    );
    // Mark closed with no bytes so the drain loop skips this chunk.
    // Text was already delivered via the matching speech.chunk event.
    buffer.markClosed();
  }

  @override
  Future<void> cancelSpeechQueue() async {
    await _cancelSpeechQueueInternal(stopPlayer: true);
  }

  Future<void> _cancelSpeechQueueInternal({required bool stopPlayer}) async {
    final int? sessionId = _activeQueueSessionId;
    if (sessionId == null &&
        _speechQueue.isEmpty &&
        _currentlyPlayingChunk == null) {
      return;
    }

    _log('cancelSpeechQueue session=$sessionId pending=${_speechQueue.length}');
    _activeQueueSessionId = null;
    _queueOnSpeakingStart = null;
    _queueOnSpeakingIdle = null;
    _queueOnChunkError = null;
    _queueOnInlinePlaybackBufferReady = null;
    _queueSpeakingFlag = false;
    _queueAnyChunkAttempted = false;

    for (final _QueuedSpeechChunk chunk in _speechQueue) {
      if (!chunk.cancelToken.isCancelled) {
        chunk.cancelToken.cancel('Queue canceled');
      }
      unawaited(_cleanupQueueChunkFile(chunk));
    }
    _speechQueue.clear();

    // Voice-mode inline buffers — mark closed so the drain loop's
    // close-watcher unblocks and the queue exits cleanly. Barge-in
    // invokes this path too.
    for (final buffer in _inlineChunkBuffers.values) {
      if (!buffer.isClosed) {
        buffer.markClosed();
      }
    }
    _inlineChunkBuffers.clear();

    final _QueuedSpeechChunk? playing = _currentlyPlayingChunk;
    if (playing != null) {
      if (!playing.cancelToken.isCancelled) {
        playing.cancelToken.cancel('Queue canceled');
      }
      unawaited(_cleanupQueueChunkFile(playing));
    }
    _currentlyPlayingChunk = null;

    final Completer<void>? pending = _currentChunkPlaybackCompleter;
    _currentChunkPlaybackCompleter = null;
    if (pending != null && !pending.isCompleted) {
      pending.complete();
    }
    final StreamSubscription<void>? sub = _currentChunkPlaybackSub;
    _currentChunkPlaybackSub = null;
    if (sub != null) {
      unawaited(sub.cancel());
    }

    if (stopPlayer) {
      try {
        await _speechPlayer.stop();
      } catch (_) {
        // Best effort; player may not be initialized.
      }
    }
  }

  Future<void> _drainSpeechQueue() async {
    if (_queueDrainInFlight) {
      return;
    }
    _queueDrainInFlight = true;
    try {
      while (true) {
        final int? sessionId = _activeQueueSessionId;
        if (sessionId == null) {
          return;
        }

        if (_speechQueue.isEmpty) {
          // Fire idle when:
          //   • the queue actually transitioned through 'speaking' (the
          //     happy path: setAudioSource succeeded for ≥ 1 chunk), OR
          //   • the drain attempted any chunks but every one was a no-op
          //     (zero audio bytes, error skip, watchdog timeout) — without
          //     this branch the host's loading indicator sticks because
          //     onSpeakingStart never fired and onSpeakingIdle was gated
          //     on the same flag.
          if (_queueSpeakingFlag || _queueAnyChunkAttempted) {
            _queueSpeakingFlag = false;
            _queueAnyChunkAttempted = false;
            _queueOnSpeakingIdle?.call();
          }
          // The idle callback may synchronously enqueue more chunks.
          if (_speechQueue.isEmpty || _activeQueueSessionId != sessionId) {
            return;
          }
          continue;
        }

        final _QueuedSpeechChunk chunk = _speechQueue.removeFirst();
        if (chunk.sessionId != sessionId) {
          unawaited(_cleanupQueueChunkFile(chunk));
          continue;
        }

        _queueAnyChunkAttempted = true;
        _currentlyPlayingChunk = chunk;
        await _playQueueChunk(chunk, sessionId);
        if (identical(_currentlyPlayingChunk, chunk)) {
          _currentlyPlayingChunk = null;
        }
      }
    } finally {
      _queueDrainInFlight = false;
    }
  }

  Future<void> _playQueueChunk(
    _QueuedSpeechChunk chunk,
    int sessionId,
  ) async {
    final bool isInline = chunk.inlineBuffer != null;

    if (isInline) {
      await _playInlineChunk(chunk, sessionId);
    } else {
      await _playLegacyChunk(chunk, sessionId);
    }
  }

  /// Voice-mode inline playback (Option A, single-player path).
  ///
  /// 1. Wait for the chunk's [_InlineChunkBuffer] to receive its
  ///    terminal frame (or for a cancellation / watchdog).
  /// 2. Write the buffered MP3 bytes to a temp file.
  /// 3. Stop the thinking loop synchronously (so its fade-out can't
  ///    overlap response audio — the "stagnated first play" symptom).
  /// 4. Play the temp file through [_speechPlayer] using
  ///    `DeviceFileSource` and await `onPlayerComplete`.
  /// 5. Release the player on the way out so SpeechRecognizer can
  ///    reclaim the AudioTrack for the next listening turn.
  Future<void> _playInlineChunk(
    _QueuedSpeechChunk chunk,
    int sessionId,
  ) async {
    final _InlineChunkBuffer buffer = chunk.inlineBuffer!;

    // Wait until the chunk closes (terminal frame received, error
    // emitted, or barge-in cancellation). Watchdog covers the case
    // where the server's TTS pipeline hangs or the SSE stream drops
    // before sending isFinal — without it the drain loop wedges and
    // the host's loading indicator never dismisses.
    try {
      await buffer.closed.timeout(_inlineChunkCloseTimeout);
    } on TimeoutException {
      _log(
        'inline chunk close watchdog timed out '
        'session=$sessionId index=${chunk.index} '
        'serverChunkIndex=${chunk.serverChunkIndex} '
        'bufferedBytes=${buffer.length} '
        'isClosed=${buffer.isClosed}',
      );
      buffer.markClosed();
      final int? serverChunkIndex = chunk.serverChunkIndex;
      if (serverChunkIndex != null) {
        _inlineChunkBuffers.remove(serverChunkIndex);
      }
    }

    if (_activeQueueSessionId != sessionId) {
      return;
    }

    _queueOnInlinePlaybackBufferReady?.call(
      chunkIndex: chunk.serverChunkIndex ?? chunk.index,
      queueIndex: chunk.index,
      bufferedBytes: buffer.length,
      contentType: buffer.mime ?? 'audio/mpeg',
      isClosed: buffer.isClosed,
    );

    // Empty buffer ⇒ chunk failed (speech.audio.error or watchdog).
    // Surface a chunk-error so the host dismisses the thinking loop.
    if (buffer.length == 0) {
      _log(
        'inline chunk skipped (no audio) session=$sessionId '
        'index=${chunk.index}',
      );
      _queueOnChunkError?.call('No audio for this segment.');
      return;
    }

    String? tempPath;
    final Completer<void> completion = Completer<void>();
    _currentChunkPlaybackCompleter = completion;
    final StreamSubscription<void> sub =
        _speechPlayer.onPlayerComplete.listen((_) {
      if (!completion.isCompleted) {
        completion.complete();
      }
    });
    _currentChunkPlaybackSub = sub;

    try {
      final Uint8List bytes = buffer.snapshot();
      final Directory directory = await getTemporaryDirectory();
      final File tempFile = File(
        '${directory.path}${Platform.pathSeparator}'
        'simi_tts_inline_${sessionId}_${chunk.index}.mp3',
      );
      await tempFile.writeAsBytes(bytes, flush: true);
      tempPath = tempFile.path;
      _log(
        'inline chunk wrote temp file session=$sessionId '
        'index=${chunk.index} bytes=${bytes.length} path=$tempPath',
      );

      if (_activeQueueSessionId != sessionId) {
        return;
      }

      // Stop the thinking loop fully BEFORE starting playback. Doing
      // this synchronously is the fix for the "stagnated first play"
      // symptom — when the loop's fade-out runs in parallel with the
      // response audio, two MediaPlayer instances both push samples
      // to AudioTrack and the user hears them blended. With the loop
      // fully torn down first there is exactly one player active
      // when response audio starts, so no overlap is possible.
      await stopThinkingLoop();

      // Re-assert release mode — `_thinkingLoopPlayer` lives on a
      // different audioplayers instance, but make the contract on
      // `_speechPlayer` explicit before each chunk for safety.
      await _speechPlayer.setReleaseMode(ReleaseMode.stop);

      if (!_queueSpeakingFlag) {
        _queueSpeakingFlag = true;
        _queueOnSpeakingStart?.call();
      }

      await _speechPlayer.play(
        DeviceFileSource(tempPath, mimeType: buffer.mime ?? 'audio/mpeg'),
      );
      _log(
        'inline chunk play() resolved session=$sessionId '
        'index=${chunk.index}',
      );

      await completion.future.timeout(_inlineChunkPlaybackTimeout);
      _log(
        'inline chunk completion fired session=$sessionId '
        'index=${chunk.index}',
      );
    } on TimeoutException catch (error) {
      _log(
        'inline chunk playback timed out session=$sessionId '
        'index=${chunk.index}: $error',
      );
      if (_activeQueueSessionId == sessionId) {
        _queueOnChunkError?.call('Voice playback timed out.');
      }
    } catch (error) {
      if (_activeQueueSessionId == sessionId && !_isCancellationError(error)) {
        _log(
          'inline chunk playback failed session=$sessionId '
          'index=${chunk.index}: $error',
        );
        _queueOnChunkError?.call(_describeTtsError(error));
      }
    } finally {
      if (identical(_currentChunkPlaybackCompleter, completion)) {
        _currentChunkPlaybackCompleter = null;
      }
      if (identical(_currentChunkPlaybackSub, sub)) {
        _currentChunkPlaybackSub = null;
      }
      await sub.cancel();
      // Release (not stop) — audioplayers' .stop() leaves the native
      // Android MediaPlayer + AudioTrack allocated. SpeechRecognizer's
      // AudioRecord then can't get a clean mic capture path on the
      // next turn (verified against 2026-05-07 device traces). Fully
      // releasing tears the MediaPlayer down; audioplayers
      // reinitialises lazily on the next play() call.
      try {
        await _speechPlayer.release();
      } catch (_) {
        // best-effort cleanup
      }
      if (tempPath != null) {
        try {
          final File f = File(tempPath);
          if (await f.exists()) {
            await f.delete();
          }
        } catch (_) {
          // best-effort cleanup
        }
      }
    }
  }

  /// Legacy synthesize-then-play path (audioplayers + DeviceFileSource).
  /// Used for non-voice-mode chunks and the speech.render guidance
  /// fallback.
  Future<void> _playLegacyChunk(
    _QueuedSpeechChunk chunk,
    int sessionId,
  ) async {
    String filePath;
    try {
      filePath = await chunk.synthesisFuture!;
    } catch (error) {
      if (_activeQueueSessionId == sessionId && !_isCancellationError(error)) {
        _log(
          'queue chunk synthesis failed session=$sessionId index=${chunk.index}: $error',
        );
        _queueOnChunkError?.call(_describeTtsError(error));
      }
      return;
    }

    chunk.filePath = filePath;

    if (_activeQueueSessionId != sessionId) {
      unawaited(_cleanupQueueChunkFile(chunk));
      return;
    }

    if (!_queueSpeakingFlag) {
      _queueSpeakingFlag = true;
      _queueOnSpeakingStart?.call();
    }

    final Completer<void> completion = Completer<void>();
    _currentChunkPlaybackCompleter = completion;
    final StreamSubscription<void> sub =
        _speechPlayer.onPlayerComplete.listen((_) {
      if (!completion.isCompleted) {
        completion.complete();
      }
    });
    _currentChunkPlaybackSub = sub;

    try {
      await _speechPlayer.play(
        DeviceFileSource(filePath, mimeType: 'audio/mpeg'),
      );
      await completion.future;
    } catch (error) {
      if (_activeQueueSessionId == sessionId) {
        _log(
          'queue chunk playback failed session=$sessionId index=${chunk.index}: $error',
        );
        _queueOnChunkError?.call(_describeTtsError(error));
      }
    } finally {
      if (identical(_currentChunkPlaybackCompleter, completion)) {
        _currentChunkPlaybackCompleter = null;
      }
      if (identical(_currentChunkPlaybackSub, sub)) {
        _currentChunkPlaybackSub = null;
      }
      await sub.cancel();
      unawaited(_cleanupQueueChunkFile(chunk));
    }
  }

  Future<String> _synthesizeQueueChunkAudio({
    required int sessionId,
    required int index,
    required String text,
    required String? localeTag,
    required CancelToken cancelToken,
  }) async {
    final Response<List<int>> response = await _apiClient.post<List<int>>(
      '/mobile/text-to-speech/synthesize',
      data: <String, dynamic>{
        'speechText': text,
        'locale': localeTag,
      },
      cancelToken: cancelToken,
      options: Options(
        responseType: ResponseType.bytes,
        headers: const <String, String>{
          'Accept': 'audio/mpeg',
        },
      ),
    );

    final bytes = response.data;
    if (bytes == null || bytes.isEmpty) {
      throw Exception('Backend returned empty audio response');
    }

    final directory = await getTemporaryDirectory();
    final file = File(
      '${directory.path}${Platform.pathSeparator}simi_tts_queue_${sessionId}_$index.mp3',
    );
    await file.writeAsBytes(bytes, flush: true);
    return file.path;
  }

  Future<void> _cleanupQueueChunkFile(_QueuedSpeechChunk chunk) async {
    final String? path = chunk.filePath;
    chunk.filePath = null;
    if (path == null || path.isEmpty) {
      return;
    }

    try {
      final file = File(path);
      if (await file.exists()) {
        await file.delete();
      }
    } catch (_) {
      // Best effort cleanup only.
    }
  }

  @override
  Future<void> dispose() async {
    _log('dispose start');
    _activeListenSessionId = null;
    _activeSpeechStatusCallback = null;
    _activeSpeechErrorCallback = null;
    _activeSpeakSessionId = null;
    await _cancelSpeechQueueInternal(stopPlayer: false);
    await _speechToText.cancel();
    _thinkingLoopActive = false;
    _thinkingLoopStopping = false;
    _thinkingLoopVolume = 0;
    try {
      await _thinkingLoopPlayer.dispose();
    } catch (_) {}
    _cancelActiveTtsRequest();
    try {
      await _speechPlayer.dispose();
    } catch (_) {}
    await _flutterTts.stop();
    await _deleteActiveSpeechFile();
    _log('dispose complete');
  }

  Future<void> _speakViaBackend(
    int sessionId,
    String text, {
    required String? localeTag,
    required VoidCallback? onStart,
  }) async {
    final CancelToken cancelToken = CancelToken();
    _activeTtsRequestCancelToken = cancelToken;

    late final Response<List<int>> response;
    try {
      response = await _apiClient.post<List<int>>(
        '/mobile/text-to-speech/synthesize',
        data: <String, dynamic>{
          'speechText': text,
          'locale': localeTag,
        },
        cancelToken: cancelToken,
        options: Options(
          responseType: ResponseType.bytes,
          headers: const <String, String>{
            'Accept': 'audio/mpeg',
          },
        ),
      );
    } finally {
      if (identical(_activeTtsRequestCancelToken, cancelToken)) {
        _activeTtsRequestCancelToken = null;
      }
    }

    _lastBackendTtsAiRunId = response.headers.value('x-ai-run-id');
    _lastBackendTtsProvider = response.headers.value('x-tts-provider');
    _lastBackendTtsVoiceId = response.headers.value('x-tts-voice-id');

    final bytes = response.data;
    if (bytes == null || bytes.isEmpty) {
      throw Exception('Backend returned empty audio response');
    }

    final directory = await getTemporaryDirectory();
    final file = File(
      '${directory.path}${Platform.pathSeparator}simi_tts_$sessionId.mp3',
    );
    await file.writeAsBytes(bytes, flush: true);
    _activeSpeechFilePath = file.path;

    if (_activeSpeakSessionId != sessionId || _ttsStopRequested) {
      await _deleteActiveSpeechFile();
      throw const _TextToSpeechCancelledException();
    }

    onStart?.call();
    await _speechPlayer
        .play(DeviceFileSource(file.path, mimeType: 'audio/mpeg'));
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

  Future<void> _prepareThinkingLoop() async {
    if (_thinkingLoopPrepared) {
      return;
    }

    _log('prepareThinkingLoop loading asset');
    await _thinkingLoopPlayer.setSource(
      AssetSource('audio/simi_thinking_loop.wav'),
    );
    _thinkingLoopPrepared = true;
  }

  Future<void> _resetRecognizerForListening() async {
    if (_speechToText.isListening) {
      _log('startListening canceling active recognizer session');
    }

    _log('startListening forcing recognizer reset');
    _activeListenSessionId = null;
    _activeSpeechStatusCallback = null;
    _activeSpeechErrorCallback = null;
    await _speechToText.cancel();
    _log(
      'startListening recognizer reset delay ${_recognizerResetDelay.inMilliseconds}ms',
    );
    await Future<void>.delayed(_recognizerResetDelay);
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

  bool get _hasActiveSpeechOutput {
    return _activeSpeakSessionId != null ||
        _activeTtsRequestCancelToken != null ||
        _activeSpeechFilePath != null;
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
    unawaited(_deleteActiveSpeechFile());

    if (treatAsCancel) {
      _onSpeakCancel?.call();
      return;
    }

    _onSpeakComplete?.call();
  }

  Future<void> _deleteActiveSpeechFile() async {
    final path = _activeSpeechFilePath;
    _activeSpeechFilePath = null;
    if (path == null || path.isEmpty) {
      return;
    }

    try {
      final file = File(path);
      if (await file.exists()) {
        await file.delete();
      }
    } catch (_) {
      // Best effort cleanup only.
    }
  }

  void _cancelActiveTtsRequest() {
    final CancelToken? cancelToken = _activeTtsRequestCancelToken;
    _activeTtsRequestCancelToken = null;
    if (cancelToken != null && !cancelToken.isCancelled) {
      cancelToken.cancel('Speech request canceled');
    }
  }

  bool get _hasFirebaseApp => Firebase.apps.isNotEmpty;

  bool _isCancellationError(Object error) {
    if (error is _TextToSpeechCancelledException) {
      return true;
    }

    if (error is DioException) {
      return error.type == DioExceptionType.cancel ||
          CancelToken.isCancel(error);
    }

    return false;
  }

  bool _shouldUseNativeFallback(Object error) {
    if (_isCancellationError(error)) {
      return false;
    }

    if (error is DioException) {
      final int? statusCode = error.response?.statusCode;
      if (statusCode != null && statusCode >= 400 && statusCode < 500) {
        return false;
      }
    }

    return true;
  }

  String _describeTtsError(Object error) {
    if (error is DioException) {
      final Object? data = error.response?.data;
      if (data is Map) {
        final Object? message = data['message'] ?? data['error'];
        if (message is String && message.trim().isNotEmpty) {
          return message.trim();
        }
      }

      if (data is String && data.trim().isNotEmpty) {
        return data.trim();
      }

      final int? statusCode = error.response?.statusCode;
      if (statusCode != null) {
        return 'Speech request failed ($statusCode).';
      }

      final String? message = error.message?.trim();
      if (message != null && message.isNotEmpty) {
        return message;
      }
    }

    final String message = error.toString().trim();
    const String exceptionPrefix = 'Exception: ';
    if (message.startsWith(exceptionPrefix)) {
      return message.substring(exceptionPrefix.length);
    }

    return message;
  }

  Future<void> _recordBackendTtsFailure(
    Object error, {
    required String stage,
    required String? localeTag,
    required int textLength,
    bool includeCrashlytics = true,
  }) async {
    final int statusCode =
        error is DioException ? error.response?.statusCode ?? -1 : -1;
    final String reason = _telemetryReason(error, statusCode: statusCode);
    final String provider = _telemetryValue(_lastBackendTtsProvider);
    final String voiceId = _telemetryValue(_lastBackendTtsVoiceId);
    final String aiRunId = _telemetryValue(_lastBackendTtsAiRunId);
    final String locale = _telemetryValue(localeTag, fallback: 'default');

    developer.log(
      'Backend TTS failure stage=$stage reason=$reason provider=$provider voiceId=$voiceId aiRunId=$aiRunId status=$statusCode length=$textLength locale=$locale',
      name: _telemetryLogName,
      error: error,
      stackTrace: StackTrace.current,
    );

    if (!_hasFirebaseApp) {
      return;
    }

    try {
      await FirebaseAnalytics.instance.logEvent(
        name: 'chat_tts_backend_failure',
        parameters: <String, Object>{
          'stage': stage,
          'reason': reason,
          'provider': provider,
          'locale': locale,
          'status_code': statusCode,
          'text_length': textLength,
        },
      );
    } catch (_) {
      // Best effort telemetry only.
    }

    if (includeCrashlytics) {
      try {
        await FirebaseCrashlytics.instance
            .setCustomKey('chat_tts_stage', stage);
        await FirebaseCrashlytics.instance
            .setCustomKey('chat_tts_reason', reason);
        await FirebaseCrashlytics.instance
            .setCustomKey('chat_tts_provider', provider);
        await FirebaseCrashlytics.instance
            .setCustomKey('chat_tts_voice_id', voiceId);
        await FirebaseCrashlytics.instance
            .setCustomKey('chat_tts_ai_run_id', aiRunId);
        await FirebaseCrashlytics.instance
            .setCustomKey('chat_tts_locale', locale);
        await FirebaseCrashlytics.instance
            .setCustomKey('chat_tts_text_length', textLength);
        await FirebaseCrashlytics.instance
            .setCustomKey('chat_tts_status_code', statusCode);
        await FirebaseCrashlytics.instance.recordError(
          error,
          StackTrace.current,
          reason: 'Chat backend text-to-speech failure',
          fatal: false,
        );
      } catch (_) {
        // Best effort telemetry only.
      }
    }
  }

  Future<void> _recordNativeFallbackActivation({
    required String reason,
    required String? localeTag,
    required int textLength,
  }) async {
    final String provider = _telemetryValue(_lastBackendTtsProvider);
    final String locale = _telemetryValue(localeTag, fallback: 'default');

    developer.log(
      'Native TTS fallback activated reason=$reason provider=$provider locale=$locale length=$textLength',
      name: _telemetryLogName,
    );

    if (!_hasFirebaseApp) {
      return;
    }

    try {
      await FirebaseAnalytics.instance.logEvent(
        name: 'chat_tts_native_fallback',
        parameters: <String, Object>{
          'reason': reason,
          'provider': provider,
          'locale': locale,
          'text_length': textLength,
        },
      );
    } catch (_) {
      // Best effort telemetry only.
    }
  }

  String _telemetryReason(Object error, {required int statusCode}) {
    if (statusCode >= 400) {
      return 'http_$statusCode';
    }

    if (error is DioException) {
      switch (error.type) {
        case DioExceptionType.connectionTimeout:
          return 'connection_timeout';
        case DioExceptionType.sendTimeout:
          return 'send_timeout';
        case DioExceptionType.receiveTimeout:
          return 'receive_timeout';
        case DioExceptionType.badCertificate:
          return 'bad_certificate';
        case DioExceptionType.badResponse:
          return 'bad_response';
        case DioExceptionType.cancel:
          return 'canceled';
        case DioExceptionType.connectionError:
          return 'connection_error';
        case DioExceptionType.unknown:
          return 'unknown_transport';
      }
    }

    return error.runtimeType.toString();
  }

  String _telemetryValue(String? value, {String fallback = 'unknown'}) {
    final String trimmed = value?.trim() ?? '';
    if (trimmed.isEmpty) {
      return fallback;
    }

    return trimmed.length <= 60 ? trimmed : trimmed.substring(0, 60);
  }

  void _log(String message) {
    if (!kDebugMode) {
      return;
    }

    debugPrint('$_logPrefix $message');
  }
}

final class _TextToSpeechCancelledException implements Exception {
  const _TextToSpeechCancelledException();

  @override
  String toString() => 'Text-to-speech canceled.';
}

class _QueuedSpeechChunk {
  _QueuedSpeechChunk({
    required this.sessionId,
    required this.index,
    required this.text,
    required this.cancelToken,
    this.synthesisFuture,
    this.inlineBuffer,
    this.serverChunkIndex,
  });

  final int sessionId;
  final int index;
  final String text;

  /// Legacy path — non-voice-mode speech chunks call the per-chunk
  /// `/mobile/text-to-speech/synthesize` endpoint and play from a temp
  /// file. Set when [inlineBuffer] is `null`.
  final Future<String>? synthesisFuture;

  /// Voice-mode inline path — audio bytes are accumulated into this
  /// buffer as `speech.audio` events arrive on the AGUI SSE stream.
  /// When the buffer closes (terminal frame, error, or watchdog) the
  /// drain loop writes its bytes to a temp MP3 and plays the file
  /// through `_speechPlayer`. Set when [synthesisFuture] is `null`.
  final _InlineChunkBuffer? inlineBuffer;

  /// The server's chunkIndex for inline chunks (matches `speech.audio`
  /// frame metadata). Useful for telemetry / logs; routing happens via
  /// [DeviceChatVoiceService._inlineChunkBuffers].
  final int? serverChunkIndex;

  final CancelToken cancelToken;
  String? filePath;
}

/// In-memory buffer for inline voice-mode audio bytes. Replaces the
/// old `AppendableAudioSource` (a `just_audio.StreamAudioSource`) now
/// that voice-mode emits a single chunk per assistant message and the
/// client plays one temp file via `audioplayers`.
///
/// Lifecycle:
/// 1. Created when a `speech.chunk` event arrives (via
///    [DeviceChatVoiceService.enqueueInlineSpeechChunk]) or lazily when
///    the first `speech.audio` frame for a new chunkIndex lands.
/// 2. [append] is called for each frame's bytes.
/// 3. [markClosed] is called when the chunk's terminal frame arrives,
///    when `speech.audio.error` fires for the chunk, or when the
///    watchdog gives up. The drain loop awaits [closed], snapshots
///    [snapshot], and writes the bytes to a temp file.
class _InlineChunkBuffer {
  final BytesBuilder _builder = BytesBuilder(copy: false);
  final Completer<void> _closed = Completer<void>();
  bool _isClosed = false;
  String? mime;

  /// Resolves once the chunk has been marked closed (cleanly or via
  /// error). The drain loop awaits this with a watchdog so a stuck
  /// server-side TTS pipeline can't wedge playback indefinitely.
  Future<void> get closed => _closed.future;

  /// `true` after [markClosed] has been called.
  bool get isClosed => _isClosed;

  /// Total bytes appended so far.
  int get length => _builder.length;

  /// Append [data] to the buffer. No-ops if the buffer is already
  /// closed (late frames after error / barge-in).
  void append(List<int> data) {
    if (_isClosed || data.isEmpty) return;
    _builder.add(data);
  }

  /// Mark the buffer closed and unblock anyone awaiting [closed].
  /// Idempotent.
  void markClosed() {
    if (_isClosed) return;
    _isClosed = true;
    if (!_closed.isCompleted) {
      _closed.complete();
    }
  }

  /// Returns a copy of the buffered bytes. The drain loop calls this
  /// after [closed] resolves, then writes the result to a temp file.
  Uint8List snapshot() => _builder.toBytes();
}
