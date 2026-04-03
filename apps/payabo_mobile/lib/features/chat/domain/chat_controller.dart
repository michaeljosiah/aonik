// ─────────────────────────────────────────────────────────
//  ChatController
//
//  Manages conversation state for the chat feature.
//  Handles streaming AG-UI responses, message history,
//  thread lifecycle, and human-in-the-loop approval flows.
// ─────────────────────────────────────────────────────────

import 'dart:async';

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

const Object _chatCopySentinel = Object();

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
    this.displayWidgets = const [],
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

  /// Display widgets rendered inline during the current response.
  final List<DisplayWidget> displayWidgets;

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
    List<DisplayWidget>? displayWidgets,
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
      displayWidgets: displayWidgets ?? this.displayWidgets,
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
      displayWidgets: const [],
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

  /// Sends a user message and starts streaming the agent's response.
  ///
  /// If a request is already in progress it is silently ignored.
  void sendMessage(String text) {
    final trimmed = text.trim();
    if (trimmed.isEmpty || state.isProcessing) return;

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

    final stream = _repository.sendMessage(
      threadId: state.threadId,
      userMessage: trimmed,
      history: state.messages,
    );

    _subscription = stream.listen(
      _onEvent,
      onError: _onStreamError,
      onDone: _onStreamDone,
      cancelOnError: false,
    );
  }

  void _onEvent(ChatStreamEvent event) {
    switch (event) {
      case ChatStreamStarted():
        state = state.copyWith(
          activity: ChatActivity.connecting,
          threadId: event.threadId ?? state.threadId,
        );

      case ChatStreamTextDelta():
        state = state.copyWith(
          activity: ChatActivity.streaming,
          streamingText: state.streamingText + event.delta,
          streamingMessageId: event.messageId,
        );

      case ChatStreamTextDone():
        // Finalise the assistant message and append it to history.
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

      case ChatStreamFinished():
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

        state = state._clearStreaming().copyWith(
              messages: messages,
              activity: ChatActivity.idle,
              pendingApprovals: const [],
            );

      case ChatStreamDisplayWidget():
        final widget = DisplayWidget(
          toolCallId: event.toolCallId,
          widgetType: event.widgetType,
          data: event.data,
        );
        state = state.copyWith(
          displayWidgets: [...state.displayWidgets, widget],
        );

      case ChatStreamError():
        state = state._clearStreaming().copyWith(
              activity: ChatActivity.error,
              errorMessage: event.message,
              pendingApprovals: const [],
            );
    }
  }

  /// Approves a pending confirmAction tool call.
  ///
  /// This resolves the completer in the AG-UI re-run loop, allowing
  /// the agent to continue with the approved mutation.
  void approveAction(String toolCallId) {
    final approval = state.pendingApprovals
        .where((a) => a.toolCallId == toolCallId)
        .firstOrNull;

    if (approval == null) return;

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

  void _onStreamError(Object error, StackTrace stackTrace) {
    state = state._clearStreaming().copyWith(
          activity: ChatActivity.error,
          errorMessage: error.toString(),
          pendingApprovals: const [],
        );
  }

  void _onStreamDone() {
    // If the stream completed without a RUN_FINISHED event (unexpected),
    // ensure we return to idle.
    if (state.isProcessing) {
      // Finalize any in-progress streaming text.
      if (state.streamingText.isNotEmpty &&
          state.streamingMessageId != null) {
        final assistantMessage = ChatMessage(
          id: state.streamingMessageId,
          sender: ChatSender.assistant,
          lines: [state.streamingText],
        );
        state = state.copyWith(
          messages: [...state.messages, assistantMessage],
        );
      }

      // Reject any pending approvals so completers don't hang.
      for (final approval in state.pendingApprovals) {
        approval.onReject('Stream ended unexpectedly');
      }

      state = state._clearStreaming().copyWith(
            activity: ChatActivity.idle,
            pendingApprovals: const [],
          );
    }
  }

  /// Starts a new conversation — clears all messages and state.
  void newConversation() {
    _subscription?.cancel();

    // Reject any pending approvals.
    for (final approval in state.pendingApprovals) {
      approval.onReject('Conversation reset');
    }

    state = ChatState.initial();
  }

  /// Loads a seeded conversation from the mock data.
  void loadConversation(ChatConversation conversation) {
    _subscription?.cancel();

    // Reject any pending approvals.
    for (final approval in state.pendingApprovals) {
      approval.onReject('Conversation changed');
    }

    state = ChatState(
      messages: List.of(conversation.messages),
      threadId: conversation.id,
    );
  }

  /// Fetches a thread from the backend and loads its messages.
  Future<void> loadThread(String threadId) async {
    _subscription?.cancel();

    // Reject any pending approvals.
    for (final approval in state.pendingApprovals) {
      approval.onReject('Conversation changed');
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

    // Reject any pending approvals.
    for (final approval in state.pendingApprovals) {
      approval.onReject('Controller disposed');
    }

    super.dispose();
  }
}
