import 'dart:math' as math;

import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
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
import 'widgets/category_selection_sheet.dart'
    show categoryDisplayName, subCategoryDisplayName;
import '../../../shared/widgets/payabo_card.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import 'spending_accounts_state.dart';
import 'widgets/spending_section_pills.dart';

// ─────────────────────────────────────────────────────────
//  Section visibility
// ─────────────────────────────────────────────────────────

const List<SpendingSection> _visibleOverviewSections = <SpendingSection>[
  SpendingSection.transactions,
  SpendingSection.budgets,
  SpendingSection.accounts,
];

// ─────────────────────────────────────────────────────────
//  Provider
// ─────────────────────────────────────────────────────────

/// Provides overview data from the spending repository.
/// Watches [accountLinksSummaryProvider] so that connect / disconnect
/// actions automatically invalidate this provider, causing the screen
/// to re-query the repository (which filters by active connections).
final _spendingOverviewFutureProvider =
    FutureProvider<SpendingOverviewData>((Ref ref) async {
  ref.watch(demoDataModeProvider);
  ref.watch(accountLinksSummaryProvider);
  final repository = ref.watch(spendingRepositoryProvider);
  return repository.getOverview();
});

// ─────────────────────────────────────────────────────────
//  Color-key resolution helpers
// ─────────────────────────────────────────────────────────

List<Color> _resolveGradient(String key, PayaboColorResolver c) {
  switch (key) {
    case 'savings':
      return c.spendingAccountGradientSavings;
    case 'bills':
      return c.spendingAccountGradientBills;
    case 'primary':
    default:
      return c.spendingAccountGradientPrimary;
  }
}

Color _resolveAccent(String key, PayaboColorResolver c) {
  switch (key) {
    case 'savings':
      return c.spendingAccountAccentSavings;
    case 'bills':
      return c.spendingAccountAccentBills;
    case 'primary':
    default:
      return c.spendingAccountAccentPrimary;
  }
}

Color _resolveSliceColor(String key, PayaboColorResolver c) {
  switch (key) {
    case 'bills':
      return c.spendingSliceBills;
    case 'success':
      return c.success;
    case 'info':
      return c.info;
    case 'other':
      return c.spendingSliceOther;
    case 'primary':
    default:
      return c.primary;
  }
}

Color _resolveIconBackground(String key, PayaboColorResolver c) {
  switch (key) {
    case 'warmSurface':
      return c.spendingMerchantIconWarmSurface;
    case 'warmAccent':
      return c.spendingMerchantIconWarmAccent;
    case 'dark':
    default:
      return c.spendingMerchantIconDark;
  }
}

Color _resolveIconForeground(String key, PayaboColorResolver c) {
  switch (key) {
    case 'dark':
      return c.spendingMerchantIconDark;
    case 'warmText':
      return c.spendingMerchantIconWarmText;
    case 'surfaceBase':
    default:
      return c.surfaceBase;
  }
}

// ─────────────────────────────────────────────────────────
//  Screen
// ─────────────────────────────────────────────────────────

class SpendingOverviewScreen extends ConsumerStatefulWidget {
  const SpendingOverviewScreen({super.key});

  @override
  ConsumerState<SpendingOverviewScreen> createState() =>
      _SpendingOverviewScreenState();
}

class _SpendingOverviewScreenState
    extends ConsumerState<SpendingOverviewScreen> {
  late final PageController _accountController;
  int _accountPage = 0;

  @override
  void initState() {
    super.initState();
    _accountController = PageController(viewportFraction: 0.9);
  }

  @override
  void dispose() {
    _accountController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final asyncOverview = ref.watch(_spendingOverviewFutureProvider);

    return Scaffold(
      backgroundColor: c.surfaceWarm,
      body: DecoratedBox(
        decoration: BoxDecoration(gradient: c.warmScreenGradient),
        child: SafeArea(
          bottom: false,
          child: Column(
            children: <Widget>[
              _OverviewHeader(onSectionSelected: _handleSectionSelected),
              Expanded(
                child: asyncOverview.when(
                  loading: () => const Center(
                    child: CircularProgressIndicator(),
                  ),
                  error: (Object error, StackTrace stack) => Center(
                    child: Padding(
                      padding: const EdgeInsets.all(PayaboSpacing.xl),
                      child: Text(
                        'Something went wrong loading your overview.',
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              color: c.muted,
                            ),
                        textAlign: TextAlign.center,
                      ),
                    ),
                  ),
                  data: (SpendingOverviewData overview) {
                    return ListView(
                      padding: const EdgeInsets.fromLTRB(
                        PayaboSpacing.xl,
                        PayaboSpacing.sm,
                        PayaboSpacing.xl,
                        PayaboSpacing.x4,
                      ),
                      children: _buildBody(
                        context: context,
                        c: c,
                        overview: overview,
                      ),
                    );
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

  List<Widget> _buildBody({
    required BuildContext context,
    required PayaboColorResolver c,
    required SpendingOverviewData overview,
  }) {
    // Fresh / empty data → single explanatory card
    if (overview.accountSnapshots.isEmpty &&
        overview.breakdownSlices.isEmpty &&
        overview.recentTransactions.isEmpty) {
      final demoMode = ref.read(demoDataModeProvider);
      if (demoMode == DemoDataMode.fresh) {
        return const <Widget>[_FreshOverviewStateCard()];
      }
      return const <Widget>[_LiveEmptyOverviewState()];
    }

    // Populated → full showcase
    return <Widget>[
      SizedBox(
        height: 204,
        child: PageView.builder(
          controller: _accountController,
          itemCount: overview.accountSnapshots.length,
          onPageChanged: (int index) {
            setState(() => _accountPage = index);
          },
          itemBuilder: (BuildContext context, int index) {
            return Padding(
              padding: const EdgeInsets.only(right: PayaboSpacing.md),
              child: _AccountSnapshotCard(
                snapshot: overview.accountSnapshots[index],
              ),
            );
          },
        ),
      ),
      const SizedBox(height: PayaboSpacing.md),
      _AccountPagerDots(
        count: overview.accountSnapshots.length,
        activeIndex: _accountPage,
      ),
      const SizedBox(height: PayaboSpacing.x2),
      _OverviewQuickActions(
        onAddAccountTap: () => context.go('/spending/accounts'),
        onManageAccountsTap: () => context.go('/spending/accounts'),
      ),
      const SizedBox(height: PayaboSpacing.xl),
      const _SectionHeading(
        title: 'Snapshot',
        subtitle: 'The numbers that matter this month',
      ),
      const SizedBox(height: PayaboSpacing.md),
      Row(
        children: <Widget>[
          Expanded(
            child: _MetricCard(metric: overview.totalBalanceMetric),
          ),
          const SizedBox(width: PayaboSpacing.md),
          Expanded(
            child: _MetricCard(metric: overview.netWorthMetric),
          ),
        ],
      ),
      const SizedBox(height: PayaboSpacing.md),
      _SafeToSpendCard(
        amountLabel: overview.safeToSpendLabel,
        subtitle: overview.safeToSpendSubtitle,
      ),
      const SizedBox(height: PayaboSpacing.xl),
      const _SectionHeading(
        title: 'Monthly breakdown',
        subtitle: 'Where this month is going so far',
      ),
      const SizedBox(height: PayaboSpacing.md),
      _MonthlyBreakdownCard(
        slices: overview.breakdownSlices,
        totalLabel: overview.breakdownTotalLabel,
      ),
      const SizedBox(height: PayaboSpacing.xl),
      const _SectionHeading(
        title: 'Spending trend',
        subtitle: 'Week-by-week movement this month',
      ),
      const SizedBox(height: PayaboSpacing.md),
      _TrendCard(
        summaryLabel: overview.trendSummaryLabel,
        spots: overview.trendSpots,
        bottomLabels: overview.trendBottomLabels,
      ),
      const SizedBox(height: PayaboSpacing.xl),
      const _SectionHeading(
        title: 'Quick insights',
        subtitle: 'AI-generated nudges from your spending patterns',
      ),
      const SizedBox(height: PayaboSpacing.md),
      _InsightCard(
        title: overview.insightTitle,
        body: overview.insightBody,
      ),
      const SizedBox(height: PayaboSpacing.xl),
      _MonthlyOverviewCard(
        slices: overview.allocationSlices,
        monthLabel: overview.allocationMonthLabel,
        yearLabel: overview.allocationYearLabel,
        chipLabel: overview.allocationChipLabel,
      ),
      const SizedBox(height: PayaboSpacing.xl),
      const _SectionHeading(
        title: 'Recent transactions',
        subtitle: 'A quick preview before you dive into everything',
      ),
      const SizedBox(height: PayaboSpacing.md),
      _RecentTransactionsCard(
        transactions: overview.recentTransactions,
        onViewAllTap: () => context.go('/spending'),
      ),
    ];
  }

  void _handleSectionSelected(SpendingSection section) {
    switch (section) {
      case SpendingSection.overview:
        return;
      case SpendingSection.transactions:
        context.go('/spending');
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
//  Header
// ─────────────────────────────────────────────────────────

class _OverviewHeader extends StatelessWidget {
  const _OverviewHeader({required this.onSectionSelected});

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
        selectedSection: SpendingSection.overview,
        sections: _visibleOverviewSections,
        onSelected: onSectionSelected,
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Empty states
// ─────────────────────────────────────────────────────────

class _FreshOverviewStateCard extends StatelessWidget {
  const _FreshOverviewStateCard();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return PayaboCard(
      backgroundColor: c.surfaceCardElevated,
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: c.primary.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(20),
            ),
            child: Icon(
              Icons.account_balance_wallet_outlined,
              color: c.primary,
              size: 28,
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'No balances or account snapshots yet',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            'Fresh demo mode clears linked-account examples, monthly breakdowns, and recent transaction previews so the spending experience starts empty.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.muted,
                  height: 1.45,
                ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'Switch back to Populated demo data in Profile whenever you want to review the seeded spending showcase again.',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: c.chatTextSecondary,
                ),
          ),
        ],
      ),
    );
  }
}

class _LiveEmptyOverviewState extends StatelessWidget {
  const _LiveEmptyOverviewState();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return PayaboCard(
      backgroundColor: c.surfaceCardElevated,
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: c.primary.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(20),
            ),
            child: Icon(
              Icons.insights_outlined,
              color: c.primary,
              size: 28,
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'No spending data yet',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            'Link a bank account to see your balances, monthly breakdowns, spending trends, and AI insights here.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.muted,
                  height: 1.45,
                ),
          ),
          const SizedBox(height: PayaboSpacing.xl),
          SizedBox(
            width: double.infinity,
            height: 50,
            child: FilledButton.icon(
              onPressed: () => context.go('/spending/accounts'),
              icon: const Icon(Icons.add),
              label: const Text('Link account'),
              style: FilledButton.styleFrom(
                backgroundColor: c.primary,
                foregroundColor: c.surfaceBase,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(18),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Accounts + Pager dots
// ─────────────────────────────────────────────────────────

class _AccountPagerDots extends StatelessWidget {
  const _AccountPagerDots({required this.count, required this.activeIndex});

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

class _OverviewQuickActions extends StatelessWidget {
  const _OverviewQuickActions({
    required this.onAddAccountTap,
    required this.onManageAccountsTap,
  });

  final VoidCallback onAddAccountTap;
  final VoidCallback onManageAccountsTap;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceEvenly,
      children: <Widget>[
        _OverviewQuickAction(
          icon: Icons.add,
          label: 'Add account',
          onTap: onAddAccountTap,
        ),
        _OverviewQuickAction(
          icon: Icons.settings_outlined,
          label: 'Manage accounts',
          onTap: onManageAccountsTap,
        ),
      ],
    );
  }
}

class _OverviewQuickAction extends StatelessWidget {
  const _OverviewQuickAction({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return SizedBox(
      width: 130,
      child: InkWell(
        onTap: onTap,
        borderRadius: PayaboRadii.radiusSm,
        child: Column(
          children: <Widget>[
            Container(
              width: 72,
              height: 72,
              decoration: BoxDecoration(
                color: c.spendingQuickActionSurface,
                shape: BoxShape.circle,
                border: Border.all(color: c.spendingQuickActionBorder),
              ),
              child: Icon(icon, color: c.primary, size: 32),
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              label,
              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: c.accentBrown,
                    fontWeight: FontWeight.w600,
                  ),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Section heading
// ─────────────────────────────────────────────────────────

class _SectionHeading extends StatelessWidget {
  const _SectionHeading({required this.title, required this.subtitle});

  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          title,
          style: Theme.of(context).textTheme.titleLarge?.copyWith(
                color: c.accentBrown,
              ),
        ),
        const SizedBox(height: PayaboSpacing.xs),
        Text(
          subtitle,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                color: c.muted,
              ),
        ),
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Account snapshot card
// ─────────────────────────────────────────────────────────

class _AccountSnapshotCard extends StatelessWidget {
  const _AccountSnapshotCard({required this.snapshot});

  final SpendingAccountSnapshot snapshot;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final gradientColors = _resolveGradient(snapshot.gradientKey, c);
    final accentColor = _resolveAccent(snapshot.gradientKey, c);
    final icon = IconData(
      snapshot.iconCodePoint,
      fontFamily: snapshot.iconFontFamily,
    );

    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: gradientColors,
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: accentColor.withValues(alpha: 0.18)),
        boxShadow: c.isDark ? PayaboShadows.soft : PayaboShadows.medium,
      ),
      child: Padding(
        padding: const EdgeInsets.all(PayaboSpacing.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: <Widget>[
            Row(
              children: <Widget>[
                Container(
                  width: 48,
                  height: 48,
                  decoration: BoxDecoration(
                    color: c.surfaceBase.withValues(alpha: 0.72),
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: Icon(icon, color: accentColor),
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
                    snapshot.statusLabel,
                    style: Theme.of(context).textTheme.labelMedium?.copyWith(
                          color: accentColor,
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                ),
              ],
            ),
            Text(
              snapshot.balanceLabel,
              style: Theme.of(context).textTheme.displayMedium?.copyWith(
                    color: accentColor,
                    height: 1,
                    fontWeight: FontWeight.w800,
                  ),
            ),
            Text(
              snapshot.label,
              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: accentColor,
                    fontWeight: FontWeight.w600,
                  ),
            ),
            Text(
              snapshot.changeLabel,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: accentColor.withValues(alpha: 0.8),
                  ),
            ),
          ],
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Metric cards
// ─────────────────────────────────────────────────────────

class _MetricCard extends StatelessWidget {
  const _MetricCard({required this.metric});

  final SpendingMetric metric;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final icon = IconData(
      metric.iconCodePoint,
      fontFamily: metric.iconFontFamily,
    );

    return PayaboCard(
      backgroundColor: c.surfaceCardElevated,
      padding: const EdgeInsets.all(PayaboSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(icon, color: c.primary),
          const SizedBox(height: PayaboSpacing.md),
          Text(
            metric.label,
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.muted,
                ),
          ),
          const SizedBox(height: PayaboSpacing.xs),
          Text(
            metric.amountLabel,
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w800,
                ),
          ),
          const SizedBox(height: PayaboSpacing.xs),
          Text(
            metric.trendLabel,
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: c.success,
                  fontWeight: FontWeight.w700,
                ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Safe to spend
// ─────────────────────────────────────────────────────────

class _SafeToSpendCard extends StatelessWidget {
  const _SafeToSpendCard({
    required this.amountLabel,
    required this.subtitle,
  });

  final String amountLabel;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        gradient: c.spendingSafeToSpendGradient,
        borderRadius: const BorderRadius.all(Radius.circular(24)),
        boxShadow: PayaboShadows.medium,
      ),
      child: Padding(
        padding: const EdgeInsets.all(PayaboSpacing.xl),
        child: Row(
          children: <Widget>[
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    'Safe to spend',
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(
                          color: Colors.white,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.xs),
                  Text(
                    amountLabel,
                    style: Theme.of(context).textTheme.displayMedium?.copyWith(
                          color: Colors.white,
                          fontWeight: FontWeight.w800,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.sm),
                  Text(
                    subtitle,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: Colors.white.withValues(alpha: 0.82),
                        ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: PayaboSpacing.lg),
            Container(
              width: 66,
              height: 66,
              decoration: BoxDecoration(
                color: Colors.white.withValues(alpha: 0.14),
                borderRadius: BorderRadius.circular(20),
              ),
              child: const Icon(
                Icons.verified_user_outlined,
                color: Colors.white,
                size: 32,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Monthly breakdown (pie chart)
// ─────────────────────────────────────────────────────────

class _MonthlyBreakdownCard extends StatelessWidget {
  const _MonthlyBreakdownCard({
    required this.slices,
    required this.totalLabel,
  });

  final List<SpendingBreakdownSlice> slices;
  final String totalLabel;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return PayaboCard(
      backgroundColor: c.spendingCardWarm,
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SizedBox(
            width: 156,
            height: 156,
            child: Stack(
              alignment: Alignment.center,
              children: <Widget>[
                PieChart(
                  PieChartData(
                    centerSpaceRadius: 38,
                    sectionsSpace: 3,
                    startDegreeOffset: -90,
                    sections: slices
                        .map(
                          (SpendingBreakdownSlice slice) =>
                              PieChartSectionData(
                            value: slice.value,
                            color: _resolveSliceColor(slice.colorKey, c),
                            radius: 18,
                            title: '',
                          ),
                        )
                        .toList(growable: false),
                  ),
                ),
                Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: <Widget>[
                    Text(
                      totalLabel,
                      style: Theme.of(context).textTheme.titleLarge?.copyWith(
                            color: c.accentBrown,
                            fontWeight: FontWeight.w800,
                          ),
                    ),
                    Text(
                      'Spent this month',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color: c.muted,
                          ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(width: PayaboSpacing.lg),
          Expanded(
            child: Column(
              children: slices
                  .map(
                    (SpendingBreakdownSlice slice) => Padding(
                      padding: const EdgeInsets.only(bottom: PayaboSpacing.md),
                      child: _BreakdownLegendRow(slice: slice),
                    ),
                  )
                  .toList(growable: false),
            ),
          ),
        ],
      ),
    );
  }
}

class _BreakdownLegendRow extends StatelessWidget {
  const _BreakdownLegendRow({required this.slice});

  final SpendingBreakdownSlice slice;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final color = _resolveSliceColor(slice.colorKey, c);

    return Row(
      children: <Widget>[
        Container(
          width: 12,
          height: 12,
          decoration: BoxDecoration(color: color, shape: BoxShape.circle),
        ),
        const SizedBox(width: PayaboSpacing.sm),
        Expanded(
          child: Text(
            slice.label,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  color: c.textPrimary,
                ),
          ),
        ),
        Text(
          slice.amountLabel,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                color: c.accentBrown,
                fontWeight: FontWeight.w700,
              ),
        ),
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Spending trend chart
// ─────────────────────────────────────────────────────────

class _TrendCard extends StatelessWidget {
  const _TrendCard({
    required this.summaryLabel,
    required this.spots,
    required this.bottomLabels,
  });

  final String summaryLabel;
  final List<SpendingTrendSpot> spots;
  final List<String> bottomLabels;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return PayaboCard(
      backgroundColor: c.spendingCardWarmElevated,
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.lg,
        PayaboSpacing.lg,
        PayaboSpacing.lg,
        PayaboSpacing.md,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            summaryLabel,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  color: c.accentBrown,
                ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          SizedBox(
            height: 220,
            child: _OverviewTrendChart(
              spots: spots,
              bottomLabels: bottomLabels,
            ),
          ),
        ],
      ),
    );
  }
}

class _OverviewTrendChart extends StatelessWidget {
  const _OverviewTrendChart({
    required this.spots,
    required this.bottomLabels,
  });

  final List<SpendingTrendSpot> spots;
  final List<String> bottomLabels;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final flSpots = spots
        .map((SpendingTrendSpot s) => FlSpot(s.x, s.y))
        .toList(growable: false);

    return LineChart(
      LineChartData(
        minX: 0,
        maxX: spots.isEmpty ? 4 : spots.last.x,
        minY: 0,
        maxY: spots.isEmpty
            ? 500
            : (spots.map((s) => s.y).reduce(math.max) * 1.2)
                .ceilToDouble()
                .clamp(100, double.infinity),
        lineTouchData: const LineTouchData(enabled: false),
        gridData: FlGridData(
          show: true,
          drawVerticalLine: false,
          horizontalInterval: 100,
          getDrawingHorizontalLine: (_) => FlLine(
            color: c.spendingTrendGrid,
            strokeWidth: 1,
          ),
        ),
        titlesData: FlTitlesData(
          leftTitles: AxisTitles(
            sideTitles: SideTitles(
              showTitles: true,
              reservedSize: 40,
              interval: 100,
              getTitlesWidget: (double value, TitleMeta meta) =>
                  _buildLeftTitle(value, meta, c.muted),
            ),
          ),
          rightTitles: const AxisTitles(
            sideTitles: SideTitles(showTitles: false),
          ),
          topTitles: const AxisTitles(
            sideTitles: SideTitles(showTitles: false),
          ),
          bottomTitles: AxisTitles(
            sideTitles: SideTitles(
              showTitles: true,
              reservedSize: 30,
              interval: 1,
              getTitlesWidget: (double value, TitleMeta meta) =>
                  _buildBottomTitle(value, meta, c.muted),
            ),
          ),
        ),
        borderData: FlBorderData(
          show: true,
          border: Border(
            bottom: BorderSide(color: c.spendingTrendGrid),
            left: BorderSide(color: c.spendingTrendGrid),
          ),
        ),
        lineBarsData: <LineChartBarData>[
          LineChartBarData(
            spots: flSpots,
            isCurved: true,
            gradient:
                LinearGradient(colors: <Color>[c.primary, c.primaryHover]),
            barWidth: 4,
            isStrokeCapRound: true,
            dotData: FlDotData(
              show: true,
              getDotPainter: (
                FlSpot spot,
                double percent,
                LineChartBarData barData,
                int index,
              ) {
                return FlDotCirclePainter(
                  radius: 4,
                  color: index == flSpots.length - 1
                      ? c.accentBrown
                      : c.primary,
                  strokeWidth: 2,
                  strokeColor: c.surfaceBase,
                );
              },
            ),
            belowBarData: BarAreaData(
              show: true,
              gradient: LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                colors: <Color>[
                  c.primary.withValues(alpha: 0.18),
                  c.primary.withValues(alpha: 0.02),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildLeftTitle(double value, TitleMeta meta, Color textColor) {
    if (value == 0 || value == 500) {
      return const SizedBox.shrink();
    }

    return SideTitleWidget(
      meta: meta,
      child: Text(
        '\u00A3${value.toInt()}',
        style: TextStyle(
          color: textColor,
          fontSize: 11,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }

  Widget _buildBottomTitle(double value, TitleMeta meta, Color textColor) {
    final int index = value.toInt();

    if (index < 0 || index >= bottomLabels.length) {
      return const SizedBox.shrink();
    }

    return SideTitleWidget(
      meta: meta,
      child: Text(
        bottomLabels[index],
        style: TextStyle(
          color: textColor,
          fontSize: 11,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  AI insight card
// ─────────────────────────────────────────────────────────

class _InsightCard extends StatelessWidget {
  const _InsightCard({
    required this.title,
    required this.body,
  });

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        gradient: c.spendingInsightGradient,
        borderRadius: const BorderRadius.all(Radius.circular(24)),
        border: Border.all(color: c.spendingInsightBorder),
        boxShadow: PayaboShadows.soft,
      ),
      child: Padding(
        padding: const EdgeInsets.all(PayaboSpacing.xl),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Container(
              width: 48,
              height: 48,
              decoration: BoxDecoration(
                color: c.surfaceBase.withValues(alpha: c.isDark ? 0.92 : 0.72),
                borderRadius: BorderRadius.circular(16),
              ),
              child: Icon(
                Icons.auto_awesome_rounded,
                color: c.primary,
              ),
            ),
            const SizedBox(width: PayaboSpacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    'AI insight',
                    style: Theme.of(context).textTheme.labelLarge?.copyWith(
                          color: c.spendingInsightLabel,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.xs),
                  Text(
                    title,
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                          color: c.accentBrown,
                          height: 1.45,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.sm),
                  Text(
                    body,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: c.accentBrownMuted,
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

// ─────────────────────────────────────────────────────────
//  Monthly overview card (allocation ring)
// ─────────────────────────────────────────────────────────

class _MonthlyOverviewCard extends StatelessWidget {
  const _MonthlyOverviewCard({
    required this.slices,
    required this.monthLabel,
    required this.yearLabel,
    required this.chipLabel,
  });

  final List<SpendingAllocationSlice> slices;
  final String monthLabel;
  final String yearLabel;
  final String chipLabel;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: c.isDark
              ? <Color>[c.surfaceCardElevated, c.surfaceWarmElevated]
              : const <Color>[Color(0xFFFFFCF7), Color(0xFFFFF2E3)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: c.borderWarm),
        boxShadow: PayaboShadows.soft,
      ),
      child: ClipRRect(
        borderRadius: PayaboRadii.radiusSm,
        child: Stack(
          children: <Widget>[
            Positioned(
              top: -18,
              right: -24,
              child: Container(
                width: 120,
                height: 120,
                decoration: BoxDecoration(
                  color: c.primary.withValues(alpha: 0.08),
                  shape: BoxShape.circle,
                ),
              ),
            ),
            Positioned(
              left: -34,
              bottom: -44,
              child: Container(
                width: 144,
                height: 144,
                decoration: BoxDecoration(
                  color: c.info.withValues(alpha: 0.06),
                  shape: BoxShape.circle,
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(PayaboSpacing.xl),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      Expanded(
                        child: Text(
                          'Overview',
                          style:
                              Theme.of(context).textTheme.titleLarge?.copyWith(
                                    color: c.accentBrown,
                                    fontWeight: FontWeight.w700,
                                  ),
                        ),
                      ),
                      _OverviewMonthChip(label: chipLabel),
                    ],
                  ),
                  const SizedBox(height: PayaboSpacing.lg),
                  Center(
                    child: _OverviewAllocationRing(
                      slices: slices,
                      monthLabel: monthLabel,
                      yearLabel: yearLabel,
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.xl),
                  ...slices.map(
                    (SpendingAllocationSlice slice) => Padding(
                      padding: const EdgeInsets.only(bottom: PayaboSpacing.md),
                      child: _OverviewAllocationRow(slice: slice),
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

class _OverviewMonthChip extends StatelessWidget {
  const _OverviewMonthChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.md,
        vertical: 10,
      ),
      decoration: BoxDecoration(
        color: c.surfaceBase.withValues(alpha: 0.82),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: c.borderWarm),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Text(
            label,
            style: Theme.of(context).textTheme.labelLarge?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
          ),
        ],
      ),
    );
  }
}

class _OverviewAllocationRing extends StatelessWidget {
  const _OverviewAllocationRing({
    required this.slices,
    required this.monthLabel,
    required this.yearLabel,
  });

  final List<SpendingAllocationSlice> slices;
  final String monthLabel;
  final String yearLabel;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return SizedBox(
      width: 220,
      height: 220,
      child: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          CustomPaint(
            size: const Size.square(220),
            painter: _OverviewAllocationRingPainter(
              slices: slices,
              trackColor: c.borderWarm,
              colorResolver: c,
            ),
          ),
          Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                monthLabel,
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: c.accentBrown,
                      fontWeight: FontWeight.w700,
                    ),
              ),
              const SizedBox(height: 2),
              Text(
                yearLabel,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: c.muted,
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

class _OverviewAllocationRingPainter extends CustomPainter {
  const _OverviewAllocationRingPainter({
    required this.slices,
    required this.trackColor,
    required this.colorResolver,
  });

  static const double _gapRadians = 0.22;
  static const double _strokeWidth = 16;

  final List<SpendingAllocationSlice> slices;
  final Color trackColor;
  final PayaboColorResolver colorResolver;

  @override
  void paint(Canvas canvas, Size size) {
    final Offset center = Offset(size.width / 2, size.height / 2);
    final double radius = (math.min(size.width, size.height) / 2) - 18;
    final Rect rect = Rect.fromCircle(center: center, radius: radius);
    final Paint trackPaint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = _strokeWidth
      ..strokeCap = StrokeCap.round
      ..color = trackColor;

    canvas.drawArc(rect, 0, math.pi * 2, false, trackPaint);

    final double total = slices.fold<double>(
      0,
      (double sum, SpendingAllocationSlice slice) => sum + slice.value,
    );
    final double totalSweep = (math.pi * 2) - (slices.length * _gapRadians);
    double startAngle = -math.pi / 2;

    for (final SpendingAllocationSlice slice in slices) {
      final double sweepAngle =
          total == 0 ? 0 : totalSweep * (slice.value / total).clamp(0.0, 1.0);
      final Paint slicePaint = Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = _strokeWidth
        ..strokeCap = StrokeCap.round
        ..color = _resolveSliceColor(slice.colorKey, colorResolver);

      canvas.drawArc(rect, startAngle, sweepAngle, false, slicePaint);
      startAngle += sweepAngle + _gapRadians;
    }
  }

  @override
  bool shouldRepaint(covariant _OverviewAllocationRingPainter oldDelegate) {
    return oldDelegate.slices != slices ||
        oldDelegate.trackColor != trackColor ||
        oldDelegate.colorResolver != colorResolver;
  }
}

class _OverviewAllocationRow extends StatelessWidget {
  const _OverviewAllocationRow({required this.slice});

  final SpendingAllocationSlice slice;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final color = _resolveSliceColor(slice.colorKey, c);

    return Row(
      children: <Widget>[
        Container(
          width: 12,
          height: 12,
          decoration: BoxDecoration(color: color, shape: BoxShape.circle),
        ),
        const SizedBox(width: PayaboSpacing.sm),
        Expanded(
          child: Text(
            slice.label,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  color: c.accentBrown,
                ),
          ),
        ),
        Text(
          slice.amountLabel,
          style: Theme.of(context).textTheme.titleSmall?.copyWith(
                color: c.accentBrown,
                fontWeight: FontWeight.w700,
              ),
        ),
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Recent transactions
// ─────────────────────────────────────────────────────────

class _RecentTransactionsCard extends StatelessWidget {
  const _RecentTransactionsCard({
    required this.transactions,
    required this.onViewAllTap,
  });

  final List<SpendingRecentTransaction> transactions;
  final VoidCallback onViewAllTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return PayaboCard(
      backgroundColor: c.spendingCardWarmElevated,
      padding: const EdgeInsets.all(PayaboSpacing.lg),
      child: Column(
        children: <Widget>[
          ...transactions.asMap().entries.map(
            (MapEntry<int, SpendingRecentTransaction> entry) {
              final bool isLast = entry.key == transactions.length - 1;

              return Column(
                children: <Widget>[
                  _RecentTransactionRow(transaction: entry.value),
                  if (!isLast)
                    Divider(
                      height: PayaboSpacing.xl,
                      color: c.borderStrong.withValues(alpha: 0.6),
                    ),
                ],
              );
            },
          ),
          const SizedBox(height: PayaboSpacing.lg),
          SizedBox(
            width: double.infinity,
            height: 50,
            child: OutlinedButton(
              onPressed: onViewAllTap,
              style: OutlinedButton.styleFrom(
                foregroundColor: c.accentBrown,
                side: BorderSide(color: c.spendingQuickActionBorder),
                backgroundColor: c.surfaceBase,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(18),
                ),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: <Widget>[
                  Text(
                    'View all transactions',
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(
                          color: c.accentBrown,
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                  const SizedBox(width: PayaboSpacing.sm),
                  const Icon(Icons.arrow_forward_rounded),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _RecentTransactionRow extends StatelessWidget {
  const _RecentTransactionRow({required this.transaction});

  final SpendingRecentTransaction transaction;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final bgColor = _resolveIconBackground(transaction.iconBackgroundKey, c);
    final fgColor = _resolveIconForeground(transaction.iconForegroundKey, c);

    return Row(
      children: <Widget>[
        Container(
          width: 46,
          height: 46,
          decoration: BoxDecoration(
            color: bgColor,
            shape: BoxShape.circle,
          ),
          alignment: Alignment.center,
          child: Text(
            transaction.iconText,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  color: fgColor,
                  fontWeight: FontWeight.w700,
                ),
          ),
        ),
        const SizedBox(width: PayaboSpacing.md),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                transaction.merchant,
                style: Theme.of(context).textTheme.titleSmall?.copyWith(
                      color: c.ink,
                    ),
              ),
              const SizedBox(height: PayaboSpacing.xxs),
              Text(
                _recentTransactionCategoryLabel(transaction),
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: c.muted,
                    ),
              ),
            ],
          ),
        ),
        const SizedBox(width: PayaboSpacing.md),
        Text(
          transaction.amountLabel,
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: c.accentBrown,
                fontWeight: FontWeight.w700,
              ),
        ),
      ],
    );
  }
}

/// Builds the category label for a recent transaction row.
///
/// Returns "Category · Subcategory" when a known subcategory is present,
/// otherwise just the category display name.
String _recentTransactionCategoryLabel(SpendingRecentTransaction transaction) {
  final String catName = categoryDisplayName(transaction.category);
  final String? subName =
      subCategoryDisplayName(transaction.category, transaction.subCategory);
  return subName != null ? '$catName · $subName' : catName;
}
