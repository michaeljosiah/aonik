// ─────────────────────────────────────────────────────────
//  AG-UI SSE Client
//
//  Streams AG-UI events from the AONIK backend over
//  Server-Sent Events using the app's existing Dio instance
//  (which already carries auth tokens and tenant headers).
//
//  Supports:
//  - Streaming all AG-UI event types
//  - Frontend tool definitions (sent to server so LLM sees them)
//  - Frontend tool execution with automatic re-run loop
//  - Human-in-the-loop via the confirmAction tool pattern
//
//  Usage:
//    final client = AgUiClient(dio: ref.read(apiClientProvider));
//    await for (final event in client.run(input)) { ... }
//
//  With frontend tools:
//    await for (final event in client.runWithTools(
//      input,
//      frontendTools: { 'confirmAction': FrontendToolRegistration(...) },
//    )) { ... }
// ─────────────────────────────────────────────────────────

import 'dart:async';
import 'dart:convert';
import 'dart:developer' as developer;

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

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

/// Context passed to a frontend tool handler when invoked.
class FrontendToolContext {
  const FrontendToolContext({
    required this.toolCallId,
    required this.toolCallName,
  });

  final String toolCallId;
  final String toolCallName;
}

/// A handler that executes a frontend tool and returns the result string.
///
/// The handler receives the parsed argument map and a context with the
/// tool call metadata. It may return a [Future] — for example, the
/// `confirmAction` handler returns a future that resolves only when the
/// user taps Approve or Reject.
typedef FrontendToolHandler = Future<String> Function(
  Map<String, dynamic> args,
  FrontendToolContext context,
);

/// Registration of a frontend-defined tool that the LLM can call.
///
/// The [tool] definition (name, description, JSON schema) is sent to the
/// server so the LLM knows the tool exists. When the LLM calls it, the
/// [handler] executes client-side and the result is sent back via re-run.
class FrontendToolRegistration {
  const FrontendToolRegistration({
    required this.tool,
    required this.handler,
  });

  /// The tool definition sent to the server in `RunAgentInput.tools`.
  final AgUiToolDefinition tool;

  /// The client-side handler that executes when the LLM calls this tool.
  final FrontendToolHandler handler;
}

/// A tool definition sent in the AG-UI request body so the LLM can see it.
class AgUiToolDefinition {
  const AgUiToolDefinition({
    required this.name,
    required this.description,
    required this.parameters,
  });

  final String name;
  final String description;

  /// JSON Schema for the tool parameters.
  final Map<String, dynamic> parameters;

  Map<String, dynamic> toJson() => {
        'name': name,
        'description': description,
        'parameters': parameters,
      };
}

/// Thin SSE streaming client for the AG-UI protocol.
///
/// Wraps a [Dio] instance (which already has auth + tenant interceptors)
/// and exposes:
/// - [run] — low-level single SSE stream (no tool execution)
/// - [runWithTools] — full AG-UI loop with frontend tool execution and re-runs
class AgUiClient {
  AgUiClient({
    required Dio dio,
    this.endpoint = '/ai/agui',
    this.maxToolReruns = 10,
  }) : _dio = dio;

  final Dio _dio;

  /// The AG-UI streaming endpoint path (relative to Dio's baseUrl).
  final String endpoint;

  /// Maximum number of automatic re-runs for client-side tool execution.
  /// Prevents infinite loops. Defaults to 10.
  final int maxToolReruns;

  static int _traceCounter = 0;

  // ─────────────────────────────────────────────────────────
  //  Low-level: single SSE stream (no re-run loop)
  // ─────────────────────────────────────────────────────────

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
    bool closeOnTerminalEvent = true,
  }) {
    late StreamController<AgUiEvent> controller;
    CancelToken? effectiveCancelToken;

    controller = StreamController<AgUiEvent>(
      onCancel: () {
        effectiveCancelToken?.cancel('Stream listener cancelled');
      },
    );

    effectiveCancelToken = cancelToken ?? CancelToken();

    _streamEvents(
      controller,
      input,
      effectiveCancelToken,
      closeOnTerminalEvent: closeOnTerminalEvent,
    );

    return controller.stream;
  }

  // ─────────────────────────────────────────────────────────
  //  High-level: AG-UI loop with frontend tool execution
  // ─────────────────────────────────────────────────────────

  /// Sends an AG-UI run request with frontend tool support.
  ///
  /// When the agent calls a frontend-defined tool:
  /// 1. The tool call events (START → ARGS → END) are emitted to the stream
  /// 2. After `RUN_FINISHED`, unresolved frontend tool calls are detected
  /// 3. Each frontend handler is invoked (may be async — e.g. user approval)
  /// 4. Tool results are appended to the conversation messages
  /// 5. The agent is automatically re-run with the updated messages
  ///
  /// This loop continues until the agent finishes without pending frontend
  /// tool calls, or [maxToolReruns] is reached.
  Stream<AgUiEvent> runWithTools(
    AgUiRunInput input, {
    Map<String, FrontendToolRegistration> frontendTools = const {},
    CancelToken? cancelToken,
  }) {
    // If no frontend tools are registered, fall back to simple run.
    if (frontendTools.isEmpty) {
      return run(input, cancelToken: cancelToken);
    }

    late StreamController<AgUiEvent> controller;
    CancelToken? effectiveCancelToken;

    controller = StreamController<AgUiEvent>(
      onCancel: () {
        effectiveCancelToken?.cancel('Stream listener cancelled');
      },
    );

    effectiveCancelToken = cancelToken ?? CancelToken();

    // Inject tool definitions into the input so the LLM sees them.
    final toolDefinitions =
        frontendTools.values.map((r) => r.tool.toJson()).toList();

    final inputWithTools = AgUiRunInput(
      threadId: input.threadId,
      runId: input.runId,
      agentId: input.agentId,
      messages: input.messages,
      state: input.state,
      tools: [
        ...?input.tools,
        ...toolDefinitions,
      ],
      context: input.context,
      forwardedProps: input.forwardedProps,
      voiceMode: input.voiceMode,
      audioFormat: input.audioFormat,
    );

    _runWithToolsLoop(
      controller,
      inputWithTools,
      frontendTools,
      effectiveCancelToken,
    );

    return controller.stream;
  }

  /// The re-run loop: streams events, detects frontend tool calls,
  /// executes handlers, appends results, and re-runs until done.
  Future<void> _runWithToolsLoop(
    StreamController<AgUiEvent> controller,
    AgUiRunInput input,
    Map<String, FrontendToolRegistration> frontendTools,
    CancelToken cancelToken,
  ) async {
    var currentInput = input;
    var rerunCount = 0;

    try {
      while (!controller.isClosed) {
        if (cancelToken.isCancelled) break;

        // Track tool calls in this run for client-side execution.
        final pendingToolCalls = <String, _PendingToolCall>{};
        final serverResolvedToolCalls = <String>{};
        var shouldRerun = false;

        // Stream one full run's events to the public controller.
        // `sync: true` delivers events to our listener synchronously,
        // avoiding an extra microtask per event during high-frequency
        // token streaming. This is safe because we only consume the stream
        // on a single listener inside this loop.
        final runController = StreamController<AgUiEvent>(sync: true);
        final runCancelToken = CancelToken();

        cancelToken.whenCancel.then((error) {
          if (!runCancelToken.isCancelled) {
            runCancelToken.cancel(error.error);
          }
        }).catchError((_) {
          // Ignore propagation errors from cancellation listeners.
        });

        _streamEvents(
          runController,
          currentInput,
          runCancelToken,
          closeOnTerminalEvent: false,
        );

        await for (final event in runController.stream) {
          if (controller.isClosed) return;

          // Track tool calls internally.
          switch (event) {
            case ToolCallStartEvent():
              pendingToolCalls[event.toolCallId] = _PendingToolCall(
                toolCallId: event.toolCallId,
                name: event.toolCallName,
                parentMessageId: event.parentMessageId,
              );

            case ToolCallArgsEvent():
              pendingToolCalls[event.toolCallId]?.argFragments.add(event.delta);

            case ToolCallResultEvent():
              // Server-side tools get results via TOOL_CALL_RESULT events;
              // these don't need client-side execution.
              serverResolvedToolCalls.add(event.toolCallId);

            default:
              break;
          }

          // Forward event to the public stream (except RUN_FINISHED when
          // we might need to re-run for frontend tools).
          if (event is RunFinishedEvent) {
            // Determine if we need to re-run.
            final frontendPendingCalls = pendingToolCalls.entries
                .where((e) =>
                    !serverResolvedToolCalls.contains(e.key) &&
                    frontendTools.containsKey(e.value.name))
                .map((e) => e.value)
                .toList();

            if (frontendPendingCalls.isEmpty) {
              // No frontend tools to execute — forward RUN_FINISHED and done.
              controller.add(event);
              break;
            }

            // Guard against infinite re-run loops.
            rerunCount++;
            if (rerunCount > maxToolReruns) {
              controller.add(event);
              break;
            }

            developer.log(
              'Frontend tool re-run #$rerunCount: ${frontendPendingCalls.map((c) => c.name).join(', ')}',
              name: 'AgUiClient',
            );

            // Execute frontend tool handlers.
            final toolResultMessages = <AgUiMessage>[];
            final assistantToolCalls = <AgUiToolCall>[];

            for (final call in frontendPendingCalls) {
              final registration = frontendTools[call.name]!;
              final argsString = call.argFragments.join('');

              assistantToolCalls.add(AgUiToolCall(
                id: call.toolCallId,
                function: AgUiFunctionCall(
                  name: call.name,
                  arguments: argsString,
                ),
              ));

              String result;
              try {
                final parsedArgs = argsString.isNotEmpty
                    ? jsonDecode(argsString) as Map<String, dynamic>
                    : <String, dynamic>{};

                result = await registration.handler(
                  parsedArgs,
                  FrontendToolContext(
                    toolCallId: call.toolCallId,
                    toolCallName: call.name,
                  ),
                );
              } catch (e) {
                result = e.toString();
              }

              toolResultMessages.add(AgUiMessage.tool(
                id: 'tool-result-${call.toolCallId}',
                toolCallId: call.toolCallId,
                content: result,
              ));
            }

            // Build updated messages: existing + assistant w/ tool calls + tool results.
            final updatedMessages = <AgUiMessage>[
              ...currentInput.messages,
              AgUiMessage.assistant(
                id: 'assistant-tc-${DateTime.now().millisecondsSinceEpoch}',
                toolCalls: assistantToolCalls,
              ),
              ...toolResultMessages,
            ];

            // Prepare re-run input.
            currentInput = AgUiRunInput(
              threadId: currentInput.threadId,
              runId:
                  'rerun_${DateTime.now().millisecondsSinceEpoch}_$rerunCount',
              agentId: currentInput.agentId,
              messages: updatedMessages,
              state: currentInput.state,
              tools: currentInput.tools,
              context: currentInput.context,
              forwardedProps: currentInput.forwardedProps,
              voiceMode: currentInput.voiceMode,
              audioFormat: currentInput.audioFormat,
            );

            developer.log(
              'Re-running agent with ${updatedMessages.length} messages (runId=${currentInput.runId})',
              name: 'AgUiClient',
            );

            // Don't forward RUN_FINISHED — restart the outer loop with the
            // tool results now appended to the conversation history.
            shouldRerun = true;
            break;
          }

          controller.add(event);
        }

        if (shouldRerun) {
          continue;
        }

        // If we didn't schedule another run, we're done.
        break;
      }

      if (!controller.isClosed) {
        await controller.close();
      }
    } catch (e, st) {
      if (!controller.isClosed) {
        controller.addError(e, st);
        await controller.close();
      }
    }
  }

  // ─────────────────────────────────────────────────────────
  //  Internal: single SSE stream
  // ─────────────────────────────────────────────────────────

  Future<void> _streamEvents(
    StreamController<AgUiEvent> controller,
    AgUiRunInput input,
    CancelToken cancelToken, {
    bool closeOnTerminalEvent = true,
  }) async {
    final traceId =
        'agui_${DateTime.now().millisecondsSinceEpoch}_${_traceCounter++}';
    final stopwatch = Stopwatch()..start();
    final requestHeaders = <String, dynamic>{
      'Accept': 'text/event-stream',
      'Content-Type': 'application/json',
    };
    if (!kIsWeb) {
      requestHeaders['X-AgUi-Trace-Id'] = traceId;
    }
    var sawFirstEvent = false;
    var sawFirstTextDelta = false;

    try {
      developer.log(
        '[trace:$traceId] POST $endpoint dispatch threadId=${input.threadId ?? '-'} runId=${input.runId ?? '-'} agentId=${input.agentId ?? '-'} messages=${input.messages.length}',
        name: 'AgUiClient',
      );

      final response = await _dio.post<ResponseBody>(
        endpoint,
        data: input.toJson(),
        options: Options(
          headers: requestHeaders,
          // Dio streams the response body chunk-by-chunk.
          responseType: ResponseType.stream,
          // SSE connections can be long-lived.
          receiveTimeout: const Duration(minutes: 5),
        ),
        cancelToken: cancelToken,
      );

      developer.log(
        '[trace:$traceId] response headers received at ${stopwatch.elapsedMilliseconds}ms status=${response.statusCode}',
        name: 'AgUiClient',
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

      // SSE events arrive as `data: {json}\n\n`. We accumulate decoded bytes
      // into a single remainder string and scan only the newly appended
      // region for newlines — avoiding the O(n²) substring/toString work
      // the previous implementation performed per chunk.
      String remainder = '';

      await for (final chunk in stream) {
        if (controller.isClosed) break;

        remainder += utf8.decode(chunk, allowMalformed: true);

        // Split the accumulated buffer into complete lines plus a trailing
        // (possibly incomplete) fragment, which we carry over to the next
        // chunk.
        final lines = remainder.split('\n');
        remainder = lines.removeLast();

        for (final line in lines) {
          final event = parseSseLine(line);
          if (event == null || controller.isClosed) {
            continue;
          }

          if (!sawFirstEvent) {
            sawFirstEvent = true;
            developer.log(
              '[trace:$traceId] first SSE event ${event.type.wire} at ${stopwatch.elapsedMilliseconds}ms',
              name: 'AgUiClient',
            );
          }

          if (!sawFirstTextDelta && event is TextMessageContentEvent) {
            sawFirstTextDelta = true;
            developer.log(
              '[trace:$traceId] first text delta at ${stopwatch.elapsedMilliseconds}ms messageId=${event.messageId}',
              name: 'AgUiClient',
            );
          }

          if (event is RunFinishedEvent) {
            developer.log(
              '[trace:$traceId] RUN_FINISHED at ${stopwatch.elapsedMilliseconds}ms metrics=${event.metrics}',
              name: 'AgUiClient',
            );
          }

          if (event is RunErrorEvent) {
            developer.log(
              '[trace:$traceId] RUN_ERROR at ${stopwatch.elapsedMilliseconds}ms code=${event.code} message=${event.message}',
              name: 'AgUiClient',
            );
          }

          controller.add(event);

          // Auto-close after terminal events.
          if (closeOnTerminalEvent &&
              (event is RunFinishedEvent || event is RunErrorEvent)) {
            await controller.close();
            return;
          }
        }
      }

      // Process any remaining data in the buffer.
      if (remainder.isNotEmpty && !controller.isClosed) {
        final event = parseSseLine(remainder);
        if (event != null) {
          controller.add(event);
        }
      }

      if (!controller.isClosed) {
        developer.log(
          '[trace:$traceId] stream closed at ${stopwatch.elapsedMilliseconds}ms firstEvent=$sawFirstEvent firstTextDelta=$sawFirstTextDelta',
          name: 'AgUiClient',
        );
        await controller.close();
      }
    } on DioException catch (e) {
      if (e.type == DioExceptionType.cancel) {
        // Cancellation is expected — close silently.
        if (!controller.isClosed) {
          developer.log(
            '[trace:$traceId] stream cancelled at ${stopwatch.elapsedMilliseconds}ms',
            name: 'AgUiClient',
          );
          await controller.close();
        }
        return;
      }

      if (!controller.isClosed) {
        developer.log(
          '[trace:$traceId] DioException at ${stopwatch.elapsedMilliseconds}ms type=${e.type} status=${e.response?.statusCode} message=${e.message}',
          name: 'AgUiClient',
        );
        controller.addError(AgUiClientException(
          e.message ?? 'Network error during AG-UI stream',
          statusCode: e.response?.statusCode,
        ));
        await controller.close();
      }
    } catch (e, st) {
      if (!controller.isClosed) {
        developer.log(
          '[trace:$traceId] unexpected error at ${stopwatch.elapsedMilliseconds}ms: $e',
          name: 'AgUiClient',
          error: e,
          stackTrace: st,
        );
        controller.addError(e, st);
        await controller.close();
      }
    }
  }
}

/// Internal tracking of a tool call during a re-run loop iteration.
class _PendingToolCall {
  _PendingToolCall({
    required this.toolCallId,
    required this.name,
    this.parentMessageId,
  });

  final String toolCallId;
  final String name;
  final String? parentMessageId;
  final List<String> argFragments = [];
}
