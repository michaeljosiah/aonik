import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/repositories/dashboard_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import 'dashboard_screen.dart';

// ─────────────────────────────────────────────────────────
//  Bill detail screen
//
//  Displays the full information for a single upcoming bill
//  identified by [billId]. Data is resolved from the
//  dashboard summary provider.
// ─────────────────────────────────────────────────────────

class BillDetailScreen extends ConsumerWidget {
  const BillDetailScreen({super.key, required this.billId});

  final String billId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final summaryAsync = ref.watch(dashboardSummaryProvider);

    return PayaboWarmScaffold(
      body: summaryAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, stack) => Center(
          child: Text('Failed to load bill details: $error'),
        ),
        data: (summary) {
          final bill = summary.upcomingBills.cast<DashboardUpcomingBill?>().firstWhere(
            (b) => b!.id == billId,
            orElse: () => null,
          );

          if (bill == null) {
            return const Center(child: Text('Bill not found'));
          }

          return _BillDetailBody(bill: bill);
        },
      ),
    );
  }
}

class _BillDetailBody extends StatelessWidget {
  const _BillDetailBody({required this.bill});

  final DashboardUpcomingBill bill;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final theme = Theme.of(context);

    return CustomScrollView(
      slivers: <Widget>[
        SliverToBoxAdapter(
          child: PayaboAppHeader(title: bill.biller),
        ),
        SliverPadding(
          padding: const EdgeInsets.all(PayaboSpacing.lg),
          sliver: SliverToBoxAdapter(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                // ── Amount hero ───────────────────────────────────
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(PayaboSpacing.xl),
                  decoration: BoxDecoration(
                    color: c.cardWarmBackground,
                    borderRadius: PayaboRadii.radiusLg,
                    border: Border.all(color: c.cardWarmBorder, width: 0.5),
                  ),
                  child: Column(
                    children: <Widget>[
                      Icon(
                        Icons.receipt_long_outlined,
                        size: 48,
                        color: theme.colorScheme.primary,
                      ),
                      const SizedBox(height: PayaboSpacing.md),
                      Text(
                        bill.amountLabel,
                        style: theme.textTheme.headlineLarge?.copyWith(
                          fontWeight: FontWeight.w700,
                          color: c.accentBrown,
                        ),
                      ),
                      const SizedBox(height: PayaboSpacing.sm),
                      Text(
                        'Due ${bill.dueDateLabel}',
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: c.muted,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: PayaboSpacing.xl),

                // ── Details card ──────────────────────────────────
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(PayaboSpacing.lg),
                  decoration: BoxDecoration(
                    color: c.cardWarmBackground,
                    borderRadius: PayaboRadii.radiusLg,
                    border: Border.all(color: c.cardWarmBorder, width: 0.5),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        'Bill details',
                        style: theme.textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.w600,
                          color: c.accentBrown,
                        ),
                      ),
                      const SizedBox(height: PayaboSpacing.lg),
                      _DetailRow(
                        label: 'Biller',
                        value: bill.biller,
                      ),
                      const SizedBox(height: PayaboSpacing.md),
                      _DetailRow(
                        label: 'Amount',
                        value: bill.amountLabel,
                      ),
                      const SizedBox(height: PayaboSpacing.md),
                      _DetailRow(
                        label: 'Due date',
                        value: bill.dueDateLabel,
                      ),
                      const SizedBox(height: PayaboSpacing.md),
                      _DetailRow(
                        label: 'Bill ID',
                        value: bill.id,
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _DetailRow extends StatelessWidget {
  const _DetailRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final theme = Theme.of(context);

    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: <Widget>[
        Text(
          label,
          style: theme.textTheme.bodyMedium?.copyWith(color: c.muted),
        ),
        Flexible(
          child: Text(
            value,
            textAlign: TextAlign.end,
            style: theme.textTheme.bodyMedium?.copyWith(
              fontWeight: FontWeight.w600,
              color: c.accentBrown,
            ),
          ),
        ),
      ],
    );
  }
}
