import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../data/repositories/chat_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/theme/payabo_theme.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../profile/presentation/profile_state.dart';
import '../domain/chat_controller.dart';
import 'chat_history_screen.dart';

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

class _ChatScreenState extends ConsumerState<ChatScreen>
    with SingleTickerProviderStateMixin {
  static const double _historyOverlayWidthFactor = 0.9;

  final TextEditingController _controller = TextEditingController();
  final ScrollController _scrollController = ScrollController();
  late final AnimationController _historyOverlayController;

  @override
  void initState() {
    super.initState();
    _historyOverlayController = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 280),
      reverseDuration: const Duration(milliseconds: 220),
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    _scrollController.dispose();
    _historyOverlayController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final String displayName = ref.watch(
      profileHeaderProvider.select(
        (ProfileHeaderState state) => state.displayName,
      ),
    );

    // Auto-scroll when streaming text updates arrive.
    ref.listen<ChatState>(chatControllerProvider, (
      ChatState? prev,
      ChatState next,
    ) {
      if (prev == null) {
        return;
      }

      final bool keepPinnedToBottom = _isNearBottom();

      if (next.streamingText.length > prev.streamingText.length &&
          keepPinnedToBottom) {
        _scrollToBottom(animated: false);
      }

      if (next.messages.length > prev.messages.length && keepPinnedToBottom) {
        _scrollToBottom();
      }

      if (next.pendingApprovals.length > prev.pendingApprovals.length &&
          keepPinnedToBottom) {
        _scrollToBottom();
      }

      if (next.displayWidgets.length > prev.displayWidgets.length &&
          keepPinnedToBottom) {
        _scrollToBottom();
      }

      // Refresh thread list when a streaming run completes so new
      // conversations appear in history.
      if (prev.activity != ChatActivity.idle &&
          next.activity == ChatActivity.idle &&
          next.messages.isNotEmpty) {
        ref.invalidate(_chatConversationsProvider);
      }
    });

    // Sync seeded conversations from provider (for history navigation only).
    final AsyncValue<List<ChatConversation>> conversationsAsync =
        ref.watch(_chatConversationsProvider);
    final String? currentConversationId = ref.watch(
      chatControllerProvider.select((ChatState state) => state.threadId),
    );

    return Stack(
      children: <Widget>[
        Scaffold(
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
                            onTap: _toggleHistoryOverlay,
                          ),
                        ],
                      ),
                    ),
                    Expanded(
                      child: _ChatStage(
                        controller: _scrollController,
                        displayName: _firstName(displayName),
                        onApprove: (String toolCallId) {
                          ref
                              .read(chatControllerProvider.notifier)
                              .approveAction(toolCallId);
                          _scrollToBottom(force: true);
                        },
                        onReject: (String toolCallId, [String? reason]) {
                          ref
                              .read(chatControllerProvider.notifier)
                              .rejectAction(toolCallId, reason);
                          _scrollToBottom(force: true);
                        },
                      ),
                    ),
                    const _ChatErrorSlot(),
                    Padding(
                      padding: const EdgeInsets.fromLTRB(
                        PayaboSpacing.md,
                        0,
                        PayaboSpacing.md,
                        PayaboSpacing.md,
                      ),
                      child: _ChatComposer(
                        controller: _controller,
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
        ),
        _ChatHistoryOverlay(
          controller: _historyOverlayController,
          onClose: _closeHistoryOverlay,
          onDragUpdate: (DragUpdateDetails details) =>
              _handleHistoryDragUpdate(details, context),
          onDragEnd: _handleHistoryDragEnd,
          child: ChatHistoryScreen(
            embedded: true,
            selectedConversationId: currentConversationId,
            onClose: _closeHistoryOverlay,
            onConversationSelected: (String selectedId) =>
                _handleHistorySelection(selectedId, conversationsAsync),
          ),
        ),
      ],
    );
  }

  void _toggleHistoryOverlay() {
    FocusScope.of(context).unfocus();

    if (_historyOverlayController.value > 0) {
      _closeHistoryOverlay();
      return;
    }

    _historyOverlayController.forward();
  }

  void _closeHistoryOverlay() {
    _historyOverlayController.reverse();
  }

  Future<void> _handleHistorySelection(
    String selectedId,
    AsyncValue<List<ChatConversation>> conversationsAsync,
  ) async {
    _closeHistoryOverlay();

    // In mock mode with populated conversations, load directly from the
    // in-memory data. Otherwise fetch the full thread from the backend.
    final conversations = conversationsAsync.value ?? const [];
    final match = conversations.cast<ChatConversation?>().firstWhere(
          (c) => c?.id == selectedId,
          orElse: () => null,
        );

    if (match != null && match.messages.isNotEmpty) {
      ref.read(chatControllerProvider.notifier).loadConversation(match);
    } else {
      await ref.read(chatControllerProvider.notifier).loadThread(selectedId);
    }

    if (!mounted) {
      return;
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
    _scrollToBottom(force: true);
  }

  void _handleHistoryDragUpdate(
    DragUpdateDetails details,
    BuildContext context,
  ) {
    final double? delta = details.primaryDelta;
    if (delta == null) {
      return;
    }

    final double panelWidth =
        MediaQuery.sizeOf(context).width * _historyOverlayWidthFactor;
    _historyOverlayController.value =
        (_historyOverlayController.value + (delta / panelWidth))
            .clamp(0.0, 1.0);
  }

  void _handleHistoryDragEnd(DragEndDetails details) {
    final double velocity = details.primaryVelocity ?? 0;
    if (velocity < -320 || _historyOverlayController.value < 0.72) {
      _closeHistoryOverlay();
      return;
    }

    _historyOverlayController.forward();
  }

  bool _isNearBottom() {
    if (!_scrollController.hasClients) {
      return true;
    }

    final ScrollPosition position = _scrollController.position;
    return position.maxScrollExtent - position.pixels <= 120;
  }

  void _scrollToBottom({bool animated = true, bool force = false}) {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!_scrollController.hasClients) {
        return;
      }

      if (!force && !_isNearBottom()) {
        return;
      }

      final double target = _scrollController.position.maxScrollExtent;

      if (!animated) {
        _scrollController.jumpTo(target);
        return;
      }

      _scrollController.animateTo(
        target,
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

class _ChatHistoryOverlay extends StatelessWidget {
  const _ChatHistoryOverlay({
    required this.controller,
    required this.onClose,
    required this.onDragUpdate,
    required this.onDragEnd,
    required this.child,
  });

  final AnimationController controller;
  final VoidCallback onClose;
  final ValueChanged<DragUpdateDetails> onDragUpdate;
  final ValueChanged<DragEndDetails> onDragEnd;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: controller,
      builder: (BuildContext context, Widget? _) {
        if (controller.isDismissed && !controller.isAnimating) {
          return const SizedBox.shrink();
        }

        final double progress = Curves.easeOutCubic.transform(controller.value);

        return IgnorePointer(
          ignoring: progress == 0,
          child: Stack(
            children: <Widget>[
              Positioned.fill(
                child: GestureDetector(
                  onTap: onClose,
                  behavior: HitTestBehavior.opaque,
                  child: ColoredBox(
                    color: Colors.black.withValues(alpha: 0.28 * progress),
                  ),
                ),
              ),
              Align(
                alignment: Alignment.centerLeft,
                child: FractionallySizedBox(
                  widthFactor: _ChatScreenState._historyOverlayWidthFactor,
                  child: FractionalTranslation(
                    translation: Offset(progress - 1, 0),
                    child: GestureDetector(
                      onHorizontalDragUpdate: onDragUpdate,
                      onHorizontalDragEnd: onDragEnd,
                      behavior: HitTestBehavior.translucent,
                      child: DecoratedBox(
                        key: const ValueKey<String>('chat-history-overlay'),
                        decoration: BoxDecoration(
                          borderRadius: const BorderRadius.horizontal(
                            right: Radius.circular(32),
                          ),
                          boxShadow: <BoxShadow>[
                            BoxShadow(
                              color: Colors.black.withValues(alpha: 0.28),
                              blurRadius: 28,
                              offset: const Offset(8, 0),
                            ),
                          ],
                        ),
                        child: ClipRRect(
                          borderRadius: const BorderRadius.horizontal(
                            right: Radius.circular(32),
                          ),
                          child: child,
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _ChatStage extends ConsumerWidget {
  const _ChatStage({
    required this.controller,
    required this.displayName,
    required this.onApprove,
    required this.onReject,
  });

  final ScrollController controller;
  final String displayName;
  final void Function(String toolCallId) onApprove;
  final void Function(String toolCallId, [String? reason]) onReject;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final ChatState chatState = ref.watch(chatControllerProvider);
    final bool showHero = !chatState.hasMessages &&
        chatState.streamingText.isEmpty &&
        chatState.activity == ChatActivity.idle;

    return AnimatedSwitcher(
      duration: const Duration(milliseconds: 320),
      switchInCurve: Curves.easeOutCubic,
      switchOutCurve: Curves.easeInCubic,
      child: showHero
          ? _EmptyChatStage(
              key: const ValueKey<String>('chat-empty'),
              displayName: displayName,
            )
          : _ConversationStage(
              key: const ValueKey<String>('chat-thread'),
              controller: controller,
              displayName: displayName,
              messages: chatState.messages,
              streamingText: chatState.streamingText,
              activity: chatState.activity,
              activeToolCalls: chatState.activeToolCalls,
              pendingApprovals: chatState.pendingApprovals,
              displayWidgets: chatState.displayWidgets,
              onApprove: onApprove,
              onReject: onReject,
            ),
    );
  }
}

class _ChatErrorSlot extends ConsumerWidget {
  const _ChatErrorSlot();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final String? errorMessage = ref.watch(
      chatControllerProvider.select((ChatState state) => state.errorMessage),
    );

    if (errorMessage == null) {
      return const SizedBox.shrink();
    }

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xl),
      child: _ChatErrorBanner(message: errorMessage),
    );
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
    this.displayWidgets = const [],
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
  final List<DisplayWidget> displayWidgets;
  final void Function(String toolCallId)? onApprove;
  final void Function(String toolCallId, [String? reason])? onReject;

  @override
  Widget build(BuildContext context) {
    final bool isStreaming = streamingText.isNotEmpty;
    final bool isThinking = activity == ChatActivity.connecting ||
        (activity == ChatActivity.toolCall && streamingText.isEmpty);
    final int itemCount = 2 +
        messages.length +
        (isStreaming ? 1 : 0) +
        (isThinking ? 1 : 0) +
        displayWidgets.length +
        pendingApprovals.length;

    return ListView.builder(
      controller: controller,
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.xl,
        PayaboSpacing.xl,
        PayaboSpacing.xl,
        PayaboSpacing.xl,
      ),
      itemCount: itemCount,
      itemBuilder: (BuildContext context, int index) {
        if (index == 0) {
          return _CompactChatIntroCard(displayName: displayName);
        }

        if (index == 1) {
          return const SizedBox(height: PayaboSpacing.xl);
        }

        int contentIndex = index - 2;

        if (contentIndex < messages.length) {
          final ChatMessage message = messages[contentIndex];
          return Padding(
            padding: const EdgeInsets.only(bottom: PayaboSpacing.xl),
            child: _ChatMessageBlock(message: message),
          );
        }
        contentIndex -= messages.length;

        if (isStreaming) {
          if (contentIndex == 0) {
            return Padding(
              padding: const EdgeInsets.only(bottom: PayaboSpacing.xl),
              child: _StreamingMessageBlock(
                text: streamingText,
                activeToolCalls: activeToolCalls,
              ),
            );
          }
          contentIndex -= 1;
        }

        if (isThinking) {
          if (contentIndex == 0) {
            return const Padding(
              padding: EdgeInsets.only(bottom: PayaboSpacing.xl),
              child: _ThinkingIndicator(),
            );
          }
          contentIndex -= 1;
        }

        if (contentIndex < displayWidgets.length) {
          return Padding(
            padding: const EdgeInsets.only(bottom: PayaboSpacing.xl),
            child: _DisplayWidgetDispatcher(
              widget: displayWidgets[contentIndex],
            ),
          );
        }
        contentIndex -= displayWidgets.length;

        final PendingApproval approval = pendingApprovals[contentIndex];
        return Padding(
          padding: const EdgeInsets.only(bottom: PayaboSpacing.xl),
          child: _ApprovalCard(
            approval: approval,
            onApprove: () => onApprove?.call(approval.toolCallId),
            onReject: () => onReject?.call(approval.toolCallId),
          ),
        );
      },
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
                    style: Theme.of(context).textTheme.labelLarge?.copyWith(
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
                  padding: const EdgeInsets.only(bottom: PayaboSpacing.sm),
                  child: Text(
                    line,
                    style: Theme.of(context).textTheme.bodyLarge?.copyWith(
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
        // Display widgets persisted in the message history.
        if (message.hasDisplayWidgets)
          ...message.displayWidgets.map(
            (ChatDisplayWidgetInfo info) => Padding(
              padding: const EdgeInsets.only(top: PayaboSpacing.lg),
              child: _DisplayWidgetDispatcher(
                widget: DisplayWidget(
                  toolCallId: info.toolCallId,
                  widgetType: info.widgetType,
                  data: info.data,
                ),
              ),
            ),
          ),
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

class _ChatComposer extends ConsumerWidget {
  const _ChatComposer({
    required this.controller,
    required this.onSubmitted,
  });

  final TextEditingController controller;
  final ValueChanged<String> onSubmitted;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.colors;
    final bool showHeroHint = ref.watch(
      chatControllerProvider.select(
        (ChatState state) =>
            !state.hasMessages &&
            state.streamingText.isEmpty &&
            state.activity == ChatActivity.idle,
      ),
    );
    final bool isProcessing = ref.watch(
      chatControllerProvider.select((ChatState state) => state.isProcessing),
    );

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
              child: ValueListenableBuilder<TextEditingValue>(
                valueListenable: controller,
                builder: (
                  BuildContext context,
                  TextEditingValue value,
                  Widget? child,
                ) {
                  final bool canSend =
                      value.text.trim().isNotEmpty && !isProcessing;

                  return Row(
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
                                hintText: showHeroHint
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
                        onTap: () => onSubmitted(value.text),
                      ),
                    ],
                  );
                },
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

// ─────────────────────────────────────────────────────────
//  Display widget cards
// ─────────────────────────────────────────────────────────

/// Routes a [DisplayWidget] to the correct card widget based on its type.
class _DisplayWidgetDispatcher extends StatelessWidget {
  const _DisplayWidgetDispatcher({required this.widget});

  final DisplayWidget widget;

  @override
  Widget build(BuildContext context) {
    switch (widget.widgetType) {
      case DisplayWidgetType.fxRateChart:
        return _FxRateChartCard(data: widget.data);
      case DisplayWidgetType.budgetBreakdown:
        return _BudgetBreakdownCard(data: widget.data);
      case DisplayWidgetType.autopilotProposal:
        return _AutopilotProposalCard(data: widget.data);
    }
  }
}

/// FX rate chart card — shows a currency pair rate window with a mini
/// line chart, current rate highlight, and timing signal badge.
class _FxRateChartCard extends StatelessWidget {
  const _FxRateChartCard({required this.data});

  final Map<String, dynamic> data;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    final baseCurrency = data['baseCurrency'] as String? ?? '???';
    final targetCurrency = data['targetCurrency'] as String? ?? '???';
    final rates = (data['rates'] as List<dynamic>?)
            ?.map((e) => e as Map<String, dynamic>)
            .toList() ??
        const [];
    final signal = data['signal'] as String? ?? 'hold';
    final signalReason = data['signalReason'] as String? ?? '';

    final Color signalColor;
    final String signalLabel;
    switch (signal) {
      case 'buy':
        signalColor = Colors.green;
        signalLabel = 'BUY';
      case 'wait':
        signalColor = Colors.red;
        signalLabel = 'WAIT';
      default:
        signalColor = Colors.orange;
        signalLabel = 'HOLD';
    }

    // Parse rate values for the mini chart.
    final rateValues =
        rates.map((r) => (r['rate'] as num?)?.toDouble() ?? 0.0).toList();
    final currentRate = rateValues.isNotEmpty ? rateValues.last : 0.0;

    return Container(
      margin: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xs),
      decoration: BoxDecoration(
        color: _chatPlanSurfaceColor(context),
        gradient: _chatPlanGradient(context),
        borderRadius: BorderRadius.circular(20),
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
            padding: const EdgeInsets.all(PayaboSpacing.lg),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                // Header row: pair label + signal badge.
                Row(
                  children: <Widget>[
                    Icon(
                      Icons.show_chart_rounded,
                      size: 18,
                      color: c.primary.withValues(alpha: 0.7),
                    ),
                    const SizedBox(width: PayaboSpacing.sm),
                    Text(
                      '$baseCurrency / $targetCurrency',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            color: _chatBodyTextColor(context),
                            fontWeight: FontWeight.w700,
                          ),
                    ),
                    const Spacer(),
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: PayaboSpacing.sm,
                        vertical: PayaboSpacing.xxs,
                      ),
                      decoration: BoxDecoration(
                        color: signalColor.withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(
                          color: signalColor.withValues(alpha: 0.24),
                        ),
                      ),
                      child: Text(
                        signalLabel,
                        style: Theme.of(context).textTheme.labelSmall?.copyWith(
                              color: signalColor.withValues(alpha: 0.9),
                              fontWeight: FontWeight.w800,
                              letterSpacing: 1.2,
                            ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: PayaboSpacing.sm),
                // Current rate highlight.
                Text(
                  currentRate.toStringAsFixed(2),
                  style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                        color: _chatBodyTextColor(context),
                        fontWeight: FontWeight.w800,
                        letterSpacing: -0.5,
                      ),
                ),
                const SizedBox(height: PayaboSpacing.md),
                // Mini line chart.
                if (rateValues.length >= 2)
                  SizedBox(
                    height: 60,
                    child: CustomPaint(
                      size: const Size(double.infinity, 60),
                      painter: _MiniLineChartPainter(
                        values: rateValues,
                        lineColor: signalColor.withValues(alpha: 0.7),
                        fillColor: signalColor.withValues(alpha: 0.08),
                      ),
                    ),
                  ),
                if (rateValues.length >= 2)
                  const SizedBox(height: PayaboSpacing.sm),
                // Date labels.
                if (rates.length >= 2)
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: <Widget>[
                      Text(
                        rates.first['date'] as String? ?? '',
                        style: Theme.of(context).textTheme.labelSmall?.copyWith(
                              color: _chatMutedTextColor(context),
                            ),
                      ),
                      Text(
                        rates.last['date'] as String? ?? '',
                        style: Theme.of(context).textTheme.labelSmall?.copyWith(
                              color: _chatMutedTextColor(context),
                            ),
                      ),
                    ],
                  ),
                // Signal reason.
                if (signalReason.isNotEmpty) ...<Widget>[
                  const SizedBox(height: PayaboSpacing.md),
                  Text(
                    signalReason,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: _chatMutedTextColor(context),
                          height: 1.5,
                        ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Custom painter for a simple mini line chart.
class _MiniLineChartPainter extends CustomPainter {
  _MiniLineChartPainter({
    required this.values,
    required this.lineColor,
    required this.fillColor,
  });

  final List<double> values;
  final Color lineColor;
  final Color fillColor;

  @override
  void paint(Canvas canvas, Size size) {
    if (values.length < 2) return;

    final minVal = values.reduce((a, b) => a < b ? a : b);
    final maxVal = values.reduce((a, b) => a > b ? a : b);
    final range = maxVal - minVal;
    if (range == 0) return;

    final step = size.width / (values.length - 1);
    final points = <Offset>[];

    for (var i = 0; i < values.length; i++) {
      final x = i * step;
      final y = size.height - ((values[i] - minVal) / range) * size.height;
      points.add(Offset(x, y));
    }

    // Draw fill.
    final fillPath = Path()
      ..moveTo(points.first.dx, size.height)
      ..lineTo(points.first.dx, points.first.dy);
    for (final p in points.skip(1)) {
      fillPath.lineTo(p.dx, p.dy);
    }
    fillPath
      ..lineTo(points.last.dx, size.height)
      ..close();

    canvas.drawPath(
      fillPath,
      Paint()..color = fillColor,
    );

    // Draw line.
    final linePath = Path()..moveTo(points.first.dx, points.first.dy);
    for (final p in points.skip(1)) {
      linePath.lineTo(p.dx, p.dy);
    }

    canvas.drawPath(
      linePath,
      Paint()
        ..color = lineColor
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2.0
        ..strokeCap = StrokeCap.round
        ..strokeJoin = StrokeJoin.round,
    );

    // Draw dot at last point.
    canvas.drawCircle(
      points.last,
      3.5,
      Paint()..color = lineColor,
    );
  }

  @override
  bool shouldRepaint(covariant _MiniLineChartPainter oldDelegate) {
    return values != oldDelegate.values || lineColor != oldDelegate.lineColor;
  }
}

/// Budget breakdown card — shows spending categories with progress bars
/// colored by status (under/on_track/over).
class _BudgetBreakdownCard extends StatelessWidget {
  const _BudgetBreakdownCard({required this.data});

  final Map<String, dynamic> data;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    final period = data['period'] as String? ?? '';
    final totalBudget = (data['totalBudget'] as num?)?.toDouble() ?? 0.0;
    final totalSpent = (data['totalSpent'] as num?)?.toDouble() ?? 0.0;
    final currency = data['currency'] as String? ?? '';
    final categories = (data['categories'] as List<dynamic>?)
            ?.map((e) => e as Map<String, dynamic>)
            .toList() ??
        const [];

    final totalPct = totalBudget > 0 ? (totalSpent / totalBudget * 100) : 0.0;
    final bool isOverall = totalSpent > totalBudget;

    return Container(
      margin: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xs),
      decoration: BoxDecoration(
        color: _chatPlanSurfaceColor(context),
        gradient: _chatPlanGradient(context),
        borderRadius: BorderRadius.circular(20),
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
            padding: const EdgeInsets.all(PayaboSpacing.lg),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                // Header.
                Row(
                  children: <Widget>[
                    Icon(
                      Icons.pie_chart_outline_rounded,
                      size: 18,
                      color: c.primary.withValues(alpha: 0.7),
                    ),
                    const SizedBox(width: PayaboSpacing.sm),
                    Text(
                      'BUDGET',
                      style: Theme.of(context).textTheme.labelLarge?.copyWith(
                            color: _chatMutedTextColor(context),
                            fontWeight: FontWeight.w700,
                            letterSpacing: 2.8,
                          ),
                    ),
                    const Spacer(),
                    if (period.isNotEmpty)
                      Text(
                        period,
                        style: Theme.of(context).textTheme.labelSmall?.copyWith(
                              color: _chatMutedTextColor(context),
                            ),
                      ),
                  ],
                ),
                const SizedBox(height: PayaboSpacing.md),
                // Total summary.
                Row(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: <Widget>[
                    Text(
                      '$currency ${totalSpent.toStringAsFixed(0)}',
                      style:
                          Theme.of(context).textTheme.headlineSmall?.copyWith(
                                color: isOverall
                                    ? Colors.red.withValues(alpha: 0.9)
                                    : _chatBodyTextColor(context),
                                fontWeight: FontWeight.w800,
                              ),
                    ),
                    const SizedBox(width: PayaboSpacing.xs),
                    Padding(
                      padding: const EdgeInsets.only(bottom: 2),
                      child: Text(
                        'of $currency ${totalBudget.toStringAsFixed(0)}',
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              color: _chatMutedTextColor(context),
                            ),
                      ),
                    ),
                    const Spacer(),
                    Text(
                      '${totalPct.toStringAsFixed(0)}%',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            color: isOverall
                                ? Colors.red.withValues(alpha: 0.9)
                                : c.primary,
                            fontWeight: FontWeight.w700,
                          ),
                    ),
                  ],
                ),
                const SizedBox(height: PayaboSpacing.lg),
                // Category rows.
                ...categories.map((cat) {
                  final name = cat['name'] as String? ?? '';
                  final budgeted = (cat['budgeted'] as num?)?.toDouble() ?? 0.0;
                  final spent = (cat['spent'] as num?)?.toDouble() ?? 0.0;
                  final status = cat['status'] as String? ?? 'on_track';
                  final pct =
                      budgeted > 0 ? (spent / budgeted).clamp(0.0, 1.5) : 0.0;

                  final Color statusColor;
                  switch (status) {
                    case 'under':
                      statusColor = Colors.green;
                    case 'over':
                      statusColor = Colors.red;
                    default:
                      statusColor = c.primary;
                  }

                  return Padding(
                    padding: const EdgeInsets.only(bottom: PayaboSpacing.md),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Row(
                          children: <Widget>[
                            Expanded(
                              child: Text(
                                name,
                                style: Theme.of(context)
                                    .textTheme
                                    .bodyMedium
                                    ?.copyWith(
                                      color: _chatBodyTextColor(context),
                                      fontWeight: FontWeight.w600,
                                    ),
                              ),
                            ),
                            Text(
                              '$currency ${spent.toStringAsFixed(0)} / ${budgeted.toStringAsFixed(0)}',
                              style: Theme.of(context)
                                  .textTheme
                                  .labelSmall
                                  ?.copyWith(
                                    color: _chatMutedTextColor(context),
                                  ),
                            ),
                          ],
                        ),
                        const SizedBox(height: PayaboSpacing.xs),
                        // Progress bar.
                        ClipRRect(
                          borderRadius: BorderRadius.circular(4),
                          child: SizedBox(
                            height: 6,
                            child: Stack(
                              children: <Widget>[
                                // Track.
                                Container(
                                  decoration: BoxDecoration(
                                    color: Colors.white.withValues(alpha: 0.06),
                                  ),
                                ),
                                // Fill.
                                FractionallySizedBox(
                                  widthFactor: pct.clamp(0.0, 1.0),
                                  child: Container(
                                    decoration: BoxDecoration(
                                      color: statusColor.withValues(alpha: 0.7),
                                      borderRadius: BorderRadius.circular(4),
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                      ],
                    ),
                  );
                }),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Autopilot proposal card — a display-only card showing a structured
/// proposal from an agent. Unlike [_ApprovalCard], this is informational
/// (no approve/reject buttons).
class _AutopilotProposalCard extends StatelessWidget {
  const _AutopilotProposalCard({required this.data});

  final Map<String, dynamic> data;

  @override
  Widget build(BuildContext context) {
    final agent = data['agent'] as String? ?? 'Agent';
    final action = data['action'] as String? ?? '';
    final description = data['description'] as String? ?? '';
    final details = (data['details'] as List<dynamic>?)
            ?.map((e) => e as Map<String, dynamic>)
            .toList() ??
        const [];
    final severity = data['severity'] as String? ?? 'medium';

    final Color severityColor;
    final IconData severityIcon;
    switch (severity) {
      case 'high':
        severityColor = Colors.red;
        severityIcon = Icons.priority_high_rounded;
      case 'low':
        severityColor = Colors.green;
        severityIcon = Icons.lightbulb_outline_rounded;
      default:
        severityColor = Colors.orange;
        severityIcon = Icons.auto_awesome_rounded;
    }

    return Container(
      margin: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xs),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            severityColor.withValues(alpha: 0.06),
            severityColor.withValues(alpha: 0.02),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(
          color: severityColor.withValues(alpha: 0.16),
        ),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x1E000000),
            blurRadius: 16,
            offset: Offset(0, 10),
          ),
        ],
      ),
      child: Padding(
        padding: const EdgeInsets.all(PayaboSpacing.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            // Agent badge + severity icon.
            Row(
              children: <Widget>[
                Container(
                  padding: const EdgeInsets.all(6),
                  decoration: BoxDecoration(
                    color: severityColor.withValues(alpha: 0.1),
                    shape: BoxShape.circle,
                  ),
                  child: Icon(
                    severityIcon,
                    size: 16,
                    color: severityColor.withValues(alpha: 0.8),
                  ),
                ),
                const SizedBox(width: PayaboSpacing.sm),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        agent.toUpperCase(),
                        style: Theme.of(context).textTheme.labelSmall?.copyWith(
                              color: severityColor.withValues(alpha: 0.7),
                              fontWeight: FontWeight.w700,
                              letterSpacing: 1.4,
                            ),
                      ),
                      if (action.isNotEmpty)
                        Text(
                          action,
                          style:
                              Theme.of(context).textTheme.titleSmall?.copyWith(
                                    color: _chatBodyTextColor(context),
                                    fontWeight: FontWeight.w700,
                                  ),
                        ),
                    ],
                  ),
                ),
              ],
            ),
            // Description.
            if (description.isNotEmpty) ...<Widget>[
              const SizedBox(height: PayaboSpacing.md),
              Text(
                description,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: _chatBodyTextColor(context).withValues(alpha: 0.8),
                      height: 1.5,
                    ),
              ),
            ],
            // Detail rows.
            if (details.isNotEmpty) ...<Widget>[
              const SizedBox(height: PayaboSpacing.md),
              Container(
                padding: const EdgeInsets.all(PayaboSpacing.md),
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.03),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(
                    color: Colors.white.withValues(alpha: 0.06),
                  ),
                ),
                child: Column(
                  children: details.asMap().entries.map((entry) {
                    final label = entry.value['label'] as String? ?? '';
                    final value = entry.value['value'] as String? ?? '';
                    final isLast = entry.key == details.length - 1;

                    return Column(
                      children: <Widget>[
                        Row(
                          children: <Widget>[
                            Text(
                              label,
                              style: Theme.of(context)
                                  .textTheme
                                  .bodySmall
                                  ?.copyWith(
                                    color: _chatMutedTextColor(context),
                                  ),
                            ),
                            const Spacer(),
                            Text(
                              value,
                              style: Theme.of(context)
                                  .textTheme
                                  .bodySmall
                                  ?.copyWith(
                                    color: _chatBodyTextColor(context),
                                    fontWeight: FontWeight.w600,
                                  ),
                            ),
                          ],
                        ),
                        if (!isLast)
                          Padding(
                            padding: const EdgeInsets.symmetric(
                              vertical: PayaboSpacing.xs,
                            ),
                            child: Container(
                              height: 1,
                              color: Colors.white.withValues(alpha: 0.04),
                            ),
                          ),
                      ],
                    );
                  }).toList(),
                ),
              ),
            ],
          ],
        ),
      ),
    );
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
