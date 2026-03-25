import 'dart:async';

import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/chat_repository.dart';
import '../mock_behavior.dart';

class MockChatRepository implements ChatRepository {
  MockChatRepository({
    this.demoDataMode = DemoDataMode.populated,
  });

  final DemoDataMode demoDataMode;

  // ─────────────────────────────────────────────────────────
  //  getConversations
  // ─────────────────────────────────────────────────────────

  static final List<ChatConversation> _populatedConversations =
      <ChatConversation>[
    ChatConversation(
      id: 'sunday-reset',
      title: 'Sunday reset',
      dateLabel: 'Today',
      messages: <ChatMessage>[
        const ChatMessage(
          id: 'mock-1',
          sender: ChatSender.user,
          lines: <String>['My finances are hot garbage.'],
        ),
        const ChatMessage(
          id: 'mock-2',
          sender: ChatSender.assistant,
          lines: <String>[
            'You say this every Sunday.',
            'It is time to fix this financial broken record.',
            'I will create a plan.',
          ],
          planTitle: 'Sunday reset',
          planItems: <String>[
            'Round up every bill due in the next 7 days.',
            'Separate non-negotiables before casual spending starts.',
            'Cut one repeat expense before tomorrow night.',
          ],
        ),
      ],
    ),
    ChatConversation(
      id: 'bill-rescue',
      title: 'Bill rescue plan',
      dateLabel: '08 Mar 2026',
      messages: <ChatMessage>[
        const ChatMessage(
          id: 'mock-3',
          sender: ChatSender.user,
          lines: <String>['I keep missing my due dates.'],
        ),
        const ChatMessage(
          id: 'mock-4',
          sender: ChatSender.assistant,
          lines: <String>[
            'That is recoverable.',
            'Let us lock the next few bill dates and reduce late fees.',
          ],
          planTitle: 'Bill rescue plan',
          planItems: <String>[
            'Sort bills by due date and urgency.',
            'Enable reminders two days before due date.',
            'Pay essentials first, then spread the rest.',
          ],
        ),
      ],
    ),
    ChatConversation(
      id: 'goal-sprint',
      title: 'Goal sprint',
      dateLabel: '03 Mar 2026',
      messages: <ChatMessage>[
        const ChatMessage(
          id: 'mock-5',
          sender: ChatSender.user,
          lines: <String>['Help me save for travel by summer.'],
        ),
        const ChatMessage(
          id: 'mock-6',
          sender: ChatSender.assistant,
          lines: <String>[
            'Great goal.',
            'We will break this into weekly actions.',
          ],
          planTitle: 'Goal sprint',
          planItems: <String>[
            'Set a target amount and date.',
            'Automate one weekly transfer.',
            'Pause one non-essential subscription.',
          ],
        ),
      ],
    ),
  ];

  @override
  Future<List<ChatConversation>> getConversations() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('chat.getConversations');

    if (demoDataMode == DemoDataMode.fresh) {
      return const <ChatConversation>[];
    }

    // Return copies so each consumer gets independent mutable lists.
    return _populatedConversations
        .map(
          (ChatConversation c) => ChatConversation(
            id: c.id,
            title: c.title,
            dateLabel: c.dateLabel,
            messages: List<ChatMessage>.of(c.messages),
          ),
        )
        .toList();
  }

  // ─────────────────────────────────────────────────────────
  //  getHistoryEntries
  // ─────────────────────────────────────────────────────────

  static const List<ChatHistoryEntry> _populatedHistoryEntries =
      <ChatHistoryEntry>[
    ChatHistoryEntry(
      id: 'sunday-reset',
      dateLabel: 'Today',
      title: 'Sunday reset',
    ),
    ChatHistoryEntry(
      id: 'bill-rescue',
      dateLabel: '1 day ago',
      title: 'Current account balance inquiry',
    ),
    ChatHistoryEntry(
      id: 'goal-sprint',
      dateLabel: '1 day ago',
      title: 'Track spending to see where money goes',
    ),
  ];

  @override
  Future<List<ChatHistoryEntry>> getHistoryEntries() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('chat.getHistoryEntries');

    if (demoDataMode == DemoDataMode.fresh) {
      return const <ChatHistoryEntry>[];
    }

    return _populatedHistoryEntries;
  }

  // ─────────────────────────────────────────────────────────
  //  sendMessage (streaming mock)
  //
  //  Keyword-aware: certain prompts trigger display widget
  //  events and approval requests inline during the mock
  //  stream, matching the live AG-UI frontend tool flow.
  // ─────────────────────────────────────────────────────────

  int _toolCallCounter = 0;

  String _nextToolCallId() => 'mock-tc-${_toolCallCounter++}';

  @override
  Stream<ChatStreamEvent> sendMessage({
    String? threadId,
    required String userMessage,
    List<ChatMessage> history = const [],
  }) async* {
    yield const ChatStreamStarted(threadId: 'mock-thread', runId: 'mock-run');
    await MockBehavior.shortDelay(200);

    final lowerPrompt = userMessage.toLowerCase();

    // ── FX rate chart ──────────────────────────────────────
    if (lowerPrompt.contains('rate') ||
        lowerPrompt.contains('fx') ||
        lowerPrompt.contains('exchange') ||
        lowerPrompt.contains('remit')) {
      yield* _mockStreamWithDisplayWidget(
        text: 'Here is the latest GBP to NGN rate window. '
            'The trend has been moving in your favour this week, '
            'so now might be a good time to lock in a transfer.',
        widgetType: DisplayWidgetType.fxRateChart,
        data: const {
          'baseCurrency': 'GBP',
          'targetCurrency': 'NGN',
          'rates': [
            {'date': 'Mar 14', 'rate': 1920.50},
            {'date': 'Mar 15', 'rate': 1918.20},
            {'date': 'Mar 16', 'rate': 1925.80},
            {'date': 'Mar 17', 'rate': 1930.10},
            {'date': 'Mar 18', 'rate': 1928.45},
            {'date': 'Mar 19', 'rate': 1935.70},
            {'date': 'Mar 20', 'rate': 1942.30},
          ],
          'signal': 'buy',
          'signalReason':
              'Rate is up 1.1% over the past 7 days and sitting near a '
                  '30-day high. Sending now locks in a favourable window.',
        },
        toolName: 'display_fx_rate_chart',
      );
      return;
    }

    // ── Budget breakdown ───────────────────────────────────
    if (lowerPrompt.contains('budget') || lowerPrompt.contains('spend')) {
      yield* _mockStreamWithDisplayWidget(
        text: 'Here is your spending breakdown for this month. '
            'Groceries and dining are running hot — everything else '
            'is on track.',
        widgetType: DisplayWidgetType.budgetBreakdown,
        data: const {
          'period': 'March 2026',
          'totalBudget': 2800,
          'totalSpent': 2340,
          'currency': 'GBP',
          'categories': [
            {
              'name': 'Rent',
              'budgeted': 1200,
              'spent': 1200,
              'status': 'on_track',
            },
            {
              'name': 'Groceries',
              'budgeted': 400,
              'spent': 485,
              'status': 'over',
            },
            {
              'name': 'Transport',
              'budgeted': 200,
              'spent': 155,
              'status': 'under',
            },
            {
              'name': 'Dining',
              'budgeted': 250,
              'spent': 310,
              'status': 'over',
            },
            {
              'name': 'Subscriptions',
              'budgeted': 80,
              'spent': 62,
              'status': 'under',
            },
            {
              'name': 'Savings',
              'budgeted': 400,
              'spent': 128,
              'status': 'under',
            },
          ],
        },
        toolName: 'display_budget_breakdown',
      );
      return;
    }

    // ── Autopilot proposal ─────────────────────────────────
    if (lowerPrompt.contains('autopilot') ||
        lowerPrompt.contains('proposal') ||
        lowerPrompt.contains('automate') ||
        lowerPrompt.contains('agent')) {
      yield* _mockStreamWithDisplayWidget(
        text: 'The Bill Agent has a recommendation for you. '
            'It wants to set up auto-pay for your electricity bill '
            'so you never miss another due date.',
        widgetType: DisplayWidgetType.autopilotProposal,
        data: const {
          'agent': 'Bill Agent',
          'action': 'Schedule auto-pay for electricity',
          'description':
              'Your electricity bill has been late twice in the past 3 months. '
                  'Setting up auto-pay will eliminate late fees (approx. £12/month) '
                  'and protect your payment history.',
          'details': [
            {'label': 'Provider', 'value': 'British Gas'},
            {'label': 'Amount', 'value': '£85.00 / month'},
            {'label': 'Next due', 'value': '28 Mar 2026'},
            {'label': 'Late fees saved', 'value': '~£12 / month'},
          ],
          'severity': 'medium',
        },
        toolName: 'display_autopilot_proposal',
      );
      return;
    }

    // ── Approval (confirmAction) ───────────────────────────
    if (lowerPrompt.contains('pay') ||
        lowerPrompt.contains('create') ||
        lowerPrompt.contains('transfer') ||
        lowerPrompt.contains('send money')) {
      yield* _mockStreamWithApproval(
        text: 'I can set that up for you right now. '
            'I just need your go-ahead before I make the payment.',
        action: 'Create Payment',
        description:
            'Transfer £250.00 to John Doe (Barclays ****4821). '
            'This will be processed immediately and cannot be reversed.',
        severity: 'high',
      );
      return;
    }

    // ── Default: text-only response ────────────────────────
    final reply = await getReply(userMessage);
    final fullText = reply.lines.join('\n');
    const messageId = 'mock-stream-msg';

    for (int i = 0; i < fullText.length; i++) {
      yield ChatStreamTextDelta(fullText[i], messageId: messageId);
      if (i % 3 == 0) {
        await MockBehavior.shortDelay();
      }
    }

    yield const ChatStreamTextDone(messageId: messageId);
    yield const ChatStreamFinished();
  }

  /// Streams text, then emits mock tool call events followed by a
  /// [ChatStreamDisplayWidget] event, then finishes.
  Stream<ChatStreamEvent> _mockStreamWithDisplayWidget({
    required String text,
    required DisplayWidgetType widgetType,
    required Map<String, dynamic> data,
    required String toolName,
  }) async* {
    const messageId = 'mock-stream-msg';

    // Stream the text.
    for (int i = 0; i < text.length; i++) {
      yield ChatStreamTextDelta(text[i], messageId: messageId);
      if (i % 3 == 0) {
        await MockBehavior.shortDelay();
      }
    }

    yield const ChatStreamTextDone(messageId: messageId);

    // Simulate tool call lifecycle.
    final toolCallId = _nextToolCallId();
    yield ChatStreamToolCallStarted(
      toolCallId: toolCallId,
      toolName: toolName,
    );
    await MockBehavior.shortDelay(100);
    yield ChatStreamToolCallArgs(toolCallId: toolCallId, delta: '{}');
    yield ChatStreamToolCallEnd(toolCallId: toolCallId);
    await MockBehavior.shortDelay(80);

    // Emit the display widget.
    yield ChatStreamDisplayWidget(
      toolCallId: toolCallId,
      widgetType: widgetType,
      data: data,
    );

    // Mark the tool call as completed.
    yield ChatStreamToolCallResult(
      toolCallId: toolCallId,
      content: 'displayed',
    );

    yield const ChatStreamFinished();
  }

  /// Streams text, emits mock tool call events, then a
  /// [ChatStreamApprovalRequested] that blocks until the user acts.
  Stream<ChatStreamEvent> _mockStreamWithApproval({
    required String text,
    required String action,
    required String description,
    String severity = 'medium',
  }) async* {
    const messageId = 'mock-stream-msg';

    // Stream the text.
    for (int i = 0; i < text.length; i++) {
      yield ChatStreamTextDelta(text[i], messageId: messageId);
      if (i % 3 == 0) {
        await MockBehavior.shortDelay();
      }
    }

    yield const ChatStreamTextDone(messageId: messageId);

    // Simulate tool call lifecycle.
    final toolCallId = _nextToolCallId();
    yield ChatStreamToolCallStarted(
      toolCallId: toolCallId,
      toolName: 'confirmAction',
    );
    await MockBehavior.shortDelay(100);
    yield ChatStreamToolCallArgs(toolCallId: toolCallId, delta: '{}');
    yield ChatStreamToolCallEnd(toolCallId: toolCallId);
    await MockBehavior.shortDelay(80);

    // Emit the approval request. The completer blocks the stream until
    // the user taps Approve or Reject.
    final completer = Completer<String>();

    yield ChatStreamApprovalRequested(
      toolCallId: toolCallId,
      action: action,
      description: description,
      severity: severity,
      onApprove: () {
        if (!completer.isCompleted) {
          completer.complete('approved');
        }
      },
      onReject: ([String? reason]) {
        if (!completer.isCompleted) {
          completer.complete(reason != null ? 'rejected: $reason' : 'rejected');
        }
      },
    );

    // Wait for user decision.
    final result = await completer.future;

    yield ChatStreamToolCallResult(
      toolCallId: toolCallId,
      content: result,
    );

    // Stream a follow-up response based on the decision.
    final followUp = result.startsWith('approved')
        ? 'Done. The payment has been scheduled and you will get a confirmation shortly.'
        : 'No problem. I have cancelled the payment. Let me know if you change your mind.';

    const followUpId = 'mock-followup-msg';
    for (int i = 0; i < followUp.length; i++) {
      yield ChatStreamTextDelta(followUp[i], messageId: followUpId);
      if (i % 3 == 0) {
        await MockBehavior.shortDelay();
      }
    }

    yield const ChatStreamTextDone(messageId: followUpId);
    yield const ChatStreamFinished();
  }

  // ─────────────────────────────────────────────────────────
  //  getThread
  // ─────────────────────────────────────────────────────────

  @override
  Future<ChatConversation?> getThread(String threadId) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('chat.getThread');

    final match = _populatedConversations
        .cast<ChatConversation?>()
        .firstWhere((c) => c?.id == threadId, orElse: () => null);

    if (match == null) return null;

    return ChatConversation(
      id: match.id,
      title: match.title,
      dateLabel: match.dateLabel,
      messages: List<ChatMessage>.of(match.messages),
    );
  }

  // ─────────────────────────────────────────────────────────
  //  deleteConversation
  // ─────────────────────────────────────────────────────────

  @override
  Future<void> deleteConversation(String id) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('chat.deleteConversation');
    // No-op in mock mode — the mock data is static.
  }

  // ─────────────────────────────────────────────────────────
  //  getReply (canned responses)
  // ─────────────────────────────────────────────────────────

  @override
  Future<ChatMessage> getReply(String prompt) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('chat.getReply');

    final String lowerPrompt = prompt.toLowerCase();

    if (lowerPrompt.contains('bill')) {
      return const ChatMessage(
        sender: ChatSender.assistant,
        lines: <String>[
          'We can stop the bill pile-up before it turns into panic.',
          'I will sort due dates first, then I will flag what can wait.',
        ],
        planTitle: 'Bill rescue plan',
        planItems: <String>[
          'Pin every due date in one list.',
          'Protect essentials before optional services.',
          'Set one reminder 48 hours before each payment.',
        ],
      );
    }

    if (lowerPrompt.contains('spend') || lowerPrompt.contains('budget')) {
      return const ChatMessage(
        sender: ChatSender.assistant,
        lines: <String>[
          'Your money is talking. We just need to make the patterns louder.',
          'I will map what is fixed, what is flexible, and what is leaking.',
        ],
        planTitle: 'Spend reset',
        planItems: <String>[
          'Lock in a weekly spend cap.',
          'Name two categories that always run hot.',
          'Trim one habit before the next payday.',
        ],
      );
    }

    if (lowerPrompt.contains('save') || lowerPrompt.contains('goal')) {
      return const ChatMessage(
        sender: ChatSender.assistant,
        lines: <String>[
          'Saving gets easier when the goal feels close enough to touch.',
          'Let us shrink it into a weekly target instead of one giant number.',
        ],
        planTitle: 'Goal sprint',
        planItems: <String>[
          'Choose the exact amount and target date.',
          'Automate a small weekly move.',
          'Create one rule for skipping impulse spend.',
        ],
      );
    }

    return const ChatMessage(
      sender: ChatSender.assistant,
      lines: <String>[
        'That is fixable.',
        'Give me the mess in plain language and I will turn it into next steps.',
      ],
      planTitle: 'First move',
      planItems: <String>[
        'Name what feels urgent.',
        'Tell me what is due next.',
        'Pick one thing to improve this week.',
      ],
    );
  }
}
