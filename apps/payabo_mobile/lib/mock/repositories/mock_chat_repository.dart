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
          sender: ChatSender.user,
          lines: <String>['My finances are hot garbage.'],
        ),
        const ChatMessage(
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
          sender: ChatSender.user,
          lines: <String>['I keep missing my due dates.'],
        ),
        const ChatMessage(
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
          sender: ChatSender.user,
          lines: <String>['Help me save for travel by summer.'],
        ),
        const ChatMessage(
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
  //  getReply
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
