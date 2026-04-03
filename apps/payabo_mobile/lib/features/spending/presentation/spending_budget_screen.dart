import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import 'spending_budget_data.dart';
import 'spending_budget_state.dart';
import 'widgets/budget_category_picker.dart';
import 'widgets/spending_budget_empty_state.dart';
import 'widgets/spending_section_pills.dart';

const List<SpendingSection> _visibleSpendingSections = <SpendingSection>[
  SpendingSection.transactions,
  SpendingSection.budgets,
  SpendingSection.accounts,
];

class SpendingBudgetScreen extends ConsumerStatefulWidget {
  const SpendingBudgetScreen({super.key});

  @override
  ConsumerState<SpendingBudgetScreen> createState() =>
      _SpendingBudgetScreenState();
}

class _SpendingBudgetScreenState extends ConsumerState<SpendingBudgetScreen> {
  String _expandedCategoryId = spendingBudgetCategories.first.id;
  bool _isCreatingBudget = false;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final AsyncValue<List<SpendingBudgetCategory>> budgetsValue =
        ref.watch(spendingBudgetsProvider);

    return Scaffold(
      backgroundColor: c.surfaceWarm,
      body: DecoratedBox(
        decoration: BoxDecoration(
          gradient: c.warmScreenGradient,
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
                child: budgetsValue.when(
                  data: (List<SpendingBudgetCategory> categories) {
                    if (categories.isEmpty) {
                      return LayoutBuilder(
                        builder: (
                          BuildContext context,
                          BoxConstraints constraints,
                        ) {
                          return RefreshIndicator(
                            onRefresh: () async {
                              ref.invalidate(spendingBudgetsProvider);
                              await ref.read(spendingBudgetsProvider.future);
                            },
                            child: SingleChildScrollView(
                              physics: const AlwaysScrollableScrollPhysics(),
                              padding: const EdgeInsets.fromLTRB(
                                PayaboSpacing.xl,
                                PayaboSpacing.md,
                                PayaboSpacing.xl,
                                PayaboSpacing.x4,
                              ),
                              child: ConstrainedBox(
                                constraints: BoxConstraints(
                                  minHeight: constraints.maxHeight -
                                      (PayaboSpacing.x4 + PayaboSpacing.md),
                                ),
                                child: SpendingBudgetEmptyState(
                                  title: 'Create your first budget',
                                  description:
                                      'Budgets help you group spending into categories, set monthly limits, and understand what is left before the month ends.',
                                  caption:
                                      'Start with one simple budget and adjust the amount as your spending pattern becomes clearer.',
                                  actionLabel: 'Create new budget',
                                  busy: _isCreatingBudget,
                                  onPressed: _handleCreateBudget,
                                ),
                              ),
                            ),
                          );
                        },
                      );
                    }

                    final SpendingBudgetSummary summary =
                        SpendingBudgetSummary.fromCategories(
                      monthLabel: spendingBudgetMonthLabel,
                      categories: categories,
                    );
                    final String expandedCategoryId;
                    if (_expandedCategoryId.isEmpty) {
                      expandedCategoryId = '';
                    } else if (categories.any(
                      (SpendingBudgetCategory category) =>
                          category.id == _expandedCategoryId,
                    )) {
                      expandedCategoryId = _expandedCategoryId;
                    } else {
                      expandedCategoryId =
                          categories.isNotEmpty ? categories.first.id : '';
                    }

                    return RefreshIndicator(
                      onRefresh: () async {
                        ref.invalidate(spendingBudgetsProvider);
                        await ref.read(spendingBudgetsProvider.future);
                      },
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
                          _BudgetSectionIntro(summary: summary),
                          const SizedBox(height: PayaboSpacing.lg),
                          ...categories.map(
                            (SpendingBudgetCategory category) => Padding(
                              padding: const EdgeInsets.only(
                                bottom: PayaboSpacing.md,
                              ),
                              child: _BudgetCategoryCard(
                                category: category,
                                expanded: expandedCategoryId == category.id,
                                onExpandToggle: () =>
                                    _toggleCategory(category.id),
                                onOpen: () => context.push(
                                  '/spending/budgets/${category.id}',
                                ),
                              ),
                            ),
                          ),
                          const SizedBox(height: PayaboSpacing.md),
                          Center(
                            child: PayaboButton(
                              key: const Key('budget-create-new'),
                              label: _isCreatingBudget
                                  ? 'Creating…'
                                  : 'Create new budget',
                              variant: PayaboButtonVariant.secondary,
                              size: PayaboButtonSize.lg,
                              leading: const Icon(Icons.add_rounded, size: 20),
                              onPressed: _isCreatingBudget
                                  ? null
                                  : _handleCreateBudget,
                            ),
                          ),
                        ],
                      ),
                    );
                  },
                  loading: () =>
                      const Center(child: CircularProgressIndicator()),
                  error: (Object error, StackTrace stackTrace) {
                    return Center(
                      child: Padding(
                        padding: const EdgeInsets.all(PayaboSpacing.xl),
                        child: Text('Unable to load budgets: $error'),
                      ),
                    );
                  },
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
        context.go('/spending/overview');
        return;
      case SpendingSection.transactions:
        context.go('/spending');
        return;
      case SpendingSection.budgets:
        return;
      case SpendingSection.accounts:
        context.go('/spending/accounts');
        return;
    }
  }

  void _toggleCategory(String categoryId) {
    setState(() {
      _expandedCategoryId = _expandedCategoryId == categoryId ? '' : categoryId;
    });
  }

  Future<void> _handleCreateBudget() async {
    if (_isCreatingBudget) {
      return;
    }

    // Determine which predefined templates are already created.
    final List<SpendingBudgetCategory> currentBudgets =
        ref.read(spendingBudgetsProvider).value ??
            const <SpendingBudgetCategory>[];
    final Set<String> existingIds =
        currentBudgets.map((SpendingBudgetCategory b) => b.id).toSet();

    final BudgetCategoryPickerResult? result = await showBudgetCategoryPicker(
      context: context,
      existingCategoryIds: existingIds,
    );

    if (result == null || !mounted) {
      return;
    }

    setState(() => _isCreatingBudget = true);

    try {
      final SpendingBudgetCategory budget = await ref
          .read(budgetRepositoryProvider)
          .createBudget(categoryId: result.categoryId);
      ref.invalidate(spendingBudgetsProvider);

      if (!mounted) {
        return;
      }

      context.push('/spending/budgets/${budget.id}');
    } catch (_) {
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Unable to create a budget right now.'),
        ),
      );
    } finally {
      if (mounted) {
        setState(() => _isCreatingBudget = false);
      }
    }
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
        selectedSection: SpendingSection.budgets,
        sections: _visibleSpendingSections,
        onSelected: onSectionSelected,
      ),
    );
  }
}

class _BudgetHeroCard extends StatelessWidget {
  const _BudgetHeroCard({required this.summary});

  final SpendingBudgetSummary summary;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        color: c.spendingCardWarmElevated,
        borderRadius: BorderRadius.circular(PayaboRadii.xl),
        border: Border.all(color: c.spendingQuickActionBorder),
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
                                  color: c.accentBrownMuted,
                                ),
                      ),
                      const SizedBox(height: PayaboSpacing.xs),
                      Text(
                        'Monthly budget',
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              color: c.muted,
                            ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: PayaboSpacing.md),
                _BudgetStatusPill(
                  label: summary.statusLabel,
                  foregroundColor: summary.statusColorRole.resolve(c),
                ),
              ],
            ),
            const SizedBox(height: PayaboSpacing.lg),
            Text(
              formatSpendingBudgetCurrency(summary.totalBudget),
              style: Theme.of(context).textTheme.displayMedium?.copyWith(
                    color: c.accentBrown,
                    height: 1,
                    fontWeight: FontWeight.w800,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              summary.description,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: c.muted,
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
                    valueColor: summary.leftToSpendColorRole.resolve(c),
                  ),
                ),
                const SizedBox(width: PayaboSpacing.md),
                Expanded(
                  child: _BudgetSummaryMetric(
                    label: 'Used so far',
                    valueLabel:
                        formatSpendingBudgetCurrency(summary.totalSpent),
                    valueColor: c.accentBrown,
                    alignEnd: true,
                  ),
                ),
              ],
            ),
            const SizedBox(height: PayaboSpacing.lg),
            _BudgetProgressBar(
              value: summary.progress,
              color: summary.statusColorRole.resolve(c),
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              summary.progressLabel,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: c.accentBrownMuted,
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
    final c = context.colors;
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
                color: c.muted,
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

  final SpendingBudgetSummary summary;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Row(
      children: <Widget>[
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                'Category budgets',
                style: Theme.of(context).textTheme.titleLarge?.copyWith(
                      color: c.accentBrown,
                      fontWeight: FontWeight.w700,
                    ),
              ),
              const SizedBox(height: PayaboSpacing.xs),
              Text(
                'Open a budget to see what is left in each spending pocket.',
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: c.accentBrownMuted,
                    ),
              ),
            ],
          ),
        ),
        const SizedBox(width: PayaboSpacing.md),
        _BudgetStatusPill(
          label: '${summary.categoryCount} active',
          foregroundColor: c.primary,
        ),
      ],
    );
  }
}

class _BudgetCategoryCard extends StatelessWidget {
  const _BudgetCategoryCard({
    required this.category,
    required this.expanded,
    required this.onExpandToggle,
    required this.onOpen,
  });

  final SpendingBudgetCategory category;
  final bool expanded;
  final VoidCallback onExpandToggle;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final SpendingBudgetState state = SpendingBudgetState.fromBudget(
      allocated: category.allocated,
      spent: category.spent,
    );

    return AnimatedContainer(
      duration: const Duration(milliseconds: 180),
      curve: Curves.easeOut,
      decoration: BoxDecoration(
        color: c.surfaceBase,
        borderRadius: BorderRadius.circular(PayaboRadii.xl),
        border: Border.all(
          color:
              expanded ? c.spendingInsightBorder : c.spendingQuickActionBorder,
        ),
        boxShadow: PayaboShadows.soft,
      ),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          key: Key('budget-card-${category.id}'),
          onTap: onOpen,
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
                      color: category.accentRole.resolve(c),
                    ),
                    const SizedBox(width: PayaboSpacing.md),
                    Expanded(
                      child: Text(
                        category.name,
                        style:
                            Theme.of(context).textTheme.titleMedium?.copyWith(
                                  color: c.accentBrown,
                                  fontWeight: FontWeight.w700,
                                ),
                      ),
                    ),
                    const SizedBox(width: PayaboSpacing.sm),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: <Widget>[
                        Text(
                          formatSpendingBudgetCurrency(category.allocated),
                          style:
                              Theme.of(context).textTheme.titleMedium?.copyWith(
                                    color: c.accentBrown,
                                    fontWeight: FontWeight.w700,
                                  ),
                        ),
                        const SizedBox(height: PayaboSpacing.xs),
                        Text(
                          state.remainingLabel,
                          style:
                              Theme.of(context).textTheme.bodySmall?.copyWith(
                                    color: state.remainingColorRole.resolve(c),
                                    fontWeight: FontWeight.w700,
                                  ),
                        ),
                      ],
                    ),
                    const SizedBox(width: PayaboSpacing.xs),
                    IconButton(
                      key: Key('budget-expand-${category.id}'),
                      onPressed: onExpandToggle,
                      splashRadius: 20,
                      icon: Icon(
                        expanded
                            ? Icons.keyboard_arrow_up_rounded
                            : Icons.keyboard_arrow_down_rounded,
                        color: c.accentBrownMuted,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: PayaboSpacing.lg),
                _BudgetProgressBar(
                  value: state.progress,
                  color: state.progressColorRole.resolve(c),
                ),
                const SizedBox(height: PayaboSpacing.sm),
                Row(
                  children: <Widget>[
                    Expanded(
                      child: Text(
                        '${state.percentUsedLabel} used',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                              color: c.accentBrownMuted,
                            ),
                      ),
                    ),
                    _BudgetStatusPill(
                      label: state.statusLabel,
                      foregroundColor: state.progressColorRole.resolve(c),
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
                                color: c.spendingQuickActionBorder,
                              ),
                              const SizedBox(height: PayaboSpacing.lg),
                              Wrap(
                                spacing: PayaboSpacing.sm,
                                runSpacing: PayaboSpacing.sm,
                                children: <Widget>[
                                  _BudgetDetailChip(
                                    label: 'Spent',
                                    value: formatSpendingBudgetCurrency(
                                        category.spent),
                                  ),
                                  _BudgetDetailChip(
                                    label: 'Remaining',
                                    value: state.remainingLabel,
                                    valueColor:
                                        state.remainingColorRole.resolve(c),
                                  ),
                                ],
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
    this.valueColor,
  });

  final String label;
  final String value;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        color: c.spendingCardWarm,
        borderRadius: BorderRadius.circular(PayaboRadii.pill),
        border: Border.all(color: c.spendingQuickActionBorder),
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
                    color: c.muted,
                    fontWeight: FontWeight.w600,
                  ),
            ),
            TextSpan(
              text: value,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: valueColor ?? c.accentBrown,
                    fontWeight: FontWeight.w700,
                  ),
            ),
          ],
        ),
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
    final c = context.colors;

    return ClipRRect(
      borderRadius: BorderRadius.circular(PayaboRadii.pill),
      child: LinearProgressIndicator(
        minHeight: 10,
        value: value.clamp(0, 1),
        backgroundColor: c.border,
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
