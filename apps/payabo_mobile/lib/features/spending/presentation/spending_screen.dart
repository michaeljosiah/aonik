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
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import '../../../shared/widgets/payabo_modal_sheet.dart';
import 'widgets/spending_section_pills.dart';

const List<String> _monthFilters = <String>[
  'Dec',
  'Jan',
  'Feb',
  'Mar',
  'Custom',
];

const List<_SpendingBreakdownItem> _categoryItems = <_SpendingBreakdownItem>[
  _SpendingBreakdownItem(
    id: 'finances',
    name: 'Finances',
    transactionCount: 30,
    totalAmount: '£2,190.72',
    changeAmount: '£148.60',
    isDecrease: true,
    icon: Icons.currency_pound,
    iconColor: PayaboColors.success,
  ),
  _SpendingBreakdownItem(
    id: 'shopping',
    name: 'Shopping',
    transactionCount: 13,
    totalAmount: '£1,770.57',
    changeAmount: '£1,209.34',
    isDecrease: false,
    icon: Icons.shopping_bag_outlined,
    iconColor: PayaboColors.info,
  ),
  _SpendingBreakdownItem(
    id: 'groceries',
    name: 'Groceries',
    transactionCount: 22,
    totalAmount: '£505.10',
    changeAmount: '£42.80',
    isDecrease: true,
    icon: Icons.local_grocery_store_outlined,
    iconColor: PayaboColors.success,
  ),
  _SpendingBreakdownItem(
    id: 'transport',
    name: 'Transport',
    transactionCount: 9,
    totalAmount: '£312.44',
    changeAmount: '£65.20',
    isDecrease: false,
    icon: Icons.directions_car_outlined,
    iconColor: PayaboColors.warning,
  ),
];

const List<_SpendingBreakdownItem> _merchantItems = <_SpendingBreakdownItem>[
  _SpendingBreakdownItem(
    id: 'amazon',
    name: 'Amazon',
    transactionCount: 6,
    totalAmount: '£410.90',
    changeAmount: '£98.20',
    isDecrease: false,
    icon: Icons.shopping_cart_outlined,
    iconColor: PayaboColors.info,
  ),
  _SpendingBreakdownItem(
    id: 'tesco',
    name: 'Tesco',
    transactionCount: 11,
    totalAmount: '£284.35',
    changeAmount: '£21.30',
    isDecrease: true,
    icon: Icons.local_grocery_store_outlined,
    iconColor: PayaboColors.success,
  ),
  _SpendingBreakdownItem(
    id: 'uber',
    name: 'Uber',
    transactionCount: 5,
    totalAmount: '£126.40',
    changeAmount: '£18.00',
    isDecrease: false,
    icon: Icons.local_taxi_outlined,
    iconColor: PayaboColors.warning,
  ),
  _SpendingBreakdownItem(
    id: 'netflix',
    name: 'Netflix',
    transactionCount: 1,
    totalAmount: '£12.99',
    changeAmount: '£0.00',
    isDecrease: true,
    icon: Icons.ondemand_video_outlined,
    iconColor: PayaboColors.primary,
  ),
];

class SpendingScreen extends StatefulWidget {
  const SpendingScreen({super.key});

  @override
  State<SpendingScreen> createState() => _SpendingScreenState();
}

class _SpendingScreenState extends State<SpendingScreen> {
  int _navIndex = 2;
  int _monthIndex = 2;
  int _breakdownViewIndex = 0;

  @override
  Widget build(BuildContext context) {
    final List<_SpendingBreakdownItem> breakdownItems =
        _breakdownViewIndex == 0 ? _categoryItems : _merchantItems;

    const String summaryTitle = 'February spend';
    const String summaryAmount = '£672.97';
    const Color summaryColor = PayaboColors.primaryHover;
    const String compareAmount = '£518.97';
    const String compareLabel = 'vs. January';

    return Scaffold(
      backgroundColor: PayaboColors.surfaceWarm,
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: PayaboGradients.warmScreen,
        ),
        child: SafeArea(
          child: Column(
            children: <Widget>[
              _SpendingHeader(
                onSectionSelected: _handleSectionSelected,
                onNotificationsTap: () => context.push('/notifications'),
                onProfileTap: () => context.go('/profile'),
              ),
              Expanded(
                child: ListView(
                  padding: const EdgeInsets.fromLTRB(
                    PayaboSpacing.xl,
                    PayaboSpacing.md,
                    PayaboSpacing.xl,
                    PayaboSpacing.x4,
                  ),
                  children: <Widget>[
                    _TransactionsHeroCard(
                      summaryTitle: summaryTitle,
                      summaryAmount: summaryAmount,
                      compareAmount: compareAmount,
                      compareLabel: compareLabel,
                      summaryColor: summaryColor,
                      onPersonaliseTap: _showInfoMessage,
                    ),
                    const SizedBox(height: PayaboSpacing.lg),
                    _MonthFilterRow(
                      selectedIndex: _monthIndex,
                      onSelected: (int index) {
                        setState(() {
                          _monthIndex = index;
                        });
                      },
                    ),
                    const SizedBox(height: PayaboSpacing.xl),
                    const SizedBox(height: 230, child: _SpendingTrendChart()),
                    const SizedBox(height: PayaboSpacing.xl),
                    _SegmentControl(
                      leftLabel: 'Categories',
                      rightLabel: 'Merchants',
                      selectedIndex: _breakdownViewIndex,
                      onChanged: (int index) {
                        setState(() {
                          _breakdownViewIndex = index;
                        });
                      },
                      backgroundColor: const Color(0xFFFFF7F0),
                      selectedColor: const Color(0xFFFFE7D3),
                      selectedTextColor: PayaboColors.accentBrown,
                      unselectedTextColor: PayaboColors.accentBrownMuted,
                    ),
                    const SizedBox(height: PayaboSpacing.lg),
                    Row(
                      children: <Widget>[
                        Expanded(
                          child: PayaboButton(
                            label: 'Edit custom categories',
                            variant: PayaboButtonVariant.link,
                            expand: true,
                            leading: const Icon(Icons.edit_outlined, size: 18),
                            onPressed: () {},
                          ),
                        ),
                        const SizedBox(width: PayaboSpacing.md),
                        const _CurrencySortButton(),
                      ],
                    ),
                    const SizedBox(height: PayaboSpacing.lg),
                    ...breakdownItems.map(
                      (_SpendingBreakdownItem item) => Padding(
                        padding:
                            const EdgeInsets.only(bottom: PayaboSpacing.sm),
                        child: PayaboListRow(
                          title: item.name,
                          subtitle: '${item.transactionCount} Transactions',
                          leading: _BreakdownIcon(
                            icon: item.icon,
                            color: item.iconColor,
                          ),
                          trailing: SizedBox(
                            width: 128,
                            child: _BreakdownAmount(
                              totalAmount: item.totalAmount,
                              changeAmount: item.changeAmount,
                              isDecrease: item.isDecrease,
                            ),
                          ),
                          onTap: () {
                            if (_breakdownViewIndex == 0) {
                              context.go('/spending/category/${item.id}');
                              return;
                            }

                            context.go('/spending/merchant/${item.id}');
                          },
                        ),
                      ),
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
        context.go('/spending');
        return;
      case SpendingSection.transactions:
        return;
      case SpendingSection.budgets:
        _showSectionComingSoon('Budgets');
        return;
      case SpendingSection.accounts:
        _showSectionComingSoon('Accounts');
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

  void _showInfoMessage() {
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
          content: Text('Spending insights are mocked in this build.')),
    );
  }

  void _showSectionComingSoon(String sectionName) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text('$sectionName view coming soon in mock build.')),
    );
  }
}

class _SpendingHeader extends StatelessWidget {
  const _SpendingHeader({
    required this.onSectionSelected,
    required this.onNotificationsTap,
    required this.onProfileTap,
  });

  final ValueChanged<SpendingSection> onSectionSelected;
  final VoidCallback onNotificationsTap;
  final VoidCallback onProfileTap;

  @override
  Widget build(BuildContext context) {
    return PayaboAppHeader(
      title: 'Spend',
      titleStyle: Theme.of(context).textTheme.headlineMedium?.copyWith(
            fontSize: 48,
            fontWeight: FontWeight.w700,
            color: PayaboColors.accentBrown,
          ),
      onNotificationsTap: onNotificationsTap,
      onProfileTap: onProfileTap,
      bottom: SpendingSectionPills(
        selectedSection: SpendingSection.transactions,
        onSelected: onSectionSelected,
      ),
    );
  }
}

class _TransactionsHeroCard extends StatelessWidget {
  const _TransactionsHeroCard({
    required this.summaryTitle,
    required this.summaryAmount,
    required this.compareAmount,
    required this.compareLabel,
    required this.summaryColor,
    required this.onPersonaliseTap,
  });

  final String summaryTitle;
  final String summaryAmount;
  final String compareAmount;
  final String compareLabel;
  final Color summaryColor;
  final VoidCallback onPersonaliseTap;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFFFFFBF8),
        borderRadius: const BorderRadius.all(Radius.circular(28)),
        border: Border.all(color: const Color(0xFFF1DEC9)),
        boxShadow: PayaboShadows.soft,
      ),
      child: Padding(
        padding: const EdgeInsets.all(PayaboSpacing.xl),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        summaryTitle,
                        style:
                            Theme.of(context).textTheme.titleMedium?.copyWith(
                                  color: PayaboColors.accentBrownMuted,
                                ),
                      ),
                      const SizedBox(height: PayaboSpacing.xs),
                      Text(
                        'Filtered view for your latest money movement.',
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              color: PayaboColors.muted,
                            ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: PayaboSpacing.md),
                _HeroActionButton(onTap: onPersonaliseTap),
              ],
            ),
            const SizedBox(height: PayaboSpacing.xl),
            Text(
              summaryAmount,
              style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                    color: summaryColor,
                    fontSize: 46,
                    height: 1,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.md),
            _ComparisonChip(
              amount: compareAmount,
              label: compareLabel,
              isDecrease: true,
            ),
          ],
        ),
      ),
    );
  }
}

class _HeroActionButton extends StatelessWidget {
  const _HeroActionButton({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 40,
      child: OutlinedButton.icon(
        onPressed: onTap,
        style: OutlinedButton.styleFrom(
          foregroundColor: PayaboColors.accentBrown,
          minimumSize: const Size(0, 40),
          padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.md),
          side: const BorderSide(color: Color(0xFFF1DEC9)),
          backgroundColor: PayaboColors.white,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(18),
          ),
        ),
        icon: const Icon(Icons.tune, size: 18),
        label: Text(
          'Personalise',
          style: Theme.of(context).textTheme.labelLarge?.copyWith(
                color: PayaboColors.accentBrown,
              ),
        ),
      ),
    );
  }
}

class _SegmentControl extends StatelessWidget {
  const _SegmentControl({
    required this.leftLabel,
    required this.rightLabel,
    required this.selectedIndex,
    required this.onChanged,
    required this.backgroundColor,
    required this.selectedColor,
    required this.selectedTextColor,
    required this.unselectedTextColor,
  });

  final String leftLabel;
  final String rightLabel;
  final int selectedIndex;
  final ValueChanged<int> onChanged;
  final Color backgroundColor;
  final Color selectedColor;
  final Color selectedTextColor;
  final Color unselectedTextColor;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: backgroundColor,
        borderRadius: PayaboRadii.radiusPill,
      ),
      child: Padding(
        padding: const EdgeInsets.all(PayaboSpacing.xs),
        child: Row(
          children: <Widget>[
            _SegmentControlOption(
              label: leftLabel,
              selected: selectedIndex == 0,
              selectedColor: selectedColor,
              selectedTextColor: selectedTextColor,
              unselectedTextColor: unselectedTextColor,
              onTap: () => onChanged(0),
            ),
            const SizedBox(width: PayaboSpacing.xs),
            _SegmentControlOption(
              label: rightLabel,
              selected: selectedIndex == 1,
              selectedColor: selectedColor,
              selectedTextColor: selectedTextColor,
              unselectedTextColor: unselectedTextColor,
              onTap: () => onChanged(1),
            ),
          ],
        ),
      ),
    );
  }
}

class _SegmentControlOption extends StatelessWidget {
  const _SegmentControlOption({
    required this.label,
    required this.selected,
    required this.selectedColor,
    required this.selectedTextColor,
    required this.unselectedTextColor,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final Color selectedColor;
  final Color selectedTextColor;
  final Color unselectedTextColor;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          borderRadius: PayaboRadii.radiusPill,
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 160),
            curve: Curves.easeOut,
            padding: const EdgeInsets.symmetric(
              horizontal: PayaboSpacing.md,
              vertical: PayaboSpacing.md,
            ),
            decoration: BoxDecoration(
              color: selected ? selectedColor : Colors.transparent,
              borderRadius: PayaboRadii.radiusPill,
            ),
            alignment: Alignment.center,
            child: Text(
              label,
              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: selected ? selectedTextColor : unselectedTextColor,
                    fontWeight: FontWeight.w700,
                  ),
              textAlign: TextAlign.center,
            ),
          ),
        ),
      ),
    );
  }
}

class _MonthFilterRow extends StatelessWidget {
  const _MonthFilterRow({
    required this.selectedIndex,
    required this.onSelected,
  });

  final int selectedIndex;
  final ValueChanged<int> onSelected;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 44,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: _monthFilters.length,
        separatorBuilder: (_, __) => const SizedBox(width: PayaboSpacing.sm),
        itemBuilder: (BuildContext context, int index) {
          final bool selected = selectedIndex == index;

          return ChoiceChip(
            label: Text(_monthFilters[index]),
            selected: selected,
            showCheckmark: false,
            selectedColor: PayaboColors.primary,
            backgroundColor: PayaboColors.surfaceWarm,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(PayaboRadii.pill),
              side: BorderSide(
                color:
                    selected ? PayaboColors.primary : const Color(0xFFE7D8CC),
              ),
            ),
            labelStyle: Theme.of(context).textTheme.titleSmall?.copyWith(
                  color: selected
                      ? PayaboColors.white
                      : PayaboColors.accentBrownMuted,
                  fontWeight: FontWeight.w700,
                ),
            onSelected: (_) => onSelected(index),
          );
        },
      ),
    );
  }
}

class _ComparisonChip extends StatelessWidget {
  const _ComparisonChip({
    required this.amount,
    required this.label,
    required this.isDecrease,
  });

  final String amount;
  final String label;
  final bool isDecrease;

  @override
  Widget build(BuildContext context) {
    final Color amountColor =
        isDecrease ? PayaboColors.success : PayaboColors.danger;
    final IconData directionIcon =
        isDecrease ? Icons.arrow_drop_down : Icons.arrow_drop_up;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: const Color(0xFFFFF9F4),
        borderRadius: PayaboRadii.radiusLg,
        border: Border.all(color: const Color(0xFFF1DEC9)),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.lg,
          vertical: PayaboSpacing.md,
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              amount,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: amountColor,
                    fontWeight: FontWeight.w700,
                  ),
            ),
            Icon(directionIcon, color: amountColor, size: 20),
            Text(
              label,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: PayaboColors.muted,
                    fontWeight: FontWeight.w500,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SpendingTrendChart extends StatelessWidget {
  const _SpendingTrendChart();

  static const List<FlSpot> _currentMonthSpots = <FlSpot>[
    FlSpot(1, 2),
    FlSpot(2, 14),
    FlSpot(3, 86),
    FlSpot(4, 88),
    FlSpot(5, 90),
    FlSpot(7, 93),
    FlSpot(10, 94),
    FlSpot(11, 16),
    FlSpot(13, 22),
    FlSpot(14, 6),
    FlSpot(15, 20),
    FlSpot(18, 20),
    FlSpot(20, 21),
    FlSpot(22, 22),
    FlSpot(23, 2),
    FlSpot(26, 2),
  ];

  static const List<FlSpot> _previousMonthSpots = <FlSpot>[
    FlSpot(1, 38),
    FlSpot(3, 42),
    FlSpot(5, 52),
    FlSpot(6, 52),
    FlSpot(7, 62),
    FlSpot(8, 62),
    FlSpot(9, 70),
    FlSpot(10, 70),
    FlSpot(11, 78),
    FlSpot(13, 78),
    FlSpot(14, 14),
    FlSpot(16, 15),
    FlSpot(18, 18),
    FlSpot(22, 18),
    FlSpot(23, 2),
    FlSpot(28, 2),
  ];

  @override
  Widget build(BuildContext context) {
    final Color previousMonthColor = PayaboColors.muted.withValues(alpha: 0.45);

    return LineChart(
      LineChartData(
        minX: 1,
        maxX: 28,
        minY: 0,
        maxY: 100,
        lineTouchData: const LineTouchData(enabled: false),
        gridData: const FlGridData(show: false),
        extraLinesData:
            const ExtraLinesData(horizontalLines: <HorizontalLine>[]),
        titlesData: FlTitlesData(
          leftTitles: const AxisTitles(
            sideTitles: SideTitles(showTitles: false),
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
          border: const Border(bottom: BorderSide(color: PayaboColors.border)),
        ),
        lineBarsData: <LineChartBarData>[
          LineChartBarData(
            spots: _previousMonthSpots,
            isCurved: false,
            color: previousMonthColor,
            barWidth: 2,
            isStrokeCapRound: true,
            dotData: FlDotData(
              show: true,
              checkToShowDot: (FlSpot spot, LineChartBarData barData) {
                return (spot.x - 28).abs() < 0.01;
              },
              getDotPainter: (
                FlSpot spot,
                double percent,
                LineChartBarData barData,
                int index,
              ) {
                return FlDotCirclePainter(
                  radius: 5,
                  color: PayaboColors.muted,
                  strokeWidth: 2,
                  strokeColor: PayaboColors.white,
                );
              },
            ),
            belowBarData: BarAreaData(show: false),
          ),
          LineChartBarData(
            spots: _currentMonthSpots,
            isCurved: false,
            color: PayaboColors.ink,
            barWidth: 3,
            isStrokeCapRound: true,
            dotData: FlDotData(
              show: true,
              checkToShowDot: (FlSpot spot, LineChartBarData barData) {
                return (spot.x - 26).abs() < 0.01;
              },
              getDotPainter: (
                FlSpot spot,
                double percent,
                LineChartBarData barData,
                int index,
              ) {
                return FlDotCirclePainter(
                  radius: 6,
                  color: PayaboColors.success,
                  strokeWidth: 2,
                  strokeColor: PayaboColors.white,
                );
              },
            ),
            belowBarData: BarAreaData(show: false),
          ),
        ],
      ),
    );
  }

  Widget _buildBottomTitle(double value, TitleMeta meta) {
    String? label;
    final int day = value.round();

    if (day == 1) {
      label = '1 Feb';
    } else if (day == 14) {
      label = '14 Feb';
    } else if (day == 28) {
      label = '28 Feb';
    }

    if (label == null) {
      return const SizedBox.shrink();
    }

    return SideTitleWidget(
      axisSide: meta.axisSide,
      space: PayaboSpacing.sm,
      child: Text(
        label,
        style: const TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w600,
          color: PayaboColors.muted,
        ),
      ),
    );
  }
}

class _CurrencySortButton extends StatelessWidget {
  const _CurrencySortButton();

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 90,
      height: 48,
      child: OutlinedButton(
        onPressed: () {},
        style: OutlinedButton.styleFrom(
          foregroundColor: PayaboColors.primary,
          minimumSize: const Size(90, 48),
          side: const BorderSide(color: PayaboColors.primary),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(PayaboRadii.sm),
          ),
          padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.lg),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              '£',
              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: PayaboColors.primary,
                  ),
            ),
            const SizedBox(width: PayaboSpacing.xs),
            const Icon(Icons.arrow_upward, size: 16),
          ],
        ),
      ),
    );
  }
}

class _BreakdownIcon extends StatelessWidget {
  const _BreakdownIcon({
    required this.icon,
    required this.color,
  });

  final IconData icon;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 40,
      height: 40,
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        shape: BoxShape.circle,
      ),
      child: Icon(icon, color: color, size: 22),
    );
  }
}

class _BreakdownAmount extends StatelessWidget {
  const _BreakdownAmount({
    required this.totalAmount,
    required this.changeAmount,
    required this.isDecrease,
  });

  final String totalAmount;
  final String changeAmount;
  final bool isDecrease;

  @override
  Widget build(BuildContext context) {
    final Color changeColor =
        isDecrease ? PayaboColors.success : PayaboColors.danger;
    final IconData direction =
        isDecrease ? Icons.arrow_drop_down : Icons.arrow_drop_up;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.end,
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Align(
          alignment: Alignment.centerRight,
          child: FittedBox(
            fit: BoxFit.scaleDown,
            child: Text(
              totalAmount,
              style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: PayaboColors.ink,
                    fontWeight: FontWeight.w700,
                  ),
            ),
          ),
        ),
        Align(
          alignment: Alignment.centerRight,
          child: FittedBox(
            fit: BoxFit.scaleDown,
            child: Row(
              mainAxisAlignment: MainAxisAlignment.end,
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Text(
                  changeAmount,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: changeColor,
                        fontWeight: FontWeight.w700,
                      ),
                ),
                Icon(direction, color: changeColor, size: 18),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _SpendingBreakdownItem {
  const _SpendingBreakdownItem({
    required this.id,
    required this.name,
    required this.transactionCount,
    required this.totalAmount,
    required this.changeAmount,
    required this.isDecrease,
    required this.icon,
    required this.iconColor,
  });

  final String id;
  final String name;
  final int transactionCount;
  final String totalAmount;
  final String changeAmount;
  final bool isDecrease;
  final IconData icon;
  final Color iconColor;
}
