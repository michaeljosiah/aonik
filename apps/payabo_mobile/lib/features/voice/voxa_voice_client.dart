// ignore_for_file: public_member_api_docs

import 'dart:async';
import 'dart:convert';
import 'dart:developer' as developer;
import 'dart:typed_data';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:web_socket_channel/io.dart';
import 'package:web_socket_channel/web_socket_channel.dart';

import '../../app/auth/auth_session_store.dart';
import '../../app/environment/app_environment.dart';
import '../../app/environment/environment_provider.dart';

/// Client for the AONIK /ai/voice WebSocket endpoint (spec 024 Phase H).
///
/// Owns the WebSocket lifecycle and the wire-protocol layer. Audio I/O — mic capture
/// and PCM playback — is intentionally NOT included here so the client can be unit-tested
/// without native plugins; the calling widget plugs a recorder into [sendPcm] and an
/// audio sink into [audioFrames].
///
/// Wire protocol (mirrors Voxa.Transports.WebSocket.Protocol.WireProtocol):
/// * **Outbound** — `{type:"hello", agentId, frontendTools, client}` (sent on connect),
///   binary frames carrying 16-bit signed LE PCM (mono), and `{type:"end"}` for graceful
///   close.
/// * **Inbound** — `{type:"transcription"|"text"|"speaking"|"interruption"|"status"|
///   "error"|"end"|"threadReady", ...}` text envelopes, plus binary PCM frames the
///   server emits as the bot speaks.
///
/// Auth: the access token is sent as `?access_token=<jwt>` because the browser/Flutter
/// WebSocket APIs can't reliably set Authorization headers; the AONIK auth setup honours
/// that fallback for this path. (Same pattern the admin UI's LiveVoiceTestCard uses.)
///
/// **Not yet wired into the chat screen.** Phase H follow-up swaps the per-screen
/// usage of `chatVoiceServiceProvider` for this client when the
/// [voxaVoiceModeEnabledProvider] flag is true.
class VoxaVoiceClient {
  VoxaVoiceClient({
    required String apiBaseUrl,
    required Future<String?> Function() getAccessToken,
    String? tenantId,
  })  : _apiBaseUrl = apiBaseUrl,
        _getAccessToken = getAccessToken,
        _tenantId = tenantId;

  final String _apiBaseUrl;
  final Future<String?> Function() _getAccessToken;
  // ignore: unused_field — surfaced as an X-Tenant-Id query fallback if the auth setup
  // grows that contract; today the JWT carries the tenant claim.
  final String? _tenantId;

  WebSocketChannel? _channel;
  StreamSubscription<dynamic>? _channelSubscription;
  bool _disposed = false;

  final StreamController<Uint8List> _audioFramesController =
      StreamController<Uint8List>.broadcast();
  final StreamController<VoxaVoiceEvent> _eventsController =
      StreamController<VoxaVoiceEvent>.broadcast();
  final StreamController<VoxaConnectionState> _stateController =
      StreamController<VoxaConnectionState>.broadcast();

  VoxaConnectionState _state = VoxaConnectionState.idle;

  /// PCM frames emitted by the server (24 kHz signed LE, mono). Plug an audio sink here
  /// to play the bot's speech. Broadcast — multiple listeners see the same frames.
  Stream<Uint8List> get audioFrames => _audioFramesController.stream;

  /// Typed events parsed from server text envelopes (transcription, status, error, etc.).
  Stream<VoxaVoiceEvent> get events => _eventsController.stream;

  /// Connection lifecycle stream. Mirrors [state] for widgets that prefer streams over
  /// pull-based reads.
  Stream<VoxaConnectionState> get stateChanges => _stateController.stream;

  VoxaConnectionState get state => _state;

  /// Open the WSS connection and send the hello envelope. Throws on transport / auth /
  /// hello failures (the caller should surface the error). Safe to await — returns once
  /// the WebSocket open handshake finishes and hello has been sent. Subsequent inbound
  /// frames flow into [audioFrames] and [events].
  Future<void> connect({
    required String agentId,
    String? chatThreadId,
    List<String> frontendTools = const <String>[],
    Map<String, String> clientInfo = const <String, String>{},
  }) async {
    if (_disposed) {
      throw StateError('VoxaVoiceClient has been disposed.');
    }
    if (_channel != null) {
      throw StateError(
          'VoxaVoiceClient is already connected; call close() before reconnecting.');
    }
    if (agentId.trim().isEmpty) {
      throw ArgumentError.value(agentId, 'agentId', 'agentId is required.');
    }

    _setState(VoxaConnectionState.connecting);

    final String? token = await _getAccessToken();
    if (token == null || token.trim().isEmpty) {
      _setState(VoxaConnectionState.error);
      throw StateError('No access token available — sign in again.');
    }

    final Uri wsUri = _buildWsUri(_apiBaseUrl, token);
    _log('connect → ${wsUri.replace(queryParameters: <String, String>{'access_token': '<redacted>'})}');

    final IOWebSocketChannel channel;
    try {
      // The IOWebSocketChannel uses HttpClient under the hood and respects the upgrade
      // handshake's headers — although we don't rely on Authorization (the ?access_token
      // query string carries the JWT), passing the X-Tenant-Id header keeps server-side
      // tenant resolution consistent with the rest of the Dio path. Works on Android,
      // iOS, desktop. Web targets would need package:web_socket_channel/html.dart.
      channel = IOWebSocketChannel.connect(
        wsUri,
        headers: <String, dynamic>{
          if (_tenantId != null && _tenantId.isNotEmpty) 'X-Tenant-Id': _tenantId,
        },
        pingInterval: const Duration(seconds: 30),
      );
    } catch (err) {
      _log('IOWebSocketChannel.connect threw synchronously: $err');
      _setState(VoxaConnectionState.error);
      rethrow;
    }
    _channel = channel;

    // Listen BEFORE awaiting handshake so any instant rejection is captured
    // by our error/done handlers (not just thrown from channel.ready).
    _channelSubscription = channel.stream.listen(
      _onChannelMessage,
      onError: (Object error, StackTrace stackTrace) {
        _log('transport ERROR: $error');
        _eventsController.add(VoxaVoiceEvent.error(
          message: error.toString(),
          code: 'transport',
        ));
        _setState(VoxaConnectionState.error);
        unawaited(_teardown());
      },
      onDone: () {
        _log('socket onDone (state was $_state)');
        // Server closed the socket — flip to closed unless we already errored.
        if (_state != VoxaConnectionState.error) {
          _setState(VoxaConnectionState.closed);
        }
        unawaited(_teardown());
      },
      cancelOnError: false,
    );

    // `IOWebSocketChannel.connect` returns immediately without confirming
    // the HTTP→WS upgrade succeeded. If the upgrade fails (401, 426, TLS
    // error, or Azure Container Apps not routing the upgrade), we'd
    // otherwise plough on and send `hello` into a half-open socket — and
    // never see a transport error fire because the listener attaches
    // before any error has been queued. Awaiting `channel.ready` surfaces
    // the failure explicitly and lets the caller see why.
    try {
      await channel.ready;
      _log('handshake OK');
    } catch (err) {
      _log('handshake FAILED: $err');
      _setState(VoxaConnectionState.error);
      _eventsController.add(VoxaVoiceEvent.error(
        message: 'WebSocket handshake failed: $err',
        code: 'handshake',
      ));
      await _teardown();
      rethrow;
    }

    // Hello envelope. The server validates agentId + frontendTools allow-list before
    // emitting any frames; if either is wrong, we'll get a {type:"error"} envelope back
    // and the socket will close.
    final Map<String, dynamic> helloEnvelope = <String, dynamic>{
      'type': 'hello',
      'agentId': agentId.trim(),
      if (chatThreadId != null && chatThreadId.trim().isNotEmpty)
        'chatThreadId': chatThreadId.trim(),
      'frontendTools': frontendTools,
      'client': <String, String>{
        'app': 'payabo_mobile',
        ...clientInfo,
      },
    };
    channel.sink.add(jsonEncode(helloEnvelope));
    _log('sent hello agentId=$agentId tools=${frontendTools.length} threadId=${chatThreadId ?? "-"}');

    _setState(VoxaConnectionState.connected);
  }

  /// Send one PCM frame to the server. No-op (drops the frame) if the socket isn't
  /// open — callers don't need to gate the recorder's `onChunk` themselves.
  void sendPcm(Uint8List pcm) {
    if (pcm.isEmpty) return;
    final WebSocketChannel? channel = _channel;
    if (channel == null) return;
    if (_state != VoxaConnectionState.connected) return;
    try {
      channel.sink.add(pcm);
    } catch (err) {
      _eventsController.add(VoxaVoiceEvent.error(
        message: 'Failed to send PCM frame: $err',
        code: 'send-failed',
      ));
    }
  }

  /// Send the graceful-end envelope. The server will drain remaining frames then close
  /// the socket; callers should typically call [close] right after.
  void sendEnd() {
    final WebSocketChannel? channel = _channel;
    if (channel == null) return;
    try {
      channel.sink.add(jsonEncode(<String, dynamic>{'type': 'end'}));
    } catch (_) {
      // Best-effort — the socket is about to close anyway.
    }
  }

  /// Close the WebSocket and tear down listeners. Safe to call multiple times. Auto-sends
  /// `{type:"end"}` first if the connection is still open.
  Future<void> close() async {
    if (_channel != null && _state == VoxaConnectionState.connected) {
      sendEnd();
    }
    await _teardown();
    if (_state != VoxaConnectionState.error) {
      _setState(VoxaConnectionState.closed);
    }
  }

  /// Permanently dispose the client. Closes the connection and the broadcast streams.
  /// Call from the Riverpod onDispose hook.
  Future<void> dispose() async {
    if (_disposed) return;
    _disposed = true;
    await _teardown();
    await _audioFramesController.close();
    await _eventsController.close();
    await _stateController.close();
  }

  // ── Internals ────────────────────────────────────────────────────────────────────

  void _setState(VoxaConnectionState next) {
    if (_state == next) return;
    _state = next;
    _stateController.add(next);
  }

  void _onChannelMessage(dynamic message) {
    if (message is Uint8List) {
      // Audio frames are high-volume; logging each one floods the console.
      _audioFramesController.add(message);
      return;
    }
    if (message is List<int>) {
      _audioFramesController.add(Uint8List.fromList(message));
      return;
    }
    if (message is String) {
      final VoxaVoiceEvent? event = _parseTextEnvelope(message);
      if (event != null) {
        _log('inbound ${event.runtimeType}');
        _eventsController.add(event);
      } else {
        _log('inbound unparseable text=${message.length > 80 ? "${message.substring(0, 80)}..." : message}');
      }
      return;
    }
    // Unknown frame type — Voxa's wire protocol says drop unknowns.
  }

  static void _log(String msg) {
    developer.log(msg, name: 'VoxaVoiceClient');
  }

  Future<void> _teardown() async {
    final StreamSubscription<dynamic>? sub = _channelSubscription;
    _channelSubscription = null;
    if (sub != null) {
      try {
        await sub.cancel();
      } catch (_) {
        // already cancelled
      }
    }
    final WebSocketChannel? channel = _channel;
    _channel = null;
    if (channel != null) {
      try {
        await channel.sink.close();
      } catch (_) {
        // already closed
      }
    }
  }

  static VoxaVoiceEvent? _parseTextEnvelope(String raw) {
    Map<String, dynamic> json;
    try {
      final dynamic decoded = jsonDecode(raw);
      if (decoded is! Map<String, dynamic>) return null;
      json = decoded;
    } catch (_) {
      return null;
    }
    final String? type = json['type'] as String?;
    switch (type) {
      case 'transcription':
        return VoxaVoiceEvent.transcription(
          text: (json['text'] as String?) ?? '',
          isFinal: (json['isFinal'] as bool?) ?? false,
          language: json['language'] as String?,
        );
      case 'text':
        return VoxaVoiceEvent.botText(text: (json['text'] as String?) ?? '');
      case 'speaking':
        return VoxaVoiceEvent.speaking(
          who: (json['who'] as String?) ?? 'bot',
          started: (json['started'] as bool?) ?? false,
        );
      case 'interruption':
        return const VoxaVoiceEvent.interruption();
      case 'status':
        return VoxaVoiceEvent.status(message: (json['message'] as String?) ?? '');
      case 'error':
        return VoxaVoiceEvent.error(
          message: (json['message'] as String?) ?? 'Server error',
          code: json['code'] as String?,
        );
      case 'end':
        return const VoxaVoiceEvent.ended();
      case 'threadReady':
        final String? threadId = json['chatThreadId'] as String?;
        if (threadId == null) return null;
        return VoxaVoiceEvent.threadReady(chatThreadId: threadId);
      case 'toolCall':
        return VoxaVoiceEvent.toolCall(
          callId: (json['callId'] as String?) ?? '',
          name: (json['name'] as String?) ?? '',
          argumentsJson: (json['argumentsJson'] as String?) ?? '{}',
        );
      default:
        return null;
    }
  }

  /// Build the wss(s):// URL with `?access_token=`. Mirrors the admin UI helper so the
  /// behaviour is identical across surfaces. apiBaseUrl is typically an absolute
  /// `https://aonik-{env}-api.{...}.azurecontainerapps.io` on mobile.
  static Uri _buildWsUri(String apiBaseUrl, String accessToken) {
    final Uri base = Uri.parse(apiBaseUrl);
    final String scheme = base.scheme == 'https' ? 'wss' : 'ws';
    final String basePath = base.path.replaceFirst(RegExp(r'/+$'), '');
    return Uri(
      scheme: scheme,
      host: base.host,
      port: base.hasPort ? base.port : null,
      path: '$basePath/ai/voice',
      queryParameters: <String, String>{'access_token': accessToken},
    );
  }
}

// ── Events ─────────────────────────────────────────────────────────────────────────

/// Typed wire envelopes. Sealed so callers can `switch` exhaustively and the analyzer
/// flags missing cases when new envelopes are added.
sealed class VoxaVoiceEvent {
  const VoxaVoiceEvent();

  const factory VoxaVoiceEvent.transcription({
    required String text,
    required bool isFinal,
    String? language,
  }) = TranscriptionEvent;

  const factory VoxaVoiceEvent.botText({required String text}) = BotTextEvent;

  const factory VoxaVoiceEvent.speaking({
    required String who,
    required bool started,
  }) = SpeakingEvent;

  const factory VoxaVoiceEvent.interruption() = InterruptionEvent;

  const factory VoxaVoiceEvent.status({required String message}) = StatusEvent;

  const factory VoxaVoiceEvent.error({required String message, String? code}) =
      ErrorEvent;

  const factory VoxaVoiceEvent.ended() = EndedEvent;

  const factory VoxaVoiceEvent.threadReady({required String chatThreadId}) =
      ThreadReadyEvent;

  const factory VoxaVoiceEvent.toolCall({
    required String callId,
    required String name,
    required String argumentsJson,
  }) = ToolCallEvent;
}

class TranscriptionEvent extends VoxaVoiceEvent {
  const TranscriptionEvent({
    required this.text,
    required this.isFinal,
    this.language,
  });
  final String text;
  final bool isFinal;
  final String? language;
}

class BotTextEvent extends VoxaVoiceEvent {
  const BotTextEvent({required this.text});
  final String text;
}

class SpeakingEvent extends VoxaVoiceEvent {
  const SpeakingEvent({required this.who, required this.started});
  final String who;
  final bool started;
}

class InterruptionEvent extends VoxaVoiceEvent {
  const InterruptionEvent();
}

class StatusEvent extends VoxaVoiceEvent {
  const StatusEvent({required this.message});
  final String message;
}

class ErrorEvent extends VoxaVoiceEvent {
  const ErrorEvent({required this.message, this.code});
  final String message;
  final String? code;
}

class EndedEvent extends VoxaVoiceEvent {
  const EndedEvent();
}

class ThreadReadyEvent extends VoxaVoiceEvent {
  const ThreadReadyEvent({required this.chatThreadId});
  final String chatThreadId;
}

class ToolCallEvent extends VoxaVoiceEvent {
  const ToolCallEvent({
    required this.callId,
    required this.name,
    required this.argumentsJson,
  });
  final String callId;
  final String name;
  final String argumentsJson;
}

enum VoxaConnectionState { idle, connecting, connected, closed, error }

// ── Riverpod wiring ────────────────────────────────────────────────────────────────

/// Feature flag for the new voice-mode WebSocket pipeline. Defaults to `true`
/// — the chat screen renders the slim 4-phase realtime stage and routes mic
/// + bot audio through the Voxa WSS pipeline. Flip back to `false` here to
/// fall back to the legacy SSE-based `chatVoiceServiceProvider` path while
/// the new pipeline is being stabilised.
final Provider<bool> voxaVoiceModeEnabledProvider = Provider<bool>((Ref ref) {
  return true;
});

/// Per-screen [VoxaVoiceClient] factory. Each `ref.watch(voxaVoiceClientProvider)` call
/// returns a fresh client — voice sessions are connection-scoped and reusing one client
/// across multiple `connect()` calls would be confusing, so the provider is `autoDispose`
/// and disposes the underlying client when no widget is listening. Riverpod 3.x folded
/// AutoDispose* into the base `Provider` type — the .autoDispose modifier flows through.
final Provider<VoxaVoiceClient> voxaVoiceClientProvider =
    Provider.autoDispose<VoxaVoiceClient>((Ref ref) {
  final AppEnvironment environment = ref.watch(appEnvironmentProvider);
  final AuthSessionStore authStore = ref.watch(authSessionStoreProvider);

  final VoxaVoiceClient client = VoxaVoiceClient(
    apiBaseUrl: environment.runtimeApiBaseUrl,
    tenantId: environment.tenantId,
    getAccessToken: () async {
      final AuthSession? session = await authStore.read();
      if (session == null || !session.hasAccessToken || session.isExpired) {
        return null;
      }
      return session.accessToken;
    },
  );

  ref.onDispose(() {
    unawaited(client.dispose());
  });

  return client;
});
