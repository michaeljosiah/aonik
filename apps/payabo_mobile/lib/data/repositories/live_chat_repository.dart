// ─────────────────────────────────────────────────────────
//  LiveChatRepository
//
//  Connects to the AONIK AG-UI streaming endpoint via the
//  AgUiClient and translates low-level AG-UI events into
//  the chat-level ChatStreamEvent abstraction.
//
//  Registers the `confirmAction` frontend tool so the LLM
//  can request human approval for mutating actions. When the
//  tool is called, a ChatStreamApprovalRequested event is
//  emitted — the UI shows an approval card, and the user's
//  decision resolves the tool call. The AgUiClient re-run
//  loop then continues the conversation automatically.
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
    final agUiMessages = _buildMessageHistory(history, userMessage);

    final input = AgUiRunInput(
      threadId: threadId,
      messages: agUiMessages,
    );

    // Create a StreamController that we'll use to bridge the approval
    // events from the confirmAction tool handler into the chat stream.
    final approvalController = StreamController<ChatStreamEvent>();

    // Register the confirmAction frontend tool.
    final frontendTools = <String, FrontendToolRegistration>{
      'confirmAction': FrontendToolRegistration(
        tool: const AgUiToolDefinition(
          name: 'confirmAction',
          description:
              'Request user approval before executing a mutating action. '
              'The user will see an approval card with Approve/Reject buttons. '
              'Use this for any action that creates, modifies, or deletes data.',
          parameters: {
            'type': 'object',
            'properties': {
              'action': {
                'type': 'string',
                'description':
                    'Short name of the action (e.g., "Create Transaction", "Archive Bill")',
              },
              'description': {
                'type': 'string',
                'description':
                    'Detailed description of what will happen if approved',
              },
              'severity': {
                'type': 'string',
                'enum': ['low', 'medium', 'high'],
                'description': 'Risk level of the action. Defaults to medium.',
              },
            },
            'required': ['action', 'description'],
          },
        ),
        handler: (args, context) {
          // The handler returns a Future that resolves only when the user
          // approves or rejects. We use a Completer to bridge the gap.
          final completer = Completer<String>();

          // Emit an approval event into the chat stream.
          approvalController.add(ChatStreamApprovalRequested(
            toolCallId: context.toolCallId,
            action: args['action'] as String? ?? 'Unknown action',
            description: args['description'] as String? ?? '',
            severity: _parseSeverity(args['severity']),
            onApprove: () {
              if (!completer.isCompleted) {
                completer.complete('approved');
              }
            },
            onReject: ([String? reason]) {
              if (!completer.isCompleted) {
                final result = reason != null
                    ? 'rejected: $reason'
                    : 'rejected';
                completer.complete(result);
              }
            },
          ));

          return completer.future;
        },
      ),
    };

    // Stream AG-UI events and translate to chat-level events.
    // Merge the main AG-UI stream with the approval events.
    final agUiStream = _agUiClient.runWithTools(
      input,
      frontendTools: frontendTools,
    );

    // We need to merge two streams: the AG-UI events (mapped to chat events)
    // and the approval events from the confirmAction handler.
    // Use a merged stream controller for this.
    final merged = StreamController<ChatStreamEvent>();

    // Listen to AG-UI events and map them.
    final agUiSub = agUiStream.listen(
      (event) {
        final chatEvent = _mapEvent(event);
        if (chatEvent != null && !merged.isClosed) {
          merged.add(chatEvent);
        }
      },
      onError: (Object e, StackTrace st) {
        if (!merged.isClosed) {
          merged.addError(e, st);
        }
      },
      onDone: () {
        if (!merged.isClosed) {
          merged.close();
        }
      },
    );

    // Listen to approval events (from confirmAction handler).
    final approvalSub = approvalController.stream.listen(
      (event) {
        if (!merged.isClosed) {
          merged.add(event);
        }
      },
      onError: (Object e, StackTrace st) {
        if (!merged.isClosed) {
          merged.addError(e, st);
        }
      },
    );

    try {
      await for (final event in merged.stream) {
        yield event;
      }
    } finally {
      await agUiSub.cancel();
      await approvalSub.cancel();
      await approvalController.close();
    }
  }

  /// Maps a low-level AG-UI event to a chat-level event.
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

      case ToolCallArgsEvent():
        return ChatStreamToolCallArgs(
          toolCallId: event.toolCallId,
          delta: event.delta,
        );

      case ToolCallEndEvent():
        return ChatStreamToolCallEnd(toolCallId: event.toolCallId);

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

  /// Builds the AG-UI message history from app-level chat messages.
  List<AgUiMessage> _buildMessageHistory(
    List<ChatMessage> history,
    String newUserMessage,
  ) {
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
      content: newUserMessage,
    ));

    return agUiMessages;
  }

  static String _parseSeverity(dynamic value) {
    const valid = {'low', 'medium', 'high'};
    final s = value?.toString() ?? 'medium';
    return valid.contains(s) ? s : 'medium';
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
