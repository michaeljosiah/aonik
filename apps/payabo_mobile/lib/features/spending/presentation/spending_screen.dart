import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import 'widgets/spending_section_pills.dart';

// ─────────────────────────────────────────────────────────
//  Static demo data
// ─────────────────────────────────────────────────────────

const List<String> _monthFilters = <String>[
  'Dec',
  'Jan',
  'Feb',
  'Mar',
  'Custom',
];

const List<SpendingSection> _visibleSpendingSections = <SpendingSection>[
  SpendingSection.transactions,
  SpendingSection.budgets,
  SpendingSection.accounts,
];

const List<_SpendingBreakdownItem> _categoryItems = <_SpendingBreakdownItem>[
  _SpendingBreakdownItem(
    id: 'finances',
    name: 'Finances',
    transactionCount: 30,
    totalAmount: '£2,190.72',
    percentage: '45.9%',
    icon: Icons.currency_pound,
  ),
  _SpendingBreakdownItem(
    id: 'shopping',
    name: 'Shopping',
    transactionCount: 13,
    totalAmount: '£1,770.57',
    percentage: '37.1%',
    icon: Icons.shopping_bag_outlined,
  ),
  _SpendingBreakdownItem(
    id: 'groceries',
    name: 'Groceries',
    transactionCount: 22,
    totalAmount: '£505.10',
    percentage: '10.6%',
    icon: Icons.local_grocery_store_outlined,
  ),
  _SpendingBreakdownItem(
    id: 'transport',
    name: 'Transport',
    transactionCount: 9,
    totalAmount: '£312.44',
    percentage: '6.5%',
    icon: Icons.directions_car_outlined,
  ),
];

const List<_SpendingBreakdownItem> _merchantItems = <_SpendingBreakdownItem>[
  _SpendingBreakdownItem(
    id: 'amazon',
    name: 'Amazon',
    transactionCount: 6,
    totalAmount: '£410.90',
    percentage: '49.2%',
    icon: Icons.shopping_cart_outlined,
  ),
  _SpendingBreakdownItem(
    id: 'tesco',
    name: 'Tesco',
    transactionCount: 11,
    totalAmount: '£284.35',
    percentage: '34.1%',
    icon: Icons.local_grocery_store_outlined,
  ),
  _SpendingBreakdownItem(
    id: 'uber',
    name: 'Uber',
    transactionCount: 5,
    totalAmount: '£126.40',
    percentage: '15.2%',
    icon: Icons.local_taxi_outlined,
  ),
  _SpendingBreakdownItem(
    id: 'netflix',
    name: 'Netflix',
    transactionCount: 1,
    totalAmount: '£12.99',
    percentage: '1.6%',
    icon: Icons.ondemand_video_outlined,
  ),
];

// ─────────────────────────────────────────────────────────
//  Screen
// ─────────────────────────────────────────────────────────

class SpendingScreen extends ConsumerStatefulWidget {
  const SpendingScreen({super.key});

  @override
  ConsumerState<SpendingScreen> createState() => _SpendingScreenState();
}

class _SpendingScreenState extends ConsumerState<SpendingScreen> {
  int _monthIndex = 2;
  int _breakdownViewIndex = 0;

  @override
  Widget build(BuildContext context) {
    final isFreshDemo = ref.watch(demoDataModeProvider) == DemoDataMode.fresh;
    final theme = Theme.of(context);
    final isDark = theme.brightness == Brightness.dark;
    final cs = theme.colorScheme;

    final List<_SpendingBreakdownItem> breakdownItems = isFreshDemo
        ? const <_SpendingBreakdownItem>[]
        : _breakdownViewIndex == 0
            ? _categoryItems
            : _merchantItems;

    final String summaryLabel =
        isFreshDemo ? 'Fresh spending state' : 'Spent this month';
    final String summaryAmount = isFreshDemo ? '£0.00' : '£672.97';

    return Scaffold(
      backgroundColor: cs.surface,
      body: SafeArea(
        bottom: false,
        child: Column(
          children: <Widget>[
            // ── Compact header ───────────────────────────
            _SpendingHeader(
              onSectionSelected: _handleSectionSelected,
              onNotificationsTap: () => context.push('/notifications'),
              onProfileTap: () => context.go('/profile'),
            ),

            // ── Stacked body: upper summary + draggable breakdown ─
            Expanded(
              child: Stack(
                children: <Widget>[
                  // Top section: summary + chart (scrolls behind sheet)
                  Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: PayaboSpacing.xl,
                    ),
                    child: isFreshDemo
                        ? _FreshTransactionsEmptyState(isDark: isDark)
                        : Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: <Widget>[
                              const SizedBox(height: PayaboSpacing.md),
                              _FilterChipRow(
                                selectedMonthIndex: _monthIndex,
                                onMonthSelected: (int i) =>
                                    setState(() => _monthIndex = i),
                              ),
                              const SizedBox(height: PayaboSpacing.lg),
                              _SpentThisMonthCard(
                                label: summaryLabel,
                                amount: summaryAmount,
                                isDark: isDark,
                              ),
                              const SizedBox(height: PayaboSpacing.lg),
                              SizedBox(
                                height: 180,
                                child: _SpendingBarChart(isDark: isDark),
                              ),
                            ],
                          ),
                  ),

                  // Draggable breakdown sheet
                  if (!isFreshDemo)
                    _BreakdownSheet(
                      breakdownItems: breakdownItems,
                      breakdownViewIndex: _breakdownViewIndex,
                      onBreakdownViewChanged: (int i) =>
                          setState(() => _breakdownViewIndex = i),
                      isDark: isDark,
                    ),
                ],
              ),
            ),
          ],
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
//  Header
// ─────────────────────────────────────────────────────────

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
    final theme = Theme.of(context);

    return PayaboAppHeader(
      title: 'Spending',
      titleStyle: theme.textTheme.headlineLarge?.copyWith(
        fontWeight: FontWeight.w700,
        color: theme.colorScheme.onSurface,
      ),
      onNotificationsTap: onNotificationsTap,
      onProfileTap: onProfileTap,
      bottom: SpendingSectionPills(
        selectedSection: SpendingSection.transactions,
        sections: _visibleSpendingSections,
        onSelected: onSectionSelected,
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Filter chip row (Starling-style dropdown chips)
// ─────────────────────────────────────────────────────────

class _FilterChipRow extends StatelessWidget {
  const _FilterChipRow({
    required this.selectedMonthIndex,
    required this.onMonthSelected,
  });

  final int selectedMonthIndex;
  final ValueChanged<int> onMonthSelected;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final cs = theme.colorScheme;
    final chipBg = theme.brightness == Brightness.dark
        ? cs.surface.withValues(alpha: 0.6)
        : cs.surfaceContainerHighest.withValues(alpha: 0.5);

    return Row(
      children: <Widget>[
        _DropdownChip(
          icon: Icons.person_outline,
          label: 'Main balance',
          backgroundColor: chipBg,
          onTap: () {},
        ),
        const SizedBox(width: PayaboSpacing.sm),
        _DropdownChip(
          icon: Icons.calendar_today_outlined,
          label: _monthFilters[selectedMonthIndex],
          backgroundColor: chipBg,
          onTap: () {
            final next = (selectedMonthIndex + 1) % _monthFilters.length;
            onMonthSelected(next);
          },
        ),
      ],
    );
  }
}

class _DropdownChip extends StatelessWidget {
  const _DropdownChip({
    required this.icon,
    required this.label,
    required this.backgroundColor,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final Color backgroundColor;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textColor = theme.colorScheme.primary;

    return Material(
      color: backgroundColor,
      borderRadius: const BorderRadius.all(Radius.circular(20)),
      child: InkWell(
        onTap: onTap,
        borderRadius: const BorderRadius.all(Radius.circular(20)),
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.md,
            vertical: PayaboSpacing.sm,
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Icon(icon, size: 14, color: textColor),
              const SizedBox(width: PayaboSpacing.xs),
              Text(
                label,
                style: theme.textTheme.titleSmall?.copyWith(
                  color: textColor,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(width: 2),
              Icon(Icons.unfold_more, size: 14, color: textColor),
            ],
          ),
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Spent this month summary card
// ─────────────────────────────────────────────────────────

class _SpentThisMonthCard extends StatelessWidget {
  const _SpentThisMonthCard({
    required this.label,
    required this.amount,
    required this.isDark,
  });

  final String label;
  final String amount;
  final bool isDark;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final cardColor = isDark
        ? theme.colorScheme.surfaceContainerHighest
        : const Color(0xFFFFFBF8);
    final borderColor = isDark
        ? theme.colorScheme.outlineVariant
        : const Color(0xFFF1DEC9);

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(PayaboSpacing.lg),
      decoration: BoxDecoration(
        color: cardColor,
        borderRadius: const BorderRadius.all(Radius.circular(16)),
        border: Border.all(color: borderColor, width: 0.5),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            label,
            style: theme.textTheme.bodySmall?.copyWith(
              color: theme.textTheme.bodySmall?.color,
            ),
          ),
          const SizedBox(height: PayaboSpacing.xs),
          Text(
            amount,
            style: theme.textTheme.displayMedium?.copyWith(
              fontWeight: FontWeight.w700,
              color: theme.colorScheme.onSurface,
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Bar chart (Starling-style grouped bars)
// ─────────────────────────────────────────────────────────

class _SpendingBarChart extends StatelessWidget {
  const _SpendingBarChart({required this.isDark});

  final bool isDark;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final barColor = theme.colorScheme.primary;
    final gridColor = isDark
        ? theme.colorScheme.outlineVariant.withValues(alpha: 0.3)
        : const Color(0xFFE8DDD2);
    final mutedColor =
        theme.textTheme.bodySmall?.color ?? theme.colorScheme.onSurface;

    return BarChart(
      BarChartData(
        maxY: 800,
        barTouchData: BarTouchData(
          touchTooltipData: BarTouchTooltipData(
            getTooltipColor: (_) => theme.colorScheme.surfaceContainerHighest,
            tooltipBorderRadius: BorderRadius.circular(8),
            getTooltipItem: (group, groupIndex, rod, rodIndex) {
              return BarTooltipItem(
                '£${rod.toY.toInt()}',
                theme.textTheme.bodySmall!.copyWith(
                  color: theme.colorScheme.onSurface,
                  fontWeight: FontWeight.w600,
                ),
              );
            },
          ),
        ),
        gridData: FlGridData(
          show: true,
          drawVerticalLine: false,
          horizontalInterval: 200,
          getDrawingHorizontalLine: (_) => FlLine(
            color: gridColor,
            strokeWidth: 0.5,
          ),
        ),
        titlesData: FlTitlesData(
          leftTitles: const AxisTitles(
            sideTitles: SideTitles(showTitles: false),
          ),
          topTitles: const AxisTitles(
            sideTitles: SideTitles(showTitles: false),
          ),
          rightTitles: AxisTitles(
            sideTitles: SideTitles(
              showTitles: true,
              reservedSize: 36,
              interval: 400,
              getTitlesWidget: (double value, TitleMeta meta) {
                if (value == 0) return const SizedBox.shrink();
                return SideTitleWidget(
                  meta: meta,
                  child: Text(
                    '£${value.toInt()}',
                    style: TextStyle(
                      color: mutedColor,
                      fontSize: 10,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                );
              },
            ),
          ),
          bottomTitles: AxisTitles(
            sideTitles: SideTitles(
              showTitles: true,
              reservedSize: 24,
              getTitlesWidget: (double value, TitleMeta meta) {
                const labels = <String>['1', '2-8', '9-13'];
                final i = value.toInt();
                if (i < 0 || i >= labels.length) {
                  return const SizedBox.shrink();
                }
                return SideTitleWidget(
                  meta: meta,
                  space: PayaboSpacing.xs,
                  child: Text(
                    labels[i],
                    style: TextStyle(
                      color: mutedColor,
                      fontSize: 10,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                );
              },
            ),
          ),
        ),
        borderData: FlBorderData(show: false),
        barGroups: <BarChartGroupData>[
          _barGroup(0, 320, barColor),
          _barGroup(1, 540, barColor),
          _barGroup(2, 285, barColor),
        ],
      ),
    );
  }

  BarChartGroupData _barGroup(int x, double y, Color color) {
    return BarChartGroupData(
      x: x,
      barRods: <BarChartRodData>[
        BarChartRodData(
          toY: y,
          color: color,
          width: 40,
          borderRadius: const BorderRadius.only(
            topLeft: Radius.circular(4),
            topRight: Radius.circular(4),
          ),
        ),
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Draggable breakdown sheet
// ─────────────────────────────────────────────────────────

class _BreakdownSheet extends StatelessWidget {
  const _BreakdownSheet({
    required this.breakdownItems,
    required this.breakdownViewIndex,
    required this.onBreakdownViewChanged,
    required this.isDark,
  });

  final List<_SpendingBreakdownItem> breakdownItems;
  final int breakdownViewIndex;
  final ValueChanged<int> onBreakdownViewChanged;
  final bool isDark;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final sheetBg = isDark
        ? theme.colorScheme.surface
        : const Color(0xFFFFFCF9);

    return DraggableScrollableSheet(
      initialChildSize: 0.38,
      minChildSize: 0.30,
      maxChildSize: 0.88,
      snap: true,
      snapSizes: const <double>[0.38, 0.88],
      builder: (BuildContext context, ScrollController scrollController) {
        return Container(
          decoration: BoxDecoration(
            color: sheetBg,
            borderRadius: const BorderRadius.only(
              topLeft: Radius.circular(20),
              topRight: Radius.circular(20),
            ),
            boxShadow: <BoxShadow>[
              BoxShadow(
                color: Colors.black.withValues(alpha: isDark ? 0.3 : 0.08),
                blurRadius: 16,
                offset: const Offset(0, -4),
              ),
            ],
          ),
          child: CustomScrollView(
            controller: scrollController,
            slivers: <Widget>[
              // Drag handle
              SliverToBoxAdapter(
                child: Center(
                  child: Padding(
                    padding: const EdgeInsets.only(top: 10, bottom: 6),
                    child: Container(
                      width: 36,
                      height: 4,
                      decoration: BoxDecoration(
                        color: theme.colorScheme.onSurface.withValues(alpha: 0.15),
                        borderRadius:
                            const BorderRadius.all(Radius.circular(2)),
                      ),
                    ),
                  ),
                ),
              ),

              // "Spending breakdown" heading
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(
                    PayaboSpacing.xl,
                    PayaboSpacing.sm,
                    PayaboSpacing.xl,
                    PayaboSpacing.md,
                  ),
                  child: Text(
                    'Spending breakdown',
                    style: theme.textTheme.titleMedium?.copyWith(
                      color: theme.colorScheme.onSurface,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
              ),

              // Segment toggle: Categories | Merchants
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.symmetric(
                    horizontal: PayaboSpacing.xl,
                  ),
                  child: _SegmentToggle(
                    leftLabel: 'Categories',
                    rightLabel: 'Merchants',
                    selectedIndex: breakdownViewIndex,
                    onChanged: onBreakdownViewChanged,
                    isDark: isDark,
                  ),
                ),
              ),
              const SliverToBoxAdapter(
                child: SizedBox(height: PayaboSpacing.lg),
              ),

              // Breakdown list
              SliverPadding(
                padding: const EdgeInsets.symmetric(
                  horizontal: PayaboSpacing.xl,
                ),
                sliver: SliverList.separated(
                  itemCount: breakdownItems.length,
                  separatorBuilder: (_, __) => Divider(
                    height: 1,
                    color: theme.colorScheme.outlineVariant.withValues(alpha: 0.3),
                  ),
                  itemBuilder: (BuildContext context, int index) {
                    final item = breakdownItems[index];
                    return _BreakdownRow(
                      item: item,
                      isDark: isDark,
                      onTap: () {
                        if (breakdownViewIndex == 0) {
                          context.go('/spending/category/${item.id}');
                          return;
                        }
                        context.go('/spending/merchant/${item.id}');
                      },
                    );
                  },
                ),
              ),

              // Bottom safety padding
              const SliverToBoxAdapter(
                child: SizedBox(height: PayaboSpacing.x4),
              ),
            ],
          ),
        );
      },
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Segment toggle (Categories | Merchants)
// ─────────────────────────────────────────────────────────

class _SegmentToggle extends StatelessWidget {
  const _SegmentToggle({
    required this.leftLabel,
    required this.rightLabel,
    required this.selectedIndex,
    required this.onChanged,
    required this.isDark,
  });

  final String leftLabel;
  final String rightLabel;
  final int selectedIndex;
  final ValueChanged<int> onChanged;
  final bool isDark;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        _SegmentOption(
          label: leftLabel,
          selected: selectedIndex == 0,
          onTap: () => onChanged(0),
          isDark: isDark,
        ),
        const SizedBox(width: PayaboSpacing.sm),
        _SegmentOption(
          label: rightLabel,
          selected: selectedIndex == 1,
          onTap: () => onChanged(1),
          isDark: isDark,
        ),
        const Spacer(),
      ],
    );
  }
}

class _SegmentOption extends StatelessWidget {
  const _SegmentOption({
    required this.label,
    required this.selected,
    required this.onTap,
    required this.isDark,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;
  final bool isDark;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final bgColor = selected
        ? (isDark
            ? theme.colorScheme.surfaceContainerHighest
            : const Color(0xFFFFE7D3))
        : Colors.transparent;
    final textColor = selected
        ? theme.colorScheme.onSurface
        : theme.textTheme.bodySmall?.color ?? theme.colorScheme.onSurface;
    final borderColor = selected
        ? (isDark
            ? theme.colorScheme.outlineVariant
            : const Color(0xFFE7D8CC))
        : (isDark
            ? theme.colorScheme.outlineVariant.withValues(alpha: 0.4)
            : const Color(0xFFE7D8CC));

    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 160),
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.lg,
          vertical: PayaboSpacing.sm,
        ),
        decoration: BoxDecoration(
          color: bgColor,
          borderRadius: const BorderRadius.all(Radius.circular(20)),
          border: Border.all(color: borderColor, width: 0.5),
        ),
        child: Text(
          label,
          style: theme.textTheme.titleSmall?.copyWith(
            color: textColor,
            fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
          ),
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Breakdown row (clean Starling-style list item)
// ─────────────────────────────────────────────────────────

class _BreakdownRow extends StatelessWidget {
  const _BreakdownRow({
    required this.item,
    required this.isDark,
    required this.onTap,
  });

  final _SpendingBreakdownItem item;
  final bool isDark;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final iconBg = isDark
        ? theme.colorScheme.surfaceContainerHighest
        : theme.colorScheme.primary.withValues(alpha: 0.08);

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.md),
        child: Row(
          children: <Widget>[
            // Icon
            Container(
              width: 36,
              height: 36,
              decoration: BoxDecoration(
                color: iconBg,
                borderRadius: const BorderRadius.all(Radius.circular(10)),
              ),
              child: Icon(
                item.icon,
                size: 18,
                color: theme.colorScheme.primary,
              ),
            ),
            const SizedBox(width: PayaboSpacing.md),

            // Name + transaction count
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    item.name,
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w600,
                      color: theme.colorScheme.onSurface,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    '${item.transactionCount} transactions',
                    style: theme.textTheme.bodySmall,
                  ),
                ],
              ),
            ),

            // Amount + percentage
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: <Widget>[
                Text(
                  item.totalAmount,
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: theme.colorScheme.onSurface,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  item.percentage,
                  style: theme.textTheme.bodySmall,
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Fresh / empty state
// ─────────────────────────────────────────────────────────

class _FreshTransactionsEmptyState extends StatelessWidget {
  const _FreshTransactionsEmptyState({required this.isDark});

  final bool isDark;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final cardBg = isDark
        ? theme.colorScheme.surfaceContainerHighest
        : const Color(0xFFFFFBF8);

    return Padding(
      padding: const EdgeInsets.only(top: PayaboSpacing.lg),
      child: Container(
        decoration: BoxDecoration(
          color: cardBg,
          borderRadius: const BorderRadius.all(Radius.circular(16)),
          border: Border.all(
            color: theme.colorScheme.outlineVariant.withValues(alpha: 0.3),
          ),
        ),
        padding: const EdgeInsets.all(PayaboSpacing.xl),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: theme.colorScheme.primary.withValues(alpha: 0.12),
                borderRadius: const BorderRadius.all(Radius.circular(12)),
              ),
              child: Icon(
                Icons.insights_outlined,
                color: theme.colorScheme.primary,
                size: 20,
              ),
            ),
            const SizedBox(height: PayaboSpacing.md),
            Text(
              'No spending activity yet',
              style: theme.textTheme.titleLarge?.copyWith(
                color: theme.colorScheme.onSurface,
              ),
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              'Transactions, category rollups, and merchant trends will appear here once activity starts.',
              style: theme.textTheme.bodyMedium?.copyWith(
                color: theme.textTheme.bodySmall?.color,
                height: 1.45,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Data model
// ─────────────────────────────────────────────────────────

class _SpendingBreakdownItem {
  const _SpendingBreakdownItem({
    required this.id,
    required this.name,
    required this.transactionCount,
    required this.totalAmount,
    required this.percentage,
    required this.icon,
  });

  final String id;
  final String name;
  final int transactionCount;
  final String totalAmount;
  final String percentage;
  final IconData icon;
}
