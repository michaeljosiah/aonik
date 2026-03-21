// ─────────────────────────────────────────────────────────
//  AG-UI SSE Client
//
//  Streams AG-UI events from the AONIK backend over
//  Server-Sent Events using the app's existing Dio instance
//  (which already carries auth tokens and tenant headers).
//
//  Usage:
//    final client = AgUiClient(dio: ref.read(apiClientProvider));
//    await for (final event in client.run(input)) { ... }
// ─────────────────────────────────────────────────────────

import 'dart:async';
import 'dart:convert';

import 'package:dio/dio.dart';

import 'agui_models.dart';

/// Exception thrown when the AG-UI endpoint returns a non-200 response
/// or the SSE stream terminates unexpectedly.
class AgUiClientException implements Exception {
  const AgUiClientException(this.message, {this.statusCode});

  final String message;
  final int? statusCode;

  @override
  String toString() => 'AgUiClientException($statusCode): $message';
}

/// Thin SSE streaming client for the AG-UI protocol.
///
/// Wraps a [Dio] instance (which already has auth + tenant interceptors)
/// and exposes a `run()` method that POSTs to `/ai/agui` and yields
/// a `Stream<AgUiEvent>` from the SSE response.
class AgUiClient {
  AgUiClient({
    required Dio dio,
    this.endpoint = '/ai/agui',
  }) : _dio = dio;

  final Dio _dio;

  /// The AG-UI streaming endpoint path (relative to Dio's baseUrl).
  final String endpoint;

  /// Sends an AG-UI run request and returns a stream of parsed events.
  ///
  /// The stream completes when the server closes the SSE connection
  /// (typically after emitting `RUN_FINISHED` or `RUN_ERROR`).
  ///
  /// Throws [AgUiClientException] if the request fails (non-200, network
  /// error, etc.).
  Stream<AgUiEvent> run(
    AgUiRunInput input, {
    CancelToken? cancelToken,
  }) {
    // Use a StreamController so we can manage the async Dio response
    // lifecycle and properly handle cancellation.
    late StreamController<AgUiEvent> controller;
    CancelToken? effectiveCancelToken;

    controller = StreamController<AgUiEvent>(
      onCancel: () {
        effectiveCancelToken?.cancel('Stream listener cancelled');
      },
    );

    effectiveCancelToken = cancelToken ?? CancelToken();

    _streamEvents(controller, input, effectiveCancelToken);

    return controller.stream;
  }

  Future<void> _streamEvents(
    StreamController<AgUiEvent> controller,
    AgUiRunInput input,
    CancelToken cancelToken,
  ) async {
    try {
      final response = await _dio.post<ResponseBody>(
        endpoint,
        data: input.toJson(),
        options: Options(
          headers: {
            'Accept': 'text/event-stream',
            'Content-Type': 'application/json',
          },
          // Dio streams the response body chunk-by-chunk.
          responseType: ResponseType.stream,
          // SSE connections can be long-lived.
          receiveTimeout: const Duration(minutes: 5),
        ),
        cancelToken: cancelToken,
      );

      if (response.statusCode != null && response.statusCode! >= 400) {
        controller.addError(AgUiClientException(
          'AG-UI endpoint returned ${response.statusCode}',
          statusCode: response.statusCode,
        ));
        await controller.close();
        return;
      }

      final stream = response.data?.stream;
      if (stream == null) {
        controller.addError(
          const AgUiClientException('No response stream from AG-UI endpoint'),
        );
        await controller.close();
        return;
      }

      // SSE events are `data: {json}\n\n`. We accumulate bytes into lines,
      // splitting on \n. Each non-empty line starting with `data:` is one event.
      String buffer = '';

      await for (final chunk in stream) {
        if (controller.isClosed) break;

        buffer += utf8.decode(chunk, allowMalformed: true);

        // Process all complete lines in the buffer.
        while (buffer.contains('\n')) {
          final newlineIndex = buffer.indexOf('\n');
          final line = buffer.substring(0, newlineIndex);
          buffer = buffer.substring(newlineIndex + 1);

          final event = parseSseLine(line);
          if (event != null && !controller.isClosed) {
            controller.add(event);

            // Auto-close after terminal events.
            if (event is RunFinishedEvent || event is RunErrorEvent) {
              await controller.close();
              return;
            }
          }
        }
      }

      // Process any remaining data in the buffer.
      if (buffer.isNotEmpty && !controller.isClosed) {
        final event = parseSseLine(buffer);
        if (event != null) {
          controller.add(event);
        }
      }

      if (!controller.isClosed) {
        await controller.close();
      }
    } on DioException catch (e) {
      if (e.type == DioExceptionType.cancel) {
        // Cancellation is expected — close silently.
        if (!controller.isClosed) {
          await controller.close();
        }
        return;
      }

      if (!controller.isClosed) {
        controller.addError(AgUiClientException(
          e.message ?? 'Network error during AG-UI stream',
          statusCode: e.response?.statusCode,
        ));
        await controller.close();
      }
    } catch (e, st) {
      if (!controller.isClosed) {
        controller.addError(e, st);
        await controller.close();
      }
    }
  }
}
