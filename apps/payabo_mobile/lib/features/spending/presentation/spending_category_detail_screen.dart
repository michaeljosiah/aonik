import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_bottom_nav.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import '../../../shared/widgets/payabo_modal_sheet.dart';

class SpendingCategoryDetailScreen extends StatefulWidget {
  const SpendingCategoryDetailScreen({
    super.key,
    required this.categoryId,
  });

  final String categoryId;

  @override
  State<SpendingCategoryDetailScreen> createState() =>
      _SpendingCategoryDetailScreenState();
}

class _SpendingCategoryDetailScreenState
    extends State<SpendingCategoryDetailScreen> {
  int _navIndex = 2;

  @override
  Widget build(BuildContext context) {
    final _SpendingCategoryDetailData detail =
        _SpendingCategoryDetailData.fromId(widget.categoryId);
    final _SpendingCategoryTransaction transaction = detail.transactions.first;

    return Scaffold(
      backgroundColor: PayaboColors.white,
      body: SafeArea(
        child: Column(
          children: <Widget>[
            _CategoryHeader(
              title: detail.title,
              onBackTap: () => context.go('/spending'),
            ),
            Expanded(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(
                  PayaboSpacing.xl,
                  PayaboSpacing.lg,
                  PayaboSpacing.xl,
                  PayaboSpacing.x4,
                ),
                children: <Widget>[
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: <Widget>[
                            Text(
                              detail.monthLabel,
                              style: Theme.of(context)
                                  .textTheme
                                  .titleMedium
                                  ?.copyWith(color: PayaboColors.muted),
                            ),
                            const SizedBox(height: PayaboSpacing.xs),
                            Text(
                              detail.totalAmount,
                              style: Theme.of(context)
                                  .textTheme
                                  .headlineMedium
                                  ?.copyWith(fontSize: 56),
                            ),
                            const SizedBox(height: PayaboSpacing.xs),
                            Row(
                              children: <Widget>[
                                Text(
                                  detail.deltaAmount,
                                  style: Theme.of(context)
                                      .textTheme
                                      .titleMedium
                                      ?.copyWith(
                                        color: detail.isDecrease
                                            ? PayaboColors.success
                                            : PayaboColors.danger,
                                        fontWeight: FontWeight.w700,
                                      ),
                                ),
                                Icon(
                                  detail.isDecrease
                                      ? Icons.arrow_drop_down
                                      : Icons.arrow_drop_up,
                                  color: detail.isDecrease
                                      ? PayaboColors.success
                                      : PayaboColors.danger,
                                  size: 20,
                                ),
                                Expanded(
                                  child: Text(
                                    detail.deltaReference,
                                    style: Theme.of(context)
                                        .textTheme
                                        .titleMedium
                                        ?.copyWith(color: PayaboColors.muted),
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: PayaboSpacing.md),
                      Container(
                        width: 120,
                        height: 120,
                        decoration: BoxDecoration(
                          color: PayaboColors.background,
                          shape: BoxShape.circle,
                          border: Border.all(color: PayaboColors.border),
                        ),
                        child: Icon(
                          detail.icon,
                          color: PayaboColors.primary,
                          size: 56,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: PayaboSpacing.lg),
                  Center(
                    child: _DetailComparisonChip(
                      deltaAmount: detail.deltaAmount,
                      deltaReference: detail.deltaReference,
                      isDecrease: detail.isDecrease,
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.xl),
                  const SizedBox(height: 250, child: _CategorySpendingChart()),
                  const SizedBox(height: PayaboSpacing.xl),
                  _ActiveAlertBanner(alertCount: detail.activeAlertCount),
                  const SizedBox(height: PayaboSpacing.xl),
                  Text(
                    detail.transactionCountLabel,
                    style: Theme.of(context)
                        .textTheme
                        .titleLarge
                        ?.copyWith(color: PayaboColors.muted),
                  ),
                  const SizedBox(height: PayaboSpacing.md),
                  _TransactionDateRow(
                    dateLabel: transaction.dateLabel,
                    totalAmount: transaction.amount,
                  ),
                  _TransactionListItem(transaction: transaction),
                  const SizedBox(height: PayaboSpacing.x3),
                  Center(
                    child: Text(
                      "That's all your transactions.",
                      style: Theme.of(context)
                          .textTheme
                          .titleLarge
                          ?.copyWith(color: PayaboColors.muted),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: PayaboBottomNav(
        items: const <PayaboBottomNavItem>[
          PayaboBottomNavItem(icon: Icons.home_outlined, label: 'Home'),
          PayaboBottomNavItem(
              icon: Icons.receipt_long_outlined, label: 'Bills'),
          PayaboBottomNavItem(
              icon: Icons.show_chart_outlined, label: 'Spending'),
          PayaboBottomNavItem(icon: Icons.more_horiz, label: 'More'),
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
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Section coming soon in mock build.')),
        );
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
}

class _CategoryHeader extends StatelessWidget {
  const _CategoryHeader({
    required this.title,
    required this.onBackTap,
  });

  final String title;
  final VoidCallback onBackTap;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.md,
        PayaboSpacing.md,
        PayaboSpacing.xl,
        PayaboSpacing.xl,
      ),
      decoration: const BoxDecoration(
        color: PayaboColors.background,
        borderRadius: BorderRadius.only(
          bottomLeft: Radius.circular(32),
          bottomRight: Radius.circular(32),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          IconButton(
            onPressed: onBackTap,
            icon: const Icon(Icons.arrow_back_ios_new),
            color: PayaboColors.primary,
          ),
          Padding(
            padding: const EdgeInsets.only(left: PayaboSpacing.md),
            child: Text(
              title,
              style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                    fontSize: 54,
                    fontWeight: FontWeight.w700,
                  ),
            ),
          ),
        ],
      ),
    );
  }
}

class _DetailComparisonChip extends StatelessWidget {
  const _DetailComparisonChip({
    required this.deltaAmount,
    required this.deltaReference,
    required this.isDecrease,
  });

  final String deltaAmount;
  final String deltaReference;
  final bool isDecrease;

  @override
  Widget build(BuildContext context) {
    final Color amountColor =
        isDecrease ? PayaboColors.success : PayaboColors.danger;

    return DecoratedBox(
      decoration: const BoxDecoration(
        color: PayaboColors.white,
        borderRadius: PayaboRadii.radiusLg,
        boxShadow: PayaboShadows.soft,
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
              deltaAmount,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: amountColor,
                    fontWeight: FontWeight.w700,
                  ),
            ),
            Icon(
              isDecrease ? Icons.arrow_drop_down : Icons.arrow_drop_up,
              color: amountColor,
            ),
            Text(
              deltaReference,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: PayaboColors.muted,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}

class _CategorySpendingChart extends StatelessWidget {
  const _CategorySpendingChart();

  static const List<FlSpot> _currentMonthSpots = <FlSpot>[
    FlSpot(1, 0),
    FlSpot(2, 0),
    FlSpot(3, 8),
    FlSpot(4, 8),
    FlSpot(5, 12),
    FlSpot(6, 12),
  ];

  static const List<FlSpot> _previousMonthSpots = <FlSpot>[
    FlSpot(1, 0),
    FlSpot(2, 4),
    FlSpot(5, 11),
    FlSpot(14, 11),
    FlSpot(15, 28),
    FlSpot(22, 28),
    FlSpot(23, 35),
    FlSpot(25, 35),
    FlSpot(26, 45),
    FlSpot(31, 45),
  ];

  @override
  Widget build(BuildContext context) {
    return LineChart(
      LineChartData(
        minX: 1,
        maxX: 31,
        minY: 0,
        maxY: 60,
        lineTouchData: const LineTouchData(enabled: false),
        gridData: const FlGridData(show: false),
        extraLinesData: ExtraLinesData(
          verticalLines: <VerticalLine>[
            VerticalLine(
              x: 5,
              color: PayaboColors.borderStrong,
              strokeWidth: 1,
              dashArray: <int>[4, 4],
            ),
          ],
        ),
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
            color: PayaboColors.muted.withValues(alpha: 0.4),
            barWidth: 3,
            dotData: const FlDotData(show: false),
            belowBarData: BarAreaData(show: false),
          ),
          LineChartBarData(
            spots: _currentMonthSpots,
            isCurved: false,
            color: PayaboColors.ink,
            barWidth: 3,
            dotData: FlDotData(
              show: true,
              checkToShowDot: (FlSpot spot, LineChartBarData barData) {
                return (spot.x - 5).abs() < 0.01;
              },
              getDotPainter: (
                FlSpot spot,
                double percent,
                LineChartBarData barData,
                int index,
              ) {
                return FlDotCirclePainter(
                  radius: 7,
                  color: PayaboColors.success,
                  strokeWidth: 2,
                  strokeColor: PayaboColors.successSoft,
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
      label = '1 Mar';
    } else if (day == 16) {
      label = '16 Mar';
    } else if (day == 31) {
      label = '31 Mar';
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

class _ActiveAlertBanner extends StatelessWidget {
  const _ActiveAlertBanner({required this.alertCount});

  final int alertCount;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: const BoxDecoration(
        color: PayaboColors.white,
        borderRadius: PayaboRadii.radiusLg,
        boxShadow: PayaboShadows.soft,
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.lg,
          vertical: PayaboSpacing.lg,
        ),
        child: Row(
          children: <Widget>[
            Container(
              width: 30,
              height: 30,
              decoration: BoxDecoration(
                border: Border.all(color: PayaboColors.primary, width: 2),
                shape: BoxShape.circle,
              ),
              child: const Icon(
                Icons.currency_pound,
                size: 18,
                color: PayaboColors.primary,
              ),
            ),
            const SizedBox(width: PayaboSpacing.md),
            Expanded(
              child: Text(
                '$alertCount active spending alert',
                style: Theme.of(context).textTheme.titleLarge?.copyWith(
                      color: PayaboColors.primary,
                      fontWeight: FontWeight.w700,
                    ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TransactionDateRow extends StatelessWidget {
  const _TransactionDateRow({
    required this.dateLabel,
    required this.totalAmount,
  });

  final String dateLabel;
  final String totalAmount;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.lg,
        vertical: PayaboSpacing.sm,
      ),
      color: PayaboColors.background,
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              dateLabel,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: PayaboColors.muted,
                  ),
            ),
          ),
          Text(
            totalAmount,
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: PayaboColors.muted,
                ),
          ),
        ],
      ),
    );
  }
}

class _TransactionListItem extends StatelessWidget {
  const _TransactionListItem({required this.transaction});

  final _SpendingCategoryTransaction transaction;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.lg,
        vertical: PayaboSpacing.lg,
      ),
      decoration: const BoxDecoration(
        border: Border(
          bottom: BorderSide(color: PayaboColors.border),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            width: 52,
            height: 52,
            decoration: BoxDecoration(
              color: transaction.avatarBackground,
              shape: BoxShape.circle,
            ),
            alignment: Alignment.center,
            child: Text(
              transaction.avatarLabel,
              style: Theme.of(context).textTheme.labelLarge?.copyWith(
                    color: transaction.avatarForeground,
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
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: PayaboSpacing.xs),
                Text(
                  transaction.time,
                  style: Theme.of(context)
                      .textTheme
                      .titleMedium
                      ?.copyWith(color: PayaboColors.muted),
                ),
              ],
            ),
          ),
          const SizedBox(width: PayaboSpacing.md),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: <Widget>[
              Text(
                transaction.amount,
                style: Theme.of(context)
                    .textTheme
                    .headlineMedium
                    ?.copyWith(fontSize: 42),
              ),
              const SizedBox(height: PayaboSpacing.sm),
              Row(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(
                    transaction.accountName,
                    style: Theme.of(context)
                        .textTheme
                        .titleMedium
                        ?.copyWith(color: PayaboColors.muted),
                  ),
                  const SizedBox(width: PayaboSpacing.sm),
                  Container(
                    width: 34,
                    height: 34,
                    decoration: const BoxDecoration(
                      color: PayaboColors.ink,
                      shape: BoxShape.circle,
                    ),
                    alignment: Alignment.center,
                    child: Text(
                      transaction.accountBadge,
                      style: Theme.of(context).textTheme.titleSmall?.copyWith(
                            color: PayaboColors.success,
                            fontWeight: FontWeight.w700,
                          ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _SpendingCategoryDetailData {
  const _SpendingCategoryDetailData({
    required this.title,
    required this.icon,
    required this.monthLabel,
    required this.totalAmount,
    required this.deltaAmount,
    required this.deltaReference,
    required this.isDecrease,
    required this.activeAlertCount,
    required this.transactionCountLabel,
    required this.transactions,
  });

  final String title;
  final IconData icon;
  final String monthLabel;
  final String totalAmount;
  final String deltaAmount;
  final String deltaReference;
  final bool isDecrease;
  final int activeAlertCount;
  final String transactionCountLabel;
  final List<_SpendingCategoryTransaction> transactions;

  static _SpendingCategoryDetailData fromId(String categoryId) {
    switch (categoryId) {
      case 'shopping':
        return const _SpendingCategoryDetailData(
          title: 'Shopping',
          icon: Icons.shopping_bag_outlined,
          monthLabel: 'March spend',
          totalAmount: '£52.00',
          deltaAmount: '£11.88',
          deltaReference: 'vs. 4 February',
          isDecrease: true,
          activeAlertCount: 1,
          transactionCountLabel: '1 Transaction',
          transactions: <_SpendingCategoryTransaction>[
            _SpendingCategoryTransaction(
              dateLabel: 'Mon 02 Mar',
              merchant: 'Uber Eats',
              amount: '£52.00',
              time: '00:17',
              accountName: 'Current Account',
              accountBadge: 'S',
              avatarLabel: 'UE',
              avatarBackground: Color(0xFF1A1C20),
              avatarForeground: Color(0xFF4ACB64),
            ),
          ],
        );
      case 'groceries':
        return const _SpendingCategoryDetailData(
          title: 'Groceries',
          icon: Icons.local_grocery_store_outlined,
          monthLabel: 'March spend',
          totalAmount: '£284.35',
          deltaAmount: '£21.30',
          deltaReference: 'vs. 4 February',
          isDecrease: true,
          activeAlertCount: 1,
          transactionCountLabel: '1 Transaction',
          transactions: <_SpendingCategoryTransaction>[
            _SpendingCategoryTransaction(
              dateLabel: 'Mon 02 Mar',
              merchant: 'Tesco',
              amount: '£284.35',
              time: '14:22',
              accountName: 'Current Account',
              accountBadge: 'S',
              avatarLabel: 'T',
              avatarBackground: Color(0xFF1A1C20),
              avatarForeground: Color(0xFF4ACB64),
            ),
          ],
        );
      case 'transport':
        return const _SpendingCategoryDetailData(
          title: 'Transport',
          icon: Icons.directions_car_outlined,
          monthLabel: 'March spend',
          totalAmount: '£126.40',
          deltaAmount: '£18.00',
          deltaReference: 'vs. 4 February',
          isDecrease: false,
          activeAlertCount: 1,
          transactionCountLabel: '1 Transaction',
          transactions: <_SpendingCategoryTransaction>[
            _SpendingCategoryTransaction(
              dateLabel: 'Mon 02 Mar',
              merchant: 'Uber',
              amount: '£126.40',
              time: '09:05',
              accountName: 'Current Account',
              accountBadge: 'S',
              avatarLabel: 'U',
              avatarBackground: Color(0xFF1A1C20),
              avatarForeground: Color(0xFF4ACB64),
            ),
          ],
        );
      case 'amazon':
        return const _SpendingCategoryDetailData(
          title: 'Amazon',
          icon: Icons.shopping_cart_outlined,
          monthLabel: 'March spend',
          totalAmount: '£410.90',
          deltaAmount: '£98.20',
          deltaReference: 'vs. 4 February',
          isDecrease: false,
          activeAlertCount: 1,
          transactionCountLabel: '1 Transaction',
          transactions: <_SpendingCategoryTransaction>[
            _SpendingCategoryTransaction(
              dateLabel: 'Mon 02 Mar',
              merchant: 'Amazon Marketplace',
              amount: '£410.90',
              time: '16:10',
              accountName: 'Current Account',
              accountBadge: 'S',
              avatarLabel: 'AM',
              avatarBackground: Color(0xFF1A1C20),
              avatarForeground: Color(0xFF4ACB64),
            ),
          ],
        );
      case 'tesco':
        return const _SpendingCategoryDetailData(
          title: 'Tesco',
          icon: Icons.local_grocery_store_outlined,
          monthLabel: 'March spend',
          totalAmount: '£284.35',
          deltaAmount: '£21.30',
          deltaReference: 'vs. 4 February',
          isDecrease: true,
          activeAlertCount: 1,
          transactionCountLabel: '1 Transaction',
          transactions: <_SpendingCategoryTransaction>[
            _SpendingCategoryTransaction(
              dateLabel: 'Mon 02 Mar',
              merchant: 'Tesco',
              amount: '£284.35',
              time: '14:22',
              accountName: 'Current Account',
              accountBadge: 'S',
              avatarLabel: 'TS',
              avatarBackground: Color(0xFF1A1C20),
              avatarForeground: Color(0xFF4ACB64),
            ),
          ],
        );
      case 'uber':
        return const _SpendingCategoryDetailData(
          title: 'Uber',
          icon: Icons.local_taxi_outlined,
          monthLabel: 'March spend',
          totalAmount: '£126.40',
          deltaAmount: '£18.00',
          deltaReference: 'vs. 4 February',
          isDecrease: false,
          activeAlertCount: 1,
          transactionCountLabel: '1 Transaction',
          transactions: <_SpendingCategoryTransaction>[
            _SpendingCategoryTransaction(
              dateLabel: 'Mon 02 Mar',
              merchant: 'Uber',
              amount: '£126.40',
              time: '09:05',
              accountName: 'Current Account',
              accountBadge: 'S',
              avatarLabel: 'UB',
              avatarBackground: Color(0xFF1A1C20),
              avatarForeground: Color(0xFF4ACB64),
            ),
          ],
        );
      case 'netflix':
        return const _SpendingCategoryDetailData(
          title: 'Netflix',
          icon: Icons.ondemand_video_outlined,
          monthLabel: 'March spend',
          totalAmount: '£12.99',
          deltaAmount: '£0.00',
          deltaReference: 'vs. 4 February',
          isDecrease: true,
          activeAlertCount: 1,
          transactionCountLabel: '1 Transaction',
          transactions: <_SpendingCategoryTransaction>[
            _SpendingCategoryTransaction(
              dateLabel: 'Mon 02 Mar',
              merchant: 'Netflix',
              amount: '£12.99',
              time: '08:00',
              accountName: 'Current Account',
              accountBadge: 'S',
              avatarLabel: 'N',
              avatarBackground: Color(0xFF1A1C20),
              avatarForeground: Color(0xFF4ACB64),
            ),
          ],
        );
      case 'finances':
      default:
        return const _SpendingCategoryDetailData(
          title: 'Finances',
          icon: Icons.currency_pound,
          monthLabel: 'March spend',
          totalAmount: '£148.60',
          deltaAmount: '£9.20',
          deltaReference: 'vs. 4 February',
          isDecrease: true,
          activeAlertCount: 1,
          transactionCountLabel: '1 Transaction',
          transactions: <_SpendingCategoryTransaction>[
            _SpendingCategoryTransaction(
              dateLabel: 'Mon 02 Mar',
              merchant: 'Transfer fee',
              amount: '£148.60',
              time: '11:45',
              accountName: 'Current Account',
              accountBadge: 'S',
              avatarLabel: 'TF',
              avatarBackground: Color(0xFF1A1C20),
              avatarForeground: Color(0xFF4ACB64),
            ),
          ],
        );
    }
  }
}

class _SpendingCategoryTransaction {
  const _SpendingCategoryTransaction({
    required this.dateLabel,
    required this.merchant,
    required this.amount,
    required this.time,
    required this.accountName,
    required this.accountBadge,
    required this.avatarLabel,
    required this.avatarBackground,
    required this.avatarForeground,
  });

  final String dateLabel;
  final String merchant;
  final String amount;
  final String time;
  final String accountName;
  final String accountBadge;
  final String avatarLabel;
  final Color avatarBackground;
  final Color avatarForeground;
}
