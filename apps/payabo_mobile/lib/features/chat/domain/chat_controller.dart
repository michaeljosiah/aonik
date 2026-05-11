// ─────────────────────────────────────────────────────────
//  ChatController
//
//  Manages conversation state for the chat feature.
//  Handles streaming AG-UI responses, message history,
//  thread lifecycle, and human-in-the-loop approval flows.
// ─────────────────────────────────────────────────────────

import 'dart:async';
import 'dart:developer' as developer;

import 'package:flutter_riverpod/legacy.dart';

import '../../../data/repositories/chat_repository.dart';

// ─────────────────────────────────────────────────────────
//  State
// ─────────────────────────────────────────────────────────

/// Represents the current activity of the agent.
enum ChatActivity {
  /// No active request — idle.
  idle,

  /// Waiting for the agent to start responding.
  connecting,

  /// Streaming text tokens from the agent.
  streaming,

  /// The agent is executing a server-side tool.
  toolCall,

  /// Waiting for user to approve or reject a mutating action.
  awaitingApproval,

  /// An error occurred during the last request.
  error,
}

/// Status of a tool call in the current response.
enum ToolCallStatus {
  /// Arguments are being streamed.
  streaming,

  /// Arguments complete, awaiting execution.
  pending,

  /// Executing server-side.
  executing,

  /// Waiting for user approval (confirmAction).
  awaitingApproval,

  /// Completed with a result.
  completed,

  /// Failed with an error.
  error,
}

/// A tool call that the agent is currently executing or has completed.
class ActiveToolCall {
  const ActiveToolCall({
    required this.toolCallId,
    required this.toolName,
    this.arguments = '',
    this.result,
    this.status = ToolCallStatus.streaming,
  });

  final String toolCallId;
  final String toolName;
  final String arguments;
  final String? result;
  final ToolCallStatus status;

  bool get isComplete =>
      status == ToolCallStatus.completed || status == ToolCallStatus.error;

  ActiveToolCall copyWith({
    String? arguments,
    String? result,
    ToolCallStatus? status,
  }) {
    return ActiveToolCall(
      toolCallId: toolCallId,
      toolName: toolName,
      arguments: arguments ?? this.arguments,
      result: result ?? this.result,
      status: status ?? this.status,
    );
  }
}

/// A pending approval from the confirmAction tool — awaiting user decision.
class PendingApproval {
  const PendingApproval({
    required this.toolCallId,
    required this.action,
    required this.description,
    this.severity = 'medium',
    required this.onApprove,
    required this.onReject,
  });

  final String toolCallId;
  final String action;
  final String description;
  final String severity;
  final void Function() onApprove;
  final void Function([String? reason]) onReject;
}

/// A pending option selection from the display_option_selector tool —
/// awaiting user choice.
class PendingOptionSelection {
  const PendingOptionSelection({
    required this.toolCallId,
    required this.question,
    required this.options,
    this.multiSelect = false,
    required this.onSelect,
  });

  final String toolCallId;
  final String question;
  final List<OptionItem> options;
  final bool multiSelect;
  final void Function(List<String> selected) onSelect;
}

/// A display widget rendered inline in the conversation, produced by a
/// frontend display tool (e.g. display_fx_rate_chart).
class DisplayWidget {
  const DisplayWidget({
    required this.toolCallId,
    required this.widgetType,
    required this.data,
  });

  final String toolCallId;
  final DisplayWidgetType widgetType;
  final Map<String, dynamic> data;
}

/// A pending deep-link navigation request from the `navigate_to_screen`
/// frontend tool. Stored in [ChatState] so the chat screen can observe it
/// (in a listener that has access to BuildContext) and navigate.
class PendingNavigation {
  const PendingNavigation({
    required this.toolCallId,
    required this.screenName,
    this.pathParameters = const <String, String>{},
    this.queryParameters = const <String, String>{},
  });

  final String toolCallId;
  final String screenName;
  final Map<String, String> pathParameters;
  final Map<String, String> queryParameters;
}

/// A single sentence-level speech chunk received during streaming. The chat
/// screen pushes these onto its playback queue as they arrive so TTS can
/// start before the assistant finishes generating.
class SpeechChunk {
  const SpeechChunk({
    required this.messageId,
    required this.chunkIndex,
    required this.speechText,
    required this.isFinal,
  });

  final String messageId;
  final int chunkIndex;
  final String speechText;
  final bool isFinal;
}

const Object _chatCopySentinel = Object();
const Object _chatNavSentinel = Object();

/// Immutable state for the chat feature.
class ChatState {
  const ChatState({
    this.messages = const [],
    this.activity = ChatActivity.idle,
    this.streamingText = '',
    this.streamingMessageId,
    this.threadId,
    this.activeToolCalls = const [],
    this.pendingApprovals = const [],
    this.pendingOptionSelections = const [],
    this.displayWidgets = const [],
    this.pendingSpeechText,
    this.pendingSpeechMessageId,
    this.pendingSpeechRequiresVisualAttention = false,
    this.pendingSpeechRequiresApproval = false,
    this.pendingSpeechChunks = const [],
    this.pendingNavigation,
    this.errorMessage,
  });

  /// All messages in the current conversation (both user and assistant).
  final List<ChatMessage> messages;

  /// Current activity of the agent.
  final ChatActivity activity;

  /// The text being streamed for the current assistant response.
  /// Grows as TEXT_MESSAGE_CONTENT events arrive.
  final String streamingText;

  /// The message ID of the currently streaming assistant message.
  final String? streamingMessageId;

  /// The AG-UI thread ID for the current conversation.
  final String? threadId;

  /// Tool calls in progress during the current response.
  final List<ActiveToolCall> activeToolCalls;

  /// Pending approvals waiting for user interaction.
  final List<PendingApproval> pendingApprovals;

  /// Pending option selections waiting for user choice.
  final List<PendingOptionSelection> pendingOptionSelections;

  /// Display widgets rendered inline during the current response.
  final List<DisplayWidget> displayWidgets;

  final String? pendingSpeechText;

  final String? pendingSpeechMessageId;

  final bool pendingSpeechRequiresVisualAttention;

  final bool pendingSpeechRequiresApproval;

  /// Sentence-level speech chunks accumulated during the current turn so
  /// the chat screen can queue them for TTS as they arrive. Cleared between
  /// turns via [_clearStreaming].
  final List<SpeechChunk> pendingSpeechChunks;

  /// Pending deep-link request from the `navigate_to_screen` tool. Cleared
  /// by the chat screen once the navigation has been dispatched.
  final PendingNavigation? pendingNavigation;

  /// Error message from the last failed request.
  final String? errorMessage;

  /// Whether the agent is currently processing (connecting, streaming,
  /// executing a tool, or waiting for approval).
  bool get isProcessing =>
      activity == ChatActivity.connecting ||
      activity == ChatActivity.streaming ||
      activity == ChatActivity.toolCall ||
      activity == ChatActivity.awaitingApproval;

  /// Whether there are any messages in the conversation.
  bool get hasMessages => messages.isNotEmpty;

  ChatState copyWith({
    List<ChatMessage>? messages,
    ChatActivity? activity,
    String? streamingText,
    String? streamingMessageId,
    String? threadId,
    List<ActiveToolCall>? activeToolCalls,
    List<PendingApproval>? pendingApprovals,
    List<PendingOptionSelection>? pendingOptionSelections,
    List<DisplayWidget>? displayWidgets,
    String? pendingSpeechText,
    String? pendingSpeechMessageId,
    bool? pendingSpeechRequiresVisualAttention,
    bool? pendingSpeechRequiresApproval,
    List<SpeechChunk>? pendingSpeechChunks,
    Object? pendingNavigation = _chatNavSentinel,
    Object? errorMessage = _chatCopySentinel,
  }) {
    return ChatState(
      messages: messages ?? this.messages,
      activity: activity ?? this.activity,
      streamingText: streamingText ?? this.streamingText,
      streamingMessageId: streamingMessageId ?? this.streamingMessageId,
      threadId: threadId ?? this.threadId,
      activeToolCalls: activeToolCalls ?? this.activeToolCalls,
      pendingApprovals: pendingApprovals ?? this.pendingApprovals,
      pendingOptionSelections:
          pendingOptionSelections ?? this.pendingOptionSelections,
      displayWidgets: displayWidgets ?? this.displayWidgets,
      pendingSpeechText: pendingSpeechText ?? this.pendingSpeechText,
      pendingSpeechMessageId:
          pendingSpeechMessageId ?? this.pendingSpeechMessageId,
      pendingSpeechRequiresVisualAttention:
          pendingSpeechRequiresVisualAttention ??
              this.pendingSpeechRequiresVisualAttention,
      pendingSpeechRequiresApproval:
          pendingSpeechRequiresApproval ?? this.pendingSpeechRequiresApproval,
      pendingSpeechChunks: pendingSpeechChunks ?? this.pendingSpeechChunks,
      pendingNavigation: pendingNavigation == _chatNavSentinel
          ? this.pendingNavigation
          : pendingNavigation as PendingNavigation?,
      errorMessage: errorMessage == _chatCopySentinel
          ? this.errorMessage
          : errorMessage as String?,
    );
  }

  /// Returns a copy with cleared streaming state (for use after a run
  /// completes or errors out).
  ChatState _clearStreaming() {
    return copyWith(
      streamingText: '',
      streamingMessageId: null,
      activeToolCalls: const [],
      pendingOptionSelections: const [],
      displayWidgets: const [],
      pendingSpeechText: null,
      pendingSpeechMessageId: null,
      pendingSpeechRequiresVisualAttention: false,
      pendingSpeechRequiresApproval: false,
      pendingSpeechChunks: const [],
      pendingNavigation: null,
      errorMessage: null,
    );
  }

  factory ChatState.initial() => const ChatState();
}

// ─────────────────────────────────────────────────────────
//  Controller
// ─────────────────────────────────────────────────────────

class ChatController extends StateNotifier<ChatState> {
  ChatController({required ChatRepository repository})
      : _repository = repository,
        super(ChatState.initial());

  final ChatRepository _repository;
  StreamSubscription<ChatStreamEvent>? _subscription;

  /// Fire-and-forget voice-mode diagnostic event reporting. The controller
  /// owns the AG-UI thread/run correlation IDs, while the screen owns the
  /// native playback state that populates [details].
  void reportVoiceEvent({
    required String eventName,
    int? clientElapsedMs,
    int? voiceTurnId,
    String? stage,
    String? reason,
    Map<String, Object?> details = const <String, Object?>{},
  }) {
    unawaited(_repository.reportVoiceEvent(
      eventName: eventName,
      clientElapsedMs: clientElapsedMs,
      threadId: state.threadId,
      runId: _currentRunId,
      agentName: 'personal-finance-agent',
      voiceTurnId: voiceTurnId,
      stage: stage,
      reason: reason,
      details: details,
    ));
  }

  // ── Realtime voice-mode injection (Voxa WSS path) ───────────────────
  //
  // The realtime path (spec 024 Phase H, lib/features/voice/) doesn't
  // talk to /mobile/chat/run — it streams over /ai/voice WSS and emits
  // typed events (TranscriptionEvent / BotTextEvent / SpeakingEvent /
  // ThreadReadyEvent / InterruptionEvent). The controller exposes the
  // methods below so the realtime controller can write transcripts and
  // bot text directly into chat state, keeping `state.messages` as the
  // single source of truth across both pipelines.
  //
  // The streaming-text batching machinery (_pendingTextDelta,
  // _flushPendingText) is reused so realtime bot chunks coalesce at the
  // same ~16 ms frame cadence as SSE token deltas — no double O(n²)
  // rebuild cost.
  String? _realtimeAssistantMessageId;

  /// Append a finalized user transcript to history. The WSS session owns
  /// the request lifecycle — this is purely a state injection.
  void addRealtimeUserTurn(String text) {
    final trimmed = text.trim();
    if (trimmed.isEmpty) return;

    final userMessage = ChatMessage(
      id: 'voice_user_${DateTime.now().millisecondsSinceEpoch}',
      sender: ChatSender.user,
      lines: [trimmed],
    );

    state = state.copyWith(
      messages: [...state.messages, userMessage],
      activity: ChatActivity.streaming,
      errorMessage: null,
    );
  }

  /// Begin a streaming assistant turn from the realtime WSS pipeline.
  /// Clears any prior streaming buffer and primes the message id so
  /// subsequent [appendRealtimeAssistantText] calls accumulate correctly.
  void beginRealtimeAssistantTurn({String? messageId}) {
    _discardPendingText();
    final id =
        messageId ?? 'voice_asst_${DateTime.now().millisecondsSinceEpoch}';
    _realtimeAssistantMessageId = id;

    state = state.copyWith(
      activity: ChatActivity.streaming,
      streamingText: '',
      streamingMessageId: id,
    );
  }

  /// Append a chunk from a BotTextEvent to the streaming assistant turn.
  /// Routes through the existing text-delta coalesce so realtime renders
  /// at the same frame cadence as SSE deltas.
  void appendRealtimeAssistantText(String chunk) {
    if (chunk.isEmpty) return;
    if (_realtimeAssistantMessageId == null) {
      // Auto-begin so the first chunk isn't dropped if the caller
      // missed the speaking-started → begin sequence.
      beginRealtimeAssistantTurn();
    }
    _pendingTextDelta.write(chunk);
    _pendingTextMessageId = _realtimeAssistantMessageId;
    _scheduleTextFlush();
  }

  /// Finalize the current streaming assistant turn into [state.messages].
  /// Safe to call when nothing has been streamed — becomes a no-op that
  /// just clears the streaming buffer.
  void finishRealtimeAssistantTurn() {
    _flushPendingText();
    final id = _realtimeAssistantMessageId;
    _realtimeAssistantMessageId = null;

    if (id == null || state.streamingText.isEmpty) {
      state = state.copyWith(
        streamingText: '',
        streamingMessageId: null,
      );
      return;
    }

    final assistantMessage = ChatMessage(
      id: id,
      sender: ChatSender.assistant,
      lines: [state.streamingText],
    );
    state = state.copyWith(
      messages: [...state.messages, assistantMessage],
      streamingText: '',
      streamingMessageId: null,
    );
  }

  /// Sync the chat thread id from the WSS threadReady envelope so a
  /// subsequent non-voice (SSE) send continues the same conversation.
  void setRealtimeThreadId(String threadId) {
    if (threadId.isEmpty) return;
    state = state.copyWith(threadId: threadId);
  }

  /// Mark the realtime session as errored. Surfaces the message via
  /// [state.errorMessage] and finalizes any in-flight streaming text so
  /// the partial response isn't lost.
  void markRealtimeError(String message) {
    finishRealtimeAssistantTurn();
    state = state.copyWith(
      activity: ChatActivity.error,
      errorMessage: message,
    );
  }

  /// End the realtime session. Finalizes any in-flight assistant text
  /// and returns the controller to idle (unless it already errored).
  void endRealtimeSession() {
    finishRealtimeAssistantTurn();
    if (state.activity != ChatActivity.error) {
      state = state.copyWith(activity: ChatActivity.idle);
    }
  }

  /// Handle a server-emitted InterruptionEvent — the user spoke over
  /// the bot. Finalize whatever was streamed so the truncated reply is
  /// preserved in history; activity stays at streaming because the
  /// session is still live.
  void markRealtimeInterruption() {
    finishRealtimeAssistantTurn();
    state = state.copyWith(activity: ChatActivity.streaming);
  }

  /// Client-side timing — started when a message is sent, used to measure
  /// round-trip latency from mobile → server → LLM → mobile.
  final Stopwatch _clientStopwatch = Stopwatch();
  int? _clientTimeToFirstTokenMs;
  String? _currentRunId;
  DateTime? _requestStartedAt;
  DateTime? _firstTextDeltaAt;
  DateTime? _finishedAt;
  bool _hasLoggedFirstTextDelta = false;

  // ── Text-delta batching ─────────────────────────────────────────────
  //
  // LLM token streams arrive at 20-100 tokens/s. Setting `state` per token
  // triggers a rebuild of every widget that watches the streaming text,
  // and Flutter's text layout re-measures the entire accumulated message
  // on each pass — O(n) per delta, O(n²) over the full response.
  //
  // We coalesce deltas into a single flush per frame (~16ms). The first
  // delta flushes immediately so there is no perceptible "first-token"
  // lag; subsequent deltas within the same frame are batched.
  static const Duration _textDeltaBatchWindow = Duration(milliseconds: 16);
  final StringBuffer _pendingTextDelta = StringBuffer();
  String? _pendingTextMessageId;
  Timer? _textDeltaFlushTimer;

  /// Sends the very first message in a conversation that was initiated by a
  /// conversation starter question. Seeds the starter as an assistant message
  /// in the history so the thread looks natural (Simi asked → user replied →
  /// AI continues), then sends the user's reply to the backend.
  void sendFirstMessage({
    required String starterQuestion,
    required String userReply,
  }) {
    final trimmedReply = userReply.trim();
    if (trimmedReply.isEmpty || state.isProcessing) return;

    _clientStopwatch.reset();
    _clientStopwatch.start();
    _clientTimeToFirstTokenMs = null;
    _currentRunId = null;
    _requestStartedAt = DateTime.now();
    _firstTextDeltaAt = null;
    _finishedAt = null;
    _hasLoggedFirstTextDelta = false;

    developer.log(
      'Chat starter submit started at ${_requestStartedAt!.toIso8601String()} threadId=${state.threadId ?? '-'} promptLength=${trimmedReply.length}',
      name: 'ChatController',
    );

    // Seed the starter question as an assistant message.
    final assistantMessage = ChatMessage(
      id: 'starter_${DateTime.now().millisecondsSinceEpoch}',
      sender: ChatSender.assistant,
      lines: [starterQuestion],
    );

    // Append the user's reply.
    final userMessage = ChatMessage(
      id: 'user_${DateTime.now().millisecondsSinceEpoch}',
      sender: ChatSender.user,
      lines: [trimmedReply],
    );
    final requestHistory = <ChatMessage>[assistantMessage];

    state = state.copyWith(
      messages: [assistantMessage, userMessage],
      activity: ChatActivity.connecting,
      streamingText: '',
      streamingMessageId: null,
      activeToolCalls: const [],
      pendingApprovals: const [],
      errorMessage: null,
    );

    _subscription?.cancel();
    _discardPendingText();

    final stream = _repository.sendMessage(
      threadId: state.threadId,
      userMessage: trimmedReply,
      history: requestHistory,
      voiceMode: false,
    );

    _subscription = stream.listen(
      _onEvent,
      onError: _onStreamError,
      onDone: _onStreamDone,
      cancelOnError: false,
    );
  }

  /// Sends a user message and starts streaming the agent's response.
  ///
  /// If a request is already in progress it is silently ignored.
  void sendMessage(String text) {
    final trimmed = text.trim();
    if (trimmed.isEmpty || state.isProcessing) return;
    final requestHistory = state.messages;

    _clientStopwatch.reset();
    _clientStopwatch.start();
    _clientTimeToFirstTokenMs = null;
    _currentRunId = null;
    _requestStartedAt = DateTime.now();
    _firstTextDeltaAt = null;
    _finishedAt = null;
    _hasLoggedFirstTextDelta = false;

    developer.log(
      'Chat submit started at ${_requestStartedAt!.toIso8601String()} threadId=${state.threadId ?? '-'} promptLength=${trimmed.length}',
      name: 'ChatController',
    );

    // Append user message to history.
    final userMessage = ChatMessage(
      id: 'user_${DateTime.now().millisecondsSinceEpoch}',
      sender: ChatSender.user,
      lines: [trimmed],
    );

    state = state.copyWith(
      messages: [...state.messages, userMessage],
      activity: ChatActivity.connecting,
      streamingText: '',
      streamingMessageId: null,
      activeToolCalls: const [],
      pendingApprovals: const [],
      errorMessage: null,
    );

    // Cancel any lingering subscription.
    _subscription?.cancel();
    _discardPendingText();

    final stream = _repository.sendMessage(
      threadId: state.threadId,
      userMessage: trimmed,
      history: requestHistory,
      voiceMode: false,
    );

    _subscription = stream.listen(
      _onEvent,
      onError: _onStreamError,
      onDone: _onStreamDone,
      cancelOnError: false,
    );
  }

  /// Schedules a text-delta flush in the next ~16ms window if one isn't
  /// already scheduled. Subsequent deltas arriving within the window
  /// accumulate into [_pendingTextDelta] and flush together.
  void _scheduleTextFlush() {
    if (_textDeltaFlushTimer != null && _textDeltaFlushTimer!.isActive) {
      return;
    }
    _textDeltaFlushTimer = Timer(_textDeltaBatchWindow, _flushPendingText);
  }

  /// Applies any pending text delta to [state.streamingText]. Safe to call
  /// when no text is pending — it becomes a no-op.
  void _flushPendingText() {
    _textDeltaFlushTimer?.cancel();
    _textDeltaFlushTimer = null;

    if (_pendingTextDelta.isEmpty) {
      return;
    }

    final String delta = _pendingTextDelta.toString();
    final String? messageId = _pendingTextMessageId;
    _pendingTextDelta.clear();

    state = state.copyWith(
      activity: ChatActivity.streaming,
      streamingText: state.streamingText + delta,
      streamingMessageId: messageId ?? state.streamingMessageId,
    );
  }

  /// Discards any buffered text without applying it. Used when a run is
  /// cancelled or reset so stale tokens don't leak into the next thread.
  void _discardPendingText() {
    _textDeltaFlushTimer?.cancel();
    _textDeltaFlushTimer = null;
    _pendingTextDelta.clear();
    _pendingTextMessageId = null;
  }

  void _onEvent(ChatStreamEvent event) {
    // Any non-text-delta event implies the pending text batch should be
    // applied first so events stay in the order the server emitted them
    // (e.g. tool-call start must not appear before the text it follows).
    if (event is! ChatStreamTextDelta) {
      _flushPendingText();
    }

    switch (event) {
      case ChatStreamStarted():
        _currentRunId = event.runId;
        state = state.copyWith(
          activity: ChatActivity.connecting,
          threadId: event.threadId ?? state.threadId,
        );

      case ChatStreamTextDelta():
        _clientTimeToFirstTokenMs ??= _clientStopwatch.elapsedMilliseconds;
        _firstTextDeltaAt ??= DateTime.now();

        if (!_hasLoggedFirstTextDelta &&
            _firstTextDeltaAt != null &&
            _requestStartedAt != null) {
          _hasLoggedFirstTextDelta = true;
          developer.log(
            'First text delta at ${_firstTextDeltaAt!.toIso8601String()} (+${_firstTextDeltaAt!.difference(_requestStartedAt!).inMilliseconds}ms) messageId=${event.messageId}',
            name: 'ChatController',
          );

          // Flush the first delta immediately so the UI shows the first
          // token without waiting for the batch window.
          _pendingTextDelta.write(event.delta);
          _pendingTextMessageId = event.messageId;
          _flushPendingText();
        } else {
          // Coalesce subsequent deltas into the next frame.
          _pendingTextDelta.write(event.delta);
          _pendingTextMessageId = event.messageId;
          _scheduleTextFlush();
        }

      case ChatStreamTextDone():
        // Pending text was flushed in the dispatcher above; finalise the
        // assistant message and append it to history.
        if (state.streamingText.isNotEmpty) {
          final assistantMessage = ChatMessage(
            id: event.messageId,
            sender: ChatSender.assistant,
            lines: [state.streamingText],
            toolCalls: state.activeToolCalls
                .map((tc) => ChatToolCallInfo(
                      toolCallId: tc.toolCallId,
                      name: tc.toolName,
                      arguments: tc.arguments,
                      result: tc.result,
                      isComplete: tc.isComplete,
                    ))
                .toList(),
          );

          state = state.copyWith(
            messages: [...state.messages, assistantMessage],
          );
        }

      case ChatStreamToolCallStarted():
        final updated = [
          ...state.activeToolCalls,
          ActiveToolCall(
            toolCallId: event.toolCallId,
            toolName: event.toolName,
            status: ToolCallStatus.streaming,
          ),
        ];
        state = state.copyWith(
          activity: ChatActivity.toolCall,
          activeToolCalls: updated,
        );

      case ChatStreamToolCallArgs():
        final updated = state.activeToolCalls.map((tc) {
          if (tc.toolCallId == event.toolCallId) {
            return tc.copyWith(
              arguments: tc.arguments + event.delta,
            );
          }
          return tc;
        }).toList();
        state = state.copyWith(activeToolCalls: updated);

      case ChatStreamToolCallEnd():
        final updated = state.activeToolCalls.map((tc) {
          if (tc.toolCallId == event.toolCallId) {
            return tc.copyWith(status: ToolCallStatus.pending);
          }
          return tc;
        }).toList();
        state = state.copyWith(activeToolCalls: updated);

      case ChatStreamToolCallResult():
        final updated = state.activeToolCalls.map((tc) {
          if (tc.toolCallId == event.toolCallId) {
            return tc.copyWith(
              result: event.content,
              status: ToolCallStatus.completed,
            );
          }
          return tc;
        }).toList();
        state = state.copyWith(activeToolCalls: updated);

      case ChatStreamApprovalRequested():
        // Add pending approval and update the tool call status.
        final approval = PendingApproval(
          toolCallId: event.toolCallId,
          action: event.action,
          description: event.description,
          severity: event.severity,
          onApprove: event.onApprove,
          onReject: event.onReject,
        );

        final updatedToolCalls = state.activeToolCalls.map((tc) {
          if (tc.toolCallId == event.toolCallId) {
            return tc.copyWith(status: ToolCallStatus.awaitingApproval);
          }
          return tc;
        }).toList();

        state = state.copyWith(
          activity: ChatActivity.awaitingApproval,
          pendingApprovals: [...state.pendingApprovals, approval],
          activeToolCalls: updatedToolCalls,
        );

      case ChatStreamOptionSelectionRequested():
        final selection = PendingOptionSelection(
          toolCallId: event.toolCallId,
          question: event.question,
          options: event.options,
          multiSelect: event.multiSelect,
          onSelect: event.onSelect,
        );

        final updatedToolCallsForOption = state.activeToolCalls.map((tc) {
          if (tc.toolCallId == event.toolCallId) {
            return tc.copyWith(status: ToolCallStatus.awaitingApproval);
          }
          return tc;
        }).toList();

        state = state.copyWith(
          activity: ChatActivity.awaitingApproval,
          pendingOptionSelections: [
            ...state.pendingOptionSelections,
            selection,
          ],
          activeToolCalls: updatedToolCallsForOption,
        );

      case ChatStreamFinished():
        // ── Log performance metrics ─────────────────────────────────
        _clientStopwatch.stop();
        _finishedAt = DateTime.now();
        final clientTotalMs = _clientStopwatch.elapsedMilliseconds;
        final clientTtftMs = _clientTimeToFirstTokenMs ?? clientTotalMs;
        final serverMetrics = event.metrics;
        final requestStartedAt = _requestStartedAt;
        final firstTextDeltaAt = _firstTextDeltaAt;
        final finishedAt = _finishedAt;

        developer.log(
          'Run completed — client: total=${clientTotalMs}ms, ttft=${clientTtftMs}ms'
          '${serverMetrics != null ? ' | server: $serverMetrics' : ''}',
          name: 'ChatController',
        );

        developer.log(
          'Timing summary submit=${requestStartedAt?.toIso8601String() ?? '-'} '
          'firstDelta=${firstTextDeltaAt?.toIso8601String() ?? '-'} '
          'finished=${finishedAt?.toIso8601String() ?? '-'} '
          'submitToFirstDelta=${requestStartedAt != null && firstTextDeltaAt != null ? firstTextDeltaAt.difference(requestStartedAt).inMilliseconds : '-'}ms '
          'submitToFinished=${requestStartedAt != null && finishedAt != null ? finishedAt.difference(requestStartedAt).inMilliseconds : '-'}ms',
          name: 'ChatController',
        );

        // Fire-and-forget: report combined client + server metrics to backend
        // for App Insights observability dashboard visualisation.
        _repository.reportMetrics(
          clientRoundTripMs: clientTotalMs,
          clientTtftMs: clientTtftMs,
          serverLatencyMs: serverMetrics?.latencyMs ?? 0,
          serverTtftMs: serverMetrics?.timeToFirstTokenMs ?? 0,
          inputTokens: serverMetrics?.inputTokens ?? 0,
          outputTokens: serverMetrics?.outputTokens ?? 0,
          threadId: state.threadId,
          runId: _currentRunId,
          agentName: 'personal-finance-agent',
        );

        // Persist any display widgets into the last assistant message
        // so they survive in history after clearing transient state.
        var messages = state.messages;
        if (state.displayWidgets.isNotEmpty && messages.isNotEmpty) {
          final last = messages.last;
          if (last.sender == ChatSender.assistant) {
            final updated = ChatMessage(
              id: last.id,
              sender: last.sender,
              lines: last.lines,
              planTitle: last.planTitle,
              planItems: last.planItems,
              toolCalls: last.toolCalls,
              displayWidgets: [
                ...last.displayWidgets,
                ...state.displayWidgets.map((dw) => ChatDisplayWidgetInfo(
                      toolCallId: dw.toolCallId,
                      widgetType: dw.widgetType,
                      data: dw.data,
                    )),
              ],
            );
            messages = [...messages.sublist(0, messages.length - 1), updated];
          }
        }

        if (state.pendingApprovals.isNotEmpty ||
            state.pendingOptionSelections.isNotEmpty) {
          // Defensive: RUN_FINISHED arrived while blocking tools are pending.
          // Preserve them rather than silently discarding.
          developer.log(
            'ChatStreamFinished received with '
            '${state.pendingApprovals.length} pending approval(s), '
            '${state.pendingOptionSelections.length} pending option selection(s) '
            '— preserving',
            name: 'ChatController',
          );
          state = state.copyWith(messages: messages);
        } else {
          state = state._clearStreaming().copyWith(
                messages: messages,
                activity: ChatActivity.idle,
              );
        }

      case ChatStreamDisplayWidget():
        final widget = DisplayWidget(
          toolCallId: event.toolCallId,
          widgetType: event.widgetType,
          data: event.data,
        );
        state = state.copyWith(
          displayWidgets: [...state.displayWidgets, widget],
        );

      case ChatStreamNavigationRequested():
        state = state.copyWith(
          pendingNavigation: PendingNavigation(
            toolCallId: event.toolCallId,
            screenName: event.screenName,
            pathParameters: event.pathParameters,
            queryParameters: event.queryParameters,
          ),
        );

      case ChatStreamSpeechRender():
        state = state.copyWith(
          pendingSpeechText: event.speechText,
          pendingSpeechMessageId: event.messageId,
          pendingSpeechRequiresVisualAttention: event.requiresVisualAttention,
          pendingSpeechRequiresApproval: event.requiresApproval,
        );

      case ChatStreamSpeechChunk():
        state = state.copyWith(
          pendingSpeechMessageId: event.messageId,
          pendingSpeechChunks: [
            ...state.pendingSpeechChunks,
            SpeechChunk(
              messageId: event.messageId,
              chunkIndex: event.chunkIndex,
              speechText: event.speechText,
              isFinal: event.isFinal,
            ),
          ],
        );

      case ChatStreamSpeechAudio():
        // Audio bytes bypass state — the legacy SSE-based voice path has
        // been removed in favour of the realtime WSS pipeline, so these
        // frames are dropped on the floor here. Kept in the switch so the
        // sealed-class exhaustiveness check still passes.
        break;

      case ChatStreamSpeechAudioError():
        break;

      case ChatStreamError():
        state = state._clearStreaming().copyWith(
          activity: ChatActivity.error,
          errorMessage: event.message,
          pendingApprovals: const [],
        );
    }
  }

  /// Clears a pending navigation request after the chat screen has
  /// dispatched the deep-link. Avoids re-navigating on subsequent rebuilds.
  void clearPendingNavigation() {
    if (state.pendingNavigation == null) return;
    state = state.copyWith(pendingNavigation: null);
  }

  /// Approves a pending confirmAction tool call.
  ///
  /// This resolves the completer in the AG-UI re-run loop, allowing
  /// the agent to continue with the approved mutation.
  void approveAction(String toolCallId) {
    developer.log(
      'approveAction called for toolCallId=$toolCallId',
      name: 'ChatController',
    );

    final approval = state.pendingApprovals
        .where((a) => a.toolCallId == toolCallId)
        .firstOrNull;

    if (approval == null) {
      developer.log(
        'approveAction: no pending approval found for $toolCallId',
        name: 'ChatController',
      );
      return;
    }

    // Resolve the completer (triggers the re-run loop to continue).
    approval.onApprove();

    // Update state: remove the approval and mark the tool call.
    final updatedApprovals = state.pendingApprovals
        .where((a) => a.toolCallId != toolCallId)
        .toList();

    final updatedToolCalls = state.activeToolCalls.map((tc) {
      if (tc.toolCallId == toolCallId) {
        return tc.copyWith(
          result: 'approved',
          status: ToolCallStatus.completed,
        );
      }
      return tc;
    }).toList();

    state = state.copyWith(
      pendingApprovals: updatedApprovals,
      activeToolCalls: updatedToolCalls,
      // If no more approvals pending, transition back to streaming/connecting
      // (the re-run loop will emit new events shortly).
      activity: updatedApprovals.isEmpty
          ? ChatActivity.connecting
          : ChatActivity.awaitingApproval,
    );
  }

  /// Rejects a pending confirmAction tool call with an optional reason.
  void rejectAction(String toolCallId, [String? reason]) {
    developer.log(
      'rejectAction called for toolCallId=$toolCallId reason=$reason',
      name: 'ChatController',
    );

    final approval = state.pendingApprovals
        .where((a) => a.toolCallId == toolCallId)
        .firstOrNull;

    if (approval == null) return;

    // Resolve the completer with rejection.
    approval.onReject(reason);

    // Update state.
    final updatedApprovals = state.pendingApprovals
        .where((a) => a.toolCallId != toolCallId)
        .toList();

    final updatedToolCalls = state.activeToolCalls.map((tc) {
      if (tc.toolCallId == toolCallId) {
        return tc.copyWith(
          result: reason != null ? 'rejected: $reason' : 'rejected',
          status: ToolCallStatus.completed,
        );
      }
      return tc;
    }).toList();

    state = state.copyWith(
      pendingApprovals: updatedApprovals,
      activeToolCalls: updatedToolCalls,
      activity: updatedApprovals.isEmpty
          ? ChatActivity.connecting
          : ChatActivity.awaitingApproval,
    );
  }

  /// Resolves a pending option selection tool call with the user's choice(s).
  void selectOption(String toolCallId, List<String> selected) {
    developer.log(
      'selectOption called for toolCallId=$toolCallId selected=$selected',
      name: 'ChatController',
    );

    final selection = state.pendingOptionSelections
        .where((s) => s.toolCallId == toolCallId)
        .firstOrNull;

    if (selection == null) {
      developer.log(
        'selectOption: no pending selection found for $toolCallId',
        name: 'ChatController',
      );
      return;
    }

    // Resolve the completer.
    selection.onSelect(selected);

    // Update state: remove the selection and mark the tool call.
    final updatedSelections = state.pendingOptionSelections
        .where((s) => s.toolCallId != toolCallId)
        .toList();

    final updatedToolCalls = state.activeToolCalls.map((tc) {
      if (tc.toolCallId == toolCallId) {
        return tc.copyWith(
          result: selected.join(', '),
          status: ToolCallStatus.completed,
        );
      }
      return tc;
    }).toList();

    final hasPendingBlocking =
        updatedSelections.isNotEmpty || state.pendingApprovals.isNotEmpty;

    state = state.copyWith(
      pendingOptionSelections: updatedSelections,
      activeToolCalls: updatedToolCalls,
      activity: hasPendingBlocking
          ? ChatActivity.awaitingApproval
          : ChatActivity.connecting,
    );
  }

  void _onStreamError(Object error, StackTrace stackTrace) {
    _discardPendingText();
    state = state._clearStreaming().copyWith(
      activity: ChatActivity.error,
      errorMessage: error.toString(),
      pendingApprovals: const [],
    );
  }

  void _onStreamDone() {
    // Apply any pending deltas before we decide the final state.
    _flushPendingText();
    // If the stream completed without a RUN_FINISHED event (unexpected),
    // ensure we return to idle.
    if (state.isProcessing) {
      // Finalize any in-progress streaming text.
      if (state.streamingText.isNotEmpty && state.streamingMessageId != null) {
        final assistantMessage = ChatMessage(
          id: state.streamingMessageId,
          sender: ChatSender.assistant,
          lines: [state.streamingText],
        );
        state = state.copyWith(
          messages: [...state.messages, assistantMessage],
        );
      }

      if (state.pendingApprovals.isNotEmpty ||
          state.pendingOptionSelections.isNotEmpty) {
        // Stream closed while user was reviewing a blocking card.
        // Reject/resolve completers so the re-run loop doesn't hang forever.
        developer.log(
          'Stream closed with ${state.pendingApprovals.length} pending approval(s), '
          '${state.pendingOptionSelections.length} pending option selection(s) — rejecting',
          name: 'ChatController',
        );
        for (final approval in state.pendingApprovals) {
          approval.onReject('Connection lost');
        }
        for (final selection in state.pendingOptionSelections) {
          selection.onSelect(const []);
        }
        state = state._clearStreaming().copyWith(
          activity: ChatActivity.error,
          errorMessage: 'Connection lost — please try again.',
          pendingApprovals: const [],
          pendingOptionSelections: const [],
        );
      } else {
        state = state._clearStreaming().copyWith(
              activity: ChatActivity.idle,
            );
      }
    }
  }

  /// Starts a new conversation — clears all messages and state.
  void newConversation() {
    _subscription?.cancel();
    _discardPendingText();

    // Reject any pending approvals / option selections.
    for (final approval in state.pendingApprovals) {
      approval.onReject('Conversation reset');
    }
    for (final selection in state.pendingOptionSelections) {
      selection.onSelect(const []);
    }

    state = ChatState.initial();
  }

  /// Loads a seeded conversation from the mock data.
  void loadConversation(ChatConversation conversation) {
    _subscription?.cancel();
    _discardPendingText();

    // Reject any pending approvals / option selections.
    for (final approval in state.pendingApprovals) {
      approval.onReject('Conversation changed');
    }
    for (final selection in state.pendingOptionSelections) {
      selection.onSelect(const []);
    }

    state = ChatState(
      messages: List.of(conversation.messages),
      threadId: conversation.id,
    );
  }

  /// Fetches a thread from the backend and loads its messages.
  Future<void> loadThread(String threadId) async {
    _subscription?.cancel();
    _discardPendingText();

    // Reject any pending approvals / option selections.
    for (final approval in state.pendingApprovals) {
      approval.onReject('Conversation changed');
    }
    for (final selection in state.pendingOptionSelections) {
      selection.onSelect(const []);
    }

    state = ChatState.initial().copyWith(activity: ChatActivity.connecting);

    try {
      final conversation = await _repository.getThread(threadId);
      if (conversation != null) {
        state = ChatState(
          messages: List.of(conversation.messages),
          threadId: conversation.id,
        );
      } else {
        state = ChatState.initial().copyWith(
          activity: ChatActivity.error,
          errorMessage: 'Conversation not found',
        );
      }
    } catch (e) {
      state = ChatState.initial().copyWith(
        activity: ChatActivity.error,
        errorMessage: 'Failed to load conversation: $e',
      );
    }
  }

  @override
  void dispose() {
    _subscription?.cancel();
    _discardPendingText();

    // Reject any pending approvals / option selections.
    for (final approval in state.pendingApprovals) {
      approval.onReject('Controller disposed');
    }
    for (final selection in state.pendingOptionSelections) {
      selection.onSelect(const []);
    }

    super.dispose();
  }
}
