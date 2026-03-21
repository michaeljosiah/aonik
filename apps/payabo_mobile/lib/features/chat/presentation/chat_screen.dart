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
import '../domain/chat_controller.dart';

final FutureProvider<List<ChatConversation>> _chatConversationsProvider =
    FutureProvider<List<ChatConversation>>((Ref ref) async {
  ref.watch(demoDataModeProvider);
  final ChatRepository repository = ref.watch(chatRepositoryProvider);
  return repository.getConversations();
});


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
    final ChatState chatState = ref.watch(chatControllerProvider);

    // Auto-scroll when streaming text updates arrive.
    ref.listen<ChatState>(chatControllerProvider, (ChatState? prev, ChatState next) {
      if (prev != null && next.streamingText.length > prev.streamingText.length) {
        _scrollToBottom();
      }
      // Also scroll when a new completed message appears.
      if (prev != null && next.messages.length > prev.messages.length) {
        _scrollToBottom();
      }
      // Scroll when a new approval card appears.
      if (prev != null &&
          next.pendingApprovals.length > prev.pendingApprovals.length) {
        _scrollToBottom();
      }
    });

    // Sync seeded conversations from provider (for history navigation only).
    final AsyncValue<List<ChatConversation>> conversationsAsync =
        ref.watch(_chatConversationsProvider);

    final bool showHero = !chatState.hasMessages &&
        chatState.streamingText.isEmpty &&
        chatState.activity == ChatActivity.idle;

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
                        onTap: () => _openHistory(isFreshDemo, conversationsAsync),
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
                            messages: chatState.messages,
                            streamingText: chatState.streamingText,
                            activity: chatState.activity,
                            activeToolCalls: chatState.activeToolCalls,
                            pendingApprovals: chatState.pendingApprovals,
                            onApprove: (String toolCallId) {
                              ref.read(chatControllerProvider.notifier).approveAction(toolCallId);
                              _scrollToBottom();
                            },
                            onReject: (String toolCallId, [String? reason]) {
                              ref.read(chatControllerProvider.notifier).rejectAction(toolCallId, reason);
                              _scrollToBottom();
                            },
                          ),
                  ),
                ),
                if (chatState.errorMessage != null)
                  Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: PayaboSpacing.xl,
                    ),
                    child: _ChatErrorBanner(message: chatState.errorMessage!),
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
                    canSend: _controller.text.trim().isNotEmpty &&
                        !chatState.isProcessing,
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

  Future<void> _openHistory(
    bool isFreshDemo,
    AsyncValue<List<ChatConversation>> conversationsAsync,
  ) async {
    final chatState = ref.read(chatControllerProvider);
    final String? currentId = chatState.threadId;

    final String? selectedId = await context.push<String>(
      currentId == null ? '/chat/history' : '/chat/history?selected=$currentId',
    );

    if (!mounted || selectedId == null) {
      return;
    }

    // Try to load from seeded conversations (mock mode).
    final conversations = conversationsAsync.value ?? const [];
    final match = conversations.cast<ChatConversation?>().firstWhere(
          (c) => c?.id == selectedId,
          orElse: () => null,
        );

    if (match != null) {
      ref.read(chatControllerProvider.notifier).loadConversation(match);
    }

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
    final String prompt = (preset ?? _controller.text).trim();

    if (prompt.isEmpty) {
      return;
    }

    FocusScope.of(context).unfocus();
    _controller.clear();

    ref.read(chatControllerProvider.notifier).sendMessage(prompt);
    _scrollToBottom();
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
    this.streamingText = '',
    this.activity = ChatActivity.idle,
    this.activeToolCalls = const [],
    this.pendingApprovals = const [],
    this.onApprove,
    this.onReject,
  });

  final ScrollController controller;
  final String displayName;
  final List<ChatMessage> messages;
  final String streamingText;
  final ChatActivity activity;
  final List<ActiveToolCall> activeToolCalls;
  final List<PendingApproval> pendingApprovals;
  final void Function(String toolCallId)? onApprove;
  final void Function(String toolCallId, [String? reason])? onReject;

  @override
  Widget build(BuildContext context) {
    final bool isStreaming = streamingText.isNotEmpty;
    final bool isThinking = activity == ChatActivity.connecting ||
        (activity == ChatActivity.toolCall && streamingText.isEmpty);

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
        // Show in-progress streaming bubble.
        if (isStreaming)
          Padding(
            padding: const EdgeInsets.only(bottom: PayaboSpacing.xl),
            child: _StreamingMessageBlock(
              text: streamingText,
              activeToolCalls: activeToolCalls,
            ),
          ),
        // Show thinking indicator when connecting or running tool calls
        // with no text yet.
        if (isThinking)
          const Padding(
            padding: EdgeInsets.only(bottom: PayaboSpacing.xl),
            child: _ThinkingIndicator(),
          ),
        // Show approval cards for pending confirmAction requests.
        ...pendingApprovals.map(
          (PendingApproval approval) => Padding(
            padding: const EdgeInsets.only(bottom: PayaboSpacing.xl),
            child: _ApprovalCard(
              approval: approval,
              onApprove: () => onApprove?.call(approval.toolCallId),
              onReject: () => onReject?.call(approval.toolCallId),
            ),
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

// ─────────────────────────────────────────────────────────
//  Streaming / activity widgets
// ─────────────────────────────────────────────────────────

/// Shows the assistant's in-progress streaming response.
class _StreamingMessageBlock extends StatelessWidget {
  const _StreamingMessageBlock({
    required this.text,
    this.activeToolCalls = const [],
  });

  final String text;
  final List<ActiveToolCall> activeToolCalls;

  @override
  Widget build(BuildContext context) {
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
                    style: Theme.of(context).textTheme.labelLarge?.copyWith(
                          color: _chatBodyTextColor(context),
                          fontWeight: FontWeight.w700,
                          letterSpacing: 0.2,
                        ),
                  ),
                ],
              ),
              const SizedBox(height: PayaboSpacing.md),
              // Show tool call chips (if any).
              if (activeToolCalls.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.only(bottom: PayaboSpacing.sm),
                  child: Wrap(
                    spacing: PayaboSpacing.xs,
                    runSpacing: PayaboSpacing.xs,
                    children: activeToolCalls
                        .map((tc) => _ToolCallChip(
                              name: tc.toolName,
                              status: tc.status,
                            ))
                        .toList(),
                  ),
                ),
              // Streaming text with a blinking cursor.
              Text.rich(
                TextSpan(
                  children: <InlineSpan>[
                    TextSpan(text: text),
                    WidgetSpan(
                      alignment: PlaceholderAlignment.middle,
                      child: _BlinkingCursor(color: c.primary),
                    ),
                  ],
                ),
                style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                      color: _chatBodyTextColor(context),
                      height: 1.58,
                    ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// Thinking indicator shown while waiting for the agent to start responding.
class _ThinkingIndicator extends StatelessWidget {
  const _ThinkingIndicator();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xs),
      child: Row(
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
            style: Theme.of(context).textTheme.labelLarge?.copyWith(
                  color: _chatBodyTextColor(context),
                  fontWeight: FontWeight.w700,
                  letterSpacing: 0.2,
                ),
          ),
          const SizedBox(width: PayaboSpacing.md),
          SizedBox(
            width: 16,
            height: 16,
            child: CircularProgressIndicator(
              strokeWidth: 2,
              valueColor: AlwaysStoppedAnimation<Color>(
                c.primary.withValues(alpha: 0.6),
              ),
            ),
          ),
          const SizedBox(width: PayaboSpacing.sm),
          Text(
            'Thinking...',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: _chatMutedTextColor(context),
                  fontStyle: FontStyle.italic,
                ),
          ),
        ],
      ),
    );
  }
}

/// Approval card shown when the agent requests user confirmation for a
/// mutating action (via the confirmAction frontend tool).
class _ApprovalCard extends StatelessWidget {
  const _ApprovalCard({
    required this.approval,
    required this.onApprove,
    required this.onReject,
  });

  final PendingApproval approval;
  final VoidCallback onApprove;
  final VoidCallback onReject;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    final Color severityColor;
    final IconData severityIcon;
    switch (approval.severity) {
      case 'high':
        severityColor = Colors.red;
        severityIcon = Icons.warning_amber_rounded;
      case 'low':
        severityColor = Colors.green;
        severityIcon = Icons.info_outline_rounded;
      default: // 'medium'
        severityColor = Colors.orange;
        severityIcon = Icons.help_outline_rounded;
    }

    return Container(
      margin: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xs),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            severityColor.withValues(alpha: 0.08),
            severityColor.withValues(alpha: 0.03),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: severityColor.withValues(alpha: 0.2),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          // Header
          Padding(
            padding: const EdgeInsets.fromLTRB(
              PayaboSpacing.md,
              PayaboSpacing.md,
              PayaboSpacing.md,
              PayaboSpacing.sm,
            ),
            child: Row(
              children: <Widget>[
                Container(
                  padding: const EdgeInsets.all(6),
                  decoration: BoxDecoration(
                    color: severityColor.withValues(alpha: 0.12),
                    shape: BoxShape.circle,
                  ),
                  child: Icon(
                    severityIcon,
                    size: 18,
                    color: severityColor.withValues(alpha: 0.9),
                  ),
                ),
                const SizedBox(width: PayaboSpacing.sm),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        'Simi wants to perform an action',
                        style: Theme.of(context).textTheme.labelSmall?.copyWith(
                              color: _chatMutedTextColor(context),
                              fontWeight: FontWeight.w500,
                              letterSpacing: 0.3,
                            ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        approval.action,
                        style: Theme.of(context).textTheme.titleSmall?.copyWith(
                              color: _chatBodyTextColor(context),
                              fontWeight: FontWeight.w700,
                            ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          // Description
          if (approval.description.isNotEmpty)
            Padding(
              padding: const EdgeInsets.fromLTRB(
                PayaboSpacing.md,
                0,
                PayaboSpacing.md,
                PayaboSpacing.md,
              ),
              child: Text(
                approval.description,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: _chatBodyTextColor(context).withValues(alpha: 0.8),
                      height: 1.5,
                    ),
              ),
            ),
          // Divider
          Container(
            height: 1,
            color: severityColor.withValues(alpha: 0.1),
          ),
          // Action buttons
          Padding(
            padding: const EdgeInsets.all(PayaboSpacing.sm),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: TextButton(
                    onPressed: onReject,
                    style: TextButton.styleFrom(
                      foregroundColor: _chatMutedTextColor(context),
                      padding: const EdgeInsets.symmetric(
                        vertical: PayaboSpacing.sm,
                      ),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(10),
                        side: BorderSide(
                          color: Colors.white.withValues(alpha: 0.08),
                        ),
                      ),
                    ),
                    child: const Text('Reject'),
                  ),
                ),
                const SizedBox(width: PayaboSpacing.sm),
                Expanded(
                  child: TextButton(
                    onPressed: onApprove,
                    style: TextButton.styleFrom(
                      foregroundColor: Colors.white,
                      backgroundColor: c.primary,
                      padding: const EdgeInsets.symmetric(
                        vertical: PayaboSpacing.sm,
                      ),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(10),
                      ),
                    ),
                    child: const Text('Approve'),
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

/// A small chip showing a tool call name with a status-appropriate icon.
class _ToolCallChip extends StatelessWidget {
  const _ToolCallChip({
    required this.name,
    required this.status,
  });

  final String name;
  final ToolCallStatus status;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    final (Color chipColor, Widget icon) = _statusVisuals(c);

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.sm,
        vertical: PayaboSpacing.xxs,
      ),
      decoration: BoxDecoration(
        color: chipColor.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: chipColor.withValues(alpha: 0.18),
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          icon,
          const SizedBox(width: PayaboSpacing.xxs),
          Text(
            _friendlyToolName(name),
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
                  color: _chatMutedTextColor(context),
                  fontWeight: FontWeight.w600,
                  letterSpacing: 0.3,
                ),
          ),
        ],
      ),
    );
  }

  (Color, Widget) _statusVisuals(PayaboColorResolver c) {
    switch (status) {
      case ToolCallStatus.streaming:
      case ToolCallStatus.executing:
        return (
          c.primary,
          SizedBox(
            width: 12,
            height: 12,
            child: CircularProgressIndicator(
              strokeWidth: 1.5,
              valueColor: AlwaysStoppedAnimation<Color>(
                c.primary.withValues(alpha: 0.6),
              ),
            ),
          ),
        );

      case ToolCallStatus.pending:
        return (
          c.primary,
          Icon(
            Icons.schedule_rounded,
            size: 14,
            color: c.primary.withValues(alpha: 0.7),
          ),
        );

      case ToolCallStatus.awaitingApproval:
        return (
          Colors.orange,
          Icon(
            Icons.shield_rounded,
            size: 14,
            color: Colors.orange.withValues(alpha: 0.8),
          ),
        );

      case ToolCallStatus.completed:
        return (
          c.primary,
          Icon(
            Icons.check_circle_rounded,
            size: 14,
            color: c.primary.withValues(alpha: 0.7),
          ),
        );

      case ToolCallStatus.error:
        return (
          Colors.red,
          Icon(
            Icons.error_rounded,
            size: 14,
            color: Colors.red.withValues(alpha: 0.7),
          ),
        );
    }
  }

  /// Converts a camelCase or PascalCase tool name to a friendly label.
  static String _friendlyToolName(String raw) {
    // Insert spaces before capital letters and capitalize first letter.
    final spaced = raw.replaceAllMapped(
      RegExp(r'(?<=[a-z])([A-Z])'),
      (m) => ' ${m.group(1)}',
    );
    if (spaced.isEmpty) return raw;
    return spaced[0].toUpperCase() + spaced.substring(1);
  }
}

/// A blinking cursor widget for the streaming text.
class _BlinkingCursor extends StatefulWidget {
  const _BlinkingCursor({required this.color});

  final Color color;

  @override
  State<_BlinkingCursor> createState() => _BlinkingCursorState();
}

class _BlinkingCursorState extends State<_BlinkingCursor>
    with SingleTickerProviderStateMixin {
  late AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 600),
    )..repeat(reverse: true);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return FadeTransition(
      opacity: _controller,
      child: Container(
        width: 2,
        height: 18,
        margin: const EdgeInsets.only(left: 1),
        color: widget.color,
      ),
    );
  }
}

/// Error banner shown above the composer when the last request failed.
class _ChatErrorBanner extends StatelessWidget {
  const _ChatErrorBanner({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: PayaboSpacing.sm),
      padding: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.md,
        vertical: PayaboSpacing.sm,
      ),
      decoration: BoxDecoration(
        color: Colors.red.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: Colors.red.withValues(alpha: 0.2),
        ),
      ),
      child: Row(
        children: <Widget>[
          Icon(
            Icons.error_outline_rounded,
            color: Colors.red.withValues(alpha: 0.7),
            size: 18,
          ),
          const SizedBox(width: PayaboSpacing.sm),
          Expanded(
            child: Text(
              message,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: Colors.red.withValues(alpha: 0.8),
                  ),
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
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
