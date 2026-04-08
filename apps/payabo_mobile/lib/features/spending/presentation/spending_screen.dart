import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../app/environment/environment_provider.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../data/repositories/spending_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import 'widgets/category_selection_sheet.dart'
    show categoryDisplayName, subCategoryDisplayName;
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_typewriter_text.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import 'account_link_connect_sheet.dart';
import 'spending_accounts_state.dart';
import 'widgets/spending_empty_action_panel.dart';
import 'widgets/spending_hero_background.dart';
import 'widgets/spending_section_pills.dart';

// ─────────────────────────────────────────────────────────
//  Section visibility
// ─────────────────────────────────────────────────────────

const List<SpendingSection> _visibleSpendingSections = <SpendingSection>[
  SpendingSection.transactions,
  SpendingSection.budgets,
  SpendingSection.bills,
  SpendingSection.accounts,
];

// ─────────────────────────────────────────────────────────
//  Screen
// ─────────────────────────────────────────────────────────

/// Provides the list of spending accounts from the repository.
///
/// Watches [accountLinksSummaryProvider] so that connect / disconnect
/// actions automatically invalidate this provider, causing the screen
/// to re-query the repository (which filters by active connections).
final _spendingAccountsFutureProvider =
    FutureProvider<List<SpendingAccountCard>>(
  (Ref ref) async {
    ref.watch(demoDataModeProvider);
    ref.watch(accountLinksSummaryProvider);
    final repository = ref.watch(spendingRepositoryProvider);
    return repository.getAccounts();
  },
);

/// Provides transactions for a given account id from the repository.
///
/// Watches [accountLinksSummaryProvider] so that connect / disconnect
/// actions automatically invalidate this provider.
final _spendingTransactionsFutureProvider =
    FutureProvider.family<List<SpendingTransaction>, String>(
  (Ref ref, String accountId) async {
    ref.watch(demoDataModeProvider);
    ref.watch(accountLinksSummaryProvider);
    final repository = ref.watch(spendingRepositoryProvider);
    return repository.getTransactions(accountId);
  },
);

/// Same dark charcoal gradient used by the Home and Pay dashboards.
const LinearGradient _backgroundGradient = LinearGradient(
  colors: <Color>[
    Color(0xFF242223),
    Color(0xFF191718),
    Color(0xFF0F0D0E),
  ],
  stops: <double>[0, 0.46, 1],
  begin: Alignment.topCenter,
  end: Alignment.bottomCenter,
);

/// Simi AI hero message for the spending screen (populated state).
const String _simiHeroMessage = 'Here\u2019s your spending overview. '
    'I\u2019ll keep an eye on your accounts and flag anything unusual.';

class SpendingScreen extends ConsumerStatefulWidget {
  const SpendingScreen({super.key});

  @override
  ConsumerState<SpendingScreen> createState() => _SpendingScreenState();
}

class _SpendingScreenState extends ConsumerState<SpendingScreen> {
  late PageController _accountPageController;
  int _selectedAccountIndex = 0;

  final ValueNotifier<double> _statusBarProgress = ValueNotifier<double>(0.0);

  static double _extentToStatusBarProgress(double extent) {
    const double fadeZone = 0.05;
    const double fadeStart = _SpendingHeroAndSheet._maxSheetSize - fadeZone;
    return Curves.easeOut.transform(
      ((extent - fadeStart) / fadeZone).clamp(0.0, 1.0).toDouble(),
    );
  }

  void _handleSheetExtentChanged(double extent) {
    final double progress = _extentToStatusBarProgress(extent);
    if ((_statusBarProgress.value - progress).abs() > 0.001) {
      _statusBarProgress.value = progress;
    }
  }

  @override
  void initState() {
    super.initState();
    _accountPageController = PageController(viewportFraction: 0.88);
  }

  @override
  void dispose() {
    _accountPageController.dispose();
    _statusBarProgress.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    final List<SpendingAccountCard> accounts;
    final List<SpendingTransaction> transactions;

    // Show a snackbar once per error transition (not on every rebuild).
    ref.listen<AsyncValue<List<SpendingAccountCard>>>(
      _spendingAccountsFutureProvider,
      (previous, next) {
        if (next.hasError && !(previous?.hasError ?? false)) {
          ScaffoldMessenger.of(context)
            ..hideCurrentSnackBar()
            ..showSnackBar(
              const SnackBar(content: Text('Could not load accounts.')),
            );
        }
      },
    );

    final accountsSnapshot = ref.watch(_spendingAccountsFutureProvider);
    accounts = accountsSnapshot.when(
      data: (List<SpendingAccountCard> data) => data,
      loading: () => const <SpendingAccountCard>[],
      error: (Object error, _) => const <SpendingAccountCard>[],
    );

    if (accounts.isNotEmpty && _selectedAccountIndex < accounts.length) {
      final selectedId = accounts[_selectedAccountIndex].id;
      final txSnapshot = ref.watch(
        _spendingTransactionsFutureProvider(selectedId),
      );
      transactions = txSnapshot.when(
        data: (List<SpendingTransaction> data) => data,
        loading: () => const <SpendingTransaction>[],
        error: (Object error, _) => const <SpendingTransaction>[],
      );
    } else {
      transactions = const <SpendingTransaction>[];
    }

    final bool isEmpty = accounts.isEmpty;

    // Determine whether the currently selected account is manual.
    final bool isSelectedManual = !isEmpty &&
        _selectedAccountIndex < accounts.length &&
        accounts[_selectedAccountIndex].isManual;

    // When empty, use a full-screen Stack layout so the hero
    // background spans behind the header — matching the setup journey
    // and dashboard patterns.
    if (isEmpty) {
      return Scaffold(
        backgroundColor: c.surfaceWarm,
        body: const _EmptyStateFullScreen(),
        bottomNavigationBar: const PayaboPrimaryAppShell(
          destination: PayaboPrimaryDestination.spending,
        ),
      );
    }

    return PayaboWarmScaffold(
      backgroundDecoration: const BoxDecoration(gradient: _backgroundGradient),
      statusBarColorNotifier: _statusBarProgress,
      bottomNavigationBar: const PayaboPrimaryAppShell(
        destination: PayaboPrimaryDestination.spending,
      ),
      floatingActionButton: isSelectedManual
          ? FloatingActionButton.extended(
              onPressed: () => _navigateToAddTransaction(
                accounts[_selectedAccountIndex],
              ),
              icon: const Icon(Icons.add),
              label: const Text('Add transaction'),
            )
          : null,
      body: _SpendingHeroAndSheet(
        accounts: accounts,
        transactions: transactions,
        selectedAccountIndex: _selectedAccountIndex,
        pageController: _accountPageController,
        onAccountPageChanged: (int index) {
          setState(() => _selectedAccountIndex = index);
        },
        onSectionSelected: _handleSectionSelected,
        onSheetExtentChanged: _handleSheetExtentChanged,
        onTransactionCategoryChanged: () {
          ref.invalidate(accountLinksSummaryProvider);
        },
        onAddTransaction: isSelectedManual
            ? () => _navigateToAddTransaction(
                  accounts[_selectedAccountIndex],
                )
            : null,
      ),
    );
  }

  void _handleSectionSelected(SpendingSection section) {
    switch (section) {
      case SpendingSection.overview:
        context.go('/spending/overview');
        return;
      case SpendingSection.transactions:
        return;
      case SpendingSection.budgets:
        context.go('/spending/budgets');
        return;
      case SpendingSection.bills:
        context.go('/spending/bills');
        return;
      case SpendingSection.accounts:
        context.go('/spending/accounts');
        return;
    }
  }

  void _navigateToAddTransaction(SpendingAccountCard account) {
    context.push(
      '/spending/accounts/${account.id}/transactions/create',
      extra: <String, dynamic>{
        'currencySymbol': account.currencySymbol,
        'currencyCode': account.currencyCode ?? account.currencySymbol,
        'accountName': account.accountName,
      },
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Full-screen empty state (hero bg spans behind header)
// ─────────────────────────────────────────────────────────

/// Simi AI messages for the spending empty state.
const String _simiMessageLive =
    'To track your spending, link a bank account or add one manually. '
    'I\u2019ll help you stay on top of everything.';

/// Full-screen Stack layout for the empty spending state.
///
/// Mirrors the setup journey screen pattern: hero background fills
/// the entire screen, header overlays on top, Simi AI message in
/// the middle area, and action panel pinned to the bottom.
class _EmptyStateFullScreen extends ConsumerWidget {
  const _EmptyStateFullScreen();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;
    final bool isDark = c.isDark;

    const String simiMessage = _simiMessageLive;

    // In light mode the hero photo has no scrim, so text must be white
    // with a soft shadow for legibility. Dark mode keeps semantic tokens.
    final Color heroTextPrimary = isDark ? c.headerTitle : Colors.white;
    final Color heroTextSecondary = isDark ? c.textSubtleWarm : Colors.white70;
    final List<Shadow> heroTextShadow = isDark
        ? const <Shadow>[]
        : const <Shadow>[
            Shadow(
              color: Color(0x66000000),
              blurRadius: 6,
            ),
          ];

    return Stack(
      children: <Widget>[
        // ── Layer 1: Full-screen hero background ──────────
        const Positioned.fill(
          child: SpendingHeroBackground(),
        ),

        // ── Layer 2: Simi top bar + AI typewriter message ─
        Positioned.fill(
          child: SafeArea(
            bottom: false,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                // ── Simi top bar (matches setup journey _buildTopBar) ──
                Padding(
                  padding: const EdgeInsets.fromLTRB(
                    PayaboSpacing.xl,
                    PayaboSpacing.md,
                    PayaboSpacing.xl,
                    0,
                  ),
                  child: Row(
                    children: <Widget>[
                      Container(
                        width: 36,
                        height: 36,
                        decoration: BoxDecoration(
                          color: isDark
                              ? c.surfaceBase.withValues(alpha: 0.8)
                              : const Color(0xCC1A1A1A), // dark glass on photo
                          borderRadius: BorderRadius.circular(10),
                          border: Border.all(
                            color: isDark
                                ? c.borderWarm.withValues(alpha: 0.5)
                                : Colors.white24,
                          ),
                        ),
                        child: Icon(
                          Icons.auto_awesome_rounded,
                          size: 18,
                          color: isDark ? c.primary : Colors.white,
                        ),
                      ),
                      const SizedBox(width: PayaboSpacing.sm),
                      Text(
                        'Simi',
                        style: textTheme.titleMedium?.copyWith(
                          color: heroTextPrimary,
                          fontWeight: FontWeight.w700,
                          shadows: heroTextShadow,
                        ),
                      ),
                    ],
                  ),
                ),

                // ── Simi message fills remaining space above the panel ──
                Expanded(
                  child: Padding(
                    padding: const EdgeInsets.only(
                      top: PayaboSpacing.lg,
                      bottom: PayaboSpacing.sm,
                    ),
                    child: Align(
                      alignment: Alignment.topLeft,
                      child: SingleChildScrollView(
                        padding: const EdgeInsets.symmetric(
                          horizontal: PayaboSpacing.x2,
                        ),
                        child: PayaboTypewriterText(
                          animationKey: 'spending-empty',
                          message: simiMessage,
                          helperText: 'Simi, your AI assistant',
                          messageStyle: textTheme.headlineMedium?.copyWith(
                            color: heroTextPrimary,
                            height: 1.4,
                            shadows: heroTextShadow,
                          ),
                          helperStyle: textTheme.bodyMedium?.copyWith(
                            color: heroTextSecondary,
                            fontStyle: FontStyle.italic,
                            shadows: heroTextShadow,
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),

        // ── Layer 3: Fixed bottom action panel ────────────
        Positioned(
          left: 0,
          right: 0,
          bottom: 0,
          child: SpendingEmptyActionPanel(
            children: <Widget>[
              // ── "Get started" heading ───────────────────
              Text(
                'Get started',
                style: textTheme.titleLarge?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: PayaboSpacing.sm),
              Text(
                'Connect your bank or add an account manually to start tracking.',
                style: textTheme.bodyMedium?.copyWith(
                  color: c.muted,
                  height: 1.4,
                ),
              ),

              const SizedBox(height: PayaboSpacing.x2),

              // ── Action: Link an account (triggers Plaid flow) ──
              SpendingActionTile(
                icon: Icons.link_rounded,
                title: 'Link an account',
                subtitle:
                    'Securely connect your bank to import transactions automatically.',
                onTap: () {
                  final environment = ref.read(appEnvironmentProvider);
                  showAccountLinkConnectSheet(
                    context,
                    ref,
                    provider: environment.resolvedAccountLinkProvider,
                    mode: 'connect',
                    title: 'Connect bank account',
                  );
                },
              ),

              const SizedBox(height: PayaboSpacing.md),

              // ── Action: Add manually ────────────────────
              SpendingActionTile(
                icon: Icons.edit_outlined,
                title: 'Add account manually',
                subtitle: 'Create an account and enter transactions yourself.',
                onTap: () => context.push('/spending/accounts/create-manual'),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Hero (Simi message) + Pinned Header + DraggableScrollableSheet
// ─────────────────────────────────────────────────────────

class _SpendingHeroAndSheet extends StatefulWidget {
  const _SpendingHeroAndSheet({
    required this.accounts,
    required this.transactions,
    required this.selectedAccountIndex,
    required this.pageController,
    required this.onAccountPageChanged,
    required this.onSectionSelected,
    required this.onTransactionCategoryChanged,
    this.onSheetExtentChanged,
    this.onAddTransaction,
  });

  /// Maximum fraction of viewport the sheet can occupy.
  static const double _maxSheetSize = 1.0;

  /// Height of the pinned header — profile row + bell only, matching
  /// the Home and Pay dashboards (76px).
  static const double _pinnedHeaderHeight = 76;

  /// Small gap between the pinned header bottom and sheet viewport top.
  static const double _sheetTopGap = 10;

  /// Minimum hero height (for the Simi AI message area).
  static const double _minHeroHeight = 200;

  /// Maximum hero height.
  static const double _maxHeroHeight = 248;

  final List<SpendingAccountCard> accounts;
  final List<SpendingTransaction> transactions;
  final int selectedAccountIndex;
  final PageController pageController;
  final ValueChanged<int> onAccountPageChanged;
  final ValueChanged<SpendingSection> onSectionSelected;
  final VoidCallback onTransactionCategoryChanged;
  final ValueChanged<double>? onSheetExtentChanged;
  final VoidCallback? onAddTransaction;

  @override
  State<_SpendingHeroAndSheet> createState() => _SpendingHeroAndSheetState();
}

class _SpendingHeroAndSheetState extends State<_SpendingHeroAndSheet> {
  late final DraggableScrollableController _sheetController;
  late final ValueNotifier<double> _sheetExtentNotifier;

  @override
  void initState() {
    super.initState();
    _sheetController = DraggableScrollableController();
    _sheetExtentNotifier = ValueNotifier<double>(0);
    _sheetController.addListener(_syncSheetExtent);
  }

  void _syncSheetExtent() {
    if (!_sheetController.isAttached) return;

    final double nextExtent = _sheetController.size;
    if ((_sheetExtentNotifier.value - nextExtent).abs() > 0.001) {
      final SchedulerPhase phase = WidgetsBinding.instance.schedulerPhase;

      if (phase == SchedulerPhase.idle ||
          phase == SchedulerPhase.postFrameCallbacks) {
        _sheetExtentNotifier.value = nextExtent;
        widget.onSheetExtentChanged?.call(nextExtent);
        return;
      }

      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted || !_sheetController.isAttached) return;
        if ((_sheetExtentNotifier.value - nextExtent).abs() > 0.001) {
          _sheetExtentNotifier.value = nextExtent;
          widget.onSheetExtentChanged?.call(nextExtent);
        }
      });
    }
  }

  @override
  void dispose() {
    _sheetController.removeListener(_syncSheetExtent);
    _sheetController.dispose();
    _sheetExtentNotifier.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (BuildContext context, BoxConstraints constraints) {
        final double viewportHeight =
            constraints.maxHeight.isFinite ? constraints.maxHeight : 640;

        // Hero area: Simi AI message text
        final double heroHeight = math.min(
          _SpendingHeroAndSheet._maxHeroHeight,
          math.max(
            _SpendingHeroAndSheet._minHeroHeight,
            viewportHeight * 0.37,
          ),
        );

        const double pinnedHeaderHeight =
            _SpendingHeroAndSheet._pinnedHeaderHeight;
        const double pinnedSheetTop =
            pinnedHeaderHeight + _SpendingHeroAndSheet._sheetTopGap;

        // The sheet viewport is everything below the pinned header.
        final double sheetViewportHeight =
            math.max(1, viewportHeight - pinnedHeaderHeight);

        // The sheet starts just below the hero content (relative to full viewport).
        final double collapsedSheetTop = math.max(
          pinnedSheetTop + 164,
          heroHeight + PayaboSpacing.sm,
        );

        // Compute initial/min sheet sizes as fractions of the sheet viewport.
        final double initialSheetSize = (1 -
                ((collapsedSheetTop - pinnedHeaderHeight) /
                    sheetViewportHeight))
            .clamp(0.62, 0.76)
            .toDouble();
        final double minSheetSize =
            (initialSheetSize - 0.10).clamp(0.56, initialSheetSize).toDouble();

        final double heroBottomPadding = math.max(
          40,
          heroHeight - collapsedSheetTop + 28,
        );

        return Stack(
          children: <Widget>[
            // ── LAYER 1: Hero — Simi AI message on dark gradient ──
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: heroHeight,
              child: _SpendingHeroBanner(bottomPadding: heroBottomPadding),
            ),

            // ── LAYER 2: Pinned header (profile + bell only, 76px) ──
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: pinnedHeaderHeight,
              child: ValueListenableBuilder<double>(
                valueListenable: _sheetExtentNotifier,
                builder: (
                  BuildContext context,
                  double sheetExtent,
                  Widget? _,
                ) {
                  final double eff =
                      (sheetExtent <= 0 ? initialSheetSize : sheetExtent)
                          .clamp(
                            minSheetSize,
                            _SpendingHeroAndSheet._maxSheetSize,
                          )
                          .toDouble();
                  const double fadeZone = 0.05;
                  final double fadeStart = math.max(
                    0.0,
                    _SpendingHeroAndSheet._maxSheetSize - fadeZone,
                  );
                  final double bgProgress = Curves.easeOut.transform(
                    ((eff - fadeStart) / fadeZone).clamp(0.0, 1.0).toDouble(),
                  );
                  return _SpendingPinnedHeader(
                    backgroundProgress: bgProgress,
                  );
                },
              ),
            ),

            // ── LAYER 3: Draggable sheet — section pills, cards, transactions ──
            Positioned(
              top: pinnedHeaderHeight,
              left: 0,
              right: 0,
              bottom: 0,
              child: DraggableScrollableSheet(
                controller: _sheetController,
                initialChildSize: initialSheetSize,
                minChildSize: minSheetSize,
                maxChildSize: _SpendingHeroAndSheet._maxSheetSize,
                snap: true,
                snapSizes: <double>[
                  initialSheetSize,
                  _SpendingHeroAndSheet._maxSheetSize,
                ],
                builder: (
                  BuildContext context,
                  ScrollController scrollController,
                ) {
                  return ValueListenableBuilder<double>(
                    valueListenable: _sheetExtentNotifier,
                    builder: (
                      BuildContext context,
                      double extent,
                      Widget? child,
                    ) {
                      // Animate top corners 24 → 0 in the last 5% of travel
                      // so the panel merges flush at full extension.
                      const double fadeZone = 0.05;
                      final double fadeFraction = ((extent -
                                  (_SpendingHeroAndSheet._maxSheetSize -
                                      fadeZone)) /
                              fadeZone)
                          .clamp(0.0, 1.0);
                      return _SpendingSheet(
                        scrollController: scrollController,
                        topBorderRadius: 24.0 * (1.0 - fadeFraction),
                        accounts: widget.accounts,
                        transactions: widget.transactions,
                        selectedAccountIndex: widget.selectedAccountIndex,
                        pageController: widget.pageController,
                        onAccountPageChanged: widget.onAccountPageChanged,
                        onSectionSelected: widget.onSectionSelected,
                        onTransactionCategoryChanged:
                            widget.onTransactionCategoryChanged,
                        onAddTransaction: widget.onAddTransaction,
                      );
                    },
                  );
                },
              ),
            ),
          ],
        );
      },
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Pinned header — profile + notification bell only (76px)
//  Matches the Home and Pay dashboard pinned headers.
// ─────────────────────────────────────────────────────────

class _SpendingPinnedHeader extends ConsumerWidget {
  const _SpendingPinnedHeader({
    required this.backgroundProgress,
  });

  final double backgroundProgress;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.colors;

    return Stack(
      children: <Widget>[
        // ── Background fade (solid surface when sheet is at top) ──
        Positioned.fill(
          child: Opacity(
            opacity: backgroundProgress,
            child: ColoredBox(color: c.surfaceBase),
          ),
        ),
        // ── Foreground: PayaboAppHeader with NO title/bottom — just profile + bell ──
        const Positioned.fill(
          child: PayaboAppHeader(
            padding: EdgeInsets.fromLTRB(
              PayaboSpacing.xl,
              PayaboSpacing.md,
              PayaboSpacing.xl,
              0,
            ),
          ),
        ),
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Hero banner — Simi AI message on dark gradient
//  Matches the Home and Pay hero banner pattern.
// ─────────────────────────────────────────────────────────

class _SpendingHeroBanner extends StatelessWidget {
  const _SpendingHeroBanner({this.bottomPadding = 40});

  final double bottomPadding;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    final TextStyle baseMessageStyle = textTheme.bodyLarge?.copyWith(
          fontSize: 17,
          color: Colors.white.withValues(alpha: 0.76),
          height: 1.5,
        ) ??
        const TextStyle(
          color: Colors.white,
          fontSize: 17,
          height: 1.5,
        );

    return SizedBox(
      width: double.infinity,
      child: Padding(
        padding: EdgeInsets.fromLTRB(
          PayaboSpacing.xl,
          0,
          PayaboSpacing.xl,
          bottomPadding,
        ),
        child: Align(
          alignment: Alignment.bottomLeft,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 360),
            child: LayoutBuilder(
              builder: (BuildContext context, BoxConstraints box) {
                final bool compact = box.maxHeight < 190;
                return Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      'Your spending',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: (compact
                              ? textTheme.headlineMedium
                              : textTheme.headlineLarge)
                          ?.copyWith(
                        color: Colors.white,
                        fontWeight: FontWeight.w700,
                        height: 1.15,
                      ),
                    ),
                    SizedBox(
                      height: compact ? PayaboSpacing.sm : PayaboSpacing.md,
                    ),
                    Text(
                      _simiHeroMessage,
                      maxLines: compact ? 3 : 4,
                      overflow: TextOverflow.ellipsis,
                      style: baseMessageStyle,
                    ),
                  ],
                );
              },
            ),
          ),
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Sheet — section pills + account cards + flat transactions
// ─────────────────────────────────────────────────────────

class _SpendingSheet extends StatelessWidget {
  const _SpendingSheet({
    required this.scrollController,
    required this.accounts,
    required this.transactions,
    required this.selectedAccountIndex,
    required this.pageController,
    required this.onAccountPageChanged,
    required this.onSectionSelected,
    required this.onTransactionCategoryChanged,
    this.onAddTransaction,
    this.topBorderRadius = 24.0,
  });

  final ScrollController scrollController;
  final List<SpendingAccountCard> accounts;
  final List<SpendingTransaction> transactions;
  final int selectedAccountIndex;
  final PageController pageController;
  final ValueChanged<int> onAccountPageChanged;
  final ValueChanged<SpendingSection> onSectionSelected;
  final VoidCallback onTransactionCategoryChanged;

  /// Called when the user taps "Add transaction" in the empty state
  /// of a manual account.
  final VoidCallback? onAddTransaction;

  /// Top corner radius — animated to 0 at full sheet extension.
  final double topBorderRadius;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final Color sheetBackground = c.surfaceBase;
    final Color handleColor = c.borderStrong;

    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: sheetBackground,
        borderRadius: BorderRadius.only(
          topLeft: Radius.circular(topBorderRadius),
          topRight: Radius.circular(topBorderRadius),
        ),
        boxShadow: topBorderRadius > 0
            ? <BoxShadow>[
                BoxShadow(
                  color: Colors.black.withValues(alpha: c.isDark ? 0.22 : 0.08),
                  blurRadius: 18,
                  offset: const Offset(0, -4),
                ),
              ]
            : const <BoxShadow>[],
      ),
      child: ListView(
        controller: scrollController,
        physics: const BouncingScrollPhysics(
          parent: AlwaysScrollableScrollPhysics(),
        ),
        padding: const EdgeInsets.fromLTRB(
          PayaboSpacing.xl,
          PayaboSpacing.md,
          PayaboSpacing.xl,
          PayaboSpacing.x4,
        ),
        children: <Widget>[
          // ── Drag handle ──────────────────────────────
          Center(
            child: Container(
              width: 42,
              height: 5,
              decoration: BoxDecoration(
                color: handleColor,
                borderRadius: BorderRadius.circular(999),
              ),
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),

          // ── Section pills (Transactions / Budgets / Accounts) ──
          SpendingSectionPills(
            selectedSection: SpendingSection.transactions,
            sections: _visibleSpendingSections,
            onSelected: onSectionSelected,
          ),
          const SizedBox(height: PayaboSpacing.xl),

          // ── Account cards (horizontal pager) ─────────
          SizedBox(
            height: 160,
            child: PageView.builder(
              controller: pageController,
              itemCount: accounts.length,
              onPageChanged: onAccountPageChanged,
              itemBuilder: (BuildContext context, int index) {
                return Padding(
                  padding: const EdgeInsets.only(right: PayaboSpacing.md),
                  child: _AccountCard(
                    account: accounts[index],
                    isSelected: index == selectedAccountIndex,
                  ),
                );
              },
            ),
          ),

          // ── Pager dots ───────────────────────────────
          if (accounts.length > 1) ...<Widget>[
            const SizedBox(height: PayaboSpacing.md),
            _PagerDots(
              count: accounts.length,
              activeIndex: selectedAccountIndex,
            ),
          ],

          const SizedBox(height: PayaboSpacing.x2),

          // ── "Recent transactions" heading ─────────────
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  'Recent transactions',
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ),
              Icon(
                Icons.chevron_right_rounded,
                color: c.accentBrown,
              ),
            ],
          ),

          const SizedBox(height: PayaboSpacing.md),

          // ── Transaction list (flat rows with dividers — like Pay activity) ──
          if (transactions.isEmpty)
            _EmptyTransactionsState(
              isManual: selectedAccountIndex < accounts.length &&
                  accounts[selectedAccountIndex].isManual,
              onAddTransaction: onAddTransaction,
            )
          else
            ..._buildFlatTransactionRows(
              context,
              transactions,
              c,
              onTransactionCategoryChanged,
            ),
        ],
      ),
    );
  }

  /// Builds a flat list of transaction rows grouped by month, with
  /// thin dividers between rows (matching the Pay activity layout).
  List<Widget> _buildFlatTransactionRows(
    BuildContext context,
    List<SpendingTransaction> transactions,
    PayaboColorResolver c,
    VoidCallback onTransactionCategoryChanged,
  ) {
    const List<String> monthNames = <String>[
      'January',
      'February',
      'March',
      'April',
      'May',
      'June',
      'July',
      'August',
      'September',
      'October',
      'November',
      'December',
    ];

    // Sort transactions by date descending (newest first).
    final List<SpendingTransaction> sorted =
        List<SpendingTransaction>.from(transactions)
          ..sort((SpendingTransaction a, SpendingTransaction b) =>
              b.date.compareTo(a.date));

    // Group by year-month key.
    final Map<String, List<SpendingTransaction>> grouped =
        <String, List<SpendingTransaction>>{};
    for (final SpendingTransaction tx in sorted) {
      final String key = '${tx.date.year}-${tx.date.month}';
      grouped.putIfAbsent(key, () => <SpendingTransaction>[]).add(tx);
    }

    final List<Widget> widgets = <Widget>[];

    for (final MapEntry<String, List<SpendingTransaction>> entry
        in grouped.entries) {
      final SpendingTransaction first = entry.value.first;
      final String label =
          '${monthNames[first.date.month - 1]} ${first.date.year}';

      // ── Month/year section header
      if (widgets.isNotEmpty) {
        widgets.add(const SizedBox(height: PayaboSpacing.xl));
      }
      widgets.add(
        Padding(
          padding: const EdgeInsets.only(bottom: PayaboSpacing.sm),
          child: Text(
            label,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  color: c.muted,
                  fontWeight: FontWeight.w600,
                ),
          ),
        ),
      );

      // ── Flat transaction rows with dividers (no card wrapper)
      for (int i = 0; i < entry.value.length; i++) {
        widgets.add(
          _TransactionRow(
            transaction: entry.value[i],
            onCategoryChanged: onTransactionCategoryChanged,
          ),
        );
        if (i < entry.value.length - 1) {
          widgets.add(
            Divider(
              height: 1,
              color: c.borderStrong.withValues(alpha: 0.3),
            ),
          );
        }
      }
    }

    return widgets;
  }
}

// ─────────────────────────────────────────────────────────
//  Account card (horizontal pager item)
// ─────────────────────────────────────────────────────────

class _AccountCard extends StatelessWidget {
  const _AccountCard({
    required this.account,
    required this.isSelected,
  });

  final SpendingAccountCard account;
  final bool isSelected;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      clipBehavior: Clip.hardEdge,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: c.spendingAccountGradientPrimary,
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: PayaboRadii.radiusLg,
        border: Border.all(
          color: c.spendingAccountAccentPrimary.withValues(alpha: 0.18),
        ),
      ),
      padding: const EdgeInsets.all(PayaboSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: <Widget>[
          // ── Top row: provider icon + account type pill ──
          Row(
            children: <Widget>[
              Container(
                width: 32,
                height: 32,
                decoration: BoxDecoration(
                  color: c.surfaceBase.withValues(alpha: 0.72),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Icon(
                  IconData(
                    account.providerIconCodePoint,
                    fontFamily: account.providerIconFontFamily,
                  ),
                  color: c.spendingAccountAccentPrimary,
                  size: 18,
                ),
              ),
              const Spacer(),
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: PayaboSpacing.md,
                  vertical: PayaboSpacing.xs,
                ),
                decoration: BoxDecoration(
                  color: c.surfaceBase.withValues(alpha: 0.72),
                  borderRadius: PayaboRadii.radiusPill,
                ),
                child: Text(
                  account.accountName,
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(
                        color: c.spendingAccountAccentPrimary,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ),
            ],
          ),

          // ── Balance ────────────────────────────────────
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              RichText(
                text: TextSpan(
                  children: <InlineSpan>[
                    TextSpan(
                      text: account.currencySymbol,
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            color: c.spendingAccountAccentPrimary,
                            fontWeight: FontWeight.w700,
                          ),
                    ),
                    TextSpan(
                      text: account.balanceMajor,
                      style:
                          Theme.of(context).textTheme.headlineMedium?.copyWith(
                                color: c.spendingAccountAccentPrimary,
                                fontWeight: FontWeight.w800,
                                height: 1,
                              ),
                    ),
                    TextSpan(
                      text: account.balanceMinor,
                      style: Theme.of(context).textTheme.titleSmall?.copyWith(
                            color: c.spendingAccountAccentPrimary
                                .withValues(alpha: 0.7),
                            fontWeight: FontWeight.w600,
                          ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: PayaboSpacing.xxs),
              Text(
                'Balance',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color:
                          c.spendingAccountAccentPrimary.withValues(alpha: 0.7),
                      fontWeight: FontWeight.w600,
                    ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Pager dots
// ─────────────────────────────────────────────────────────

class _PagerDots extends StatelessWidget {
  const _PagerDots({required this.count, required this.activeIndex});

  final int count;
  final int activeIndex;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: List<Widget>.generate(
        count,
        (int index) => AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          width: index == activeIndex ? 18 : 8,
          height: 8,
          margin: const EdgeInsets.symmetric(horizontal: 4),
          decoration: BoxDecoration(
            color: index == activeIndex ? c.primary : c.spendingDotInactive,
            borderRadius: BorderRadius.circular(999),
          ),
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Transaction row (flat — no card wrapper)
// ─────────────────────────────────────────────────────────

class _TransactionRow extends StatelessWidget {
  const _TransactionRow({
    required this.transaction,
    required this.onCategoryChanged,
  });

  final SpendingTransaction transaction;
  final VoidCallback onCategoryChanged;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final theme = Theme.of(context);

    // Resolve icon from code point + font family if available.
    final bool hasIcon = transaction.iconCodePoint != null;
    final IconData? resolvedIcon = hasIcon
        ? IconData(
            transaction.iconCodePoint!,
            fontFamily: transaction.iconFontFamily,
          )
        : null;

    // Icon circle: use merchant icon or first-letter avatar
    final Widget iconContent;
    if (resolvedIcon != null) {
      iconContent = Icon(
        resolvedIcon,
        color: theme.colorScheme.primary,
        size: 18,
      );
    } else {
      iconContent = Text(
        transaction.iconText ?? '?',
        style: theme.textTheme.titleSmall?.copyWith(
          color: c.spendingMerchantIconDark,
          fontWeight: FontWeight.w700,
        ),
      );
    }

    final Color iconBg = resolvedIcon != null
        ? c.primary.withValues(alpha: 0.08)
        : (c.isDark
            ? theme.colorScheme.surfaceContainerHighest
            : c.spendingMerchantIconWarmSurface);

    return InkWell(
      onTap: () async {
        final bool? categoryChanged = await context.push<bool>(
          '/spending/transaction/${transaction.id}',
          extra: <String, dynamic>{
            'merchant': transaction.merchant,
            'category': transaction.category,
            'subCategory': transaction.subCategory,
            'amountLabel': transaction.amountLabel,
            'amountMajor': transaction.amountMajor,
            'amountMinor': transaction.amountMinor,
            'currencySymbol': transaction.currencySymbol,
            'isCredit': transaction.isCredit,
            'iconText': transaction.iconText,
            'iconCodePoint': transaction.iconCodePoint,
            'iconFontFamily': transaction.iconFontFamily,
            'date': transaction.date,
            'notes': transaction.notes,
          },
        );

        if (categoryChanged == true) onCategoryChanged();
      },
      borderRadius: PayaboRadii.radiusSm,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.md),
        child: Row(
          children: <Widget>[
            // ── Icon circle ──────────────────────────
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: iconBg,
                borderRadius: BorderRadius.circular(10),
              ),
              alignment: Alignment.center,
              child: iconContent,
            ),

            const SizedBox(width: PayaboSpacing.md),

            // ── Merchant + category ──────────────────
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    transaction.merchant,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w600,
                      color: theme.colorScheme.onSurface,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    _transactionCategoryLabel(transaction),
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: c.textMuted,
                    ),
                  ),
                ],
              ),
            ),

            const SizedBox(width: PayaboSpacing.md),

            // ── Amount + credit indicator ────────────
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: <Widget>[
                Text(
                  '${transaction.isCredit ? '+' : ''}${transaction.currencySymbol}${transaction.amountMajor}${transaction.amountMinor}',
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: transaction.isCredit
                        ? c.success
                        : theme.colorScheme.onSurface,
                  ),
                ),
                if (transaction.isCredit) ...<Widget>[
                  const SizedBox(height: 4),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 10,
                      vertical: 3,
                    ),
                    decoration: BoxDecoration(
                      color:
                          c.success.withValues(alpha: c.isDark ? 0.22 : 0.12),
                      borderRadius: PayaboRadii.radiusPill,
                    ),
                    child: Text(
                      'Credit',
                      style: theme.textTheme.labelSmall?.copyWith(
                        color: c.success,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// Builds the category label for a transaction row.
///
/// Returns "Category · Subcategory" when a known subcategory is present,
/// otherwise just the category display name.
String _transactionCategoryLabel(SpendingTransaction transaction) {
  final String catName = categoryDisplayName(transaction.category);
  final String? subName =
      subCategoryDisplayName(transaction.category, transaction.subCategory);
  return subName != null ? '$catName · $subName' : catName;
}

class _EmptyTransactionsState extends StatelessWidget {
  const _EmptyTransactionsState({
    this.isManual = false,
    this.onAddTransaction,
  });

  final bool isManual;
  final VoidCallback? onAddTransaction;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.x2),
      child: Column(
        children: <Widget>[
          Icon(
            isManual ? Icons.add_card_outlined : Icons.receipt_long_outlined,
            size: 48,
            color: c.muted.withValues(alpha: 0.4),
          ),
          const SizedBox(height: PayaboSpacing.md),
          Text(
            isManual ? 'No transactions yet' : 'No transactions found',
            style: textTheme.titleMedium?.copyWith(
              color: c.accentBrown,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            isManual
                ? 'Add your first transaction to start tracking spending on this account.'
                : 'Transactions will appear here once your linked accounts have activity.',
            textAlign: TextAlign.center,
            style: textTheme.bodyMedium?.copyWith(
              color: c.muted,
              height: 1.4,
            ),
          ),
          if (isManual && onAddTransaction != null) ...<Widget>[
            const SizedBox(height: PayaboSpacing.lg),
            FilledButton.icon(
              onPressed: onAddTransaction,
              icon: const Icon(Icons.add, size: 18),
              label: const Text('Add transaction'),
            ),
          ],
        ],
      ),
    );
  }
}
