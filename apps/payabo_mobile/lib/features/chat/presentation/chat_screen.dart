import 'dart:async';
import 'dart:math';

import 'package:flutter/foundation.dart';
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
import '../../voice/realtime_voice_controller.dart';
import '../../voice/realtime_voice_stage.dart';
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
  return const Color(0xFFF37920);
}

LinearGradient _chatUserBubbleGradient(BuildContext context) {
  return const LinearGradient(
    colors: <Color>[
      Color(0xFFF37920),
      Color(0xFFD55F0B),
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

class ChatScreen extends ConsumerStatefulWidget {
  const ChatScreen({super.key});

  @override
  ConsumerState<ChatScreen> createState() => _ChatScreenState();
}

class _ChatScreenState extends ConsumerState<ChatScreen>
    with SingleTickerProviderStateMixin {
  static const double _historyOverlayWidthFactor = 0.9;
  static const Duration _streamingAutoScrollMinInterval =
      Duration(milliseconds: 48);
  static const String _voiceLogPrefix = '[ChatVoice]';

  /// Maximum time any single voice tap async operation
  /// (`start` / `stop`) is allowed to occupy [_voiceBusy]. If a platform
  /// call hangs forever without this watchdog the finally block never runs,
  /// [_voiceBusy] stays `true`, and every subsequent tap is silently
  /// ignored — forcing the user to restart the app. The watchdog forcibly
  /// clears the flag and surfaces a toast so the failure is recoverable.
  static const Duration _voiceBusyMaxDuration = Duration(seconds: 8);

  final TextEditingController _controller = TextEditingController();
  final ScrollController _scrollController = ScrollController();
  late final AnimationController _historyOverlayController;
  DateTime? _lastStreamingAutoScrollAt;
  bool _showVoiceStage = false;

  /// Guards against re-entrant voice taps while an async voice operation
  /// (start / stop) is in flight. Without this, rapid taps cause the state
  /// machine to diverge from the realtime controller's state.
  bool _voiceBusy = false;

  /// Watchdog paired with [_voiceBusy]. If the async op takes longer than
  /// [_voiceBusyMaxDuration], we force-clear the flag so the user can
  /// recover with another tap. See [_voiceBusyMaxDuration] doc.
  Timer? _voiceBusyWatchdog;

  /// The conversation starter question currently displayed on the empty chat
  /// stage. Stored here so it can be prepended as context when the user sends
  /// their first message in response to it.
  late final String _activeStarterQuestion;

  @override
  void initState() {
    super.initState();
    _activeStarterQuestion =
        _conversationStarters[Random().nextInt(_conversationStarters.length)];
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
    _voiceBusyWatchdog?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final bool isVoiceActive = _showVoiceStage;
    // When the realtime orb is on screen the composer is dead weight — the
    // orb is the entire interaction surface. Hide the whole composer rail
    // (text field + mic) so the orb gets the full screen.
    final bool hideComposer = _showVoiceStage;
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
        _scrollStreamingToBottom();
      }

      if (next.messages.length > prev.messages.length && keepPinnedToBottom) {
        _lastStreamingAutoScrollAt = null;
        _scrollToBottom();
      }

      if (next.pendingApprovals.length > prev.pendingApprovals.length &&
          keepPinnedToBottom) {
        _scrollToBottom();
      }

      if (next.pendingOptionSelections.length >
              prev.pendingOptionSelections.length &&
          keepPinnedToBottom) {
        _scrollToBottom();
      }

      if (next.displayWidgets.length > prev.displayWidgets.length &&
          keepPinnedToBottom) {
        _scrollToBottom();
      }

      final PendingNavigation? nav = next.pendingNavigation;
      if (nav != null && nav != prev.pendingNavigation) {
        // Clear immediately so rebuilds don't re-trigger the navigation.
        ref.read(chatControllerProvider.notifier).clearPendingNavigation();
        _dispatchPendingNavigation(nav);
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
                    if (!isVoiceActive)
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
                            const SizedBox(width: PayaboSpacing.md),
                            const Expanded(child: _ChatHeaderSimiIdentity()),
                            const SizedBox(width: PayaboSpacing.md),
                            _ChatHeaderNewChatButton(
                              onTap: _startNewConversation,
                            ),
                          ],
                        ),
                      ),
                    Expanded(
                      child: _ChatStage(
                        controller: _scrollController,
                        displayName: _firstName(displayName),
                        starterQuestion: _activeStarterQuestion,
                        showVoiceStage: _showVoiceStage,
                        onVoiceOrbTap: _handleVoiceTap,
                        onSuggestionTap: _submitPrompt,
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
                        onSelect: (String toolCallId, List<String> selected) {
                          ref
                              .read(chatControllerProvider.notifier)
                              .selectOption(toolCallId, selected);
                          _scrollToBottom(force: true);
                        },
                      ),
                    ),
                    const _ChatErrorSlot(),
                    if (!hideComposer)
                      Padding(
                        padding: const EdgeInsets.fromLTRB(
                          PayaboSpacing.md,
                          0,
                          PayaboSpacing.md,
                          PayaboSpacing.md,
                        ),
                        child: _ChatComposer(
                          controller: _controller,
                          onVoiceTap: () => unawaited(_handleVoiceTap()),
                          onSubmitted: _submitPrompt,
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
          bottomNavigationBar: isVoiceActive
              ? null
              : Theme(
                  data: buildPayaboDarkTheme(),
                  child: const PayaboPrimaryAppShell(
                    destination: PayaboPrimaryDestination.chat,
                    backgroundOverride: Color(0xFF0E0A08),
                    borderOverride: Color(0xFF1E1610),
                    shadowOverride: Color(0x40000000),
                    selectedOverride: Color(0xFFF37920),
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

  void _startNewConversation() {
    FocusScope.of(context).unfocus();
    _closeHistoryOverlay();
    unawaited(_dismissVoiceStage());

    ref.read(chatControllerProvider.notifier).newConversation();
    ref.invalidate(_chatConversationsProvider);

    _controller.clear();
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
    unawaited(_dismissVoiceStage());

    // If this is the very first message the user is replying to the
    // conversation starter question. Seed it as an assistant message so
    // the thread looks like a natural conversation (Simi asked → user
    // replied), and the backend has the full context.
    final ChatState chatState = ref.read(chatControllerProvider);
    if (!chatState.hasMessages && chatState.streamingText.isEmpty) {
      ref.read(chatControllerProvider.notifier).sendFirstMessage(
            starterQuestion: _activeStarterQuestion,
            userReply: prompt,
          );
    } else {
      ref.read(chatControllerProvider.notifier).sendMessage(prompt);
    }

    _scrollToBottom(force: true);
  }

  /// Realtime (Voxa WSS) voice path. The realtime controller owns turn
  /// detection (server VAD), barge-in, and the bot's audio playback — the
  /// chat screen only manages stage visibility and re-entrancy.
  Future<void> _handleVoiceTap() async {
    if (_voiceBusy) {
      _voiceLog('voice tap IGNORED — async operation in flight');
      return;
    }

    final RealtimeVoiceController notifier =
        ref.read(realtimeVoiceControllerProvider.notifier);
    final RealtimeVoicePhase phase =
        ref.read(realtimeVoiceControllerProvider).phase;
    _voiceLog('realtime voice tap while phase=$phase');

    switch (phase) {
      case RealtimeVoicePhase.idle:
      case RealtimeVoicePhase.error:
        if (mounted) {
          setState(() {
            _showVoiceStage = true;
          });
        }
        await _runBusyVoiceOp('realtimeStart', () async {
          await notifier.start();
        });
      case RealtimeVoicePhase.connecting:
      case RealtimeVoicePhase.live:
        await _runBusyVoiceOp('realtimeStop', () async {
          await notifier.stop();
        });
        if (mounted) {
          setState(() {
            _showVoiceStage = false;
          });
        }
    }
  }

  /// Runs [op] while [_voiceBusy] is held, with a watchdog timer that
  /// force-clears the flag after [_voiceBusyMaxDuration] in case [op]
  /// hangs on a stale platform channel.
  ///
  /// Without the watchdog, an awaited MethodChannel call that never
  /// completes would leave [_voiceBusy] stuck forever, silently swallowing
  /// every subsequent voice tap and forcing the user to restart the app.
  Future<void> _runBusyVoiceOp(
    String label,
    Future<void> Function() op,
  ) async {
    _voiceBusy = true;
    _voiceBusyWatchdog?.cancel();
    _voiceBusyWatchdog = Timer(_voiceBusyMaxDuration, () {
      if (!_voiceBusy) {
        return;
      }
      _voiceLog(
        'voice busy watchdog FIRED for $label — force-clearing _voiceBusy after ${_voiceBusyMaxDuration.inSeconds}s',
      );
      _voiceBusy = false;
      if (mounted) {
        _showVoiceSnackBar(
          'Voice took too long to respond. Tap Talk to try again.',
        );
      }
    });
    try {
      await op();
    } finally {
      _voiceBusyWatchdog?.cancel();
      _voiceBusyWatchdog = null;
      _voiceBusy = false;
    }
  }

  /// Stop any active realtime voice session and hide the stage. Called when
  /// the user pivots to a different surface (new conversation, typed send,
  /// history navigation) so the WSS session doesn't keep running in the
  /// background.
  Future<void> _dismissVoiceStage() async {
    final RealtimeVoiceController notifier =
        ref.read(realtimeVoiceControllerProvider.notifier);
    final RealtimeVoicePhase phase =
        ref.read(realtimeVoiceControllerProvider).phase;

    if (phase == RealtimeVoicePhase.connecting ||
        phase == RealtimeVoicePhase.live) {
      await notifier.stop();
    }

    if (!mounted) {
      return;
    }

    if (_showVoiceStage) {
      setState(() {
        _showVoiceStage = false;
      });
    }
  }

  void _showVoiceSnackBar(String message) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(message)));
  }

  void _voiceLog(String message) {
    if (!kDebugMode) {
      return;
    }

    debugPrint('$_voiceLogPrefix $message');
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

  void _dispatchPendingNavigation(PendingNavigation nav) {
    if (!mounted) return;
    // Defer until after the current frame so ref.read side effects from
    // the listener complete cleanly before we push a new route.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      try {
        context.goNamed(
          nav.screenName,
          pathParameters: nav.pathParameters,
          queryParameters: nav.queryParameters,
        );
      } catch (e, st) {
        debugPrint('navigate_to_screen failed for ${nav.screenName}: $e\n$st');
      }
    });
  }

  void _scrollStreamingToBottom() {
    final DateTime now = DateTime.now();
    final DateTime? lastAutoScrollAt = _lastStreamingAutoScrollAt;
    if (lastAutoScrollAt != null &&
        now.difference(lastAutoScrollAt) < _streamingAutoScrollMinInterval) {
      return;
    }

    _lastStreamingAutoScrollAt = now;
    _scrollToBottom(animated: false);
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
    required this.starterQuestion,
    required this.showVoiceStage,
    required this.onVoiceOrbTap,
    required this.onSuggestionTap,
    required this.onApprove,
    required this.onReject,
    required this.onSelect,
  });

  final ScrollController controller;
  final String displayName;
  final String starterQuestion;
  final bool showVoiceStage;
  final Future<void> Function() onVoiceOrbTap;
  final void Function(String prompt) onSuggestionTap;
  final void Function(String toolCallId) onApprove;
  final void Function(String toolCallId, [String? reason]) onReject;
  final void Function(String toolCallId, List<String> selected) onSelect;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    late final Widget stageChild;

    if (showVoiceStage) {
      // The Voxa WSS realtime path: server VAD owns turn detection, the
      // bot's audio playback streams duplex, and the controller exposes
      // a slim 4-phase state machine the stage widget binds to directly.
      stageChild = KeyedSubtree(
        key: const ValueKey<String>('chat-realtime-voice-stage'),
        child: RealtimeVoiceStage(onOrbTap: onVoiceOrbTap),
      );
    } else {
      // Narrow the watch surface — watch only the fields that affect the
      // conversation's *structure* (what slots the list renders, which
      // buttons/cards are visible). The streaming text itself is watched
      // inside _StreamingMessageBlock so that per-token updates don't
      // rebuild the whole stage.
      final List<ChatMessage> messages = ref.watch(
        chatControllerProvider.select((ChatState s) => s.messages),
      );
      final ChatActivity activity = ref.watch(
        chatControllerProvider.select((ChatState s) => s.activity),
      );
      // Note: gating on `streamingText.isNotEmpty` matches the original
      // behaviour. `streamingMessageId` is NOT included here because
      // ChatState.copyWith has a latent null-coalescing bug that leaves
      // the ID set after _clearStreaming() — which would leave an empty
      // streaming bubble on screen after every run. See _clearStreaming
      // in chat_controller.dart.
      final bool hasStreamingSlot = ref.watch(
        chatControllerProvider.select(
          (ChatState s) => s.streamingText.isNotEmpty,
        ),
      );
      final List<ActiveToolCall> activeToolCalls = ref.watch(
        chatControllerProvider.select((ChatState s) => s.activeToolCalls),
      );
      final List<PendingApproval> pendingApprovals = ref.watch(
        chatControllerProvider.select((ChatState s) => s.pendingApprovals),
      );
      final List<PendingOptionSelection> pendingOptionSelections = ref.watch(
        chatControllerProvider
            .select((ChatState s) => s.pendingOptionSelections),
      );
      final List<DisplayWidget> displayWidgets = ref.watch(
        chatControllerProvider.select((ChatState s) => s.displayWidgets),
      );

      final bool showHero = messages.isEmpty &&
          !hasStreamingSlot &&
          activity == ChatActivity.idle;

      stageChild = showHero
          ? _EmptyChatStage(
              key: const ValueKey<String>('chat-empty'),
              displayName: displayName,
              starterQuestion: starterQuestion,
            )
          : _ConversationStage(
              key: const ValueKey<String>('chat-thread'),
              controller: controller,
              displayName: displayName,
              messages: messages,
              hasStreamingSlot: hasStreamingSlot,
              activity: activity,
              activeToolCalls: activeToolCalls,
              pendingApprovals: pendingApprovals,
              pendingOptionSelections: pendingOptionSelections,
              displayWidgets: displayWidgets,
              onSuggestionTap: onSuggestionTap,
              onApprove: onApprove,
              onReject: onReject,
              onSelect: onSelect,
            );
    }

    return AnimatedSwitcher(
      duration: const Duration(milliseconds: 320),
      switchInCurve: Curves.easeOutCubic,
      switchOutCurve: Curves.easeInCubic,
      child: stageChild,
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

const List<String> _conversationStarters = <String>[
  "What's one money goal you'd love to tick off this year?",
  'Anything about your finances keeping you up at night?',
  "Want me to take a quick look at how your spending's going?",
  "Got any bills coming up you'd like a reminder for?",
  'If you had an extra £100 right now, what would you do with it?',
  'Want to set up a budget together? It only takes a minute.',
  'Curious how much you spent eating out last month?',
  'Is there a subscription you keep meaning to cancel?',
  "What's the one thing you wish was easier about managing money?",
  'Want me to check if any of your bills have gone up recently?',
  'Saving for anything fun at the moment?',
  'Ever wonder where your money actually goes each month?',
  'Need help splitting a bill with someone?',
  'What would make tomorrow a great financial day for you?',
  "Want a quick snapshot of what's left until payday?",
  "Got a money question you've been too embarrassed to ask?",
  'Thinking about cutting back on anything this month?',
  'Want me to find your biggest spending category last month?',
  'If you could automate one money task, what would it be?',
  "Anything you'd like to understand better about your finances?",
];

class _EmptyChatStage extends StatelessWidget {
  const _EmptyChatStage({
    super.key,
    required this.displayName,
    required this.starterQuestion,
  });

  final String displayName;
  final String starterQuestion;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.xl,
        PayaboSpacing.x4,
        PayaboSpacing.xl,
        PayaboSpacing.xl,
      ),
      child: Align(
        alignment: Alignment.topLeft,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              'Hey, you',
              style: Theme.of(context).textTheme.displayLarge?.copyWith(
                        color: _chatBodyTextColor(context),
                        fontSize: 42,
                        fontWeight: FontWeight.w800,
                        height: 1,
                        letterSpacing: -1.0,
                      ) ??
                  TextStyle(
                    color: _chatBodyTextColor(context),
                    fontSize: 42,
                    fontWeight: FontWeight.w800,
                    height: 1,
                    letterSpacing: -1.0,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.lg),
            Text(
              starterQuestion,
              style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                        color: _chatMutedTextColor(context),
                      ) ??
                  TextStyle(
                    color: _chatMutedTextColor(context),
                  ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ConversationStage extends StatelessWidget {
  const _ConversationStage({
    super.key,
    required this.controller,
    required this.displayName,
    required this.messages,
    this.hasStreamingSlot = false,
    this.activity = ChatActivity.idle,
    this.activeToolCalls = const [],
    this.pendingApprovals = const [],
    this.pendingOptionSelections = const [],
    this.displayWidgets = const [],
    this.onSuggestionTap,
    this.onApprove,
    this.onReject,
    this.onSelect,
  });

  final ScrollController controller;
  final String displayName;
  final List<ChatMessage> messages;
  final bool hasStreamingSlot;
  final ChatActivity activity;
  final List<ActiveToolCall> activeToolCalls;
  final List<PendingApproval> pendingApprovals;
  final List<PendingOptionSelection> pendingOptionSelections;
  final List<DisplayWidget> displayWidgets;
  final void Function(String prompt)? onSuggestionTap;
  final void Function(String toolCallId)? onApprove;
  final void Function(String toolCallId, [String? reason])? onReject;
  final void Function(String toolCallId, List<String> selected)? onSelect;

  @override
  Widget build(BuildContext context) {
    final bool isStreaming = hasStreamingSlot;
    final bool isThinking = activity == ChatActivity.connecting ||
        (activity == ChatActivity.toolCall && !hasStreamingSlot);
    final int itemCount = 2 +
        messages.length +
        (isStreaming ? 1 : 0) +
        (isThinking ? 1 : 0) +
        displayWidgets.length +
        pendingApprovals.length +
        pendingOptionSelections.length;

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
            child: _ChatMessageBlock(
              message: message,
              onSuggestionTap: onSuggestionTap,
            ),
          );
        }
        contentIndex -= messages.length;

        if (isStreaming) {
          if (contentIndex == 0) {
            return Padding(
              padding: const EdgeInsets.only(bottom: PayaboSpacing.xl),
              child: _StreamingMessageBlock(
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
              onSuggestionTap: onSuggestionTap,
            ),
          );
        }
        contentIndex -= displayWidgets.length;

        if (contentIndex < pendingApprovals.length) {
          final PendingApproval approval = pendingApprovals[contentIndex];
          return Padding(
            padding: const EdgeInsets.only(bottom: PayaboSpacing.xl),
            child: _ApprovalCard(
              approval: approval,
              onApprove: () => onApprove?.call(approval.toolCallId),
              onReject: () => onReject?.call(approval.toolCallId),
            ),
          );
        }
        contentIndex -= pendingApprovals.length;

        final PendingOptionSelection selection =
            pendingOptionSelections[contentIndex];
        return Padding(
          padding: const EdgeInsets.only(bottom: PayaboSpacing.xl),
          child: _OptionSelectorCard(
            selection: selection,
            onSelect: (List<String> selected) =>
                onSelect?.call(selection.toolCallId, selected),
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
      child: Text(
        'Hey, you',
        style: Theme.of(context).textTheme.displayLarge?.copyWith(
              color: _chatBodyTextColor(context),
              fontSize: 42,
              fontWeight: FontWeight.w800,
              height: 1.0,
              letterSpacing: -1.0,
            ),
      ),
    );
  }
}

class _ChatMessageBlock extends StatelessWidget {
  const _ChatMessageBlock({
    required this.message,
    this.onSuggestionTap,
  });

  final ChatMessage message;
  final void Function(String prompt)? onSuggestionTap;

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
                boxShadow: const <BoxShadow>[
                  BoxShadow(
                    color: Color(0x33000000),
                    blurRadius: 14,
                    offset: Offset(0, 8),
                  ),
                ],
              ),
              child: Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: PayaboSpacing.lg,
                  vertical: PayaboSpacing.md,
                ),
                child: Text(
                  message.lines.first,
                  style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                        color: Colors.white,
                        height: 1.35,
                      ),
                ),
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
                onSuggestionTap: onSuggestionTap,
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

class _ChatHeaderSimiIdentity extends StatelessWidget {
  const _ChatHeaderSimiIdentity();

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          width: 40,
          height: 40,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            border: Border.all(
              color: const Color(0xFFF37920).withValues(alpha: 0.5),
              width: 1.5,
            ),
            image: const DecorationImage(
              image: AssetImage('assets/images/simi.png'),
              fit: BoxFit.cover,
              alignment: Alignment(0, -0.6),
            ),
          ),
        ),
        const SizedBox(width: PayaboSpacing.sm),
        Flexible(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                'Simi',
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: _chatBodyTextColor(context),
                      fontWeight: FontWeight.w700,
                      height: 1.1,
                    ),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: 2),
              Text(
                'AI companion \u00B7 always listening',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: _chatMutedTextColor(context),
                      height: 1.2,
                    ),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
      ],
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

class _ChatHeaderNewChatButton extends StatelessWidget {
  const _ChatHeaderNewChatButton({required this.onTap});

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
            Icons.edit_outlined,
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
    required this.onVoiceTap,
    required this.onSubmitted,
  });

  final TextEditingController controller;
  final VoidCallback onVoiceTap;
  final ValueChanged<String> onSubmitted;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final bool isProcessing = ref.watch(
      chatControllerProvider.select((ChatState state) => state.isProcessing),
    );

    return ValueListenableBuilder<TextEditingValue>(
      valueListenable: controller,
      builder: (
        BuildContext context,
        TextEditingValue value,
        Widget? child,
      ) {
        final bool canSend = value.text.trim().isNotEmpty && !isProcessing;

        return ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 720),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: <Widget>[
              Expanded(
                child: TextField(
                  controller: controller,
                  minLines: 1,
                  maxLines: 4,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: _chatBodyTextColor(context),
                        height: 1.35,
                        fontWeight: FontWeight.w400,
                      ),
                  cursorColor: context.colors.primary,
                  textInputAction: TextInputAction.send,
                  onSubmitted: onSubmitted,
                  decoration: InputDecoration(
                    hintText: 'Ask Simi anything\u2026',
                    hintStyle: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: _chatMutedTextColor(context),
                          fontWeight: FontWeight.w400,
                        ),
                    filled: true,
                    fillColor: Colors.white.withValues(alpha: 0.08),
                    isDense: true,
                    contentPadding: const EdgeInsets.symmetric(
                      horizontal: 16,
                      vertical: 12,
                    ),
                    enabledBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(50),
                      borderSide: BorderSide(
                        color: Colors.white.withValues(alpha: 0.1),
                      ),
                    ),
                    focusedBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(50),
                      borderSide: BorderSide(
                        color: Colors.white.withValues(alpha: 0.25),
                      ),
                    ),
                  ),
                ),
              ),
              const SizedBox(width: PayaboSpacing.sm),
              _ChatComposerActionButton(
                icon: canSend ? Icons.send_rounded : Icons.mic_none_rounded,
                semanticLabel: canSend ? 'Send message' : 'Voice chat',
                isEnabled: canSend || !isProcessing,
                onTap: canSend ? () => onSubmitted(value.text) : onVoiceTap,
              ),
            ],
          ),
        );
      },
    );
  }
}

class _ChatComposerActionButton extends StatelessWidget {
  const _ChatComposerActionButton({
    required this.icon,
    required this.semanticLabel,
    required this.isEnabled,
    required this.onTap,
  });

  final IconData icon;
  final String semanticLabel;
  final bool isEnabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: semanticLabel,
      child: Material(
        color: Colors.transparent,
        shape: const CircleBorder(),
        child: InkWell(
          onTap: isEnabled ? onTap : null,
          customBorder: const CircleBorder(),
          child: Ink(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color:
                  isEnabled ? const Color(0xFFF37920) : const Color(0xFF624221),
            ),
            child: Icon(
              icon,
              color: Colors.white.withValues(alpha: isEnabled ? 1.0 : 0.5),
              size: 20,
            ),
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
/// Renders the in-progress assistant response. Self-watches
/// [ChatState.streamingText] via a narrow selector so per-token updates
/// rebuild only this subtree — not the conversation stage or list.
///
/// The blinking cursor is rendered as a sibling widget (not a WidgetSpan
/// inside Text.rich) to avoid forcing an inline layout pass on every
/// text update.
class _StreamingMessageBlock extends ConsumerWidget {
  const _StreamingMessageBlock({
    this.activeToolCalls = const [],
  });

  final List<ActiveToolCall> activeToolCalls;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.colors;
    final String text = ref.watch(
      chatControllerProvider.select((ChatState s) => s.streamingText),
    );

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
              // Streaming text — the blinking cursor is rendered below as a
              // sibling to avoid re-measuring the inline WidgetSpan on every
              // token delta.
              Text(
                text,
                style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                      color: _chatBodyTextColor(context),
                      height: 1.58,
                    ),
              ),
              Padding(
                padding: const EdgeInsets.only(top: 2),
                child: _BlinkingCursor(color: c.primary),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// Thinking indicator shown while waiting for the agent to start responding.
///
/// Shows a contextual phrase derived from the active tool call name, or cycles
/// through generic "working" phrases while connecting. Phrases animate in/out
/// with a fade so the user sees natural motion without raw tool-call details.
class _ThinkingIndicator extends ConsumerStatefulWidget {
  const _ThinkingIndicator();

  @override
  ConsumerState<_ThinkingIndicator> createState() => _ThinkingIndicatorState();
}

class _ThinkingIndicatorState extends ConsumerState<_ThinkingIndicator> {
  // Shown in rotation when no specific tool phrase applies.
  static const List<String> _genericPhrases = <String>[
    'Thinking through this...',
    'Looking at your finances...',
    'Working on that for you...',
    'Putting it together...',
  ];

  int _phraseIndex = 0;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _timer = Timer.periodic(const Duration(milliseconds: 2200), (_) {
      if (mounted) {
        setState(() {
          _phraseIndex = (_phraseIndex + 1) % _genericPhrases.length;
        });
      }
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  /// Returns the first incomplete (still-executing) tool call, or null.
  ActiveToolCall? _activeToolCall(List<ActiveToolCall> calls) {
    for (final tc in calls.reversed) {
      if (!tc.isComplete) return tc;
    }
    return null;
  }

  /// Maps a raw tool name to a user-friendly, sentence-style thinking hint.
  String _toolPhrase(String toolName) {
    final String lower = toolName.toLowerCase();
    if (lower.contains('account')) return 'Checking your accounts...';
    if (lower.contains('transaction')) return 'Looking up your transactions...';
    if (lower.contains('spending')) return 'Reviewing your spending...';
    if (lower.contains('budget')) return 'Looking at your budget...';
    if (lower.contains('order')) return 'Checking your payment history...';
    if (lower.contains('fx') ||
        lower.contains('exchange') ||
        lower.contains('rate')) return 'Fetching exchange rates...';
    if (lower.contains('navigate')) return 'Getting that ready...';
    if (lower.contains('goal')) return 'Reviewing your goals...';
    if (lower.contains('bill')) return 'Checking your bills...';
    if (lower.contains('categor')) return 'Categorising transactions...';
    if (lower.contains('balance')) return 'Checking your balance...';
    if (lower.contains('payment')) return 'Looking up payment details...';
    return _genericPhrases[_phraseIndex];
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final List<ActiveToolCall> activeToolCalls = ref.watch(
      chatControllerProvider.select((ChatState s) => s.activeToolCalls),
    );

    final ActiveToolCall? activeTc = _activeToolCall(activeToolCalls);
    final String phrase = activeTc != null
        ? _toolPhrase(activeTc.toolName)
        : _genericPhrases[_phraseIndex];

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xs),
      child: Row(
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
          Expanded(
            child: AnimatedSwitcher(
              duration: const Duration(milliseconds: 350),
              transitionBuilder: (Widget child, Animation<double> animation) {
                return FadeTransition(
                  opacity: animation,
                  child: SlideTransition(
                    position: Tween<Offset>(
                      begin: const Offset(0, 0.25),
                      end: Offset.zero,
                    ).animate(CurvedAnimation(
                      parent: animation,
                      curve: Curves.easeOut,
                    )),
                    child: child,
                  ),
                );
              },
              child: Text(
                phrase,
                key: ValueKey<String>(phrase),
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: _chatMutedTextColor(context),
                      fontStyle: FontStyle.italic,
                    ),
              ),
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

/// A card presenting a set of options for the user to choose from.
///
/// For single-select: tapping an option resolves immediately.
/// For multi-select: checkboxes with a "Confirm" button.
class _OptionSelectorCard extends StatefulWidget {
  const _OptionSelectorCard({
    required this.selection,
    required this.onSelect,
  });

  final PendingOptionSelection selection;
  final void Function(List<String> selected) onSelect;

  @override
  State<_OptionSelectorCard> createState() => _OptionSelectorCardState();
}

class _OptionSelectorCardState extends State<_OptionSelectorCard> {
  final Set<String> _selected = {};

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    const accentColor = Colors.blue;

    return Container(
      margin: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xs),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            accentColor.withValues(alpha: 0.08),
            accentColor.withValues(alpha: 0.03),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: accentColor.withValues(alpha: 0.2),
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
                    color: accentColor.withValues(alpha: 0.12),
                    shape: BoxShape.circle,
                  ),
                  child: Icon(
                    Icons.touch_app_rounded,
                    size: 18,
                    color: accentColor.withValues(alpha: 0.9),
                  ),
                ),
                const SizedBox(width: PayaboSpacing.sm),
                Expanded(
                  child: Text(
                    widget.selection.question,
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(
                          color: _chatBodyTextColor(context),
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                ),
              ],
            ),
          ),
          // Divider
          Container(
            height: 1,
            color: accentColor.withValues(alpha: 0.1),
          ),
          // Options
          Padding(
            padding: const EdgeInsets.all(PayaboSpacing.sm),
            child: Column(
              children: <Widget>[
                for (final option in widget.selection.options)
                  _buildOption(context, option, c),
                if (widget.selection.multiSelect && _selected.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: PayaboSpacing.sm),
                    child: SizedBox(
                      width: double.infinity,
                      child: TextButton(
                        onPressed: () => widget.onSelect(_selected.toList()),
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
                        child: Text(
                          'Confirm (${_selected.length})',
                        ),
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

  Widget _buildOption(
    BuildContext context,
    OptionItem option,
    dynamic c,
  ) {
    final bool isSelected = _selected.contains(option.label);

    return Padding(
      padding: const EdgeInsets.only(bottom: PayaboSpacing.xs),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          borderRadius: BorderRadius.circular(10),
          onTap: () {
            if (widget.selection.multiSelect) {
              setState(() {
                if (isSelected) {
                  _selected.remove(option.label);
                } else {
                  _selected.add(option.label);
                }
              });
            } else {
              // Single-select: resolve immediately.
              widget.onSelect([option.label]);
            }
          },
          child: Container(
            padding: const EdgeInsets.symmetric(
              horizontal: PayaboSpacing.md,
              vertical: PayaboSpacing.sm,
            ),
            decoration: BoxDecoration(
              color: isSelected
                  ? Colors.blue.withValues(alpha: 0.12)
                  : Colors.white.withValues(alpha: 0.04),
              borderRadius: BorderRadius.circular(10),
              border: Border.all(
                color: isSelected
                    ? Colors.blue.withValues(alpha: 0.4)
                    : Colors.white.withValues(alpha: 0.08),
              ),
            ),
            child: Row(
              children: <Widget>[
                if (widget.selection.multiSelect)
                  Padding(
                    padding: const EdgeInsets.only(right: PayaboSpacing.sm),
                    child: Icon(
                      isSelected
                          ? Icons.check_box_rounded
                          : Icons.check_box_outline_blank_rounded,
                      size: 20,
                      color: isSelected
                          ? Colors.blue
                          : _chatMutedTextColor(context),
                    ),
                  ),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        option.label,
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              color: _chatBodyTextColor(context),
                              fontWeight: FontWeight.w600,
                            ),
                      ),
                      if (option.description != null &&
                          option.description!.isNotEmpty)
                        Text(
                          option.description!,
                          style:
                              Theme.of(context).textTheme.bodySmall?.copyWith(
                                    color: _chatMutedTextColor(context),
                                  ),
                        ),
                    ],
                  ),
                ),
                if (!widget.selection.multiSelect)
                  Icon(
                    Icons.chevron_right_rounded,
                    size: 20,
                    color: _chatMutedTextColor(context),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _FollowUpSuggestionsCard extends StatelessWidget {
  const _FollowUpSuggestionsCard({
    required this.data,
    this.onSuggestionTap,
  });

  final Map<String, dynamic> data;
  final void Function(String prompt)? onSuggestionTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final prompt = data['prompt'] as String? ?? 'Pick a next step';
    final suggestions = (data['suggestions'] as List<dynamic>? ?? const [])
        .whereType<Map<Object?, Object?>>()
        .map((item) => Map<String, dynamic>.from(item))
        .where((item) =>
            (item['label']?.toString().trim().isNotEmpty ?? false) &&
            (item['prompt']?.toString().trim().isNotEmpty ?? false))
        .toList();

    if (suggestions.isEmpty) {
      return const SizedBox.shrink();
    }

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
      child: Padding(
        padding: const EdgeInsets.all(PayaboSpacing.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(
              prompt,
              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: _chatBodyTextColor(context),
                    fontWeight: FontWeight.w700,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.md),
            Wrap(
              spacing: PayaboSpacing.sm,
              runSpacing: PayaboSpacing.sm,
              children: suggestions.map((item) {
                final label = item['label']!.toString();
                final suggestionPrompt = item['prompt']!.toString();
                return ActionChip(
                  label: Text(label),
                  onPressed: onSuggestionTap == null
                      ? null
                      : () => onSuggestionTap!(suggestionPrompt),
                  labelStyle: Theme.of(context).textTheme.labelLarge?.copyWith(
                        color: c.primary,
                        fontWeight: FontWeight.w700,
                      ),
                  side: BorderSide(color: c.primary.withValues(alpha: 0.18)),
                  backgroundColor: c.primary.withValues(alpha: 0.08),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(999),
                  ),
                );
              }).toList(),
            ),
          ],
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Display widget cards
// ─────────────────────────────────────────────────────────

/// Routes a [DisplayWidget] to the correct card widget based on its type.
class _DisplayWidgetDispatcher extends StatelessWidget {
  const _DisplayWidgetDispatcher({
    required this.widget,
    this.onSuggestionTap,
  });

  final DisplayWidget widget;
  final void Function(String prompt)? onSuggestionTap;

  @override
  Widget build(BuildContext context) {
    switch (widget.widgetType) {
      case DisplayWidgetType.fxRateChart:
        return _FxRateChartCard(data: widget.data);
      case DisplayWidgetType.budgetBreakdown:
        return _BudgetBreakdownCard(data: widget.data);
      case DisplayWidgetType.spendingPieChart:
        return _SpendingPieChartCard(data: widget.data);
      case DisplayWidgetType.autopilotProposal:
        return _AutopilotProposalCard(data: widget.data);
      case DisplayWidgetType.followUpSuggestions:
        return _FollowUpSuggestionsCard(
          data: widget.data,
          onSuggestionTap: onSuggestionTap,
        );
      case DisplayWidgetType.optionSelector:
        // Option selector is rendered as a blocking card via
        // pendingOptionSelections, not as a display widget.
        return const SizedBox.shrink();
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

/// Spending pie chart card — renders a donut chart showing the proportional
/// split of spending across categories, with a legend listing each category's
/// amount and percentage.
class _SpendingPieChartCard extends StatelessWidget {
  const _SpendingPieChartCard({required this.data});

  final Map<String, dynamic> data;

  static const List<Color> _sliceColors = [
    Color(0xFF3B82F6), // blue
    Color(0xFF10B981), // green
    Color(0xFFF59E0B), // amber
    Color(0xFFEF4444), // red
    Color(0xFF8B5CF6), // violet
    Color(0xFFEC4899), // pink
    Color(0xFF06B6D4), // cyan
    Color(0xFFF97316), // orange
    Color(0xFF14B8A6), // teal
    Color(0xFF6366F1), // indigo
  ];

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    final title = data['title'] as String? ?? '';
    final currency = data['currency'] as String? ?? '';
    final totalSpent = (data['totalSpent'] as num?)?.toDouble() ?? 0.0;
    final rawCategories = (data['categories'] as List<dynamic>?)
            ?.map((e) => e as Map<String, dynamic>)
            .toList() ??
        const [];

    // Sort by amount descending and compute percentages.
    final categories = rawCategories.toList()
      ..sort((a, b) {
        final aAmt = (a['amount'] as num?)?.toDouble() ?? 0.0;
        final bAmt = (b['amount'] as num?)?.toDouble() ?? 0.0;
        return bAmt.compareTo(aAmt);
      });

    final slices = categories.map((cat) {
      final amount = (cat['amount'] as num?)?.toDouble() ?? 0.0;
      final pct = totalSpent > 0 ? amount / totalSpent * 100.0 : 0.0;
      return _PieSlice(
        name: cat['name'] as String? ?? '',
        amount: amount,
        percentage: pct,
      );
    }).toList();

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
                      Icons.donut_large_rounded,
                      size: 18,
                      color: c.primary.withValues(alpha: 0.7),
                    ),
                    const SizedBox(width: PayaboSpacing.sm),
                    Expanded(
                      child: Text(
                        title.isNotEmpty ? title : 'SPENDING',
                        style: Theme.of(context).textTheme.labelLarge?.copyWith(
                              color: _chatMutedTextColor(context),
                              fontWeight: FontWeight.w700,
                              letterSpacing: title.isNotEmpty ? 0.0 : 2.8,
                            ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: PayaboSpacing.lg),
                // Donut chart + total centre label.
                Center(
                  child: SizedBox(
                    width: 160,
                    height: 160,
                    child: Stack(
                      alignment: Alignment.center,
                      children: <Widget>[
                        CustomPaint(
                          size: const Size(160, 160),
                          painter: _DonutChartPainter(
                            slices: slices,
                            colors: _sliceColors,
                          ),
                        ),
                        Column(
                          mainAxisSize: MainAxisSize.min,
                          children: <Widget>[
                            Text(
                              'Total',
                              style: Theme.of(context)
                                  .textTheme
                                  .labelSmall
                                  ?.copyWith(
                                    color: _chatMutedTextColor(context),
                                  ),
                            ),
                            Text(
                              '$currency${totalSpent.toStringAsFixed(totalSpent == totalSpent.roundToDouble() ? 0 : 2)}',
                              style: Theme.of(context)
                                  .textTheme
                                  .titleMedium
                                  ?.copyWith(
                                    color: _chatBodyTextColor(context),
                                    fontWeight: FontWeight.w800,
                                  ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: PayaboSpacing.lg),
                // Legend rows.
                ...slices.asMap().entries.map((entry) {
                  final i = entry.key;
                  final slice = entry.value;
                  final color = _sliceColors[i % _sliceColors.length];

                  return Padding(
                    padding: const EdgeInsets.only(bottom: PayaboSpacing.sm),
                    child: Row(
                      children: <Widget>[
                        Container(
                          width: 10,
                          height: 10,
                          decoration: BoxDecoration(
                            color: color,
                            borderRadius: BorderRadius.circular(3),
                          ),
                        ),
                        const SizedBox(width: PayaboSpacing.sm),
                        Expanded(
                          child: Text(
                            slice.name,
                            style: Theme.of(context)
                                .textTheme
                                .bodyMedium
                                ?.copyWith(
                                  color: _chatBodyTextColor(context),
                                  fontWeight: FontWeight.w500,
                                ),
                          ),
                        ),
                        Text(
                          '$currency${slice.amount.toStringAsFixed(slice.amount == slice.amount.roundToDouble() ? 0 : 2)}',
                          style:
                              Theme.of(context).textTheme.bodyMedium?.copyWith(
                                    color: _chatBodyTextColor(context),
                                    fontWeight: FontWeight.w600,
                                  ),
                        ),
                        const SizedBox(width: PayaboSpacing.sm),
                        SizedBox(
                          width: 42,
                          child: Text(
                            '${slice.percentage.toStringAsFixed(0)}%',
                            textAlign: TextAlign.end,
                            style: Theme.of(context)
                                .textTheme
                                .labelSmall
                                ?.copyWith(
                                  color: _chatMutedTextColor(context),
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

/// Simple data holder for a donut slice.
class _PieSlice {
  const _PieSlice({
    required this.name,
    required this.amount,
    required this.percentage,
  });

  final String name;
  final double amount;
  final double percentage;
}

/// Custom painter that draws a donut chart from [_PieSlice] data.
class _DonutChartPainter extends CustomPainter {
  _DonutChartPainter({
    required this.slices,
    required this.colors,
  });

  final List<_PieSlice> slices;
  final List<Color> colors;

  static const double _strokeWidth = 28.0;

  @override
  void paint(Canvas canvas, Size size) {
    if (slices.isEmpty) return;

    final center = Offset(size.width / 2, size.height / 2);
    final radius = (size.shortestSide - _strokeWidth) / 2;
    final rect = Rect.fromCircle(center: center, radius: radius);

    const startAngle = -1.5707963; // -π/2 (12 o'clock)
    var currentAngle = startAngle;

    // Small gap between slices (in radians).
    const gap = 0.04;
    final totalGap = slices.length > 1 ? gap * slices.length : 0.0;
    final availableSweep = 2 * 3.141592653589793 - totalGap;

    for (var i = 0; i < slices.length; i++) {
      final fraction = slices[i].percentage / 100.0;
      final sweep = fraction * availableSweep;

      final paint = Paint()
        ..color = colors[i % colors.length]
        ..style = PaintingStyle.stroke
        ..strokeWidth = _strokeWidth
        ..strokeCap = StrokeCap.butt;

      canvas.drawArc(rect, currentAngle, sweep, false, paint);

      currentAngle += sweep + (slices.length > 1 ? gap : 0.0);
    }
  }

  @override
  bool shouldRepaint(covariant _DonutChartPainter oldDelegate) {
    return slices != oldDelegate.slices;
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
