import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../data/repositories/chat_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/theme/payabo_theme.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../profile/presentation/profile_state.dart';

final FutureProvider<List<ChatConversation>> _chatConversationsProvider =
    FutureProvider<List<ChatConversation>>((Ref ref) async {
  ref.watch(demoDataModeProvider);
  final ChatRepository repository = ref.watch(chatRepositoryProvider);
  return repository.getConversations();
});

const List<_ComposerActionSpec> _composerActions = <_ComposerActionSpec>[
  _ComposerActionSpec(
    label: 'Attach',
    icon: Icons.attach_file_rounded,
  ),
  _ComposerActionSpec(
    label: 'Camera',
    icon: Icons.photo_camera_outlined,
  ),
  _ComposerActionSpec(
    label: 'Voice',
    icon: Icons.keyboard_voice_outlined,
  ),
];

Color _chatBaseColor(BuildContext context) {
  final c = context.colors;

  return c.isDark ? const Color(0xFF070505) : const Color(0xFF0A0706);
}

Color _chatTrayColor(BuildContext context) {
  final c = context.colors;

  return c.isDark ? const Color(0xFF15110F) : const Color(0xFF17110E);
}

LinearGradient _chatTrayGradient(BuildContext context) {
  final c = context.colors;

  return LinearGradient(
    colors: c.isDark
        ? const <Color>[
            Color(0xFF221912),
            Color(0xFF18110D),
            Color(0xFF100B09),
          ]
        : const <Color>[
            Color(0xFF261C16),
            Color(0xFF1A130F),
            Color(0xFF120D0A),
          ],
    stops: const <double>[0, 0.46, 1],
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
  );
}

Color _chatInputSurfaceColor(BuildContext context) {
  final c = context.colors;

  return c.isDark ? const Color(0xFF1E1712) : const Color(0xFF201814);
}

LinearGradient _chatInputGradient(BuildContext context) {
  final c = context.colors;

  return LinearGradient(
    colors: c.isDark
        ? const <Color>[
            Color(0xFF2A1E17),
            Color(0xFF1E1611),
          ]
        : const <Color>[
            Color(0xFF2E2119),
            Color(0xFF211812),
          ],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );
}

Color _chatBorderColor(BuildContext context) {
  final c = context.colors;

  return Colors.white.withValues(alpha: c.isDark ? 0.08 : 0.1);
}

Color _chatPremiumBorderColor(BuildContext context) {
  final c = context.colors;

  return Colors.white.withValues(alpha: c.isDark ? 0.12 : 0.14);
}

Color _chatPremiumHighlightColor(BuildContext context) {
  final c = context.colors;

  return Colors.white.withValues(alpha: c.isDark ? 0.07 : 0.09);
}

Color _chatBodyTextColor(BuildContext context) {
  return Colors.white.withValues(alpha: 0.9);
}

Color _chatMutedTextColor(BuildContext context) {
  return Colors.white.withValues(alpha: 0.64);
}

Color _chatUserBubbleColor(BuildContext context) {
  final c = context.colors;

  return c.isDark ? const Color(0xFF1A313B) : const Color(0xFF1E2F38);
}

LinearGradient _chatUserBubbleGradient(BuildContext context) {
  final c = context.colors;

  return LinearGradient(
    colors: c.isDark
        ? const <Color>[
            Color(0xFF20414E),
            Color(0xFF17313B),
          ]
        : const <Color>[
            Color(0xFF274A57),
            Color(0xFF1D3640),
          ],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );
}

Color _chatPlanSurfaceColor(BuildContext context) {
  final c = context.colors;

  return c.isDark ? const Color(0xFF14110F) : const Color(0xFF181311);
}

LinearGradient _chatPlanGradient(BuildContext context) {
  final c = context.colors;

  return LinearGradient(
    colors: c.isDark
        ? const <Color>[
            Color(0xFF1A1411),
            Color(0xFF110D0B),
            Color(0xFF0C0908),
          ]
        : const <Color>[
            Color(0xFF201814),
            Color(0xFF15100D),
            Color(0xFF100B09),
          ],
    stops: const <double>[0, 0.5, 1],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );
}

LinearGradient _chatHeroGradient() {
  return const LinearGradient(
    colors: <Color>[
      Color(0xFF34231B),
      Color(0xFF1A120E),
      Color(0xFF070505),
    ],
    stops: <double>[0, 0.42, 1],
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
  );
}

List<BoxShadow> _chatTrayShadow() {
  return const <BoxShadow>[
    BoxShadow(
      color: Color(0x47000000),
      blurRadius: 24,
      offset: Offset(0, 10),
    ),
    BoxShadow(
      color: Color(0x18000000),
      blurRadius: 2,
      offset: Offset(0, 1),
    ),
  ];
}

class ChatScreen extends ConsumerStatefulWidget {
  const ChatScreen({super.key});

  @override
  ConsumerState<ChatScreen> createState() => _ChatScreenState();
}

class _ChatScreenState extends ConsumerState<ChatScreen> {
  final TextEditingController _controller = TextEditingController();
  final ScrollController _scrollController = ScrollController();
  final List<ChatMessage> _freshMessages = <ChatMessage>[];
  List<ChatConversation> _conversations = <ChatConversation>[];
  int _activeConversationIndex = 0;

  List<ChatMessage> get _messages {
    if (_conversations.isEmpty) {
      return const <ChatMessage>[];
    }
    return _conversations[_activeConversationIndex].messages;
  }

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
    final bool isFreshDemo =
        ref.watch(demoDataModeProvider) == DemoDataMode.fresh;
    final ProfileHeaderState profileState = ref.watch(profileHeaderProvider);

    // Sync conversations from provider into local mutable state.
    final AsyncValue<List<ChatConversation>> conversationsAsync =
        ref.watch(_chatConversationsProvider);
    conversationsAsync.whenData((List<ChatConversation> data) {
      if (_conversations.isEmpty && data.isNotEmpty) {
        _conversations = data;
      }
    });

    final List<ChatMessage> visibleMessages =
        isFreshDemo ? _freshMessages : _messages;
    final bool showHero = visibleMessages.isEmpty;

    return Scaffold(
      backgroundColor: _chatBaseColor(context),
      body: Stack(
        children: <Widget>[
          Positioned.fill(
            child: ColoredBox(color: _chatBaseColor(context)),
          ),
          Positioned.fill(
            child: IgnorePointer(
              child: DecoratedBox(
                decoration: BoxDecoration(
                  gradient: _chatHeroGradient(),
                ),
              ),
            ),
          ),
          const Positioned(
            top: -110,
            left: -90,
            child: _ChatGlowOrb(
              size: 320,
              color: Color(0x2638251B),
            ),
          ),
          const Positioned(
            top: -90,
            right: -70,
            child: _ChatGlowOrb(
              size: 300,
              color: Color(0x21422C1E),
            ),
          ),
          SafeArea(
            child: Column(
              children: <Widget>[
                Padding(
                  padding: const EdgeInsets.fromLTRB(
                    PayaboSpacing.xl,
                    PayaboSpacing.sm,
                    PayaboSpacing.xl,
                    0,
                  ),
                  child: Row(
                    children: <Widget>[
                      _ChatHeaderMenuButton(
                        onTap: () => _openHistory(isFreshDemo),
                      ),
                    ],
                  ),
                ),
                Expanded(
                  child: AnimatedSwitcher(
                    duration: const Duration(milliseconds: 320),
                    switchInCurve: Curves.easeOutCubic,
                    switchOutCurve: Curves.easeInCubic,
                    child: showHero
                        ? _EmptyChatStage(
                            key: const ValueKey<String>('chat-empty'),
                            displayName: _firstName(profileState.displayName),
                          )
                        : _ConversationStage(
                            key: const ValueKey<String>('chat-thread'),
                            controller: _scrollController,
                            displayName: _firstName(profileState.displayName),
                            messages: visibleMessages,
                          ),
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
                    isFreshDemo: showHero,
                    onSubmitted: _submitPrompt,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
      bottomNavigationBar: Theme(
        data: buildPayaboDarkTheme(),
        child: const PayaboPrimaryAppShell(
          destination: PayaboPrimaryDestination.chat,
          backgroundOverride: Color(0xFF0E0A08),
          borderOverride: Color(0xFF1E1610),
          shadowOverride: Color(0x40000000),
          selectedOverride: Color(0xFFF4A027),
          unselectedOverride: Color(0xFF6B5B4E),
          fabBackgroundOverride: Color(0xFFF37920),
          fabShadowOverride: Color(0x30F37920),
        ),
      ),
    );
  }

  void _handleDraftChanged() {
    if (mounted) {
      setState(() {});
    }
  }

  Future<void> _openHistory(bool isFreshDemo) async {
    final String? currentId = isFreshDemo || _conversations.isEmpty
        ? null
        : _conversations[_activeConversationIndex].id;
    final String? selectedId = await context.push<String>(
      currentId == null ? '/chat/history' : '/chat/history?selected=$currentId',
    );

    if (!mounted || selectedId == null) {
      return;
    }

    final int selectedIndex = _conversations.indexWhere(
      (ChatConversation conversation) => conversation.id == selectedId,
    );
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

  void _submitPrompt([String? preset]) {
    final bool isFreshDemo =
        ref.read(demoDataModeProvider) == DemoDataMode.fresh;
    final String prompt = (preset ?? _controller.text).trim();

    if (prompt.isEmpty) {
      return;
    }

    FocusScope.of(context).unfocus();
    _controller.clear();

    final List<ChatMessage> targetMessages =
        isFreshDemo ? _freshMessages : _messages;

    setState(() {
      targetMessages.add(
        ChatMessage(
          sender: ChatSender.user,
          lines: <String>[prompt],
        ),
      );
    });
    _scrollToBottom();

    final ChatRepository repository = ref.read(chatRepositoryProvider);
    repository.getReply(prompt).then((ChatMessage reply) {
      if (!mounted) {
        return;
      }

      setState(() {
        targetMessages.add(reply);
      });
      _scrollToBottom();
    });
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

  String _firstName(String displayName) {
    final String trimmed = displayName.trim();
    if (trimmed.isEmpty) {
      return '';
    }

    return trimmed.split(' ').first;
  }
}

class _EmptyChatStage extends StatelessWidget {
  const _EmptyChatStage({
    super.key,
    required this.displayName,
  });

  final String displayName;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final TextStyle helloStyle =
        Theme.of(context).textTheme.displayLarge?.copyWith(
                  color: _chatBodyTextColor(context),
                  fontSize: 66,
                  fontWeight: FontWeight.w800,
                  height: 0.92,
                  letterSpacing: -1.6,
                ) ??
            TextStyle(
              color: _chatBodyTextColor(context),
              fontSize: 66,
              fontWeight: FontWeight.w800,
              height: 0.92,
              letterSpacing: -1.6,
            );
    final TextStyle nameStyle = helloStyle.copyWith(
      color: c.primary,
      fontSize: 70,
      letterSpacing: -1.9,
    );
    final TextStyle simiStyle =
        Theme.of(context).textTheme.labelLarge?.copyWith(
                  color: _chatBodyTextColor(context),
                  fontSize: 16,
                  fontWeight: FontWeight.w700,
                  height: 1.1,
                  letterSpacing: 3.8,
                ) ??
            TextStyle(
              color: _chatBodyTextColor(context),
              fontSize: 16,
              fontWeight: FontWeight.w700,
              height: 1.1,
              letterSpacing: 3.8,
            );

    return LayoutBuilder(
      builder: (BuildContext context, BoxConstraints constraints) {
        return SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(
            PayaboSpacing.xl,
            PayaboSpacing.x2,
            PayaboSpacing.xl,
            PayaboSpacing.xl,
          ),
          child: ConstrainedBox(
            constraints: BoxConstraints(minHeight: constraints.maxHeight),
            child: Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: PayaboSpacing.md,
                      vertical: PayaboSpacing.sm,
                    ),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.06),
                      borderRadius: BorderRadius.circular(999),
                      border: Border.all(color: _chatBorderColor(context)),
                    ),
                    child: Text(
                      'SIMI',
                      style: Theme.of(context).textTheme.labelLarge?.copyWith(
                            color: _chatBodyTextColor(context),
                            fontWeight: FontWeight.w700,
                            letterSpacing: 2.8,
                          ),
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.x3),
                  Text(
                    'Hello!',
                    textAlign: TextAlign.center,
                    style: helloStyle,
                  ),
                  if (displayName.isNotEmpty) ...<Widget>[
                    const SizedBox(height: 4),
                    Text(
                      displayName,
                      textAlign: TextAlign.center,
                      style: nameStyle,
                    ),
                  ],
                  const SizedBox(height: PayaboSpacing.xl),
                  Text(
                    'I\'M SIMI',
                    textAlign: TextAlign.center,
                    style: simiStyle,
                  ),
                  const SizedBox(height: PayaboSpacing.md),
                  ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 340),
                    child: Text(
                      'I am here to guide you through bills, budgets, and the money moves that matter most.',
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            color: _chatMutedTextColor(context),
                            fontSize: 20,
                            height: 1.6,
                          ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }
}

class _ConversationStage extends StatelessWidget {
  const _ConversationStage({
    super.key,
    required this.controller,
    required this.displayName,
    required this.messages,
  });

  final ScrollController controller;
  final String displayName;
  final List<ChatMessage> messages;

  @override
  Widget build(BuildContext context) {
    return ListView(
      controller: controller,
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.xl,
        PayaboSpacing.xl,
        PayaboSpacing.xl,
        PayaboSpacing.xl,
      ),
      children: <Widget>[
        _CompactChatIntroCard(displayName: displayName),
        const SizedBox(height: PayaboSpacing.xl),
        ...messages.map(
          (ChatMessage message) => Padding(
            padding: const EdgeInsets.only(bottom: PayaboSpacing.xl),
            child: _ChatMessageBlock(message: message),
          ),
        ),
      ],
    );
  }
}

class _CompactChatIntroCard extends StatelessWidget {
  const _CompactChatIntroCard({required this.displayName});

  final String displayName;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: PayaboSpacing.sm),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          if (displayName.isNotEmpty)
            Text(
              'Hey $displayName',
              style: Theme.of(context).textTheme.displayLarge?.copyWith(
                    color: _chatBodyTextColor(context),
                    fontSize: 48,
                    fontWeight: FontWeight.w800,
                    height: 1.0,
                    letterSpacing: -1.6,
                  ),
            )
          else
            Text(
              'Hey there',
              style: Theme.of(context).textTheme.displayLarge?.copyWith(
                    color: _chatBodyTextColor(context),
                    fontSize: 48,
                    fontWeight: FontWeight.w800,
                    height: 1.0,
                    letterSpacing: -1.6,
                  ),
            ),
        ],
      ),
    );
  }
}

class _ChatMessageBlock extends StatelessWidget {
  const _ChatMessageBlock({required this.message});

  final ChatMessage message;

  @override
  Widget build(BuildContext context) {
    if (message.sender == ChatSender.user) {
      return Align(
        alignment: Alignment.centerRight,
        child: ConstrainedBox(
          constraints: BoxConstraints(
            maxWidth: MediaQuery.sizeOf(context).width * 0.78,
          ),
          child: ClipRRect(
            borderRadius: const BorderRadius.only(
              topLeft: Radius.circular(22),
              topRight: Radius.circular(22),
              bottomLeft: Radius.circular(22),
              bottomRight: Radius.circular(10),
            ),
            child: DecoratedBox(
              decoration: BoxDecoration(
                color: _chatUserBubbleColor(context),
                gradient: _chatUserBubbleGradient(context),
                border: Border.all(color: _chatPremiumBorderColor(context)),
                boxShadow: const <BoxShadow>[
                  BoxShadow(
                    color: Color(0x22000000),
                    blurRadius: 14,
                    offset: Offset(0, 8),
                  ),
                ],
              ),
              child: Stack(
                children: <Widget>[
                  Positioned(
                    top: 0,
                    left: 18,
                    right: 18,
                    child: Container(
                      height: 1,
                      color: Colors.white.withValues(alpha: 0.14),
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: PayaboSpacing.lg,
                      vertical: PayaboSpacing.md,
                    ),
                    child: Text(
                      message.lines.first,
                      style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                            color: _chatBodyTextColor(context),
                            height: 1.35,
                          ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
    }

    final c = context.colors;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xs),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Container(
                    width: 10,
                    height: 10,
                    decoration: BoxDecoration(
                      color: c.primary,
                      shape: BoxShape.circle,
                    ),
                  ),
                  const SizedBox(width: PayaboSpacing.sm),
                  Text(
                    'Simi',
                    style:
                        Theme.of(context).textTheme.labelLarge?.copyWith(
                              color: _chatBodyTextColor(context),
                              fontWeight: FontWeight.w700,
                              letterSpacing: 0.2,
                            ),
                  ),
                ],
              ),
              const SizedBox(height: PayaboSpacing.md),
              ...message.lines.map(
                (String line) => Padding(
                  padding:
                      const EdgeInsets.only(bottom: PayaboSpacing.sm),
                  child: Text(
                    line,
                    style:
                        Theme.of(context).textTheme.bodyLarge?.copyWith(
                              color: _chatBodyTextColor(context),
                              height: 1.58,
                            ),
                  ),
                ),
              ),
            ],
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
    final c = context.colors;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: _chatPlanSurfaceColor(context),
        gradient: _chatPlanGradient(context),
        borderRadius: BorderRadius.circular(28),
        border: Border.all(color: _chatPremiumBorderColor(context)),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x1E000000),
            blurRadius: 16,
            offset: Offset(0, 10),
          ),
        ],
      ),
      child: Stack(
        children: <Widget>[
          Positioned(
            top: 0,
            left: 18,
            right: 18,
            child: Container(
              height: 1,
              color: _chatPremiumHighlightColor(context),
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(PayaboSpacing.xl),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  'ACTION PLAN',
                  style: Theme.of(context).textTheme.labelLarge?.copyWith(
                        color: _chatMutedTextColor(context),
                        fontWeight: FontWeight.w700,
                        letterSpacing: 2.8,
                      ),
                ),
                const SizedBox(height: PayaboSpacing.sm),
                Text(
                  title,
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        color: _chatBodyTextColor(context),
                        fontWeight: FontWeight.w700,
                      ),
                ),
                const SizedBox(height: PayaboSpacing.lg),
                ...items.asMap().entries.map(
                      (MapEntry<int, String> entry) => Padding(
                        padding:
                            const EdgeInsets.only(bottom: PayaboSpacing.md),
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: <Widget>[
                            Container(
                              width: 28,
                              height: 28,
                              alignment: Alignment.center,
                              decoration: BoxDecoration(
                                color: c.primary.withValues(alpha: 0.12),
                                borderRadius: BorderRadius.circular(999),
                                border: Border.all(
                                  color: c.primary.withValues(alpha: 0.26),
                                ),
                              ),
                              child: Text(
                                '${entry.key + 1}',
                                style: Theme.of(context)
                                    .textTheme
                                    .labelMedium
                                    ?.copyWith(
                                      color: c.primary,
                                      fontWeight: FontWeight.w800,
                                    ),
                              ),
                            ),
                            const SizedBox(width: PayaboSpacing.md),
                            Expanded(
                              child: Padding(
                                padding: const EdgeInsets.only(top: 2),
                                child: Text(
                                  entry.value,
                                  style: Theme.of(context)
                                      .textTheme
                                      .bodyLarge
                                      ?.copyWith(
                                        color: _chatMutedTextColor(context),
                                        height: 1.5,
                                      ),
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
        ],
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
          width: 52,
          height: 52,
          decoration: BoxDecoration(
            color: Colors.white.withValues(alpha: 0.05),
            shape: BoxShape.circle,
            border: Border.all(color: _chatBorderColor(context)),
          ),
          child: Icon(
            Icons.menu_rounded,
            size: 22,
            color: _chatBodyTextColor(context),
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
    required this.isFreshDemo,
    required this.onSubmitted,
  });

  final TextEditingController controller;
  final bool canSend;
  final bool isFreshDemo;
  final ValueChanged<String> onSubmitted;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return ClipRRect(
      borderRadius: BorderRadius.circular(34),
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: _chatTrayColor(context),
          gradient: _chatTrayGradient(context),
          borderRadius: BorderRadius.circular(34),
          border: Border.all(color: _chatPremiumBorderColor(context)),
          boxShadow: _chatTrayShadow(),
        ),
        child: Stack(
          children: <Widget>[
            Positioned(
              top: 0,
              left: 22,
              right: 22,
              child: Container(
                height: 1,
                color: _chatPremiumHighlightColor(context),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(
                PayaboSpacing.lg,
                PayaboSpacing.lg,
                PayaboSpacing.lg,
                PayaboSpacing.md,
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      Expanded(
                        child: DecoratedBox(
                          decoration: BoxDecoration(
                            color: _chatInputSurfaceColor(context),
                            gradient: _chatInputGradient(context),
                            borderRadius: BorderRadius.circular(24),
                            border: Border.all(
                              color: _chatPremiumBorderColor(context),
                            ),
                            boxShadow: const <BoxShadow>[
                              BoxShadow(
                                color: Color(0x14000000),
                                blurRadius: 8,
                                offset: Offset(0, 4),
                              ),
                            ],
                          ),
                          child: Padding(
                            padding: const EdgeInsets.symmetric(
                              horizontal: PayaboSpacing.lg,
                            ),
                            child: TextField(
                              controller: controller,
                              minLines: 1,
                              maxLines: 4,
                              style: Theme.of(context)
                                  .textTheme
                                  .bodyLarge
                                  ?.copyWith(
                                    color: _chatBodyTextColor(context),
                                    height: 1.35,
                                    fontWeight: FontWeight.w400,
                                  ),
                              cursorColor: c.primary,
                              textInputAction: TextInputAction.send,
                              onSubmitted: onSubmitted,
                              decoration: InputDecoration(
                                hintText: isFreshDemo
                                    ? 'Try asking "How do I stop missing bills?"'
                                    : 'Write here...',
                                hintStyle: Theme.of(context)
                                    .textTheme
                                    .bodyLarge
                                    ?.copyWith(
                                      color: _chatMutedTextColor(context),
                                      fontWeight: FontWeight.w400,
                                    ),
                                border: InputBorder.none,
                                enabledBorder: InputBorder.none,
                                focusedBorder: InputBorder.none,
                                filled: false,
                                contentPadding: const EdgeInsets.symmetric(
                                  vertical: 18,
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(width: PayaboSpacing.md),
                      _ChatSendButton(
                        isEnabled: canSend,
                        onTap: () => onSubmitted(controller.text),
                      ),
                    ],
                  ),
                  const SizedBox(height: PayaboSpacing.md),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: PayaboSpacing.xs,
                      vertical: PayaboSpacing.xs,
                    ),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.025),
                      borderRadius: BorderRadius.circular(22),
                      border: Border.all(
                        color: Colors.white.withValues(alpha: 0.05),
                      ),
                    ),
                    child: Row(
                      children: _composerActions
                          .asMap()
                          .entries
                          .expand((MapEntry<int, _ComposerActionSpec> entry) {
                        final List<Widget> widgets = <Widget>[
                          Expanded(
                            child: _ComposerActionButton(spec: entry.value),
                          ),
                        ];
                        if (entry.key != _composerActions.length - 1) {
                          widgets.add(
                            Container(
                              width: 1,
                              height: 22,
                              color: Colors.white.withValues(alpha: 0.08),
                            ),
                          );
                        }
                        return widgets;
                      }).toList(growable: false),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ChatSendButton extends StatelessWidget {
  const _ChatSendButton({
    required this.isEnabled,
    required this.onTap,
  });

  final bool isEnabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      shape: const CircleBorder(),
      child: InkWell(
        onTap: isEnabled ? onTap : null,
        customBorder: const CircleBorder(),
        child: Ink(
          width: 60,
          height: 60,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            gradient: LinearGradient(
              colors: isEnabled
                  ? const <Color>[Color(0xFFF4A027), Color(0xFFD16E1D)]
                  : const <Color>[Color(0xFF85592E), Color(0xFF624221)],
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
            ),
            border: Border.all(
              color: Colors.white.withValues(alpha: isEnabled ? 0.12 : 0.06),
            ),
            boxShadow: <BoxShadow>[
              BoxShadow(
                color: isEnabled
                    ? const Color(0x2CF4A027)
                    : const Color(0x14000000),
                blurRadius: 16,
                offset: const Offset(0, 8),
              ),
            ],
          ),
          child: Icon(
            Icons.send_rounded,
            color: Colors.black.withValues(alpha: isEnabled ? 0.92 : 0.42),
            size: 28,
          ),
        ),
      ),
    );
  }
}

class _ComposerActionButton extends StatelessWidget {
  const _ComposerActionButton({required this.spec});

  final _ComposerActionSpec spec;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () {},
      borderRadius: BorderRadius.circular(18),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.sm,
          vertical: PayaboSpacing.md,
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Icon(
              spec.icon,
              size: 20,
              color: _chatBodyTextColor(context),
            ),
            const SizedBox(width: PayaboSpacing.sm),
            Text(
              spec.label,
              style: Theme.of(context).textTheme.labelLarge?.copyWith(
                    color: _chatBodyTextColor(context),
                    fontWeight: FontWeight.w500,
                    letterSpacing: 0.2,
                  ),
            ),
          ],
        ),
      ),
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
            colors: <Color>[color, Colors.transparent],
          ),
        ),
      ),
    );
  }
}

class _ComposerActionSpec {
  const _ComposerActionSpec({
    required this.label,
    required this.icon,
  });

  final String label;
  final IconData icon;
}
