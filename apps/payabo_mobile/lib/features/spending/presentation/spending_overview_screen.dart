import 'dart:math' as math;

import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../app/demo/demo_mode.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_card.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
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
//  Demo data — only used when isDemoProvider is true
// ─────────────────────────────────────────────────────────

List<_AccountSnapshot> _demoAccountSnapshots(PayaboColorResolver c) =>
    <_AccountSnapshot>[
      _AccountSnapshot(
        label: 'Current account',
        balanceLabel: '\u00A33,842.16',
        statusLabel: 'Primary',
        changeLabel: '+\u00A3186.40 this week',
        gradientColors: c.spendingAccountGradientPrimary,
        accentColor: c.spendingAccountAccentPrimary,
        icon: Icons.account_balance_wallet_outlined,
      ),
      _AccountSnapshot(
        label: 'Rainy day fund',
        balanceLabel: '\u00A36,240.00',
        statusLabel: 'Savings',
        changeLabel: '+\u00A3120.00 auto-saved',
        gradientColors: c.spendingAccountGradientSavings,
        accentColor: c.spendingAccountAccentSavings,
        icon: Icons.savings_outlined,
      ),
      _AccountSnapshot(
        label: 'Bills pocket',
        balanceLabel: '\u00A31,090.30',
        statusLabel: 'Bills',
        changeLabel: 'Covers 6 upcoming payments',
        gradientColors: c.spendingAccountGradientBills,
        accentColor: c.spendingAccountAccentBills,
        icon: Icons.receipt_long_outlined,
      ),
    ];

List<_BreakdownSlice> _demoBreakdownSlices(PayaboColorResolver c) =>
    <_BreakdownSlice>[
      _BreakdownSlice(
        label: 'Food',
        amountLabel: '\u00A3570',
        value: 31,
        color: c.primary,
      ),
      _BreakdownSlice(
        label: 'Bills',
        amountLabel: '\u00A3410',
        value: 22,
        color: c.spendingSliceBills,
      ),
      _BreakdownSlice(
        label: 'Transport',
        amountLabel: '\u00A3312',
        value: 17,
        color: c.success,
      ),
      _BreakdownSlice(
        label: 'Shopping',
        amountLabel: '\u00A3260',
        value: 14,
        color: c.info,
      ),
      _BreakdownSlice(
        label: 'Other',
        amountLabel: '\u00A3288',
        value: 16,
        color: c.spendingSliceOther,
      ),
    ];

List<_RecentTransactionPreview> _demoRecentTransactions(
  PayaboColorResolver c,
) =>
    <_RecentTransactionPreview>[
      _RecentTransactionPreview(
        merchant: 'Uber',
        category: 'Pending ride',
        amountLabel: '\u00A314.20',
        iconText: 'U',
        iconBackground: c.spendingMerchantIconDark,
        iconForeground: c.surfaceBase,
      ),
      _RecentTransactionPreview(
        merchant: 'Amazon',
        category: 'Shopping',
        amountLabel: '\u00A311.00',
        iconText: 'a',
        iconBackground: c.spendingMerchantIconWarmSurface,
        iconForeground: c.spendingMerchantIconDark,
      ),
      _RecentTransactionPreview(
        merchant: 'Nando\'s',
        category: 'Food and dining',
        amountLabel: '\u00A328.45',
        iconText: 'N',
        iconBackground: c.spendingMerchantIconWarmAccent,
        iconForeground: c.spendingMerchantIconWarmText,
      ),
    ];

List<_OverviewAllocationSlice> _demoOverviewAllocationSlices(
  PayaboColorResolver c,
) =>
    <_OverviewAllocationSlice>[
      _OverviewAllocationSlice(
        label: 'Income',
        amountLabel: '\u00A34,232.24',
        value: 4232.24,
        color: c.success,
      ),
      _OverviewAllocationSlice(
        label: 'Expenses',
        amountLabel: '\u00A32,660.12',
        value: 2660.12,
        color: c.primary,
      ),
      _OverviewAllocationSlice(
        label: 'Investments',
        amountLabel: '\u00A31,754.64',
        value: 1754.64,
        color: c.info,
      ),
    ];

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
    final isDemo = ref.watch(isDemoProvider);
    final isFreshDemo =
        isDemo && ref.watch(demoDataModeProvider) == DemoDataMode.fresh;

    // In non-demo mode these would come from the API via a repository.
    // For now the screen shows empty states when not in demo mode.
    final bool showPopulated = isDemo && !isFreshDemo;

    final accountSnapshots =
        showPopulated ? _demoAccountSnapshots(c) : <_AccountSnapshot>[];
    final breakdownSlices =
        showPopulated ? _demoBreakdownSlices(c) : <_BreakdownSlice>[];
    final transactions = showPopulated
        ? _demoRecentTransactions(c)
        : <_RecentTransactionPreview>[];
    final allocationSlices = showPopulated
        ? _demoOverviewAllocationSlices(c)
        : <_OverviewAllocationSlice>[];

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
                child: ListView(
                  padding: const EdgeInsets.fromLTRB(
                    PayaboSpacing.xl,
                    PayaboSpacing.sm,
                    PayaboSpacing.xl,
                    PayaboSpacing.x4,
                  ),
                  children: _buildBody(
                    context: context,
                    c: c,
                    isDemo: isDemo,
                    isFreshDemo: isFreshDemo,
                    showPopulated: showPopulated,
                    accountSnapshots: accountSnapshots,
                    breakdownSlices: breakdownSlices,
                    transactions: transactions,
                    allocationSlices: allocationSlices,
                  ),
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
    required bool isDemo,
    required bool isFreshDemo,
    required bool showPopulated,
    required List<_AccountSnapshot> accountSnapshots,
    required List<_BreakdownSlice> breakdownSlices,
    required List<_RecentTransactionPreview> transactions,
    required List<_OverviewAllocationSlice> allocationSlices,
  }) {
    // Fresh demo → single explanatory card
    if (isFreshDemo) {
      return const <Widget>[_FreshOverviewStateCard()];
    }

    // Live mode with no data → empty state
    if (!isDemo) {
      return const <Widget>[_LiveEmptyOverviewState()];
    }

    // Populated demo mode → full showcase
    return <Widget>[
      SizedBox(
        height: 204,
        child: PageView.builder(
          controller: _accountController,
          itemCount: accountSnapshots.length,
          onPageChanged: (int index) {
            setState(() => _accountPage = index);
          },
          itemBuilder: (BuildContext context, int index) {
            return Padding(
              padding: const EdgeInsets.only(right: PayaboSpacing.md),
              child: _AccountSnapshotCard(snapshot: accountSnapshots[index]),
            );
          },
        ),
      ),
      const SizedBox(height: PayaboSpacing.md),
      _AccountPagerDots(
        count: accountSnapshots.length,
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
      const Row(
        children: <Widget>[
          Expanded(
            child: _MetricCard(
              label: 'Total balance',
              amountLabel: '\u00A311,172.46',
              trendLabel: '+4.6% vs last month',
              icon: Icons.stacked_line_chart,
            ),
          ),
          SizedBox(width: PayaboSpacing.md),
          Expanded(
            child: _MetricCard(
              label: 'Net worth',
              amountLabel: '\u00A318,406.20',
              trendLabel: '+\u00A3620 this month',
              icon: Icons.diamond_outlined,
            ),
          ),
        ],
      ),
      const SizedBox(height: PayaboSpacing.md),
      const _SafeToSpendCard(),
      const SizedBox(height: PayaboSpacing.xl),
      const _SectionHeading(
        title: 'Monthly breakdown',
        subtitle: 'Where this month is going so far',
      ),
      const SizedBox(height: PayaboSpacing.md),
      _MonthlyBreakdownCard(slices: breakdownSlices),
      const SizedBox(height: PayaboSpacing.xl),
      const _SectionHeading(
        title: 'Spending trend',
        subtitle: 'Week-by-week movement this month',
      ),
      const SizedBox(height: PayaboSpacing.md),
      const _TrendCard(),
      const SizedBox(height: PayaboSpacing.xl),
      const _SectionHeading(
        title: 'Quick insights',
        subtitle: 'AI-generated nudges from your spending patterns',
      ),
      const SizedBox(height: PayaboSpacing.md),
      const _InsightCard(),
      const SizedBox(height: PayaboSpacing.xl),
      _MonthlyOverviewCard(slices: allocationSlices),
      const SizedBox(height: PayaboSpacing.xl),
      const _SectionHeading(
        title: 'Recent transactions',
        subtitle: 'A quick preview before you dive into everything',
      ),
      const SizedBox(height: PayaboSpacing.md),
      _RecentTransactionsCard(
        transactions: transactions,
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

  final _AccountSnapshot snapshot;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: snapshot.gradientColors,
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: snapshot.accentColor.withValues(alpha: 0.18)),
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
                  child: Icon(snapshot.icon, color: snapshot.accentColor),
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
                          color: snapshot.accentColor,
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                ),
              ],
            ),
            Text(
              snapshot.balanceLabel,
              style: Theme.of(context).textTheme.displayMedium?.copyWith(
                    color: snapshot.accentColor,
                    height: 1,
                    fontWeight: FontWeight.w800,
                  ),
            ),
            Text(
              snapshot.label,
              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: snapshot.accentColor,
                    fontWeight: FontWeight.w600,
                  ),
            ),
            Text(
              snapshot.changeLabel,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: snapshot.accentColor.withValues(alpha: 0.8),
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
  const _MetricCard({
    required this.label,
    required this.amountLabel,
    required this.trendLabel,
    required this.icon,
  });

  final String label;
  final String amountLabel;
  final String trendLabel;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return PayaboCard(
      backgroundColor: c.surfaceCardElevated,
      padding: const EdgeInsets.all(PayaboSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(icon, color: c.primary),
          const SizedBox(height: PayaboSpacing.md),
          Text(
            label,
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.muted,
                ),
          ),
          const SizedBox(height: PayaboSpacing.xs),
          Text(
            amountLabel,
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: c.accentBrown,
                  fontWeight: FontWeight.w800,
                ),
          ),
          const SizedBox(height: PayaboSpacing.xs),
          Text(
            trendLabel,
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
  const _SafeToSpendCard();

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
                    '\u00A3820.00',
                    style: Theme.of(context).textTheme.displayMedium?.copyWith(
                          color: Colors.white,
                          fontWeight: FontWeight.w800,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.sm),
                  Text(
                    'After bills, goals, and your weekly safety buffer.',
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
  const _MonthlyBreakdownCard({required this.slices});

  final List<_BreakdownSlice> slices;

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
                          (_BreakdownSlice slice) => PieChartSectionData(
                            value: slice.value,
                            color: slice.color,
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
                      '\u00A31,840',
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
                    (_BreakdownSlice slice) => Padding(
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

  final _BreakdownSlice slice;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Row(
      children: <Widget>[
        Container(
          width: 12,
          height: 12,
          decoration: BoxDecoration(color: slice.color, shape: BoxShape.circle),
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
  const _TrendCard();

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
            'Spend is tracking 6% lower than last month.',
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  color: c.accentBrown,
                ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          const SizedBox(height: 220, child: _OverviewTrendChart()),
        ],
      ),
    );
  }
}

class _OverviewTrendChart extends StatelessWidget {
  const _OverviewTrendChart();

  static const List<FlSpot> _spots = <FlSpot>[
    FlSpot(0, 360),
    FlSpot(1, 410),
    FlSpot(2, 325),
    FlSpot(3, 298),
    FlSpot(4, 340),
  ];

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return LineChart(
      LineChartData(
        minX: 0,
        maxX: 4,
        minY: 0,
        maxY: 500,
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
            spots: _spots,
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
                  color:
                      index == _spots.length - 1 ? c.accentBrown : c.primary,
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
    const List<String> labels = <String>['W1', 'W2', 'W3', 'W4', 'Now'];
    final int index = value.toInt();

    if (index < 0 || index >= labels.length) {
      return const SizedBox.shrink();
    }

    return SideTitleWidget(
      meta: meta,
      child: Text(
        labels[index],
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
  const _InsightCard();

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
                    'Your food spending is 12% higher than usual this week.',
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                          color: c.accentBrown,
                          height: 1.45,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.sm),
                  Text(
                    'Most of the lift came from weekday deliveries after 8pm.',
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
  const _MonthlyOverviewCard({required this.slices});

  final List<_OverviewAllocationSlice> slices;

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
                      const _OverviewMonthChip(label: 'Mar'),
                    ],
                  ),
                  const SizedBox(height: PayaboSpacing.lg),
                  Center(child: _OverviewAllocationRing(slices: slices)),
                  const SizedBox(height: PayaboSpacing.xl),
                  ...slices.map(
                    (_OverviewAllocationSlice slice) => Padding(
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
          const SizedBox(width: PayaboSpacing.xs),
          Icon(
            Icons.keyboard_arrow_down_rounded,
            size: 18,
            color: c.accentBrown,
          ),
        ],
      ),
    );
  }
}

class _OverviewAllocationRing extends StatelessWidget {
  const _OverviewAllocationRing({required this.slices});

  final List<_OverviewAllocationSlice> slices;

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
            ),
          ),
          Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                'March',
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: c.accentBrown,
                      fontWeight: FontWeight.w700,
                    ),
              ),
              const SizedBox(height: 2),
              Text(
                '2026',
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
  });

  static const double _gapRadians = 0.22;
  static const double _strokeWidth = 16;

  final List<_OverviewAllocationSlice> slices;
  final Color trackColor;

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
      (double sum, _OverviewAllocationSlice slice) => sum + slice.value,
    );
    final double totalSweep = (math.pi * 2) - (slices.length * _gapRadians);
    double startAngle = -math.pi / 2;

    for (final _OverviewAllocationSlice slice in slices) {
      final double sweepAngle =
          total == 0 ? 0 : totalSweep * (slice.value / total).clamp(0.0, 1.0);
      final Paint slicePaint = Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = _strokeWidth
        ..strokeCap = StrokeCap.round
        ..color = slice.color;

      canvas.drawArc(rect, startAngle, sweepAngle, false, slicePaint);
      startAngle += sweepAngle + _gapRadians;
    }
  }

  @override
  bool shouldRepaint(covariant _OverviewAllocationRingPainter oldDelegate) {
    return oldDelegate.slices != slices || oldDelegate.trackColor != trackColor;
  }
}

class _OverviewAllocationRow extends StatelessWidget {
  const _OverviewAllocationRow({required this.slice});

  final _OverviewAllocationSlice slice;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Row(
      children: <Widget>[
        Container(
          width: 12,
          height: 12,
          decoration: BoxDecoration(color: slice.color, shape: BoxShape.circle),
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

  final List<_RecentTransactionPreview> transactions;
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
            (MapEntry<int, _RecentTransactionPreview> entry) {
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

  final _RecentTransactionPreview transaction;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Row(
      children: <Widget>[
        Container(
          width: 46,
          height: 46,
          decoration: BoxDecoration(
            color: transaction.iconBackground,
            shape: BoxShape.circle,
          ),
          alignment: Alignment.center,
          child: Text(
            transaction.iconText,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  color: transaction.iconForeground,
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
                transaction.category,
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

// ─────────────────────────────────────────────────────────
//  Data models
// ─────────────────────────────────────────────────────────

class _AccountSnapshot {
  const _AccountSnapshot({
    required this.label,
    required this.balanceLabel,
    required this.statusLabel,
    required this.changeLabel,
    required this.gradientColors,
    required this.accentColor,
    required this.icon,
  });

  final String label;
  final String balanceLabel;
  final String statusLabel;
  final String changeLabel;
  final List<Color> gradientColors;
  final Color accentColor;
  final IconData icon;
}

class _BreakdownSlice {
  const _BreakdownSlice({
    required this.label,
    required this.amountLabel,
    required this.value,
    required this.color,
  });

  final String label;
  final String amountLabel;
  final double value;
  final Color color;
}

class _OverviewAllocationSlice {
  const _OverviewAllocationSlice({
    required this.label,
    required this.amountLabel,
    required this.value,
    required this.color,
  });

  final String label;
  final String amountLabel;
  final double value;
  final Color color;
}

class _RecentTransactionPreview {
  const _RecentTransactionPreview({
    required this.merchant,
    required this.category,
    required this.amountLabel,
    required this.iconText,
    required this.iconBackground,
    required this.iconForeground,
  });

  final String merchant;
  final String category;
  final String amountLabel;
  final String iconText;
  final Color iconBackground;
  final Color iconForeground;
}
