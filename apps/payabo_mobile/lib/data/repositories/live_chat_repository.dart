// ─────────────────────────────────────────────────────────
//  LiveChatRepository
//
//  Connects to the AONIK AG-UI streaming endpoint via the
//  AgUiClient and translates low-level AG-UI events into
//  the chat-level ChatStreamEvent abstraction.
//
//  Registers frontend tools:
//  - confirmAction — human-in-the-loop approval for mutations
//  - display_fx_rate_chart — GBP/NGN rate window with timing signal
//  - display_budget_breakdown — spending categories with over/under
//  - display_autopilot_proposal — structured approve/reject card
// ─────────────────────────────────────────────────────────

import 'dart:async';
import 'dart:developer' as developer;

import 'package:dio/dio.dart';

import '../agui/agui_client.dart';
import '../agui/agui_models.dart';
import '../api/api_exception.dart';
import 'chat_repository.dart';

class LiveChatRepository implements ChatRepository {
  LiveChatRepository({
    required AgUiClient agUiClient,
    required Dio apiClient,
  })  : _agUiClient = agUiClient,
        _apiClient = apiClient;

  final AgUiClient _agUiClient;
  final Dio _apiClient;

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
      agentId: 'personal-finance-agent',
      messages: agUiMessages,
    );

    // Side-channel controller for events emitted by frontend tool handlers
    // (approval requests, display widget requests) that need to merge into
    // the main chat stream.
    final sideChannel = StreamController<ChatStreamEvent>();

    // ── Frontend tool registrations ──────────────────────

    final frontendTools = <String, FrontendToolRegistration>{
      // Human-in-the-loop approval for mutating actions.
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
          final completer = Completer<String>();

          sideChannel.add(ChatStreamApprovalRequested(
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

      // Display: FX rate chart with timing signal.
      'display_fx_rate_chart': FrontendToolRegistration(
        tool: const AgUiToolDefinition(
          name: 'display_fx_rate_chart',
          description:
              'Display an FX rate chart showing a currency pair rate window '
              'with a timing signal (buy/hold/wait). Use when the user asks '
              'about exchange rates or remittance timing.',
          parameters: {
            'type': 'object',
            'properties': {
              'baseCurrency': {
                'type': 'string',
                'description': 'ISO 4217 base currency code (e.g., "GBP")',
              },
              'targetCurrency': {
                'type': 'string',
                'description': 'ISO 4217 target currency code (e.g., "NGN")',
              },
              'rates': {
                'type': 'array',
                'description': 'Historical rate data points',
                'items': {
                  'type': 'object',
                  'properties': {
                    'date': {
                      'type': 'string',
                      'description': 'Date label (e.g., "Mar 15")',
                    },
                    'rate': {
                      'type': 'number',
                      'description': 'Exchange rate value',
                    },
                  },
                  'required': ['date', 'rate'],
                },
              },
              'signal': {
                'type': 'string',
                'enum': ['buy', 'hold', 'wait'],
                'description': 'Timing signal recommendation',
              },
              'signalReason': {
                'type': 'string',
                'description': 'Brief explanation of the signal',
              },
            },
            'required': ['baseCurrency', 'targetCurrency', 'rates', 'signal'],
          },
        ),
        handler: _makeDisplayToolHandler(
          sideChannel,
          DisplayWidgetType.fxRateChart,
        ),
      ),

      // Display: budget breakdown with category status.
      'display_budget_breakdown': FrontendToolRegistration(
        tool: const AgUiToolDefinition(
          name: 'display_budget_breakdown',
          description:
              'Display a budget breakdown showing spending categories with '
              'over/under status. Use when the user asks about their budget, '
              'spending breakdown, or where their money is going.',
          parameters: {
            'type': 'object',
            'properties': {
              'period': {
                'type': 'string',
                'description': 'Budget period label (e.g., "March 2026")',
              },
              'totalBudget': {
                'type': 'number',
                'description': 'Total budgeted amount for the period',
              },
              'totalSpent': {
                'type': 'number',
                'description': 'Total amount spent so far',
              },
              'currency': {
                'type': 'string',
                'description': 'ISO 4217 currency code (e.g., "GBP")',
              },
              'categories': {
                'type': 'array',
                'description': 'Spending categories with budget vs actual',
                'items': {
                  'type': 'object',
                  'properties': {
                    'name': {
                      'type': 'string',
                      'description': 'Category name (e.g., "Groceries")',
                    },
                    'budgeted': {
                      'type': 'number',
                      'description': 'Budgeted amount for this category',
                    },
                    'spent': {
                      'type': 'number',
                      'description': 'Amount spent in this category',
                    },
                    'status': {
                      'type': 'string',
                      'enum': ['under', 'on_track', 'over'],
                      'description': 'Whether spending is under, on track, or over budget',
                    },
                  },
                  'required': ['name', 'budgeted', 'spent', 'status'],
                },
              },
            },
            'required': ['period', 'totalBudget', 'totalSpent', 'currency', 'categories'],
          },
        ),
        handler: _makeDisplayToolHandler(
          sideChannel,
          DisplayWidgetType.budgetBreakdown,
        ),
      ),

      // Display: autopilot proposal card.
      'display_autopilot_proposal': FrontendToolRegistration(
        tool: const AgUiToolDefinition(
          name: 'display_autopilot_proposal',
          description:
              'Display a structured proposal card for an automated action '
              'that an agent wants to take. Use when presenting a specific '
              'recommendation with details the user should review before '
              'the agent proceeds.',
          parameters: {
            'type': 'object',
            'properties': {
              'agent': {
                'type': 'string',
                'description': 'Name of the agent making the proposal (e.g., "Bill Agent", "Savings Agent")',
              },
              'action': {
                'type': 'string',
                'description': 'Short action title (e.g., "Schedule auto-pay for electricity")',
              },
              'description': {
                'type': 'string',
                'description': 'Detailed explanation of the proposal',
              },
              'details': {
                'type': 'array',
                'description': 'Key-value detail rows for the proposal card',
                'items': {
                  'type': 'object',
                  'properties': {
                    'label': {
                      'type': 'string',
                      'description': 'Detail label (e.g., "Amount")',
                    },
                    'value': {
                      'type': 'string',
                      'description': 'Detail value (e.g., "£85.00")',
                    },
                  },
                  'required': ['label', 'value'],
                },
              },
              'severity': {
                'type': 'string',
                'enum': ['low', 'medium', 'high'],
                'description': 'Importance level. Defaults to medium.',
              },
            },
            'required': ['agent', 'action', 'description'],
          },
        ),
        handler: _makeDisplayToolHandler(
          sideChannel,
          DisplayWidgetType.autopilotProposal,
        ),
      ),
    };

    // ── Stream merging ───────────────────────────────────

    final agUiStream = _agUiClient.runWithTools(
      input,
      frontendTools: frontendTools,
    );

    // Merge the main AG-UI event stream with the side-channel events
    // (approval requests, display widgets).
    final merged = StreamController<ChatStreamEvent>();

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

    final sideChannelSub = sideChannel.stream.listen(
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
      await sideChannelSub.cancel();
      await sideChannel.close();
    }
  }

  /// Creates a [FrontendToolHandler] for a display-only tool.
  ///
  /// Display tools resolve immediately — they emit a [ChatStreamDisplayWidget]
  /// event into the side channel and return a success string so the AG-UI
  /// re-run loop can continue without waiting for user interaction.
  static FrontendToolHandler _makeDisplayToolHandler(
    StreamController<ChatStreamEvent> sideChannel,
    DisplayWidgetType widgetType,
  ) {
    return (Map<String, dynamic> args, FrontendToolContext context) async {
      sideChannel.add(ChatStreamDisplayWidget(
        toolCallId: context.toolCallId,
        widgetType: widgetType,
        data: args,
      ));

      return 'displayed';
    };
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

  // ── History methods (backed by /ai/threads endpoints) ───

  @override
  Future<List<ChatConversation>> getConversations() async {
    // Load the full thread list, then fetch each thread's messages.
    // For now we fetch summaries only — callers that need messages
    // should use ChatController.loadConversation which calls getThread.
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/ai/threads',
        queryParameters: {'page': 1, 'pageSize': 50},
      );

      final data = response.data;
      if (data == null) return const <ChatConversation>[];

      final threads = data['threads'] as List<dynamic>? ?? const [];
      return threads
          .whereType<Map<Object?, Object?>>()
          .map((item) {
            final map = Map<String, dynamic>.from(item);
            return ChatConversation(
              id: map['id']?.toString() ?? '',
              title: map['title']?.toString() ?? 'Untitled',
              dateLabel: _formatDateLabel(map['lastMessageAt'] ?? map['createdAt']),
              messages: const <ChatMessage>[],
            );
          })
          .toList(growable: false);
    } on DioException catch (e) {
      developer.log(
        'Failed to fetch conversations',
        error: e,
        name: 'LiveChatRepository',
      );
      // Fail gracefully — return empty rather than crashing the UI.
      return const <ChatConversation>[];
    }
  }

  @override
  Future<List<ChatHistoryEntry>> getHistoryEntries() async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/ai/threads',
        queryParameters: {'page': 1, 'pageSize': 50},
      );

      final data = response.data;
      if (data == null) return const <ChatHistoryEntry>[];

      final threads = data['threads'] as List<dynamic>? ?? const [];
      return threads
          .whereType<Map<Object?, Object?>>()
          .map((item) {
            final map = Map<String, dynamic>.from(item);
            return ChatHistoryEntry(
              id: map['id']?.toString() ?? '',
              title: map['title']?.toString() ?? 'Untitled',
              dateLabel: _formatDateLabel(map['lastMessageAt'] ?? map['createdAt']),
            );
          })
          .toList(growable: false);
    } on DioException catch (e) {
      developer.log(
        'Failed to fetch history entries',
        error: e,
        name: 'LiveChatRepository',
      );
      return const <ChatHistoryEntry>[];
    }
  }

  @override
  Future<void> deleteConversation(String id) async {
    try {
      await _apiClient.delete<void>('/ai/threads/$id');
    } on DioException catch (e) {
      developer.log(
        'Failed to archive conversation $id',
        error: e,
        name: 'LiveChatRepository',
      );
      throw mapDioException(e);
    }
  }

  /// Formats an ISO 8601 date string into a human-friendly label.
  static String _formatDateLabel(dynamic value) {
    if (value == null) return '';
    try {
      final date = DateTime.parse(value.toString());
      final now = DateTime.now();
      final diff = now.difference(date);

      if (diff.inDays == 0) return 'Today';
      if (diff.inDays == 1) return 'Yesterday';
      if (diff.inDays < 7) return '${diff.inDays} days ago';

      return '${date.day.toString().padLeft(2, '0')} '
          '${_monthAbbrev(date.month)} '
          '${date.year}';
    } catch (_) {
      return '';
    }
  }

  static String _monthAbbrev(int month) {
    const months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
    ];
    return months[month.clamp(1, 12) - 1];
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
