import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/dashboard_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import 'dashboard_screen.dart';

// ─────────────────────────────────────────────────────────
//  Order detail screen
//
//  Displays the full information for a single recent order
//  identified by [orderId]. Data is resolved from the
//  dashboard summary provider.
// ─────────────────────────────────────────────────────────

class OrderDetailScreen extends ConsumerWidget {
  const OrderDetailScreen({super.key, required this.orderId});

  final String orderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final summaryAsync = ref.watch(dashboardSummaryProvider);

    final c = context.colors;

    return PayaboWarmScaffold(
      body: Column(
        children: <Widget>[
          SafeArea(
            bottom: false,
            child: Padding(
              padding: const EdgeInsets.only(
                left: PayaboSpacing.sm,
                top: PayaboSpacing.sm,
              ),
              child: Align(
                alignment: Alignment.centerLeft,
                child: IconButton(
                  icon: Icon(Icons.arrow_back_rounded, color: c.headerTitle),
                  onPressed: () {
                    if (context.canPop()) {
                      context.pop();
                    } else {
                      context.go('/');
                    }
                  },
                ),
              ),
            ),
          ),
          Expanded(
            child: summaryAsync.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, stack) => Center(
                child: Text('Failed to load order details: $error'),
              ),
              data: (summary) {
                final order = summary.recentOrders.cast<DashboardRecentOrder?>().firstWhere(
                  (o) => o!.id == orderId,
                  orElse: () => null,
                );

                if (order == null) {
                  return const Center(child: Text('Order not found'));
                }

                return _OrderDetailBody(order: order);
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _OrderDetailBody extends StatelessWidget {
  const _OrderDetailBody({required this.order});

  final DashboardRecentOrder order;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final theme = Theme.of(context);

    Color statusColor;
    switch (order.status) {
      case 'Completed':
        statusColor = c.success;
        break;
      case 'Failed':
        statusColor = c.danger;
        break;
      default:
        statusColor = c.warning;
    }

    return CustomScrollView(
      slivers: <Widget>[
        SliverToBoxAdapter(
          child: PayaboAppHeader(title: order.beneficiaryName),
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
                      CircleAvatar(
                        radius: 28,
                        backgroundColor: c.surfaceWarmAccent,
                        backgroundImage: order.beneficiaryPhotoUrl != null
                            ? NetworkImage(order.beneficiaryPhotoUrl!)
                            : null,
                        child: order.beneficiaryPhotoUrl == null
                            ? Text(
                                _initials(order.beneficiaryName),
                                style: theme.textTheme.titleMedium?.copyWith(
                                  color: c.accentBrown,
                                  fontWeight: FontWeight.w700,
                                ),
                              )
                            : null,
                      ),
                      const SizedBox(height: PayaboSpacing.md),
                      Text(
                        order.amountLabel,
                        style: theme.textTheme.headlineLarge?.copyWith(
                          fontWeight: FontWeight.w700,
                          color: c.accentBrown,
                        ),
                      ),
                      const SizedBox(height: PayaboSpacing.sm),
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 4,
                        ),
                        decoration: BoxDecoration(
                          color: statusColor.withValues(alpha: 0.12),
                          borderRadius: BorderRadius.circular(999),
                        ),
                        child: Text(
                          order.status,
                          style: theme.textTheme.labelMedium?.copyWith(
                            color: statusColor,
                            fontWeight: FontWeight.w600,
                          ),
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
                        'Order details',
                        style: theme.textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.w600,
                          color: c.accentBrown,
                        ),
                      ),
                      const SizedBox(height: PayaboSpacing.lg),
                      _DetailRow(
                        label: 'Beneficiary',
                        value: order.beneficiaryName,
                      ),
                      const SizedBox(height: PayaboSpacing.md),
                      _DetailRow(
                        label: 'Amount',
                        value: order.amountLabel,
                      ),
                      const SizedBox(height: PayaboSpacing.md),
                      _DetailRow(
                        label: 'Type',
                        value: order.orderType,
                      ),
                      const SizedBox(height: PayaboSpacing.md),
                      _DetailRow(
                        label: 'Date',
                        value: order.dateLabel,
                      ),
                      const SizedBox(height: PayaboSpacing.md),
                      _DetailRow(
                        label: 'Status',
                        value: order.status,
                      ),
                      const SizedBox(height: PayaboSpacing.md),
                      _DetailRow(
                        label: 'Order ID',
                        value: order.id,
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

  static String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+'));
    if (parts.length >= 2) {
      return '${parts.first[0]}${parts.last[0]}'.toUpperCase();
    }
    return parts.first.isNotEmpty ? parts.first[0].toUpperCase() : '?';
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
