import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/pay_activity_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import '../application/pay_activity_providers.dart';
import 'pay_dashboard_screen.dart';

// ═══════════════════════════════════════════════════════════════════════════
// Pay Activity — full transaction history with tabs and filters.
//
// Header: back arrow + "Pay Activity" title + search icon
// Tabs:   All | Transfers | Bills
// Chips:  Today | This week | This month | Failed
// Body:   Grouped transaction list (TODAY, YESTERDAY, date sections)
// ═══════════════════════════════════════════════════════════════════════════

class PayActivityScreen extends ConsumerStatefulWidget {
  const PayActivityScreen({super.key});

  @override
  ConsumerState<PayActivityScreen> createState() => _PayActivityScreenState();
}

class _PayActivityScreenState extends ConsumerState<PayActivityScreen> {
  int _selectedTabIndex = 0;
  int _selectedFilterIndex = 0;

  static const List<String> _tabs = <String>['All', 'Transfers', 'Bills'];
  static const List<String> _filters = <String>[
    'Today',
    'This week',
    'This month',
    'Failed',
  ];

  /// Groups a flat list of transactions by their [dateGroupLabel].
  List<_ActivityGroup> _groupTransactions(
    List<PayActivityTransaction> transactions,
  ) {
    final Map<String, List<PayActivityTransaction>> grouped =
        <String, List<PayActivityTransaction>>{};

    for (final txn in transactions) {
      grouped.putIfAbsent(txn.dateGroupLabel, () => <PayActivityTransaction>[]);
      grouped[txn.dateGroupLabel]!.add(txn);
    }

    return grouped.entries
        .map((entry) => _ActivityGroup(label: entry.key, items: entry.value))
        .toList();
  }

  List<_ActivityGroup> _applyFilters(
    List<PayActivityTransaction> transactions,
  ) {
    List<PayActivityTransaction> filtered = transactions;

    // Filter by tab (type)
    if (_selectedTabIndex == 1) {
      filtered = filtered
          .where((t) => t.type == PayActivityTransactionType.transfer)
          .toList();
    } else if (_selectedTabIndex == 2) {
      filtered = filtered
          .where((t) => t.type == PayActivityTransactionType.bill)
          .toList();
    }

    // Filter by chip
    if (_selectedFilterIndex == 3) {
      filtered = filtered
          .where((t) => t.status.toLowerCase() == 'failed')
          .toList();
    }

    return _groupTransactions(filtered);
  }

  // Same dark charcoal gradient as the dashboard.
  static const LinearGradient _backgroundGradient = LinearGradient(
    colors: <Color>[
      Color(0xFF242223),
      Color(0xFF191718),
      Color(0xFF0F0D0E),
    ],
    stops: <double>[0, 0.46, 1],
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
  );

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;
    final activityAsync = ref.watch(payActivitySummaryProvider);

    return PayaboWarmScaffold(
      backgroundDecoration: const BoxDecoration(gradient: _backgroundGradient),
      bottomNavigationBar: const PayaboPrimaryAppShell(
        destination: PayaboPrimaryDestination.pay,
      ),
      body: SafeArea(
        bottom: false,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            // ── Header: back + title + search ─────────────────
            Padding(
              padding: const EdgeInsets.fromLTRB(
                PayaboSpacing.sm,
                PayaboSpacing.md,
                PayaboSpacing.lg,
                PayaboSpacing.md,
              ),
              child: Row(
                children: <Widget>[
                  IconButton(
                    onPressed: () => context.pop(),
                    icon: const Icon(Icons.arrow_back, color: Colors.white),
                    splashRadius: 22,
                  ),
                  Expanded(
                    child: Text(
                      'Pay Activity',
                      style: textTheme.titleLarge?.copyWith(
                        color: Colors.white,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  const SizedBox(width: 48),
                ],
              ),
            ),

            // ── Tab bar: All | Transfers | Bills ──────────────
            Padding(
              padding: const EdgeInsets.symmetric(
                horizontal: PayaboSpacing.xl,
              ),
              child: _TabBar(
                tabs: _tabs,
                selectedIndex: _selectedTabIndex,
                onChanged: (int index) {
                  setState(() => _selectedTabIndex = index);
                },
              ),
            ),
            const SizedBox(height: PayaboSpacing.md),

            // ── Filter chips ──────────────────────────────────
            SizedBox(
              height: 34,
              child: ListView.separated(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.symmetric(
                  horizontal: PayaboSpacing.xl,
                ),
                itemCount: _filters.length,
                separatorBuilder: (_, __) =>
                    const SizedBox(width: PayaboSpacing.sm),
                itemBuilder: (BuildContext context, int index) {
                  final bool selected = _selectedFilterIndex == index;
                  return GestureDetector(
                    onTap: () {
                      setState(() => _selectedFilterIndex = index);
                    },
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 14,
                        vertical: 7,
                      ),
                      decoration: BoxDecoration(
                        color: selected
                            ? c.primary.withValues(alpha: 0.18)
                            : Colors.transparent,
                        borderRadius: PayaboRadii.radiusPill,
                        border: Border.all(
                          color: selected
                              ? c.primary
                              : Colors.white.withValues(alpha: 0.25),
                        ),
                      ),
                      child: Text(
                        _filters[index],
                        style: textTheme.labelMedium?.copyWith(
                          color: selected
                              ? c.primary
                              : Colors.white.withValues(alpha: 0.70),
                          fontWeight:
                              selected ? FontWeight.w700 : FontWeight.w500,
                        ),
                      ),
                    ),
                  );
                },
              ),
            ),
            const SizedBox(height: PayaboSpacing.lg),

            // ── Grouped transaction list ──────────────────────
            Expanded(
              child: activityAsync.when(
                loading: () => const Center(
                  child: CircularProgressIndicator(),
                ),
                error: (_, __) => Center(
                  child: Text(
                    'Unable to load activity',
                    style: textTheme.bodyLarge?.copyWith(
                      color: Colors.white.withValues(alpha: 0.5),
                    ),
                  ),
                ),
                data: (PayActivitySummary summary) {
                  final groups = _applyFilters(summary.transactions);

                  if (groups.isEmpty) {
                    return Center(
                      child: Text(
                        'No transactions found',
                        style: textTheme.bodyLarge?.copyWith(
                          color: Colors.white.withValues(alpha: 0.5),
                        ),
                      ),
                    );
                  }

                  return ListView.builder(
                    padding: const EdgeInsets.fromLTRB(
                      PayaboSpacing.xl,
                      0,
                      PayaboSpacing.xl,
                      PayaboSpacing.x4,
                    ),
                    itemCount: groups.length,
                    itemBuilder: (BuildContext context, int groupIndex) {
                      final group = groups[groupIndex];
                      return Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          // Date group label
                          Padding(
                            padding: EdgeInsets.only(
                              top: groupIndex == 0 ? 0 : PayaboSpacing.lg,
                              bottom: PayaboSpacing.sm,
                            ),
                            child: Text(
                              group.label,
                              style: textTheme.labelMedium?.copyWith(
                                color: Colors.white.withValues(alpha: 0.45),
                                fontWeight: FontWeight.w600,
                                letterSpacing: 0.5,
                              ),
                            ),
                          ),
                          // Items in group
                          for (int i = 0;
                              i < group.items.length;
                              i++) ...<Widget>[
                            PayActivityRow(
                              item: group.items[i],
                              onTap: () => context.push(
                                '/payments/transaction-details/${group.items[i].id}',
                              ),
                            ),
                            if (i < group.items.length - 1)
                              Divider(
                                height: 1,
                                color: Colors.white.withValues(alpha: 0.08),
                              ),
                          ],
                        ],
                      );
                    },
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Custom tab bar — matches the design mockup underline style
// ═══════════════════════════════════════════════════════════════════════════

class _TabBar extends StatelessWidget {
  const _TabBar({
    required this.tabs,
    required this.selectedIndex,
    required this.onChanged,
  });

  final List<String> tabs;
  final int selectedIndex;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final c = context.colors;

    return Row(
      children: <Widget>[
        for (int i = 0; i < tabs.length; i++) ...<Widget>[
          GestureDetector(
            onTap: () => onChanged(i),
            behavior: HitTestBehavior.opaque,
            child: Padding(
              padding: const EdgeInsets.only(right: PayaboSpacing.xl),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(
                    tabs[i],
                    style: textTheme.titleSmall?.copyWith(
                      color: i == selectedIndex
                          ? c.primary
                          : Colors.white.withValues(alpha: 0.50),
                      fontWeight: i == selectedIndex
                          ? FontWeight.w700
                          : FontWeight.w500,
                    ),
                  ),
                  const SizedBox(height: 6),
                  Container(
                    height: 2.5,
                    width: 28,
                    decoration: BoxDecoration(
                      color:
                          i == selectedIndex ? c.primary : Colors.transparent,
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ],
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Activity group model
// ═══════════════════════════════════════════════════════════════════════════

class _ActivityGroup {
  const _ActivityGroup({required this.label, required this.items});

  final String label;
  final List<PayActivityTransaction> items;
}
