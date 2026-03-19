import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import 'pay_dashboard_screen.dart';

// ═══════════════════════════════════════════════════════════════════════════
// Pay Activity — full transaction history with tabs and filters.
//
// Header: back arrow + "Pay Activity" title + search icon
// Tabs:   All | Transfers | Bills
// Chips:  Today | This week | This month | Failed
// Body:   Grouped transaction list (TODAY, YESTERDAY, date sections)
// ═══════════════════════════════════════════════════════════════════════════

class PayActivityScreen extends StatefulWidget {
  const PayActivityScreen({super.key});

  @override
  State<PayActivityScreen> createState() => _PayActivityScreenState();
}

class _PayActivityScreenState extends State<PayActivityScreen> {
  int _selectedTabIndex = 0;
  int _selectedFilterIndex = 0;

  static const List<String> _tabs = <String>['All', 'Transfers', 'Bills'];
  static const List<String> _filters = <String>[
    'Today',
    'This week',
    'This month',
    'Failed',
  ];

  // Demo data grouped by date
  static const List<_ActivityGroup> _allGroups = <_ActivityGroup>[
    _ActivityGroup(
      label: 'TODAY',
      items: <PayActivityItem>[
        PayActivityItem(
          title: 'Transfer to Ama Serwaa',
          subtitle: 'Today, 09:42 AM',
          amount: 'GHS 500.00',
          status: 'Completed',
          type: PayActivityType.transfer,
        ),
        PayActivityItem(
          title: 'DSTV subscription',
          subtitle: 'Today, 08:10 AM',
          amount: 'GHS 240.00',
          status: 'Failed',
          type: PayActivityType.bill,
        ),
      ],
    ),
    _ActivityGroup(
      label: 'YESTERDAY',
      items: <PayActivityItem>[
        PayActivityItem(
          title: 'Transfer to Mum',
          subtitle: 'Yesterday, 07:18 PM',
          amount: 'GHS 200.00',
          status: 'Processing',
          type: PayActivityType.transfer,
        ),
        PayActivityItem(
          title: 'ECG prepaid top-up',
          subtitle: 'Monday, 11:05 AM',
          amount: 'GHS 120.00',
          status: 'Processing',
          type: PayActivityType.bill,
        ),
      ],
    ),
    _ActivityGroup(
      label: 'MAY 3',
      items: <PayActivityItem>[
        PayActivityItem(
          title: 'Water bill',
          subtitle: 'May 3, 2026, 07:18 PM',
          amount: 'GHS 56.00',
          status: 'Completed',
          type: PayActivityType.bill,
        ),
      ],
    ),
  ];

  List<_ActivityGroup> get _filteredGroups {
    // Filter by tab (type)
    List<_ActivityGroup> groups = _allGroups;

    if (_selectedTabIndex == 1) {
      // Transfers only
      groups = groups
          .map((g) => _ActivityGroup(
                label: g.label,
                items: g.items
                    .where((i) => i.type == PayActivityType.transfer)
                    .toList(),
              ))
          .where((g) => g.items.isNotEmpty)
          .toList();
    } else if (_selectedTabIndex == 2) {
      // Bills only
      groups = groups
          .map((g) => _ActivityGroup(
                label: g.label,
                items: g.items
                    .where((i) => i.type == PayActivityType.bill)
                    .toList(),
              ))
          .where((g) => g.items.isNotEmpty)
          .toList();
    }

    // Filter by chip
    if (_selectedFilterIndex == 3) {
      // Failed only
      groups = groups
          .map((g) => _ActivityGroup(
                label: g.label,
                items: g.items
                    .where((i) => i.status.toLowerCase() == 'failed')
                    .toList(),
              ))
          .where((g) => g.items.isNotEmpty)
          .toList();
    }

    return groups;
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
                  IconButton(
                    onPressed: () {
                      // TODO: implement search
                    },
                    icon: const Icon(Icons.search, color: Colors.white),
                    splashRadius: 22,
                  ),
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
              child: _filteredGroups.isEmpty
                  ? Center(
                      child: Text(
                        'No transactions found',
                        style: textTheme.bodyLarge?.copyWith(
                          color: Colors.white.withValues(alpha: 0.5),
                        ),
                      ),
                    )
                  : ListView.builder(
                      padding: const EdgeInsets.fromLTRB(
                        PayaboSpacing.xl,
                        0,
                        PayaboSpacing.xl,
                        PayaboSpacing.x4,
                      ),
                      itemCount: _filteredGroups.length,
                      itemBuilder: (BuildContext context, int groupIndex) {
                        final group = _filteredGroups[groupIndex];
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
                            for (int i = 0; i < group.items.length; i++) ...<Widget>[
                              PayActivityRow(
                                item: group.items[i],
                                onTap: () => context.push(
                                  '/payments/transaction-details',
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
  final List<PayActivityItem> items;
}
