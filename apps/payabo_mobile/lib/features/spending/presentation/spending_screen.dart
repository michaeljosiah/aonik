import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../data/repositories/spending_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_card.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_typewriter_text.dart';
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

class SpendingScreen extends ConsumerStatefulWidget {
  const SpendingScreen({super.key});

  @override
  ConsumerState<SpendingScreen> createState() => _SpendingScreenState();
}

class _SpendingScreenState extends ConsumerState<SpendingScreen> {
  late PageController _accountPageController;
  int _selectedAccountIndex = 0;

  @override
  void initState() {
    super.initState();
    _accountPageController = PageController(viewportFraction: 0.88);
  }

  @override
  void dispose() {
    _accountPageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    // The repository already handles fresh vs populated branching based on
    // the demo data mode. We use FutureProvider-based AsyncValue watches
    // here. Mock repos resolve after a 250ms simulated delay; the screen
    // shows an empty state during loading.
    //
    // TODO(live): Convert to proper loading/error states when live repos land.
    final List<SpendingAccountCard> accounts;
    final List<SpendingTransaction> transactions;

    // Eagerly resolve the futures produced by the mock repo via Riverpod's
    // FutureProvider / AsyncValue.  When live repositories are wired up
    // these will become proper AsyncValue watches with loading indicators.
    final accountsSnapshot = ref.watch(_spendingAccountsFutureProvider);
    accounts = accountsSnapshot.when(
      data: (List<SpendingAccountCard> data) => data,
      loading: () => const <SpendingAccountCard>[],
      error: (_, __) => const <SpendingAccountCard>[],
    );

    if (accounts.isNotEmpty && _selectedAccountIndex < accounts.length) {
      final selectedId = accounts[_selectedAccountIndex].id;
      final txSnapshot = ref.watch(
        _spendingTransactionsFutureProvider(selectedId),
      );
      transactions = txSnapshot.when(
        data: (List<SpendingTransaction> data) => data,
        loading: () => const <SpendingTransaction>[],
        error: (_, __) => const <SpendingTransaction>[],
      );
    } else {
      transactions = const <SpendingTransaction>[];
    }

    final bool isEmpty = accounts.isEmpty;

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

    return Scaffold(
      backgroundColor: c.surfaceWarm,
      body: DecoratedBox(
        decoration: BoxDecoration(gradient: c.warmScreenGradient),
        child: SafeArea(
          bottom: false,
          child: Column(
            children: <Widget>[
              // ── Pinned header ─────────────────────────
              _TransactionsHeader(
                onSectionSelected: _handleSectionSelected,
              ),

              // ── Body ──────────────────────────────────
              Expanded(
                child: _SpendingHeroAndSheet(
                  accounts: accounts,
                  transactions: transactions,
                  selectedAccountIndex: _selectedAccountIndex,
                  pageController: _accountPageController,
                  onAccountPageChanged: (int index) {
                    setState(() => _selectedAccountIndex = index);
                  },
                ),
              ),
            ],
          ),
        ),
      ),
      bottomNavigationBar: const PayaboPrimaryAppShell(
        destination: PayaboPrimaryDestination.spending,
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
      case SpendingSection.accounts:
        context.go('/spending/accounts');
        return;
    }
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

    const String simiMessage = _simiMessageLive;

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
                          color: c.surfaceBase.withValues(alpha: 0.8),
                          borderRadius: BorderRadius.circular(10),
                          border: Border.all(
                            color: c.borderWarm.withValues(alpha: 0.5),
                          ),
                        ),
                        child: Icon(
                          Icons.auto_awesome_rounded,
                          size: 18,
                          color: c.primary,
                        ),
                      ),
                      const SizedBox(width: PayaboSpacing.sm),
                      Text(
                        'Simi',
                        style: textTheme.titleMedium?.copyWith(
                          color: c.headerTitle,
                          fontWeight: FontWeight.w700,
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
                            color: c.headerTitle,
                            height: 1.4,
                          ),
                          helperStyle: textTheme.bodyMedium?.copyWith(
                            color: c.textSubtleWarm,
                            fontStyle: FontStyle.italic,
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
                  ref
                      .read(accountLinkFlowControllerProvider.notifier)
                      .connect(provider: null, mode: 'connect', connectionId: null);
                },
              ),

              const SizedBox(height: PayaboSpacing.md),

              // ── Action: Add manually ────────────────────
              SpendingActionTile(
                icon: Icons.edit_outlined,
                title: 'Add account manually',
                subtitle:
                    'Create an account and enter transactions yourself.',
                onTap: () => context.go('/spending/accounts'),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Header (with section pills — used for populated state)
// ─────────────────────────────────────────────────────────

class _TransactionsHeader extends StatelessWidget {
  const _TransactionsHeader({required this.onSectionSelected});

  final ValueChanged<SpendingSection> onSectionSelected;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return PayaboAppHeader(
      title: 'Spend',
      titleStyle: Theme.of(context).textTheme.headlineLarge?.copyWith(
            fontWeight: FontWeight.w700,
            color: c.accentBrown,
          ),
      bottom: SpendingSectionPills(
        selectedSection: SpendingSection.transactions,
        sections: _visibleSpendingSections,
        onSelected: onSectionSelected,
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Hero (account cards) + DraggableScrollableSheet
// ─────────────────────────────────────────────────────────

class _SpendingHeroAndSheet extends StatefulWidget {
  const _SpendingHeroAndSheet({
    required this.accounts,
    required this.transactions,
    required this.selectedAccountIndex,
    required this.pageController,
    required this.onAccountPageChanged,
  });

  /// Height of the account card area including top/bottom padding.
  static const double _heroContentHeight = 248;

  /// Maximum fraction of viewport the sheet can occupy.
  static const double _maxSheetSize = 1.0;

  final List<SpendingAccountCard> accounts;
  final List<SpendingTransaction> transactions;
  final int selectedAccountIndex;
  final PageController pageController;
  final ValueChanged<int> onAccountPageChanged;

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
      final SchedulerPhase phase =
          WidgetsBinding.instance.schedulerPhase;

      if (phase == SchedulerPhase.idle ||
          phase == SchedulerPhase.postFrameCallbacks) {
        _sheetExtentNotifier.value = nextExtent;
        return;
      }

      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted || !_sheetController.isAttached) return;
        if ((_sheetExtentNotifier.value - nextExtent).abs() > 0.001) {
          _sheetExtentNotifier.value = nextExtent;
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

        // Hero area: account cards + pager dots
        final double heroHeight = math.min(
          _SpendingHeroAndSheet._heroContentHeight,
          viewportHeight * 0.42,
        );

        // The sheet starts just below the hero content.
        final double collapsedSheetTop =
            heroHeight + PayaboSpacing.sm;

        // Compute initial/min sheet sizes as fractions of the viewport.
        final double initialSheetSize =
            (1 - (collapsedSheetTop / viewportHeight))
                .clamp(0.50, 0.78)
                .toDouble();
        final double minSheetSize =
            (initialSheetSize - 0.08).clamp(0.44, initialSheetSize).toDouble();

        return Stack(
          children: <Widget>[
            // ── Hero: account cards ─────────────────────
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: heroHeight,
              child: _AccountCardsHero(
                accounts: widget.accounts,
                selectedAccountIndex: widget.selectedAccountIndex,
                pageController: widget.pageController,
                onAccountPageChanged: widget.onAccountPageChanged,
              ),
            ),

            // ── Draggable sheet: transactions ──────────
            Positioned.fill(
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
                      return _SpendingTransactionsSheet(
                        scrollController: scrollController,
                        topBorderRadius: 24.0 * (1.0 - fadeFraction),
                        transactions: widget.transactions,
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
//  Hero section: account cards + pager dots
// ─────────────────────────────────────────────────────────

class _AccountCardsHero extends StatelessWidget {
  const _AccountCardsHero({
    required this.accounts,
    required this.selectedAccountIndex,
    required this.pageController,
    required this.onAccountPageChanged,
  });

  final List<SpendingAccountCard> accounts;
  final int selectedAccountIndex;
  final PageController pageController;
  final ValueChanged<int> onAccountPageChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        const SizedBox(height: PayaboSpacing.md),

        // ── Horizontal account cards ─────────────────
        Expanded(
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
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Transactions sheet (the slide-up panel)
// ─────────────────────────────────────────────────────────

class _SpendingTransactionsSheet extends StatelessWidget {
  const _SpendingTransactionsSheet({
    required this.scrollController,
    required this.transactions,
    this.topBorderRadius = 24.0,
  });

  final ScrollController scrollController;
  final List<SpendingTransaction> transactions;

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
                  color:
                      Colors.black.withValues(alpha: c.isDark ? 0.22 : 0.08),
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

          // ── Transaction list card / empty state ───────
          if (transactions.isEmpty)
            const _EmptyTransactionsState()
          else
            ..._buildGroupedTransactions(context, transactions, c),
        ],
      ),
    );
  }

  /// Groups transactions by month/year and builds section headers + cards.
  List<Widget> _buildGroupedTransactions(
    BuildContext context,
    List<SpendingTransaction> transactions,
    PayaboColorResolver c,
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

      // ── Transaction card for this month
      widgets.add(
        PayaboCard(
          backgroundColor: c.spendingCardWarmElevated,
          padding: const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.lg,
            vertical: PayaboSpacing.md,
          ),
          child: Column(
            children: entry.value
                .asMap()
                .entries
                .map(
                  (MapEntry<int, SpendingTransaction> e) {
                    final bool isLast = e.key == entry.value.length - 1;
                    return Column(
                      children: <Widget>[
                        _TransactionRow(transaction: e.value),
                        if (!isLast)
                          Divider(
                            height: PayaboSpacing.xl,
                            color: c.borderStrong.withValues(alpha: 0.6),
                          ),
                      ],
                    );
                  },
                )
                .toList(growable: false),
          ),
        ),
      );
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
        boxShadow: c.isDark ? PayaboShadows.soft : PayaboShadows.medium,
      ),
      padding: const EdgeInsets.all(PayaboSpacing.lg),
      child: LayoutBuilder(
        builder: (BuildContext context, BoxConstraints cardConstraints) {
          // On small viewports (e.g. 600px test surface) the card may
          // be too short for the full layout.  Use a compact layout when
          // the inner area is under ~100px.
          final bool compact = cardConstraints.maxHeight < 100;
          final double iconSize = compact ? 32.0 : 40.0;

          return Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: <Widget>[
              // ── Top row: provider icon + account type pill ──
              Row(
                children: <Widget>[
                  Container(
                    width: iconSize,
                    height: iconSize,
                    decoration: BoxDecoration(
                      color: c.surfaceBase.withValues(alpha: 0.72),
                      borderRadius: BorderRadius.circular(compact ? 10 : 14),
                    ),
                    child: Icon(
                      IconData(
                        account.providerIconCodePoint,
                        fontFamily: account.providerIconFontFamily,
                      ),
                      color: c.spendingAccountAccentPrimary,
                      size: compact ? 18.0 : 22.0,
                    ),
                  ),
                  const Spacer(),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: PayaboSpacing.md,
                      vertical: PayaboSpacing.sm,
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
                          style: (compact
                                  ? Theme.of(context).textTheme.titleMedium
                                  : Theme.of(context).textTheme.headlineMedium)
                              ?.copyWith(
                            color: c.spendingAccountAccentPrimary,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        TextSpan(
                          text: account.balanceMajor,
                          style: (compact
                                  ? Theme.of(context).textTheme.headlineMedium
                                  : Theme.of(context).textTheme.displayLarge)
                              ?.copyWith(
                            color: c.spendingAccountAccentPrimary,
                            fontWeight: FontWeight.w800,
                            height: 1,
                          ),
                        ),
                        TextSpan(
                          text: account.balanceMinor,
                          style: (compact
                                  ? Theme.of(context).textTheme.titleSmall
                                  : Theme.of(context).textTheme.headlineSmall)
                              ?.copyWith(
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
                          color: c.spendingAccountAccentPrimary
                              .withValues(alpha: 0.7),
                          fontWeight: FontWeight.w600,
                        ),
                  ),
                ],
              ),
            ],
          );
        },
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
//  Transaction row
// ─────────────────────────────────────────────────────────

class _TransactionRow extends StatelessWidget {
  const _TransactionRow({required this.transaction});

  final SpendingTransaction transaction;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

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
        color: c.primary,
        size: 22,
      );
    } else {
      iconContent = Text(
        transaction.iconText ?? '?',
        style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: c.spendingMerchantIconDark,
              fontWeight: FontWeight.w700,
            ),
      );
    }

    final Color iconBg = resolvedIcon != null
        ? c.primary.withValues(alpha: 0.12)
        : c.spendingMerchantIconWarmSurface;

    return GestureDetector(
      onTap: () {
        context.push(
          '/spending/transaction/${transaction.id}',
          extra: <String, dynamic>{
            'merchant': transaction.merchant,
            'category': transaction.category,
            'amountLabel': transaction.amountLabel,
            'amountMajor': transaction.amountMajor,
            'amountMinor': transaction.amountMinor,
            'currencySymbol': transaction.currencySymbol,
            'isCredit': transaction.isCredit,
            'iconText': transaction.iconText,
            'iconCodePoint': transaction.iconCodePoint,
            'iconFontFamily': transaction.iconFontFamily,
            'date': transaction.date,
          },
        );
      },
      behavior: HitTestBehavior.opaque,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.sm),
        child: Row(
          children: <Widget>[
            // ── Icon circle ──────────────────────────
            Container(
              width: 46,
              height: 46,
              decoration: BoxDecoration(
                color: iconBg,
                shape: BoxShape.circle,
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
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(
                          color: c.ink,
                          fontWeight: FontWeight.w600,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.xxs),
                  Text(
                    transaction.category,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: c.muted,
                        ),
                  ),
                ],
              ),
            ),

            const SizedBox(width: PayaboSpacing.md),

            // ── Amount (Cleo-style split rendering) ──
            RichText(
              text: TextSpan(
                children: <InlineSpan>[
                  TextSpan(
                    text: transaction.isCredit ? '+' : '',
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: transaction.isCredit ? c.success : c.accentBrown,
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                  TextSpan(
                    text: transaction.currencySymbol,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: transaction.isCredit ? c.success : c.accentBrown,
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                  TextSpan(
                    text: transaction.amountMajor,
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          color: transaction.isCredit ? c.success : c.accentBrown,
                          fontWeight: FontWeight.w800,
                        ),
                  ),
                  TextSpan(
                    text: transaction.amountMinor,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: (transaction.isCredit ? c.success : c.accentBrown)
                              .withValues(alpha: 0.6),
                          fontWeight: FontWeight.w600,
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


class _EmptyTransactionsState extends StatelessWidget {
  const _EmptyTransactionsState();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return PayaboCard(
      backgroundColor: c.spendingCardWarmElevated,
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        children: <Widget>[
          Icon(
            Icons.receipt_long_outlined,
            color: c.muted,
            size: 32,
          ),
          const SizedBox(height: PayaboSpacing.md),
          Text(
            'No transactions yet',
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w600,
                ),
          ),
          const SizedBox(height: PayaboSpacing.xs),
          Text(
            'Transactions for this account will appear here.',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: c.muted,
                ),
          ),
        ],
      ),
    );
  }
}
