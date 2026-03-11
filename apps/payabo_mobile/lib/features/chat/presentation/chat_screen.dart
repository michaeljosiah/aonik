import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_gradients.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_bottom_nav.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import '../../../shared/widgets/payabo_modal_sheet.dart';

const List<String> _quickPrompts = <String>[
  'Build me a Sunday reset',
  'Help me catch up on bills',
  'Find spending leaks',
];

class ChatScreen extends StatefulWidget {
  const ChatScreen({super.key});

  @override
  State<ChatScreen> createState() => _ChatScreenState();
}

class _ChatScreenState extends State<ChatScreen> {
  final TextEditingController _controller = TextEditingController();
  final ScrollController _scrollController = ScrollController();
  final List<_ChatConversation> _conversations = <_ChatConversation>[
    _ChatConversation(
      id: 'sunday-reset',
      title: 'Sunday reset',
      dateLabel: 'Today',
      messages: <_ChatMessage>[
        const _ChatMessage(
          sender: _ChatSender.user,
          lines: <String>['My finances are hot garbage.'],
        ),
        const _ChatMessage(
          sender: _ChatSender.assistant,
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
    _ChatConversation(
      id: 'bill-rescue',
      title: 'Bill rescue plan',
      dateLabel: '08 Mar 2026',
      messages: <_ChatMessage>[
        const _ChatMessage(
          sender: _ChatSender.user,
          lines: <String>['I keep missing my due dates.'],
        ),
        const _ChatMessage(
          sender: _ChatSender.assistant,
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
    _ChatConversation(
      id: 'goal-sprint',
      title: 'Goal sprint',
      dateLabel: '03 Mar 2026',
      messages: <_ChatMessage>[
        const _ChatMessage(
          sender: _ChatSender.user,
          lines: <String>['Help me save for travel by summer.'],
        ),
        const _ChatMessage(
          sender: _ChatSender.assistant,
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

  int _navIndex = 3;
  int _activeConversationIndex = 0;

  List<_ChatMessage> get _messages =>
      _conversations[_activeConversationIndex].messages;

  @override
  void initState() {
    super.initState();
    _controller.addListener(_handleDraftChanged);
  }

  @override
  void dispose() {
    _controller
      ..removeListener(_handleDraftChanged)
      ..dispose();
    _scrollController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      backgroundColor: PayaboColors.chatScreenSurface,
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: PayaboGradients.chatScreen,
        ),
        child: Stack(
          children: <Widget>[
            const Positioned(
              top: -80,
              right: -70,
              child: _ChatGlowOrb(
                size: 240,
                color: PayaboColors.chatGlowPrimary,
              ),
            ),
            const Positioned(
              left: -120,
              bottom: 200,
              child: _ChatGlowOrb(
                size: 300,
                color: PayaboColors.chatGlowSecondary,
              ),
            ),
            SafeArea(
              child: Column(
                children: <Widget>[
                  PayaboAppHeader(
                    trailingAction: _ChatHeaderMenuButton(onTap: _openHistory),
                  ),
                  Expanded(
                    child: ListView(
                      controller: _scrollController,
                      padding: const EdgeInsets.fromLTRB(
                        PayaboSpacing.xl,
                        PayaboSpacing.md,
                        PayaboSpacing.xl,
                        PayaboSpacing.xl,
                      ),
                      children: <Widget>[
                        Text(
                          'Hey you',
                          style: theme.textTheme.displayMedium?.copyWith(
                            fontSize: 58,
                            fontWeight: FontWeight.w300,
                            color: PayaboColors.chatTextPrimary,
                          ),
                        ),
                        const SizedBox(height: PayaboSpacing.sm),
                        Text(
                          'Tell me what feels messy and I will turn it into a plan.',
                          style: theme.textTheme.titleSmall?.copyWith(
                            color: PayaboColors.chatTextSecondary,
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                        const SizedBox(height: PayaboSpacing.x3),
                        ..._messages.map(
                          (_ChatMessage message) => Padding(
                            padding:
                                const EdgeInsets.only(bottom: PayaboSpacing.xl),
                            child: _ChatMessageBlock(message: message),
                          ),
                        ),
                        Text(
                          'Try one of these',
                          style: theme.textTheme.labelLarge?.copyWith(
                            color: PayaboColors.chatTextSecondary,
                          ),
                        ),
                        const SizedBox(height: PayaboSpacing.md),
                        Wrap(
                          spacing: PayaboSpacing.sm,
                          runSpacing: PayaboSpacing.sm,
                          children: _quickPrompts
                              .map(
                                (String prompt) => _QuickPromptChip(
                                  label: prompt,
                                  onTap: () => _submitPrompt(prompt),
                                ),
                              )
                              .toList(growable: false),
                        ),
                        const SizedBox(height: PayaboSpacing.md),
                      ],
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(
                      PayaboSpacing.md,
                      0,
                      PayaboSpacing.md,
                      PayaboSpacing.md,
                    ),
                    child: _ChatComposer(
                      controller: _controller,
                      canSend: _controller.text.trim().isNotEmpty,
                      onSubmitted: _submitPrompt,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: PayaboBottomNav(
        items: const <PayaboBottomNavItem>[
          PayaboBottomNavItem(icon: Icons.home_outlined, label: 'Home'),
          PayaboBottomNavItem(
              icon: Icons.receipt_long_outlined, label: 'Bills'),
          PayaboBottomNavItem(
              icon: Icons.show_chart_outlined, label: 'Spending'),
          PayaboBottomNavItem(icon: Icons.chat_bubble_outline, label: 'Chat'),
        ],
        currentIndex: _navIndex,
        onTap: _handleNavTap,
        onCenterTap: _showQuickActions,
      ),
    );
  }

  void _handleDraftChanged() {
    if (mounted) {
      setState(() {});
    }
  }

  Future<void> _openHistory() async {
    final currentId = _conversations[_activeConversationIndex].id;
    final selectedId = await context.push<String>(
      '/chat/history?selected=$currentId',
    );

    if (!mounted || selectedId == null) {
      return;
    }

    final selectedIndex = _conversations
        .indexWhere((conversation) => conversation.id == selectedId);
    if (selectedIndex < 0) {
      return;
    }

    setState(() {
      _activeConversationIndex = selectedIndex;
    });

    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!_scrollController.hasClients) {
        return;
      }

      _scrollController.animateTo(
        0,
        duration: const Duration(milliseconds: 220),
        curve: Curves.easeOut,
      );
    });
  }

  void _handleNavTap(int index) {
    setState(() {
      _navIndex = index;
    });

    switch (index) {
      case 0:
        context.go('/dashboard');
        return;
      case 1:
        context.go('/payments/country');
        return;
      case 2:
        context.go('/spending');
        return;
      case 3:
        context.go('/chat');
        return;
    }
  }

  Future<void> _showQuickActions() async {
    await showPayaboModalSheet<void>(
      context: context,
      title: 'Quick Actions',
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          PayaboListRow(
            title: 'Pay a bill',
            subtitle: 'Start a bill payment now',
            leading: const Icon(Icons.receipt_long_outlined),
            onTap: () {
              Navigator.of(context).pop();
              context.go('/payments/country');
            },
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Transfer',
            subtitle: 'Send money to another account',
            leading: const Icon(Icons.compare_arrows_outlined),
            onTap: () => Navigator.of(context).pop(),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Account',
            subtitle: 'Manage your account details',
            leading: const Icon(Icons.account_balance_outlined),
            onTap: () => Navigator.of(context).pop(),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Income',
            subtitle: 'Track and categorize income',
            leading: const Icon(Icons.trending_up_outlined),
            onTap: () => Navigator.of(context).pop(),
          ),
        ],
      ),
    );
  }

  void _submitPrompt([String? preset]) {
    final String prompt = (preset ?? _controller.text).trim();

    if (prompt.isEmpty) {
      return;
    }

    FocusScope.of(context).unfocus();
    _controller.clear();

    setState(() {
      _messages.add(
        _ChatMessage(
          sender: _ChatSender.user,
          lines: <String>[prompt],
        ),
      );
    });
    _scrollToBottom();

    final _ChatMessage reply = _buildReply(prompt);

    Future<void>.delayed(const Duration(milliseconds: 220), () {
      if (!mounted) {
        return;
      }

      setState(() {
        _messages.add(reply);
      });
      _scrollToBottom();
    });
  }

  _ChatMessage _buildReply(String prompt) {
    final String lowerPrompt = prompt.toLowerCase();

    if (lowerPrompt.contains('bill')) {
      return const _ChatMessage(
        sender: _ChatSender.assistant,
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
      return const _ChatMessage(
        sender: _ChatSender.assistant,
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
      return const _ChatMessage(
        sender: _ChatSender.assistant,
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

    return const _ChatMessage(
      sender: _ChatSender.assistant,
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

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!_scrollController.hasClients) {
        return;
      }

      _scrollController.animateTo(
        _scrollController.position.maxScrollExtent,
        duration: const Duration(milliseconds: 260),
        curve: Curves.easeOutCubic,
      );
    });
  }
}

class _ChatMessageBlock extends StatelessWidget {
  const _ChatMessageBlock({required this.message});

  final _ChatMessage message;

  @override
  Widget build(BuildContext context) {
    if (message.sender == _ChatSender.user) {
      return Align(
        alignment: Alignment.centerRight,
        child: ConstrainedBox(
          constraints: BoxConstraints(
            maxWidth: MediaQuery.sizeOf(context).width * 0.76,
          ),
          child: DecoratedBox(
            decoration: const BoxDecoration(
              color: PayaboColors.white,
              borderRadius: BorderRadius.only(
                topLeft: Radius.circular(30),
                topRight: Radius.circular(30),
                bottomLeft: Radius.circular(30),
                bottomRight: Radius.circular(8),
              ),
              boxShadow: PayaboShadows.soft,
            ),
            child: Padding(
              padding: const EdgeInsets.symmetric(
                horizontal: PayaboSpacing.lg,
                vertical: PayaboSpacing.md,
              ),
              child: Text(
                message.lines.first,
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: PayaboColors.chatTextPrimary,
                      fontWeight: FontWeight.w600,
                    ),
              ),
            ),
          ),
        ),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Container(
          padding: const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.md,
            vertical: PayaboSpacing.sm,
          ),
          decoration: BoxDecoration(
            color: PayaboColors.white.withValues(alpha: 0.5),
            borderRadius: PayaboRadii.radiusPill,
          ),
          child: Text(
            'Payabo coach',
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
                  color: PayaboColors.chatTextSecondary,
                  fontWeight: FontWeight.w700,
                ),
          ),
        ),
        const SizedBox(height: PayaboSpacing.md),
        ...message.lines.map(
          (String line) => Padding(
            padding: const EdgeInsets.only(bottom: PayaboSpacing.xs),
            child: Text(
              line,
              style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                    fontSize: 18,
                    height: 1.4,
                    color: PayaboColors.chatTextPrimary,
                  ),
            ),
          ),
        ),
        if (message.hasPlan) ...<Widget>[
          const SizedBox(height: PayaboSpacing.lg),
          _ChatPlanCard(
            title: message.planTitle!,
            items: message.planItems,
          ),
        ],
      ],
    );
  }
}

class _ChatPlanCard extends StatelessWidget {
  const _ChatPlanCard({
    required this.title,
    required this.items,
  });

  final String title;
  final List<String> items;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: PayaboColors.white.withValues(alpha: 0.82),
        borderRadius: const BorderRadius.all(Radius.circular(28)),
        boxShadow: PayaboShadows.soft,
        border: Border.all(color: PayaboColors.chatPlanBorder),
      ),
      child: Padding(
        padding: const EdgeInsets.all(PayaboSpacing.xl),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                Container(
                  width: 40,
                  height: 40,
                  decoration: BoxDecoration(
                    color: PayaboColors.chatPlanIconSurface,
                    borderRadius: BorderRadius.circular(14),
                  ),
                  child: const Icon(
                    Icons.auto_awesome_rounded,
                    color: PayaboColors.primary,
                  ),
                ),
                const SizedBox(width: PayaboSpacing.md),
                Expanded(
                  child: Text(
                    title,
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          color: PayaboColors.chatTextPrimary,
                        ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: PayaboSpacing.lg),
            ...items.map(
              (String item) => Padding(
                padding: const EdgeInsets.only(bottom: PayaboSpacing.md),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Container(
                      width: 8,
                      height: 8,
                      margin: const EdgeInsets.only(top: 8),
                      decoration: const BoxDecoration(
                        color: PayaboColors.primary,
                        shape: BoxShape.circle,
                      ),
                    ),
                    const SizedBox(width: PayaboSpacing.md),
                    Expanded(
                      child: Text(
                        item,
                        style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                              color: PayaboColors.chatTextTertiary,
                              height: 1.45,
                            ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _QuickPromptChip extends StatelessWidget {
  const _QuickPromptChip({
    required this.label,
    required this.onTap,
  });

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: PayaboColors.white.withValues(alpha: 0.64),
      borderRadius: PayaboRadii.radiusPill,
      child: InkWell(
        onTap: onTap,
        borderRadius: PayaboRadii.radiusPill,
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.lg,
            vertical: PayaboSpacing.md,
          ),
          child: Text(
            label,
            style: Theme.of(context).textTheme.labelLarge?.copyWith(
                  color: PayaboColors.chatTextTertiary,
                ),
          ),
        ),
      ),
    );
  }
}

class _ChatHeaderMenuButton extends StatelessWidget {
  const _ChatHeaderMenuButton({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: Ink(
          width: 44,
          height: 44,
          decoration: BoxDecoration(
            color: PayaboColors.headerIconSurface,
            shape: BoxShape.circle,
            border: Border.all(color: PayaboColors.headerIconBorder),
          ),
          child: const Center(
            child: Icon(
              Icons.menu_rounded,
              size: 22,
              color: PayaboColors.headerIconAccent,
            ),
          ),
        ),
      ),
    );
  }
}

class _ChatComposer extends StatelessWidget {
  const _ChatComposer({
    required this.controller,
    required this.canSend,
    required this.onSubmitted,
  });

  final TextEditingController controller;
  final bool canSend;
  final ValueChanged<String> onSubmitted;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: TextField(
            controller: controller,
            minLines: 1,
            maxLines: 4,
            textInputAction: TextInputAction.send,
            onSubmitted: onSubmitted,
            decoration: InputDecoration(
              hintText: 'Ask me anything...',
              filled: false,
              isDense: true,
              contentPadding: const EdgeInsets.symmetric(
                horizontal: PayaboSpacing.sm,
                vertical: PayaboSpacing.md,
              ),
              border: const UnderlineInputBorder(
                borderSide: BorderSide(color: PayaboColors.chatInputBorder),
              ),
              enabledBorder: const UnderlineInputBorder(
                borderSide: BorderSide(color: PayaboColors.chatInputBorder),
              ),
              focusedBorder: const UnderlineInputBorder(
                borderSide: BorderSide(color: PayaboColors.primary),
              ),
              hintStyle: Theme.of(context).textTheme.bodyLarge?.copyWith(
                    color: PayaboColors.chatTextSecondary,
                  ),
            ),
          ),
        ),
        const SizedBox(width: PayaboSpacing.md),
        Material(
          color: canSend ? PayaboColors.chatSendActive : PayaboColors.white,
          shape: const CircleBorder(),
          child: InkWell(
            onTap: canSend ? () => onSubmitted(controller.text) : null,
            customBorder: const CircleBorder(),
            child: SizedBox(
              width: 52,
              height: 52,
              child: Stack(
                alignment: Alignment.center,
                children: <Widget>[
                  Icon(
                    Icons.auto_graph_rounded,
                    color: canSend
                        ? PayaboColors.white
                        : PayaboColors.chatTextSecondary,
                  ),
                  const Positioned(
                    top: 12,
                    right: 11,
                    child: Icon(
                      Icons.auto_awesome,
                      size: 11,
                      color: PayaboColors.primary,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }
}

class _ChatGlowOrb extends StatelessWidget {
  const _ChatGlowOrb({
    required this.size,
    required this.color,
  });

  final double size;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return IgnorePointer(
      child: Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          gradient: RadialGradient(
            colors: <Color>[color, PayaboColors.transparent],
          ),
        ),
      ),
    );
  }
}

class _ChatConversation {
  _ChatConversation({
    required this.id,
    required this.title,
    required this.dateLabel,
    required this.messages,
  });

  final String id;
  final String title;
  final String dateLabel;
  final List<_ChatMessage> messages;
}

class _ChatMessage {
  const _ChatMessage({
    required this.sender,
    required this.lines,
    this.planTitle,
    this.planItems = const <String>[],
  });

  final _ChatSender sender;
  final List<String> lines;
  final String? planTitle;
  final List<String> planItems;

  bool get hasPlan => planTitle != null && planItems.isNotEmpty;
}

enum _ChatSender {
  user,
  assistant,
}
