// ─────────────────────────────────────────────────────────
//  ChatController
//
//  Manages conversation state for the chat feature.
//  Handles streaming AG-UI responses, message history,
//  and thread lifecycle.
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

  /// An error occurred during the last request.
  error,
}

/// A tool call that the agent is currently executing or has completed.
class ActiveToolCall {
  const ActiveToolCall({
    required this.toolCallId,
    required this.toolName,
    this.result,
    this.isComplete = false,
  });

  final String toolCallId;
  final String toolName;
  final String? result;
  final bool isComplete;
}

/// Immutable state for the chat feature.
class ChatState {
  const ChatState({
    this.messages = const [],
    this.activity = ChatActivity.idle,
    this.streamingText = '',
    this.streamingMessageId,
    this.threadId,
    this.activeToolCalls = const [],
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

  /// Error message from the last failed request.
  final String? errorMessage;

  /// Whether the agent is currently processing (connecting, streaming, or
  /// executing a tool).
  bool get isProcessing =>
      activity == ChatActivity.connecting ||
      activity == ChatActivity.streaming ||
      activity == ChatActivity.toolCall;

  /// Whether there are any messages in the conversation.
  bool get hasMessages => messages.isNotEmpty;

  ChatState copyWith({
    List<ChatMessage>? messages,
    ChatActivity? activity,
    String? streamingText,
    String? streamingMessageId,
    String? threadId,
    List<ActiveToolCall>? activeToolCalls,
    String? errorMessage,
  }) {
    return ChatState(
      messages: messages ?? this.messages,
      activity: activity ?? this.activity,
      streamingText: streamingText ?? this.streamingText,
      streamingMessageId: streamingMessageId ?? this.streamingMessageId,
      threadId: threadId ?? this.threadId,
      activeToolCalls: activeToolCalls ?? this.activeToolCalls,
      errorMessage: errorMessage ?? this.errorMessage,
    );
  }

  /// Returns a copy with cleared streaming state (for use after a run
  /// completes or errors out).
  ChatState _clearStreaming() {
    return copyWith(
      streamingText: '',
      streamingMessageId: null,
      activeToolCalls: const [],
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
          ),
        ];
        state = state.copyWith(
          activity: ChatActivity.toolCall,
          activeToolCalls: updated,
        );

      case ChatStreamToolCallResult():
        final updated = state.activeToolCalls.map((tc) {
          if (tc.toolCallId == event.toolCallId) {
            return ActiveToolCall(
              toolCallId: tc.toolCallId,
              toolName: tc.toolName,
              result: event.content,
              isComplete: true,
            );
          }
          return tc;
        }).toList();

        state = state.copyWith(activeToolCalls: updated);

      case ChatStreamFinished():
        state = state._clearStreaming().copyWith(
              activity: ChatActivity.idle,
            );

      case ChatStreamError():
        state = state._clearStreaming().copyWith(
              activity: ChatActivity.error,
              errorMessage: event.message,
            );
    }
  }

  void _onStreamError(Object error, StackTrace stackTrace) {
    state = state._clearStreaming().copyWith(
          activity: ChatActivity.error,
          errorMessage: error.toString(),
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

      state = state._clearStreaming().copyWith(
            activity: ChatActivity.idle,
          );
    }
  }

  /// Starts a new conversation — clears all messages and state.
  void newConversation() {
    _subscription?.cancel();
    state = ChatState.initial();
  }

  /// Loads a seeded conversation from the mock data.
  void loadConversation(ChatConversation conversation) {
    _subscription?.cancel();
    state = ChatState(
      messages: List.of(conversation.messages),
      threadId: conversation.id,
    );
  }

  @override
  void dispose() {
    _subscription?.cancel();
    super.dispose();
  }
}
