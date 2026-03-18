import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../app/demo/demo_mode.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../data/repositories/spending_category_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';

// ─────────────────────────────────────────────────────────
//  Provider
// ─────────────────────────────────────────────────────────

final spendingCategoryDetailProvider =
    FutureProvider.family<SpendingCategoryDetail?, String>(
  (Ref ref, String categoryId) async {
    ref.watch(demoDataModeProvider);
    final repository = ref.watch(spendingCategoryRepositoryProvider);
    return repository.getCategoryDetail(categoryId);
  },
);

// ─────────────────────────────────────────────────────────
//  Screen
// ─────────────────────────────────────────────────────────

class SpendingCategoryDetailScreen extends ConsumerStatefulWidget {
  const SpendingCategoryDetailScreen({
    super.key,
    required this.categoryId,
  });

  final String categoryId;

  @override
  ConsumerState<SpendingCategoryDetailScreen> createState() =>
      _SpendingCategoryDetailScreenState();
}

class _SpendingCategoryDetailScreenState
    extends ConsumerState<SpendingCategoryDetailScreen> {
  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final isDemo = ref.watch(isDemoProvider);
    final isFreshDemo = ref.watch(demoDataModeProvider) == DemoDataMode.fresh;
    final detailAsync = ref.watch(
      spendingCategoryDetailProvider(widget.categoryId),
    );

    return Scaffold(
      backgroundColor: c.surfaceWarm,
      body: DecoratedBox(
        decoration: BoxDecoration(
          gradient: c.warmScreenGradient,
        ),
        child: SafeArea(
          child: detailAsync.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (Object error, StackTrace stack) => Center(
              child: Text('Something went wrong: $error'),
            ),
            data: (SpendingCategoryDetail? detail) {
              if (detail == null) {
                return Center(
                  child: Text(
                    'Category not found',
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          color: c.muted,
                        ),
                  ),
                );
              }

              final IconData icon = IconData(
                detail.iconCodePoint,
                fontFamily: detail.iconFontFamily,
              );

              final SpendingCategoryTransaction? transaction =
                  (isDemo && !isFreshDemo && detail.transactions.isNotEmpty)
                      ? detail.transactions.first
                      : null;

              return Column(
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
                        if (!isDemo)
                          _LiveModeEmptyState(
                            title: detail.title,
                            icon: icon,
                          )
                        else if (isFreshDemo)
                          _FreshSpendingDetailState(
                            title: detail.title,
                            icon: icon,
                          )
                        else ...<Widget>[
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
                                           ?.copyWith(color: c.muted),
                                     ),
                                     const SizedBox(height: PayaboSpacing.xs),
                                     Text(
                                       detail.totalAmount,
                                       style: Theme.of(context)
                                           .textTheme
                                           .displayMedium,
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
                                                     ? c.success
                                                     : c.danger,
                                                   fontWeight: FontWeight.w700,
                                                 ),
                                         ),
                                         Icon(
                                           detail.isDecrease
                                               ? Icons.arrow_drop_down
                                               : Icons.arrow_drop_up,
                                           color: detail.isDecrease
                                               ? c.success
                                               : c.danger,
                                           size: 20,
                                         ),
                                        Expanded(
                                          child: Text(
                                            detail.deltaReference,
                                            style: Theme.of(context)
                                                .textTheme
                                                 .titleMedium
                                                 ?.copyWith(
                                                   color: c.muted,
                                                 ),
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
                                  color: c.background,
                                  shape: BoxShape.circle,
                                  border: Border.all(color: c.border),
                                ),
                                child: Icon(
                                  icon,
                                  color: c.primary,
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
                          SizedBox(
                            height: 250,
                            child: _CategorySpendingChart(
                              currentMonthSpots: detail.chartCurrentMonthSpots,
                              previousMonthSpots:
                                  detail.chartPreviousMonthSpots,
                            ),
                          ),
                          const SizedBox(height: PayaboSpacing.xl),
                          _ActiveAlertBanner(
                            alertCount: detail.activeAlertCount,
                          ),
                          const SizedBox(height: PayaboSpacing.xl),
                           Text(
                             detail.transactionCountLabel,
                             style: Theme.of(context)
                                 .textTheme
                                 .titleLarge
                                 ?.copyWith(color: c.muted),
                           ),
                          const SizedBox(height: PayaboSpacing.md),
                          if (transaction != null) ...<Widget>[
                            _TransactionDateRow(
                              dateLabel: transaction.dateLabel,
                              totalAmount: transaction.amount,
                            ),
                            _TransactionListItem(transaction: transaction),
                          ],
                          const SizedBox(height: PayaboSpacing.x3),
                          Center(
                            child: Text(
                              "That's all your transactions.",
                              style: Theme.of(context)
                                  .textTheme
                                  .titleLarge
                                  ?.copyWith(color: c.muted),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ],
              );
            },
          ),
        ),
      ),
      bottomNavigationBar: const PayaboPrimaryAppShell(
        destination: PayaboPrimaryDestination.spending,
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Empty states
// ─────────────────────────────────────────────────────────

class _LiveModeEmptyState extends StatelessWidget {
  const _LiveModeEmptyState({required this.title, required this.icon});

  final String title;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        color: c.surfaceBase,
        borderRadius: PayaboRadii.radiusLg,
        boxShadow: PayaboShadows.soft,
      ),
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                width: 56,
                height: 56,
                decoration: BoxDecoration(
                  color: c.background,
                  shape: BoxShape.circle,
                  border: Border.all(color: c.border),
                ),
                child: Icon(
                  icon,
                  color: c.primary,
                  size: 28,
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),
              Expanded(
                child: Text(
                  'No ${title.toLowerCase()} data yet',
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'Connect a bank account to see your spending insights for ${title.toLowerCase()} here.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.muted,
                  height: 1.45,
                ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'Once your transactions are imported, charts, alerts, and breakdowns will appear automatically.',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: c.chatTextSecondary,
                ),
          ),
        ],
      ),
    );
  }
}

class _FreshSpendingDetailState extends StatelessWidget {
  const _FreshSpendingDetailState({required this.title, required this.icon});

  final String title;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        color: c.surfaceBase,
        borderRadius: PayaboRadii.radiusLg,
        boxShadow: PayaboShadows.soft,
      ),
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                width: 56,
                height: 56,
                decoration: BoxDecoration(
                  color: c.background,
                  shape: BoxShape.circle,
                  border: Border.all(color: c.border),
                ),
                child: Icon(
                  icon,
                  color: c.primary,
                  size: 28,
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),
              Expanded(
                child: Text(
                  'No ${title.toLowerCase()} transactions yet',
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'Fresh demo mode removes sample charts, alerts, and transaction rows for this spending detail view.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.muted,
                  height: 1.45,
                ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'Switch to Populated demo data in Profile if you want sample category or merchant insights here.',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: c.chatTextSecondary,
                ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Header
// ─────────────────────────────────────────────────────────

class _CategoryHeader extends StatelessWidget {
  const _CategoryHeader({
    required this.title,
    required this.onBackTap,
  });

  final String title;
  final VoidCallback onBackTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: c.surfaceWarmAccent,
        borderRadius: const BorderRadius.only(
          bottomLeft: Radius.circular(32),
          bottomRight: Radius.circular(32),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const PayaboAppHeader(
            padding: EdgeInsets.fromLTRB(
              PayaboSpacing.xl,
              PayaboSpacing.md,
              PayaboSpacing.xl,
              PayaboSpacing.md,
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(
              PayaboSpacing.md,
              0,
              PayaboSpacing.xl,
              PayaboSpacing.xl,
            ),
            child: Row(
              children: <Widget>[
                IconButton(
                  onPressed: onBackTap,
                  icon: const Icon(Icons.arrow_back_ios_new),
                  color: c.primary,
                ),
                const SizedBox(width: PayaboSpacing.xs),
                Expanded(
                  child: Text(
                    title,
                    style: Theme.of(context).textTheme.headlineLarge?.copyWith(
                          fontWeight: FontWeight.w700,
                          color: c.accentBrown,
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

// ─────────────────────────────────────────────────────────
//  Comparison chip
// ─────────────────────────────────────────────────────────

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
    final c = context.colors;
    final Color amountColor =
        isDecrease ? c.success : c.danger;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: c.surfaceBase,
        borderRadius: PayaboRadii.radiusLg,
        boxShadow: PayaboShadows.soft,
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.lg,
          vertical: PayaboSpacing.md,
        ),
        child: FittedBox(
          fit: BoxFit.scaleDown,
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
                      color: c.muted,
                    ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Spending chart
// ─────────────────────────────────────────────────────────

class _CategorySpendingChart extends StatelessWidget {
  const _CategorySpendingChart({
    required this.currentMonthSpots,
    required this.previousMonthSpots,
  });

  final List<List<double>> currentMonthSpots;
  final List<List<double>> previousMonthSpots;

  List<FlSpot> _toFlSpots(List<List<double>> raw) =>
      raw.map((List<double> pair) => FlSpot(pair[0], pair[1])).toList();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final List<FlSpot> current = _toFlSpots(currentMonthSpots);
    final List<FlSpot> previous = _toFlSpots(previousMonthSpots);

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
              color: c.borderStrong,
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
              getTitlesWidget: (double value, TitleMeta meta) =>
                  _buildBottomTitle(value, meta, c.muted),
            ),
          ),
        ),
        borderData: FlBorderData(
          show: true,
          border: Border(bottom: BorderSide(color: c.border)),
        ),
        lineBarsData: <LineChartBarData>[
          LineChartBarData(
            spots: previous,
            isCurved: false,
            color: c.muted.withValues(alpha: 0.4),
            barWidth: 3,
            dotData: const FlDotData(show: false),
            belowBarData: BarAreaData(show: false),
          ),
          LineChartBarData(
            spots: current,
            isCurved: false,
            color: c.ink,
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
                  color: c.success,
                  strokeWidth: 2,
                  strokeColor: c.successSoft,
                );
              },
            ),
            belowBarData: BarAreaData(show: false),
          ),
        ],
      ),
    );
  }

  Widget _buildBottomTitle(double value, TitleMeta meta, Color textColor) {
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
      meta: meta,
      space: PayaboSpacing.sm,
      child: Text(
        label,
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w600,
          color: textColor,
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Active alert banner
// ─────────────────────────────────────────────────────────

class _ActiveAlertBanner extends StatelessWidget {
  const _ActiveAlertBanner({required this.alertCount});

  final int alertCount;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return DecoratedBox(
      decoration: BoxDecoration(
        color: c.surfaceBase,
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
                border: Border.all(color: c.primary, width: 2),
                shape: BoxShape.circle,
              ),
              child: Icon(
                Icons.currency_pound,
                size: 18,
                color: c.primary,
              ),
            ),
            const SizedBox(width: PayaboSpacing.md),
            Expanded(
              child: Text(
                '$alertCount active spending alert',
                style: Theme.of(context).textTheme.titleLarge?.copyWith(
                      color: c.primary,
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

// ─────────────────────────────────────────────────────────
//  Transaction rows
// ─────────────────────────────────────────────────────────

class _TransactionDateRow extends StatelessWidget {
  const _TransactionDateRow({
    required this.dateLabel,
    required this.totalAmount,
  });

  final String dateLabel;
  final String totalAmount;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.lg,
        vertical: PayaboSpacing.sm,
      ),
      color: c.background,
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              dateLabel,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: c.muted,
                  ),
            ),
          ),
          Text(
            totalAmount,
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: c.muted,
                ),
          ),
        ],
      ),
    );
  }
}

class _TransactionListItem extends StatelessWidget {
  const _TransactionListItem({required this.transaction});

  final SpendingCategoryTransaction transaction;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.lg,
        vertical: PayaboSpacing.lg,
      ),
      decoration: BoxDecoration(
        border: Border(
          bottom: BorderSide(color: c.border),
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
                      ?.copyWith(color: c.muted),
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
                    .displaySmall,
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
                        ?.copyWith(color: c.muted),
                  ),
                  const SizedBox(width: PayaboSpacing.sm),
                  Container(
                    width: 34,
                    height: 34,
                    decoration: BoxDecoration(
                      color: c.ink,
                      shape: BoxShape.circle,
                    ),
                    alignment: Alignment.center,
                    child: Text(
                      transaction.accountBadge,
                      style: Theme.of(context).textTheme.titleSmall?.copyWith(
                            color: c.success,
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
