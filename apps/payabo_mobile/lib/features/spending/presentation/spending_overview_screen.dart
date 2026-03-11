import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_gradients.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_bottom_nav.dart';
import '../../../shared/widgets/payabo_card.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import '../../../shared/widgets/payabo_modal_sheet.dart';
import 'widgets/spending_section_pills.dart';

const List<_AccountSnapshot> _accountSnapshots = <_AccountSnapshot>[
  _AccountSnapshot(
    label: 'Current account',
    balanceLabel: '£3,842.16',
    statusLabel: 'Primary',
    changeLabel: '+£186.40 this week',
    gradientColors: PayaboColors.spendingAccountGradientPrimary,
    accentColor: PayaboColors.spendingAccountAccentPrimary,
    icon: Icons.account_balance_wallet_outlined,
  ),
  _AccountSnapshot(
    label: 'Rainy day fund',
    balanceLabel: '£6,240.00',
    statusLabel: 'Savings',
    changeLabel: '+£120.00 auto-saved',
    gradientColors: PayaboColors.spendingAccountGradientSavings,
    accentColor: PayaboColors.spendingAccountAccentSavings,
    icon: Icons.savings_outlined,
  ),
  _AccountSnapshot(
    label: 'Bills pocket',
    balanceLabel: '£1,090.30',
    statusLabel: 'Bills',
    changeLabel: 'Covers 6 upcoming payments',
    gradientColors: PayaboColors.spendingAccountGradientBills,
    accentColor: PayaboColors.spendingAccountAccentBills,
    icon: Icons.receipt_long_outlined,
  ),
];

const List<_BreakdownSlice> _breakdownSlices = <_BreakdownSlice>[
  _BreakdownSlice(
    label: 'Food',
    amountLabel: '£570',
    value: 31,
    color: PayaboColors.primary,
  ),
  _BreakdownSlice(
    label: 'Bills',
    amountLabel: '£410',
    value: 22,
    color: PayaboColors.spendingSliceBills,
  ),
  _BreakdownSlice(
    label: 'Transport',
    amountLabel: '£312',
    value: 17,
    color: PayaboColors.success,
  ),
  _BreakdownSlice(
    label: 'Shopping',
    amountLabel: '£260',
    value: 14,
    color: PayaboColors.info,
  ),
  _BreakdownSlice(
    label: 'Other',
    amountLabel: '£288',
    value: 16,
    color: PayaboColors.spendingSliceOther,
  ),
];

const List<_RecentTransactionPreview> _recentTransactions =
    <_RecentTransactionPreview>[
  _RecentTransactionPreview(
    merchant: 'Uber',
    category: 'Pending ride',
    amountLabel: '£14.20',
    iconText: 'U',
    iconBackground: PayaboColors.spendingMerchantIconDark,
    iconForeground: PayaboColors.white,
  ),
  _RecentTransactionPreview(
    merchant: 'Amazon',
    category: 'Shopping',
    amountLabel: '£11.00',
    iconText: 'a',
    iconBackground: PayaboColors.spendingMerchantIconWarmSurface,
    iconForeground: PayaboColors.spendingMerchantIconDark,
  ),
  _RecentTransactionPreview(
    merchant: 'Nando\'s',
    category: 'Food and dining',
    amountLabel: '£28.45',
    iconText: 'N',
    iconBackground: PayaboColors.spendingMerchantIconWarmAccent,
    iconForeground: PayaboColors.spendingMerchantIconWarmText,
  ),
];

class SpendingOverviewScreen extends StatefulWidget {
  const SpendingOverviewScreen({super.key});

  @override
  State<SpendingOverviewScreen> createState() => _SpendingOverviewScreenState();
}

class _SpendingOverviewScreenState extends State<SpendingOverviewScreen> {
  late final PageController _accountController;
  int _navIndex = 2;
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
    return Scaffold(
      backgroundColor: PayaboColors.surfaceWarm,
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: PayaboGradients.warmScreen,
        ),
        child: SafeArea(
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
                  children: <Widget>[
                    SizedBox(
                      height: 210,
                      child: PageView.builder(
                        controller: _accountController,
                        itemCount: _accountSnapshots.length,
                        onPageChanged: (int index) {
                          setState(() {
                            _accountPage = index;
                          });
                        },
                        itemBuilder: (BuildContext context, int index) {
                          return Padding(
                            padding:
                                const EdgeInsets.only(right: PayaboSpacing.md),
                            child: _AccountSnapshotCard(
                              snapshot: _accountSnapshots[index],
                            ),
                          );
                        },
                      ),
                    ),
                    const SizedBox(height: PayaboSpacing.md),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: List<Widget>.generate(
                        _accountSnapshots.length,
                        (int index) => AnimatedContainer(
                          duration: const Duration(milliseconds: 180),
                          width: index == _accountPage ? 18 : 8,
                          height: 8,
                          margin: const EdgeInsets.symmetric(horizontal: 4),
                          decoration: BoxDecoration(
                            color: index == _accountPage
                                ? PayaboColors.primary
                                : PayaboColors.spendingDotInactive,
                            borderRadius: BorderRadius.circular(999),
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(height: PayaboSpacing.x2),
                    _OverviewQuickActions(
                      onAddAccountTap: () => _showSectionComingSoon('Accounts'),
                      onManageAccountsTap: () =>
                          _showSectionComingSoon('Accounts'),
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
                            amountLabel: '£11,172.46',
                            trendLabel: '+4.6% vs last month',
                            icon: Icons.stacked_line_chart,
                          ),
                        ),
                        SizedBox(width: PayaboSpacing.md),
                        Expanded(
                          child: _MetricCard(
                            label: 'Net worth',
                            amountLabel: '£18,406.20',
                            trendLabel: '+£620 this month',
                            icon: Icons.diamond_outlined,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: PayaboSpacing.md),
                    const _SafeToSpendCard(),
                    const SizedBox(height: PayaboSpacing.xl),
                    const _SectionHeading(
                      title: 'Monthly spending breakdown',
                      subtitle: 'Where this month is going so far',
                    ),
                    const SizedBox(height: PayaboSpacing.md),
                    const _MonthlyBreakdownCard(),
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
                      subtitle:
                          'AI-generated nudges from your spending patterns',
                    ),
                    const SizedBox(height: PayaboSpacing.md),
                    const _InsightCard(),
                    const SizedBox(height: PayaboSpacing.xl),
                    const _SectionHeading(
                      title: 'Recent transactions',
                      subtitle:
                          'A quick preview before you dive into everything',
                    ),
                    const SizedBox(height: PayaboSpacing.md),
                    _RecentTransactionsCard(
                      transactions: _recentTransactions,
                      onViewAllTap: () => context.go('/spending/transactions'),
                    ),
                  ],
                ),
              ),
            ],
          ),
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

  void _handleSectionSelected(SpendingSection section) {
    switch (section) {
      case SpendingSection.overview:
        return;
      case SpendingSection.transactions:
        context.go('/spending/transactions');
        return;
      case SpendingSection.budgets:
        _showSectionComingSoon('Budgets');
        return;
      case SpendingSection.accounts:
        _showSectionComingSoon('Accounts');
        return;
    }
  }

  void _showSectionComingSoon(String sectionName) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text('$sectionName view coming soon in mock build.')),
    );
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
}

class _OverviewHeader extends StatelessWidget {
  const _OverviewHeader({required this.onSectionSelected});

  final ValueChanged<SpendingSection> onSectionSelected;

  @override
  Widget build(BuildContext context) {
    return PayaboAppHeader(
      title: 'Spend',
      titleStyle: Theme.of(context).textTheme.headlineMedium?.copyWith(
            fontSize: 48,
            fontWeight: FontWeight.w700,
            color: PayaboColors.accentBrown,
          ),
      bottom: SpendingSectionPills(
        selectedSection: SpendingSection.overview,
        onSelected: onSectionSelected,
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
    return SizedBox(
      width: 130,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(24),
        child: Column(
          children: <Widget>[
            Container(
              width: 72,
              height: 72,
              decoration: BoxDecoration(
                color: PayaboColors.spendingQuickActionSurface,
                shape: BoxShape.circle,
                border:
                    Border.all(color: PayaboColors.spendingQuickActionBorder),
              ),
              child: Icon(icon, color: PayaboColors.primary, size: 32),
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              label,
              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: PayaboColors.accentBrown,
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

class _SectionHeading extends StatelessWidget {
  const _SectionHeading({
    required this.title,
    required this.subtitle,
  });

  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(title, style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: PayaboSpacing.xs),
        Text(
          subtitle,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                color: PayaboColors.muted,
              ),
        ),
      ],
    );
  }
}

class _AccountSnapshotCard extends StatelessWidget {
  const _AccountSnapshotCard({required this.snapshot});

  final _AccountSnapshot snapshot;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: snapshot.gradientColors,
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: const BorderRadius.all(Radius.circular(28)),
        boxShadow: PayaboShadows.medium,
        border: Border.all(color: snapshot.accentColor.withValues(alpha: 0.18)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(PayaboSpacing.lg),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                Container(
                  width: 48,
                  height: 48,
                  decoration: BoxDecoration(
                    color: PayaboColors.white.withValues(alpha: 0.72),
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
                    color: PayaboColors.white.withValues(alpha: 0.72),
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
              style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                    fontSize: 34,
                    height: 1,
                    color: snapshot.accentColor,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.xs),
            Text(
              snapshot.label,
              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: snapshot.accentColor,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.xs),
            Text(
              snapshot.changeLabel,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: snapshot.accentColor.withValues(alpha: 0.75),
                  ),
            ),
          ],
        ),
      ),
    );
  }
}

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
    return PayaboCard(
      backgroundColor: PayaboColors.surfaceWarm,
      padding: const EdgeInsets.all(PayaboSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(icon, color: PayaboColors.primary),
          const SizedBox(height: PayaboSpacing.md),
          Text(label, style: Theme.of(context).textTheme.bodyMedium),
          const SizedBox(height: PayaboSpacing.xs),
          Text(
            amountLabel,
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  fontSize: 24,
                  color: PayaboColors.accentBrown,
                ),
          ),
          const SizedBox(height: PayaboSpacing.xs),
          Text(
            trendLabel,
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: PayaboColors.success,
                  fontWeight: FontWeight.w700,
                ),
          ),
        ],
      ),
    );
  }
}

class _SafeToSpendCard extends StatelessWidget {
  const _SafeToSpendCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        gradient: PayaboGradients.spendingSafeToSpend,
        borderRadius: BorderRadius.all(Radius.circular(24)),
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
                          color: PayaboColors.white,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.xs),
                  Text(
                    '£820.00',
                    style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                          fontSize: 36,
                          color: PayaboColors.white,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.sm),
                  Text(
                    'After bills, goals, and your weekly safety buffer.',
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: PayaboColors.white.withValues(alpha: 0.82),
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
                color: PayaboColors.white.withValues(alpha: 0.14),
                borderRadius: BorderRadius.circular(20),
              ),
              child: const Icon(
                Icons.verified_user_outlined,
                color: PayaboColors.white,
                size: 32,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MonthlyBreakdownCard extends StatelessWidget {
  const _MonthlyBreakdownCard();

  @override
  Widget build(BuildContext context) {
    return PayaboCard(
      backgroundColor: PayaboColors.spendingCardWarm,
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        children: <Widget>[
          Row(
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
                        sections: _breakdownSlices
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
                          '£1,840',
                          style: Theme.of(context)
                              .textTheme
                              .titleLarge
                              ?.copyWith(color: PayaboColors.accentBrown),
                        ),
                        Text(
                          'Spent this month',
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(width: PayaboSpacing.lg),
              Expanded(
                child: Column(
                  children: _breakdownSlices
                      .map(
                        (_BreakdownSlice slice) => Padding(
                          padding:
                              const EdgeInsets.only(bottom: PayaboSpacing.md),
                          child: _BreakdownLegendRow(slice: slice),
                        ),
                      )
                      .toList(growable: false),
                ),
              ),
            ],
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
    return Row(
      children: <Widget>[
        Container(
          width: 12,
          height: 12,
          decoration: BoxDecoration(color: slice.color, shape: BoxShape.circle),
        ),
        const SizedBox(width: PayaboSpacing.sm),
        Expanded(
          child:
              Text(slice.label, style: Theme.of(context).textTheme.titleSmall),
        ),
        Text(
          slice.amountLabel,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                color: PayaboColors.accentBrown,
                fontWeight: FontWeight.w700,
              ),
        ),
      ],
    );
  }
}

class _TrendCard extends StatelessWidget {
  const _TrendCard();

  @override
  Widget build(BuildContext context) {
    return PayaboCard(
      backgroundColor: PayaboColors.spendingCardWarmElevated,
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
                  color: PayaboColors.accentBrown,
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
          getDrawingHorizontalLine: (_) => const FlLine(
            color: PayaboColors.spendingTrendGrid,
            strokeWidth: 1,
          ),
        ),
        titlesData: FlTitlesData(
          leftTitles: AxisTitles(
            sideTitles: SideTitles(
              showTitles: true,
              reservedSize: 40,
              interval: 100,
              getTitlesWidget: _buildLeftTitle,
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
              getTitlesWidget: _buildBottomTitle,
            ),
          ),
        ),
        borderData: FlBorderData(
          show: true,
          border: const Border(
            bottom: BorderSide(color: PayaboColors.spendingTrendGrid),
            left: BorderSide(color: PayaboColors.spendingTrendGrid),
          ),
        ),
        lineBarsData: <LineChartBarData>[
          LineChartBarData(
            spots: _spots,
            isCurved: true,
            gradient: const LinearGradient(
              colors: <Color>[PayaboColors.primary, PayaboColors.primaryHover],
            ),
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
                  color: index == _spots.length - 1
                      ? PayaboColors.accentBrown
                      : PayaboColors.primary,
                  strokeWidth: 2,
                  strokeColor: PayaboColors.white,
                );
              },
            ),
            belowBarData: BarAreaData(
              show: true,
              gradient: LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                colors: <Color>[
                  PayaboColors.primary.withValues(alpha: 0.18),
                  PayaboColors.primary.withValues(alpha: 0.02),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildLeftTitle(double value, TitleMeta meta) {
    if (value == 0 || value == 500) {
      return const SizedBox.shrink();
    }

    return SideTitleWidget(
      meta: meta,
      child: Text(
        '£${value.toInt()}',
        style: const TextStyle(
          color: PayaboColors.muted,
          fontSize: 11,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }

  Widget _buildBottomTitle(double value, TitleMeta meta) {
    const List<String> labels = <String>['W1', 'W2', 'W3', 'W4', 'Now'];
    final int index = value.toInt();

    if (index < 0 || index >= labels.length) {
      return const SizedBox.shrink();
    }

    return SideTitleWidget(
      meta: meta,
      child: Text(
        labels[index],
        style: const TextStyle(
          color: PayaboColors.muted,
          fontSize: 11,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}

class _InsightCard extends StatelessWidget {
  const _InsightCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        gradient: PayaboGradients.spendingInsight,
        borderRadius: const BorderRadius.all(Radius.circular(24)),
        border: Border.all(color: PayaboColors.spendingInsightBorder),
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
                color: PayaboColors.white.withValues(alpha: 0.72),
                borderRadius: BorderRadius.circular(16),
              ),
              child: const Icon(
                Icons.auto_awesome_rounded,
                color: PayaboColors.primary,
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
                          color: PayaboColors.spendingInsightLabel,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.xs),
                  Text(
                    'Your food spending is 12% higher than usual this week.',
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                          color: PayaboColors.accentBrown,
                          height: 1.45,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.sm),
                  Text(
                    'Most of the lift came from weekday deliveries after 8pm.',
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: PayaboColors.accentBrownMuted,
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

class _RecentTransactionsCard extends StatelessWidget {
  const _RecentTransactionsCard({
    required this.transactions,
    required this.onViewAllTap,
  });

  final List<_RecentTransactionPreview> transactions;
  final VoidCallback onViewAllTap;

  @override
  Widget build(BuildContext context) {
    return PayaboCard(
      backgroundColor: PayaboColors.spendingCardWarmElevated,
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.lg,
        PayaboSpacing.lg,
        PayaboSpacing.lg,
        PayaboSpacing.lg,
      ),
      child: Column(
        children: <Widget>[
          ...transactions.asMap().entries.map(
            (MapEntry<int, _RecentTransactionPreview> entry) {
              final bool isLast = entry.key == transactions.length - 1;

              return Column(
                children: <Widget>[
                  _RecentTransactionRow(transaction: entry.value),
                  if (!isLast) const Divider(height: PayaboSpacing.xl),
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
                foregroundColor: PayaboColors.accentBrown,
                side: const BorderSide(
                    color: PayaboColors.spendingQuickActionBorder),
                backgroundColor: PayaboColors.white,
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
                          color: PayaboColors.accentBrown,
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
                      color: PayaboColors.ink,
                    ),
              ),
              const SizedBox(height: PayaboSpacing.xxs),
              Text(
                transaction.category,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: PayaboColors.muted,
                    ),
              ),
            ],
          ),
        ),
        const SizedBox(width: PayaboSpacing.md),
        Text(
          transaction.amountLabel,
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: PayaboColors.accentBrown,
                fontWeight: FontWeight.w700,
              ),
        ),
      ],
    );
  }
}

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
