import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_gradients.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import 'widgets/spending_section_pills.dart';

const List<SpendingSection> _visibleSpendingSections = <SpendingSection>[
  SpendingSection.transactions,
  SpendingSection.budgets,
  SpendingSection.accounts,
];

const List<_BudgetCategory> _budgetCategories = <_BudgetCategory>[
  _BudgetCategory(
    id: 'housing',
    name: 'Housing',
    icon: Icons.home_work_outlined,
    accentColor: PayaboColors.primary,
    lineItems: <_BudgetLineItem>[
      _BudgetLineItem(id: 'rent', name: 'Rent', allocated: 850, spent: 850),
      _BudgetLineItem(
        id: 'repairs',
        name: 'Repairs',
        allocated: 100,
        spent: 42,
      ),
      _BudgetLineItem(
        id: 'supplies',
        name: 'Supplies',
        allocated: 250,
        spent: 58,
      ),
    ],
  ),
  _BudgetCategory(
    id: 'groceries',
    name: 'Food & Groceries',
    icon: Icons.local_grocery_store_outlined,
    accentColor: PayaboColors.success,
    lineItems: <_BudgetLineItem>[
      _BudgetLineItem(
        id: 'supermarket',
        name: 'Supermarket',
        allocated: 350,
        spent: 240,
      ),
      _BudgetLineItem(
        id: 'market',
        name: 'Fresh market',
        allocated: 150,
        spent: 123.65,
      ),
      _BudgetLineItem(
        id: 'snacks',
        name: 'Coffee & snacks',
        allocated: 100,
        spent: 55,
      ),
    ],
  ),
  _BudgetCategory(
    id: 'transport',
    name: 'Transport',
    icon: Icons.directions_bus_outlined,
    accentColor: PayaboColors.warning,
    lineItems: <_BudgetLineItem>[
      _BudgetLineItem(id: 'fuel', name: 'Fuel', allocated: 200, spent: 232.2),
      _BudgetLineItem(
        id: 'ride-apps',
        name: 'Ride apps',
        allocated: 120,
        spent: 106,
      ),
      _BudgetLineItem(
        id: 'public-transit',
        name: 'Public transit',
        allocated: 100,
        spent: 110,
      ),
    ],
  ),
  _BudgetCategory(
    id: 'utilities',
    name: 'Utilities',
    icon: Icons.lightbulb_outline_rounded,
    accentColor: PayaboColors.info,
    lineItems: <_BudgetLineItem>[
      _BudgetLineItem(
        id: 'electricity',
        name: 'Electricity',
        allocated: 180,
        spent: 120.4,
      ),
      _BudgetLineItem(id: 'water', name: 'Water', allocated: 70, spent: 48.9),
      _BudgetLineItem(
        id: 'internet',
        name: 'Internet',
        allocated: 130,
        spent: 72,
      ),
    ],
  ),
  _BudgetCategory(
    id: 'personal',
    name: 'Personal care',
    icon: Icons.spa_outlined,
    accentColor: PayaboColors.headerIconAccent,
    lineItems: <_BudgetLineItem>[
      _BudgetLineItem(
        id: 'grooming',
        name: 'Hair & grooming',
        allocated: 150,
        spent: 125.55,
      ),
      _BudgetLineItem(
        id: 'pharmacy',
        name: 'Pharmacy',
        allocated: 120,
        spent: 52,
      ),
      _BudgetLineItem(id: 'gym', name: 'Gym', allocated: 80, spent: 33),
    ],
  ),
];

final NumberFormat _currencyFormat = NumberFormat.currency(
  locale: 'en_GB',
  symbol: '£',
);

class SpendingBudgetScreen extends ConsumerStatefulWidget {
  const SpendingBudgetScreen({super.key});

  @override
  ConsumerState<SpendingBudgetScreen> createState() =>
      _SpendingBudgetScreenState();
}

class _SpendingBudgetScreenState extends ConsumerState<SpendingBudgetScreen> {
  String _expandedCategoryId = _budgetCategories.first.id;

  @override
  Widget build(BuildContext context) {
    final bool isFreshDemo =
        ref.watch(demoDataModeProvider) == DemoDataMode.fresh;
    final List<_BudgetCategory> categories =
        isFreshDemo ? const <_BudgetCategory>[] : _budgetCategories;
    final _BudgetSummary summary = _BudgetSummary.fromCategories(
      monthLabel: 'March 2026',
      categories: categories,
    );

    return Scaffold(
      backgroundColor: PayaboColors.surfaceWarm,
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: PayaboGradients.warmScreen,
        ),
        child: SafeArea(
          child: Column(
            children: <Widget>[
              _BudgetHeader(
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
                    _BudgetHeroCard(summary: summary),
                    const SizedBox(height: PayaboSpacing.lg),
                    if (isFreshDemo)
                      const _FreshBudgetEmptyState()
                    else ...<Widget>[
                      _BudgetSectionIntro(summary: summary),
                      const SizedBox(height: PayaboSpacing.lg),
                      ...categories.map(
                        (_BudgetCategory category) => Padding(
                          padding:
                              const EdgeInsets.only(bottom: PayaboSpacing.md),
                          child: _BudgetCategoryCard(
                            category: category,
                            expanded: _expandedCategoryId == category.id,
                            onTap: () => _toggleCategory(category.id),
                          ),
                        ),
                      ),
                    ],
                  ],
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

  void _handleSectionSelected(SpendingSection section) {
    switch (section) {
      case SpendingSection.overview:
      case SpendingSection.transactions:
        context.go('/spending');
        return;
      case SpendingSection.budgets:
        return;
      case SpendingSection.accounts:
        _showSectionComingSoon('Accounts');
        return;
    }
  }

  void _toggleCategory(String categoryId) {
    setState(() {
      _expandedCategoryId = _expandedCategoryId == categoryId ? '' : categoryId;
    });
  }

  void _showSectionComingSoon(String sectionName) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text('$sectionName view coming soon in mock build.')),
    );
  }
}

class _BudgetHeader extends StatelessWidget {
  const _BudgetHeader({
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
        selectedSection: SpendingSection.budgets,
        sections: _visibleSpendingSections,
        onSelected: onSectionSelected,
      ),
    );
  }
}

class _BudgetHeroCard extends StatelessWidget {
  const _BudgetHeroCard({required this.summary});

  final _BudgetSummary summary;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: PayaboColors.spendingCardWarmElevated,
        borderRadius: BorderRadius.circular(PayaboRadii.xl),
        border: Border.all(color: PayaboColors.spendingQuickActionBorder),
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
                        summary.monthLabel,
                        style:
                            Theme.of(context).textTheme.titleMedium?.copyWith(
                                  color: PayaboColors.accentBrownMuted,
                                ),
                      ),
                      const SizedBox(height: PayaboSpacing.xs),
                      Text(
                        'Monthly budget',
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              color: PayaboColors.muted,
                            ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: PayaboSpacing.md),
                _BudgetStatusPill(
                  label: summary.statusLabel,
                  foregroundColor: summary.statusColor,
                ),
              ],
            ),
            const SizedBox(height: PayaboSpacing.lg),
            Text(
              _formatCurrency(summary.totalBudget),
              style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                    color: PayaboColors.accentBrown,
                    fontSize: 44,
                    height: 1,
                    fontWeight: FontWeight.w800,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              summary.description,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: PayaboColors.muted,
                    height: 1.45,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.xl),
            Row(
              children: <Widget>[
                Expanded(
                  child: _BudgetSummaryMetric(
                    label: 'Left to spend',
                    valueLabel: summary.leftToSpendLabel,
                    valueColor: summary.leftToSpendColor,
                  ),
                ),
                const SizedBox(width: PayaboSpacing.md),
                Expanded(
                  child: _BudgetSummaryMetric(
                    label: 'Used so far',
                    valueLabel: _formatCurrency(summary.totalSpent),
                    valueColor: PayaboColors.accentBrown,
                    alignEnd: true,
                  ),
                ),
              ],
            ),
            const SizedBox(height: PayaboSpacing.lg),
            _BudgetProgressBar(
              value: summary.progress,
              color: summary.statusColor,
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              summary.progressLabel,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: PayaboColors.accentBrownMuted,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}

class _BudgetSummaryMetric extends StatelessWidget {
  const _BudgetSummaryMetric({
    required this.label,
    required this.valueLabel,
    required this.valueColor,
    this.alignEnd = false,
  });

  final String label;
  final String valueLabel;
  final Color valueColor;
  final bool alignEnd;

  @override
  Widget build(BuildContext context) {
    final CrossAxisAlignment crossAxisAlignment =
        alignEnd ? CrossAxisAlignment.end : CrossAxisAlignment.start;
    final Alignment alignment =
        alignEnd ? Alignment.centerRight : Alignment.centerLeft;

    return Column(
      crossAxisAlignment: crossAxisAlignment,
      children: <Widget>[
        Text(
          label,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: PayaboColors.muted,
                fontWeight: FontWeight.w600,
              ),
        ),
        const SizedBox(height: PayaboSpacing.xs),
        Align(
          alignment: alignment,
          child: FittedBox(
            fit: BoxFit.scaleDown,
            child: Text(
              valueLabel,
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: valueColor,
                    fontWeight: FontWeight.w700,
                  ),
            ),
          ),
        ),
      ],
    );
  }
}

class _BudgetSectionIntro extends StatelessWidget {
  const _BudgetSectionIntro({required this.summary});

  final _BudgetSummary summary;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                'Category budgets',
                style: Theme.of(context).textTheme.titleLarge?.copyWith(
                      color: PayaboColors.accentBrown,
                      fontWeight: FontWeight.w700,
                    ),
              ),
              const SizedBox(height: PayaboSpacing.xs),
              Text(
                'Open a budget to see what is left in each spending pocket.',
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: PayaboColors.accentBrownMuted,
                    ),
              ),
            ],
          ),
        ),
        const SizedBox(width: PayaboSpacing.md),
        _BudgetStatusPill(
          label: '${summary.categoryCount} active',
          foregroundColor: PayaboColors.primary,
        ),
      ],
    );
  }
}

class _BudgetCategoryCard extends StatelessWidget {
  const _BudgetCategoryCard({
    required this.category,
    required this.expanded,
    required this.onTap,
  });

  final _BudgetCategory category;
  final bool expanded;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final _BudgetState state = _BudgetState.fromBudget(
      allocated: category.allocated,
      spent: category.spent,
    );

    return AnimatedContainer(
      duration: const Duration(milliseconds: 180),
      curve: Curves.easeOut,
      decoration: BoxDecoration(
        color: PayaboColors.white,
        borderRadius: BorderRadius.circular(PayaboRadii.xl),
        border: Border.all(
          color: expanded
              ? PayaboColors.spendingInsightBorder
              : PayaboColors.spendingQuickActionBorder,
        ),
        boxShadow: PayaboShadows.soft,
      ),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(PayaboRadii.xl),
          child: Padding(
            padding: const EdgeInsets.all(PayaboSpacing.lg),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    _BudgetCategoryIcon(
                      icon: category.icon,
                      color: category.accentColor,
                    ),
                    const SizedBox(width: PayaboSpacing.md),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          Text(
                            category.name,
                            style: Theme.of(context)
                                .textTheme
                                .titleMedium
                                ?.copyWith(
                                  color: PayaboColors.accentBrown,
                                  fontWeight: FontWeight.w700,
                                ),
                          ),
                          const SizedBox(height: PayaboSpacing.xs),
                          Text(
                            '${category.lineItems.length} budget lines',
                            style:
                                Theme.of(context).textTheme.bodySmall?.copyWith(
                                      color: PayaboColors.muted,
                                    ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: PayaboSpacing.sm),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: <Widget>[
                        Text(
                          _formatCurrency(category.allocated),
                          style:
                              Theme.of(context).textTheme.titleMedium?.copyWith(
                                    color: PayaboColors.accentBrown,
                                    fontWeight: FontWeight.w700,
                                  ),
                        ),
                        const SizedBox(height: PayaboSpacing.xs),
                        Text(
                          state.remainingLabel,
                          style:
                              Theme.of(context).textTheme.bodySmall?.copyWith(
                                    color: state.remainingColor,
                                    fontWeight: FontWeight.w700,
                                  ),
                        ),
                      ],
                    ),
                    const SizedBox(width: PayaboSpacing.xs),
                    Icon(
                      expanded
                          ? Icons.keyboard_arrow_up_rounded
                          : Icons.keyboard_arrow_down_rounded,
                      color: PayaboColors.accentBrownMuted,
                    ),
                  ],
                ),
                const SizedBox(height: PayaboSpacing.lg),
                _BudgetProgressBar(
                    value: state.progress, color: state.progressColor),
                const SizedBox(height: PayaboSpacing.sm),
                Row(
                  children: <Widget>[
                    Expanded(
                      child: Text(
                        '${state.percentUsedLabel} used',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                              color: PayaboColors.accentBrownMuted,
                            ),
                      ),
                    ),
                    _BudgetStatusPill(
                      label: state.statusLabel,
                      foregroundColor: state.progressColor,
                    ),
                  ],
                ),
                AnimatedSize(
                  duration: const Duration(milliseconds: 180),
                  curve: Curves.easeOut,
                  child: expanded
                      ? Padding(
                          padding: const EdgeInsets.only(top: PayaboSpacing.lg),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: <Widget>[
                              Container(
                                height: 1,
                                color: PayaboColors.spendingQuickActionBorder,
                              ),
                              const SizedBox(height: PayaboSpacing.lg),
                              Wrap(
                                spacing: PayaboSpacing.sm,
                                runSpacing: PayaboSpacing.sm,
                                children: <Widget>[
                                  _BudgetDetailChip(
                                    label: 'Spent',
                                    value: _formatCurrency(category.spent),
                                  ),
                                  _BudgetDetailChip(
                                    label: 'Remaining',
                                    value: state.remainingLabel,
                                    valueColor: state.remainingColor,
                                  ),
                                ],
                              ),
                              const SizedBox(height: PayaboSpacing.lg),
                              ...category.lineItems.map(
                                (_BudgetLineItem item) => Padding(
                                  padding: const EdgeInsets.only(
                                    bottom: PayaboSpacing.md,
                                  ),
                                  child: _BudgetLineItemCard(item: item),
                                ),
                              ),
                            ],
                          ),
                        )
                      : const SizedBox.shrink(),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _BudgetCategoryIcon extends StatelessWidget {
  const _BudgetCategoryIcon({
    required this.icon,
    required this.color,
  });

  final IconData icon;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 44,
      height: 44,
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        shape: BoxShape.circle,
      ),
      child: Icon(icon, color: color, size: 24),
    );
  }
}

class _BudgetDetailChip extends StatelessWidget {
  const _BudgetDetailChip({
    required this.label,
    required this.value,
    this.valueColor = PayaboColors.accentBrown,
  });

  final String label;
  final String value;
  final Color valueColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: PayaboColors.spendingCardWarm,
        borderRadius: BorderRadius.circular(PayaboRadii.pill),
        border: Border.all(color: PayaboColors.spendingQuickActionBorder),
      ),
      padding: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.md,
        vertical: PayaboSpacing.sm,
      ),
      child: RichText(
        text: TextSpan(
          children: <InlineSpan>[
            TextSpan(
              text: '$label ',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: PayaboColors.muted,
                    fontWeight: FontWeight.w600,
                  ),
            ),
            TextSpan(
              text: value,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: valueColor,
                    fontWeight: FontWeight.w700,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}

class _BudgetLineItemCard extends StatelessWidget {
  const _BudgetLineItemCard({required this.item});

  final _BudgetLineItem item;

  @override
  Widget build(BuildContext context) {
    final _BudgetState state = _BudgetState.fromBudget(
      allocated: item.allocated,
      spent: item.spent,
    );

    return Container(
      decoration: BoxDecoration(
        color: PayaboColors.spendingCardWarm,
        borderRadius: BorderRadius.circular(PayaboRadii.lg),
      ),
      padding: const EdgeInsets.all(PayaboSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  item.name,
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                        color: PayaboColors.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ),
              const SizedBox(width: PayaboSpacing.sm),
              Text(
                state.remainingLabel,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: state.remainingColor,
                      fontWeight: FontWeight.w700,
                    ),
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.sm),
          _BudgetProgressBar(value: state.progress, color: state.progressColor),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            '${_formatCurrency(item.spent)} of ${_formatCurrency(item.allocated)} used',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: PayaboColors.accentBrownMuted,
                ),
          ),
        ],
      ),
    );
  }
}

class _BudgetProgressBar extends StatelessWidget {
  const _BudgetProgressBar({
    required this.value,
    required this.color,
  });

  final double value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(PayaboRadii.pill),
      child: LinearProgressIndicator(
        minHeight: 10,
        value: value.clamp(0, 1),
        backgroundColor: PayaboColors.border,
        valueColor: AlwaysStoppedAnimation<Color>(color),
      ),
    );
  }
}

class _BudgetStatusPill extends StatelessWidget {
  const _BudgetStatusPill({
    required this.label,
    required this.foregroundColor,
  });

  final String label;
  final Color foregroundColor;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: foregroundColor.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(PayaboRadii.pill),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: PayaboSpacing.md,
          vertical: PayaboSpacing.sm,
        ),
        child: Text(
          label,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: foregroundColor,
                fontWeight: FontWeight.w700,
              ),
        ),
      ),
    );
  }
}

class _FreshBudgetEmptyState extends StatelessWidget {
  const _FreshBudgetEmptyState();

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: PayaboColors.spendingCardWarmElevated,
        borderRadius: BorderRadius.circular(PayaboRadii.xl),
        border: Border.all(color: PayaboColors.spendingQuickActionBorder),
        boxShadow: PayaboShadows.soft,
      ),
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: PayaboColors.primary.withValues(alpha: 0.14),
              borderRadius: BorderRadius.circular(PayaboRadii.lg),
            ),
            child: const Icon(
              Icons.savings_outlined,
              color: PayaboColors.primary,
              size: 28,
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'No budgets set yet',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: PayaboColors.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            'Fresh demo mode removes the seeded budget plan so this page starts clean and ready for your first category budget.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: PayaboColors.muted,
                  height: 1.45,
                ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'Once you add a budget, each category can expand here to show what is left in every spending pocket.',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: PayaboColors.chatTextSecondary,
                ),
          ),
        ],
      ),
    );
  }
}

class _BudgetSummary {
  const _BudgetSummary({
    required this.monthLabel,
    required this.totalBudget,
    required this.totalSpent,
    required this.categoryCount,
  });

  factory _BudgetSummary.fromCategories({
    required String monthLabel,
    required List<_BudgetCategory> categories,
  }) {
    final double totalBudget = categories.fold<double>(
      0,
      (double sum, _BudgetCategory category) => sum + category.allocated,
    );
    final double totalSpent = categories.fold<double>(
      0,
      (double sum, _BudgetCategory category) => sum + category.spent,
    );

    return _BudgetSummary(
      monthLabel: monthLabel,
      totalBudget: totalBudget,
      totalSpent: totalSpent,
      categoryCount: categories.length,
    );
  }

  final String monthLabel;
  final double totalBudget;
  final double totalSpent;
  final int categoryCount;

  double get remaining => totalBudget - totalSpent;
  double get progress => totalBudget == 0 ? 0 : totalSpent / totalBudget;

  String get statusLabel {
    if (categoryCount == 0) {
      return 'Start planning';
    }

    if (remaining < 0) {
      return 'Over plan';
    }

    if (progress >= 0.9) {
      return 'Almost there';
    }

    return 'On track';
  }

  Color get statusColor {
    if (categoryCount == 0) {
      return PayaboColors.primary;
    }

    if (remaining < 0) {
      return PayaboColors.danger;
    }

    if (progress >= 0.9) {
      return PayaboColors.warning;
    }

    return PayaboColors.success;
  }

  String get description {
    if (categoryCount == 0) {
      return 'Create your first category budget to start tracking how much is left before month end.';
    }

    return '$categoryCount active budgets covering home, food, transport, utilities, and personal care.';
  }

  String get leftToSpendLabel {
    if (remaining >= 0) {
      return _formatCurrency(remaining);
    }

    return '${_formatCurrency(remaining.abs())} over';
  }

  Color get leftToSpendColor {
    if (categoryCount == 0) {
      return PayaboColors.primary;
    }

    return remaining >= 0 ? PayaboColors.success : PayaboColors.danger;
  }

  String get progressLabel {
    if (totalBudget == 0) {
      return 'No monthly budget set yet.';
    }

    return '${(progress * 100).toStringAsFixed(1)}% of this month\'s plan is already used.';
  }
}

class _BudgetCategory {
  const _BudgetCategory({
    required this.id,
    required this.name,
    required this.icon,
    required this.accentColor,
    required this.lineItems,
  });

  final String id;
  final String name;
  final IconData icon;
  final Color accentColor;
  final List<_BudgetLineItem> lineItems;

  double get allocated => lineItems.fold<double>(
        0,
        (double sum, _BudgetLineItem item) => sum + item.allocated,
      );

  double get spent => lineItems.fold<double>(
        0,
        (double sum, _BudgetLineItem item) => sum + item.spent,
      );
}

class _BudgetLineItem {
  const _BudgetLineItem({
    required this.id,
    required this.name,
    required this.allocated,
    required this.spent,
  });

  final String id;
  final String name;
  final double allocated;
  final double spent;
}

class _BudgetState {
  const _BudgetState({
    required this.progress,
    required this.progressColor,
    required this.statusLabel,
    required this.remainingLabel,
    required this.remainingColor,
    required this.percentUsedLabel,
  });

  factory _BudgetState.fromBudget({
    required double allocated,
    required double spent,
  }) {
    final double remaining = allocated - spent;
    final double progress = allocated == 0 ? 0 : spent / allocated;
    final bool isOver = remaining < 0;
    final bool isClose = !isOver && progress >= 0.9;

    final Color progressColor = isOver
        ? PayaboColors.danger
        : isClose
            ? PayaboColors.warning
            : PayaboColors.primary;

    final String remainingLabel = isOver
        ? '${_formatCurrency(remaining.abs())} over'
        : '${_formatCurrency(remaining)} left';

    return _BudgetState(
      progress: progress,
      progressColor: progressColor,
      statusLabel: isOver
          ? 'Overspent'
          : isClose
              ? 'Close'
              : 'On track',
      remainingLabel: remainingLabel,
      remainingColor: isOver ? PayaboColors.danger : PayaboColors.success,
      percentUsedLabel: '${(progress.clamp(0, 1) * 100).toStringAsFixed(0)}%',
    );
  }

  final double progress;
  final Color progressColor;
  final String statusLabel;
  final String remainingLabel;
  final Color remainingColor;
  final String percentUsedLabel;
}

String _formatCurrency(double amount) => _currencyFormat.format(amount);
