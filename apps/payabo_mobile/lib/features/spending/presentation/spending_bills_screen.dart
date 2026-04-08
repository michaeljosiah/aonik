import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/dashboard_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import 'widgets/spending_section_pills.dart';

// ─────────────────────────────────────────────────────────
//  Section visibility (shared with the other spending tabs)
// ─────────────────────────────────────────────────────────

const List<SpendingSection> _visibleSpendingSections = <SpendingSection>[
  SpendingSection.transactions,
  SpendingSection.budgets,
  SpendingSection.bills,
  SpendingSection.accounts,
];

// ─────────────────────────────────────────────────────────
//  Provider — bill list derived from the dashboard summary
// ─────────────────────────────────────────────────────────

final _spendingBillsProvider =
    FutureProvider<List<DashboardUpcomingBill>>((Ref ref) async {
  final repository = ref.watch(dashboardRepositoryProvider);
  final summary = await repository.getSummary();
  return summary.upcomingBills;
});

// ─────────────────────────────────────────────────────────
//  Screen
// ─────────────────────────────────────────────────────────

class SpendingBillsScreen extends ConsumerWidget {
  const SpendingBillsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.colors;
    final AsyncValue<List<DashboardUpcomingBill>> billsValue =
        ref.watch(_spendingBillsProvider);

    return Scaffold(
      backgroundColor: c.surfaceWarm,
      body: DecoratedBox(
        decoration: BoxDecoration(gradient: c.warmScreenGradient),
        child: SafeArea(
          child: Column(
            children: <Widget>[
              _BillsHeader(
                onSectionSelected: (SpendingSection section) =>
                    _handleSectionSelected(context, section),
                onNotificationsTap: () => context.push('/notifications'),
                onProfileTap: () => context.go('/profile'),
              ),
              Expanded(
                child: billsValue.when(
                  data: (List<DashboardUpcomingBill> bills) {
                    if (bills.isEmpty) {
                      return const _EmptyBillsState();
                    }
                    return RefreshIndicator(
                      onRefresh: () async {
                        ref.invalidate(_spendingBillsProvider);
                        await ref.read(_spendingBillsProvider.future);
                      },
                      child: ListView.separated(
                        padding: const EdgeInsets.fromLTRB(
                          PayaboSpacing.xl,
                          PayaboSpacing.md,
                          PayaboSpacing.xl,
                          PayaboSpacing.x4,
                        ),
                        itemCount: bills.length,
                        separatorBuilder: (_, __) =>
                            const SizedBox(height: PayaboSpacing.sm),
                        itemBuilder: (BuildContext context, int index) {
                          return _BillRow(
                            bill: bills[index],
                            onTap: () => context.push(
                              '/dashboard/bills/${bills[index].id}',
                            ),
                          );
                        },
                      ),
                    );
                  },
                  loading: () =>
                      const Center(child: CircularProgressIndicator()),
                  error: (Object error, _) => Center(
                    child: Padding(
                      padding: const EdgeInsets.all(PayaboSpacing.xl),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          Icon(
                            Icons.error_outline_rounded,
                            size: 48,
                            color: c.muted,
                          ),
                          const SizedBox(height: PayaboSpacing.md),
                          Text(
                            'Unable to load bills right now.',
                            style: Theme.of(context)
                                .textTheme
                                .bodyMedium
                                ?.copyWith(color: c.muted),
                            textAlign: TextAlign.center,
                          ),
                          const SizedBox(height: PayaboSpacing.lg),
                          TextButton(
                            onPressed: () =>
                                ref.invalidate(_spendingBillsProvider),
                            child: const Text('Try again'),
                          ),
                        ],
                      ),
                    ),
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

  void _handleSectionSelected(BuildContext context, SpendingSection section) {
    switch (section) {
      case SpendingSection.overview:
        context.go('/spending/overview');
        return;
      case SpendingSection.transactions:
        context.go('/spending');
        return;
      case SpendingSection.budgets:
        context.go('/spending/budgets');
        return;
      case SpendingSection.bills:
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

class _BillsHeader extends StatelessWidget {
  const _BillsHeader({
    required this.onSectionSelected,
    required this.onNotificationsTap,
    required this.onProfileTap,
  });

  final ValueChanged<SpendingSection> onSectionSelected;
  final VoidCallback onNotificationsTap;
  final VoidCallback onProfileTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return PayaboAppHeader(
      title: 'Spend',
      titleStyle: Theme.of(context).textTheme.headlineLarge?.copyWith(
            fontWeight: FontWeight.w700,
            color: c.accentBrown,
          ),
      onNotificationsTap: onNotificationsTap,
      onProfileTap: onProfileTap,
      bottom: SpendingSectionPills(
        selectedSection: SpendingSection.bills,
        sections: _visibleSpendingSections,
        onSelected: onSectionSelected,
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Bill row
// ─────────────────────────────────────────────────────────

class _BillRow extends StatelessWidget {
  const _BillRow({
    required this.bill,
    required this.onTap,
  });

  final DashboardUpcomingBill bill;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final theme = Theme.of(context);

    return Container(
      decoration: BoxDecoration(
        color: c.surfaceBase,
        borderRadius: BorderRadius.circular(PayaboRadii.xl),
        border: Border.all(color: c.spendingQuickActionBorder),
        boxShadow: PayaboShadows.soft,
      ),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(PayaboRadii.xl),
          child: Padding(
            padding: const EdgeInsets.symmetric(
              horizontal: PayaboSpacing.lg,
              vertical: PayaboSpacing.md,
            ),
            child: Row(
              children: <Widget>[
                // ── Icon ────────────────────────────────────
                Container(
                  width: 44,
                  height: 44,
                  decoration: BoxDecoration(
                    color: c.primary.withValues(alpha: 0.1),
                    shape: BoxShape.circle,
                  ),
                  child: Icon(
                    Icons.receipt_long_rounded,
                    color: c.primary,
                    size: 22,
                  ),
                ),
                const SizedBox(width: PayaboSpacing.md),

                // ── Biller name + due date ───────────────────
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        bill.biller,
                        style: theme.textTheme.titleSmall?.copyWith(
                          color: c.accentBrown,
                          fontWeight: FontWeight.w700,
                        ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                      const SizedBox(height: PayaboSpacing.xs),
                      Text(
                        'Due ${bill.dueDateLabel}',
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: c.muted,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: PayaboSpacing.md),

                // ── Amount ───────────────────────────────────
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: <Widget>[
                    Text(
                      bill.amountLabel,
                      style: theme.textTheme.titleSmall?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: PayaboSpacing.xs),
                    Icon(
                      Icons.chevron_right_rounded,
                      size: 18,
                      color: c.muted,
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Empty state
// ─────────────────────────────────────────────────────────

class _EmptyBillsState extends StatelessWidget {
  const _EmptyBillsState();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final theme = Theme.of(context);

    return Center(
      child: Padding(
        padding: const EdgeInsets.all(PayaboSpacing.x4),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Container(
              width: 72,
              height: 72,
              decoration: BoxDecoration(
                color: c.primary.withValues(alpha: 0.1),
                shape: BoxShape.circle,
              ),
              child: Icon(
                Icons.receipt_long_rounded,
                size: 36,
                color: c.primary,
              ),
            ),
            const SizedBox(height: PayaboSpacing.xl),
            Text(
              'No upcoming bills',
              style: theme.textTheme.titleLarge?.copyWith(
                color: c.accentBrown,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              'Bills you add will appear here so you can see what\u2019s coming up in the next 30 days.',
              style: theme.textTheme.bodyMedium?.copyWith(
                color: c.muted,
                height: 1.5,
              ),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }
}
