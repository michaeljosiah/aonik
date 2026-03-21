// ─────────────────────────────────────────────────────────
//  LiveChatRepository
//
//  Connects to the AONIK AG-UI streaming endpoint via the
//  AgUiClient and translates low-level AG-UI events into
//  the chat-level ChatStreamEvent abstraction.
// ─────────────────────────────────────────────────────────

import 'dart:async';

import '../agui/agui_client.dart';
import '../agui/agui_models.dart';
import 'chat_repository.dart';

class LiveChatRepository implements ChatRepository {
  LiveChatRepository({required AgUiClient agUiClient})
      : _agUiClient = agUiClient;

  final AgUiClient _agUiClient;

  int _messageCounter = 0;

  String _nextId() => 'msg_${DateTime.now().millisecondsSinceEpoch}_${_messageCounter++}';

  @override
  Stream<ChatStreamEvent> sendMessage({
    String? threadId,
    required String userMessage,
    List<ChatMessage> history = const [],
  }) async* {
    // Build AG-UI message array from conversation history.
    final agUiMessages = <AgUiMessage>[];

    for (final msg in history) {
      if (msg.sender == ChatSender.user) {
        agUiMessages.add(AgUiMessage.user(
          id: msg.id ?? _nextId(),
          content: msg.lines.join('\n'),
        ));
      } else {
        agUiMessages.add(AgUiMessage.assistant(
          id: msg.id ?? _nextId(),
          content: msg.lines.join('\n'),
          toolCalls: msg.toolCalls.isNotEmpty
              ? msg.toolCalls
                  .map((tc) => AgUiToolCall(
                        id: tc.toolCallId,
                        function: AgUiFunctionCall(
                          name: tc.name,
                          arguments: tc.arguments ?? '{}',
                        ),
                      ))
                  .toList()
              : null,
        ));

        // If the assistant message had tool calls with results, add tool
        // result messages after the assistant message.
        for (final tc in msg.toolCalls) {
          if (tc.result != null) {
            agUiMessages.add(AgUiMessage.tool(
              id: _nextId(),
              toolCallId: tc.toolCallId,
              content: tc.result!,
            ));
          }
        }
      }
    }

    // Append the new user message.
    agUiMessages.add(AgUiMessage.user(
      id: _nextId(),
      content: userMessage,
    ));

    final input = AgUiRunInput(
      threadId: threadId,
      messages: agUiMessages,
    );

    // Stream AG-UI events and translate to chat-level events.
    await for (final event in _agUiClient.run(input)) {
      final chatEvent = _mapEvent(event);
      if (chatEvent != null) {
        yield chatEvent;
      }
    }
  }

  ChatStreamEvent? _mapEvent(AgUiEvent event) {
    switch (event) {
      case RunStartedEvent():
        return ChatStreamStarted(
          threadId: event.threadId,
          runId: event.runId,
        );

      case TextMessageContentEvent():
        return ChatStreamTextDelta(
          event.delta,
          messageId: event.messageId,
        );

      case TextMessageEndEvent():
        return ChatStreamTextDone(messageId: event.messageId);

      case ToolCallStartEvent():
        return ChatStreamToolCallStarted(
          toolCallId: event.toolCallId,
          toolName: event.toolCallName,
        );

      case ToolCallResultEvent():
        return ChatStreamToolCallResult(
          toolCallId: event.toolCallId,
          content: event.content,
        );

      case RunFinishedEvent():
        return const ChatStreamFinished();

      case RunErrorEvent():
        return ChatStreamError(event.message, code: event.code);

      // Events we don't surface at the chat level.
      case TextMessageStartEvent():
      case ToolCallArgsEvent():
      case ToolCallEndEvent():
      case StepStartedEvent():
      case StepFinishedEvent():
      case StateSnapshotEvent():
      case StateDeltaEvent():
      case MessagesSnapshotEvent():
      case CustomEvent():
      case UnknownEvent():
        return null;
    }
  }

  // ── History methods (not yet backed by a server endpoint) ──

  @override
  Future<List<ChatConversation>> getConversations() async {
    // No server-side conversation persistence yet — return empty.
    return const <ChatConversation>[];
  }

  @override
  Future<List<ChatHistoryEntry>> getHistoryEntries() async {
    // No server-side conversation persistence yet — return empty.
    return const <ChatHistoryEntry>[];
  }

  @override
  Future<ChatMessage> getReply(String prompt) async {
    // Legacy method — for live mode, callers should use sendMessage() instead.
    // Provide a minimal implementation that collects the stream into one message.
    final buffer = StringBuffer();
    String? messageId;

    await for (final event in sendMessage(userMessage: prompt)) {
      if (event is ChatStreamTextDelta) {
        buffer.write(event.delta);
        messageId ??= event.messageId;
      }
    }

    return ChatMessage(
      id: messageId,
      sender: ChatSender.assistant,
      lines: buffer.isEmpty ? const ['...'] : [buffer.toString()],
    );
  }
}
