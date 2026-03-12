import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_gradients.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import 'spending_budget_data.dart';
import 'spending_budget_state.dart';

class SpendingBudgetDetailScreen extends ConsumerStatefulWidget {
  const SpendingBudgetDetailScreen({
    super.key,
    required this.budgetId,
  });

  final String budgetId;

  @override
  ConsumerState<SpendingBudgetDetailScreen> createState() =>
      _SpendingBudgetDetailScreenState();
}

class _SpendingBudgetDetailScreenState
    extends ConsumerState<SpendingBudgetDetailScreen> {
  static const double _amountStep = 25;

  late String _selectedBudgetId;
  late double _draftAmount;
  bool _needsRepositorySync = true;

  @override
  void initState() {
    super.initState();
    _selectedBudgetId = widget.budgetId;
    _draftAmount = 0;
  }

  @override
  void didUpdateWidget(covariant SpendingBudgetDetailScreen oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.budgetId != widget.budgetId) {
      _selectedBudgetId = widget.budgetId;
      _draftAmount = 0;
      _needsRepositorySync = true;
    }
  }

  @override
  Widget build(BuildContext context) {
    final AsyncValue<List<SpendingBudgetCategory>> budgetsValue =
        ref.watch(spendingBudgetsProvider);
    final List<SpendingBudgetCategory> categories =
        budgetsValue.value ?? const <SpendingBudgetCategory>[];
    final SpendingBudgetCategory? selectedCategory =
        categories.isEmpty ? null : _resolveSelectedCategory(categories);

    if (_needsRepositorySync && selectedCategory != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) {
          return;
        }

        setState(() {
          _selectedBudgetId = selectedCategory.id;
          _draftAmount = selectedCategory.allocated;
          _needsRepositorySync = false;
        });
      });
    }

    return Scaffold(
      backgroundColor: PayaboColors.surfaceWarm,
      bottomNavigationBar: _BudgetDetailActionBar(
        onSave: selectedCategory == null
            ? null
            : () => unawaited(_handleSave(selectedCategory)),
        onDelete: selectedCategory == null
            ? null
            : () => unawaited(_handleDelete(selectedCategory)),
      ),
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: PayaboGradients.warmScreen,
        ),
        child: SafeArea(
          child: Column(
            children: <Widget>[
              _BudgetDetailHeader(onBackTap: _handleBack),
              Expanded(
                child: budgetsValue.when(
                  data: (List<SpendingBudgetCategory> loadedCategories) {
                    if (loadedCategories.isEmpty) {
                      return const _FreshBudgetDetailState();
                    }

                    final SpendingBudgetCategory currentCategory =
                        _resolveSelectedCategory(loadedCategories);

                    return _BudgetDetailBody(
                      categories: loadedCategories,
                      selectedCategory: currentCategory,
                      draftAmount: _draftAmount,
                      onDecrease: () => _adjustBudget(-_amountStep),
                      onIncrease: () => _adjustBudget(_amountStep),
                      onSelectCategory: _showCategoryPicker,
                      onViewTransactions: () =>
                          _handleViewTransactions(currentCategory),
                    );
                  },
                  loading: () =>
                      const Center(child: CircularProgressIndicator()),
                  error: (Object error, StackTrace stackTrace) {
                    return Center(
                      child: Padding(
                        padding: const EdgeInsets.all(PayaboSpacing.xl),
                        child: Text('Unable to load this budget: $error'),
                      ),
                    );
                  },
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  SpendingBudgetCategory _resolveSelectedCategory(
    List<SpendingBudgetCategory> categories,
  ) {
    for (final SpendingBudgetCategory category in categories) {
      if (category.id == _selectedBudgetId) {
        return category;
      }
    }

    return categories.first;
  }

  void _adjustBudget(double delta) {
    setState(() {
      _draftAmount = math.max(0, _draftAmount + delta);
    });
  }

  Future<void> _showCategoryPicker(
    List<SpendingBudgetCategory> categories,
  ) async {
    final SpendingBudgetCategory? selected =
        await showModalBottomSheet<SpendingBudgetCategory>(
      context: context,
      backgroundColor: PayaboColors.surfaceWarmElevated,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(PayaboRadii.xl),
        ),
      ),
      builder: (BuildContext context) {
        return SafeArea(
          top: false,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(
              PayaboSpacing.xl,
              PayaboSpacing.lg,
              PayaboSpacing.xl,
              PayaboSpacing.xl,
            ),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxHeight: 420),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Center(
                    child: Container(
                      width: 56,
                      height: 4,
                      decoration: BoxDecoration(
                        color: PayaboColors.borderWarm,
                        borderRadius: BorderRadius.circular(PayaboRadii.pill),
                      ),
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.lg),
                  Text(
                    'Switch budget',
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          color: PayaboColors.accentBrown,
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.xs),
                  Text(
                    'Move between categories to edit another monthly budget.',
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: PayaboColors.accentBrownMuted,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.lg),
                  Flexible(
                    child: SingleChildScrollView(
                      child: Column(
                        children: categories
                            .map(
                              (SpendingBudgetCategory category) => Padding(
                                padding: const EdgeInsets.only(
                                    bottom: PayaboSpacing.sm),
                                child: _BudgetPickerTile(
                                  category: category,
                                  selected: category.id == _selectedBudgetId,
                                  onTap: () =>
                                      Navigator.of(context).pop(category),
                                ),
                              ),
                            )
                            .toList(),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );

    if (selected == null || !mounted) {
      return;
    }

    setState(() {
      _selectedBudgetId = selected.id;
      _draftAmount = selected.allocated;
      _needsRepositorySync = false;
    });
  }

  void _handleBack() {
    if (context.canPop()) {
      context.pop();
      return;
    }

    context.go('/spending/budgets');
  }

  Future<void> _handleSave(SpendingBudgetCategory category) async {
    final messenger = ScaffoldMessenger.of(context);

    await ref.read(budgetRepositoryProvider).saveBudgetAmount(
          budgetId: category.id,
          totalAllocated: _draftAmount,
        );
    ref.invalidate(spendingBudgetsProvider);

    if (!mounted) {
      return;
    }

    messenger.showSnackBar(
      SnackBar(
        content: Text(
          'Saved ${category.name.toLowerCase()} budget at ${formatSpendingBudgetCurrency(_draftAmount)} in this mock build.',
        ),
      ),
    );
  }

  Future<void> _handleDelete(SpendingBudgetCategory category) async {
    final bool? confirmed = await showDialog<bool>(
      context: context,
      builder: (BuildContext context) {
        return AlertDialog(
          backgroundColor: PayaboColors.white,
          title: Text(
            'Delete ${category.name.toLowerCase()} budget?',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: PayaboColors.accentBrown,
                  fontWeight: FontWeight.w700,
                ),
          ),
          content: Text(
            'This mock action removes the budget and returns you to your budget list.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: PayaboColors.accentBrownMuted,
                ),
          ),
          actions: <Widget>[
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('Cancel'),
            ),
            TextButton(
              onPressed: () => Navigator.of(context).pop(true),
              child: const Text('Delete'),
            ),
          ],
        );
      },
    );

    if (confirmed != true || !mounted) {
      return;
    }

    final messenger = ScaffoldMessenger.of(context);

    await ref.read(budgetRepositoryProvider).deleteBudget(category.id);
    ref.invalidate(spendingBudgetsProvider);

    if (!mounted) {
      return;
    }

    messenger.showSnackBar(
      SnackBar(
        content: Text(
          '${category.name} budget deleted in this mock build.',
        ),
      ),
    );
    context.go('/spending/budgets');
  }

  void _handleViewTransactions(SpendingBudgetCategory category) {
    final String? linkedCategoryId = category.linkedSpendingCategoryId;

    if (linkedCategoryId == null) {
      context.go('/spending');
      return;
    }

    context.go('/spending/category/$linkedCategoryId');
  }
}

class _BudgetDetailHeader extends StatelessWidget {
  const _BudgetDetailHeader({required this.onBackTap});

  final VoidCallback onBackTap;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.xl,
        PayaboSpacing.md,
        PayaboSpacing.xl,
        PayaboSpacing.lg,
      ),
      child: Row(
        children: <Widget>[
          _BudgetDetailIconButton(
            icon: Icons.arrow_back_ios_new_rounded,
            onTap: onBackTap,
          ),
          Expanded(
            child: Text(
              'Monthly budget',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                    color: PayaboColors.accentBrown,
                    fontWeight: FontWeight.w800,
                  ),
            ),
          ),
          const SizedBox(width: 42),
        ],
      ),
    );
  }
}

class _BudgetDetailIconButton extends StatelessWidget {
  const _BudgetDetailIconButton({
    required this.icon,
    required this.onTap,
  });

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Ink(
      width: 42,
      height: 42,
      decoration: BoxDecoration(
        color: PayaboColors.white.withValues(alpha: 0.82),
        shape: BoxShape.circle,
        border: Border.all(color: PayaboColors.borderWarm),
      ),
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: Icon(
          icon,
          size: 18,
          color: PayaboColors.headerIconAccent,
        ),
      ),
    );
  }
}

class _BudgetDetailBody extends StatelessWidget {
  const _BudgetDetailBody({
    required this.categories,
    required this.selectedCategory,
    required this.draftAmount,
    required this.onDecrease,
    required this.onIncrease,
    required this.onSelectCategory,
    required this.onViewTransactions,
  });

  final List<SpendingBudgetCategory> categories;
  final SpendingBudgetCategory selectedCategory;
  final double draftAmount;
  final VoidCallback onDecrease;
  final VoidCallback onIncrease;
  final Future<void> Function(List<SpendingBudgetCategory> categories)
      onSelectCategory;
  final VoidCallback onViewTransactions;

  @override
  Widget build(BuildContext context) {
    final SpendingBudgetState draftState = SpendingBudgetState.fromBudget(
      allocated: draftAmount,
      spent: selectedCategory.spent,
    );

    return ListView(
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.xl,
        PayaboSpacing.sm,
        PayaboSpacing.xl,
        PayaboSpacing.x3,
      ),
      children: <Widget>[
        Center(
          child: _BudgetCategorySelector(
            category: selectedCategory,
            onTap: () => onSelectCategory(categories),
          ),
        ),
        const SizedBox(height: PayaboSpacing.x2),
        _BudgetAmountEditor(
          amount: draftAmount,
          onDecrease: onDecrease,
          onIncrease: onIncrease,
        ),
        const SizedBox(height: PayaboSpacing.lg),
        Center(
          child: Text(
            'Adjust in £25 steps and compare against how this category has been performing all year.',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: PayaboColors.accentBrownMuted,
                  height: 1.45,
                ),
          ),
        ),
        const SizedBox(height: PayaboSpacing.xl),
        _BudgetDetailMetrics(
          category: selectedCategory,
          draftState: draftState,
          draftAmount: draftAmount,
        ),
        const SizedBox(height: PayaboSpacing.xl),
        _BudgetHistoryCard(
          category: selectedCategory,
          targetAmount: draftAmount,
        ),
        const SizedBox(height: PayaboSpacing.xl),
        Center(
          child: TextButton(
            key: const Key('budget-view-transactions'),
            onPressed: onViewTransactions,
            child: Text(
              'View transactions',
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: PayaboColors.primary,
                    fontWeight: FontWeight.w700,
                  ),
            ),
          ),
        ),
      ],
    );
  }
}

class _BudgetCategorySelector extends StatelessWidget {
  const _BudgetCategorySelector({
    required this.category,
    required this.onTap,
  });

  final SpendingBudgetCategory category;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        key: const Key('budget-category-selector'),
        onTap: onTap,
        borderRadius: BorderRadius.circular(PayaboRadii.pill),
        child: Ink(
          decoration: BoxDecoration(
            color: PayaboColors.white.withValues(alpha: 0.86),
            borderRadius: BorderRadius.circular(PayaboRadii.pill),
            border: Border.all(color: PayaboColors.spendingQuickActionBorder),
            boxShadow: PayaboShadows.soft,
          ),
          padding: const EdgeInsets.symmetric(
            horizontal: PayaboSpacing.lg,
            vertical: PayaboSpacing.md,
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              _BudgetCategoryBadge(category: category),
              const SizedBox(width: PayaboSpacing.md),
              Flexible(
                child: Text(
                  category.name,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        color: PayaboColors.accentBrown,
                        fontWeight: FontWeight.w800,
                      ),
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),
              const Icon(
                Icons.keyboard_arrow_down_rounded,
                color: PayaboColors.accentBrownMuted,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _BudgetCategoryBadge extends StatelessWidget {
  const _BudgetCategoryBadge({required this.category});

  final SpendingBudgetCategory category;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 44,
      height: 44,
      decoration: BoxDecoration(
        color: category.accentColor.withValues(alpha: 0.12),
        shape: BoxShape.circle,
      ),
      child: Icon(
        category.icon,
        color: category.accentColor,
        size: 24,
      ),
    );
  }
}

class _BudgetAmountEditor extends StatelessWidget {
  const _BudgetAmountEditor({
    required this.amount,
    required this.onDecrease,
    required this.onIncrease,
  });

  final double amount;
  final VoidCallback onDecrease;
  final VoidCallback onIncrease;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        _BudgetAdjustButton(
          icon: Icons.remove,
          semanticLabel: 'Decrease budget',
          onTap: onDecrease,
        ),
        const SizedBox(width: PayaboSpacing.md),
        Expanded(
          child: Container(
            decoration: BoxDecoration(
              color: PayaboColors.white.withValues(alpha: 0.82),
              borderRadius: BorderRadius.circular(PayaboRadii.xl),
              border: Border.all(color: PayaboColors.spendingQuickActionBorder),
              boxShadow: PayaboShadows.soft,
            ),
            padding: const EdgeInsets.symmetric(
              horizontal: PayaboSpacing.lg,
              vertical: PayaboSpacing.x2,
            ),
            child: FittedBox(
              fit: BoxFit.scaleDown,
              child: Text(
                formatSpendingBudgetCurrency(amount),
                key: const Key('budget-amount-value'),
                style: Theme.of(context).textTheme.displaySmall?.copyWith(
                      color: PayaboColors.accentBrown,
                      fontWeight: FontWeight.w800,
                    ),
              ),
            ),
          ),
        ),
        const SizedBox(width: PayaboSpacing.md),
        _BudgetAdjustButton(
          icon: Icons.add,
          semanticLabel: 'Increase budget',
          onTap: onIncrease,
        ),
      ],
    );
  }
}

class _BudgetAdjustButton extends StatelessWidget {
  const _BudgetAdjustButton({
    required this.icon,
    required this.semanticLabel,
    required this.onTap,
  });

  final IconData icon;
  final String semanticLabel;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: semanticLabel,
      child: Ink(
        width: 60,
        height: 60,
        decoration: BoxDecoration(
          color: PayaboColors.white.withValues(alpha: 0.86),
          shape: BoxShape.circle,
          border: Border.all(color: PayaboColors.borderWarm),
          boxShadow: PayaboShadows.soft,
        ),
        child: IconButton(
          onPressed: onTap,
          icon: Icon(
            icon,
            color: PayaboColors.headerIconAccent,
          ),
        ),
      ),
    );
  }
}

class _BudgetDetailMetrics extends StatelessWidget {
  const _BudgetDetailMetrics({
    required this.category,
    required this.draftState,
    required this.draftAmount,
  });

  final SpendingBudgetCategory category;
  final SpendingBudgetState draftState;
  final double draftAmount;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: PayaboSpacing.md,
      runSpacing: PayaboSpacing.md,
      children: <Widget>[
        _BudgetMetricChip(
          label: 'Spent this month',
          value: formatSpendingBudgetCurrency(category.spent),
        ),
        _BudgetMetricChip(
          label: 'Left after update',
          value: draftState.remainingLabel,
          valueColor: draftState.remainingColor,
        ),
        _BudgetMetricChip(
          label: 'Budget lines',
          value: '${category.lineItems.length}',
        ),
        _BudgetMetricChip(
          label: 'Target',
          value: formatSpendingBudgetCurrency(draftAmount),
          valueColor: draftState.progressColor,
        ),
      ],
    );
  }
}

class _BudgetMetricChip extends StatelessWidget {
  const _BudgetMetricChip({
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
        color: PayaboColors.spendingCardWarmElevated,
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

class _BudgetHistoryCard extends StatelessWidget {
  const _BudgetHistoryCard({
    required this.category,
    required this.targetAmount,
  });

  final SpendingBudgetCategory category;
  final double targetAmount;

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
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  'Year 2026',
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        color: PayaboColors.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ),
              _BudgetTargetChip(targetAmount: targetAmount),
            ],
          ),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            'Orange bars show the current month and periods that reached this target.',
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: PayaboColors.accentBrownMuted,
                ),
          ),
          const SizedBox(height: PayaboSpacing.xl),
          _BudgetHistoryChart(
            history: category.history,
            targetAmount: targetAmount,
          ),
        ],
      ),
    );
  }
}

class _BudgetTargetChip extends StatelessWidget {
  const _BudgetTargetChip({required this.targetAmount});

  final double targetAmount;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: PayaboColors.primary.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(PayaboRadii.pill),
      ),
      padding: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.md,
        vertical: PayaboSpacing.sm,
      ),
      child: Text(
        'Target ${formatSpendingBudgetCurrency(targetAmount)}',
        style: Theme.of(context).textTheme.bodySmall?.copyWith(
              color: PayaboColors.primary,
              fontWeight: FontWeight.w700,
            ),
      ),
    );
  }
}

class _BudgetHistoryChart extends StatelessWidget {
  const _BudgetHistoryChart({
    required this.history,
    required this.targetAmount,
  });

  final List<SpendingBudgetHistoryPoint> history;
  final double targetAmount;

  @override
  Widget build(BuildContext context) {
    final double peakHistoryAmount = history.fold<double>(
      targetAmount,
      (double currentMax, SpendingBudgetHistoryPoint point) =>
          math.max(currentMax, point.amount),
    );
    final double chartMax = math.max(targetAmount, peakHistoryAmount) * 1.15;

    return SizedBox(
      height: 260,
      child: LayoutBuilder(
        builder: (BuildContext context, BoxConstraints constraints) {
          final double labelHeight = 28;
          final double barAreaHeight = constraints.maxHeight - labelHeight;
          final double targetRatio =
              chartMax == 0 ? 0 : targetAmount / chartMax;
          final double targetTop =
              (1 - targetRatio.clamp(0.0, 1.0)) * (barAreaHeight - 24);

          return Stack(
            children: <Widget>[
              Positioned(
                top: targetTop,
                left: 0,
                right: 0,
                child: const SizedBox(
                  height: 2,
                  child: CustomPaint(
                    painter: _BudgetDashedLinePainter(),
                  ),
                ),
              ),
              Positioned.fill(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: history.map(
                    (SpendingBudgetHistoryPoint point) {
                      final double height = chartMax == 0
                          ? 0
                          : (point.amount / chartMax) * (barAreaHeight - 12);

                      return Expanded(
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.end,
                          children: <Widget>[
                            AnimatedContainer(
                              duration: const Duration(milliseconds: 180),
                              width: 18,
                              height: height.clamp(18, barAreaHeight - 10),
                              decoration: BoxDecoration(
                                color: point.isCurrent ||
                                        point.amount >= targetAmount
                                    ? PayaboColors.primary
                                    : PayaboColors.border,
                                borderRadius: BorderRadius.circular(
                                  PayaboRadii.lg,
                                ),
                              ),
                            ),
                            const SizedBox(height: PayaboSpacing.md),
                            Text(
                              point.label,
                              style: Theme.of(context)
                                  .textTheme
                                  .labelMedium
                                  ?.copyWith(
                                    color: PayaboColors.accentBrownMuted,
                                    fontWeight: FontWeight.w700,
                                  ),
                            ),
                          ],
                        ),
                      );
                    },
                  ).toList(),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _BudgetDashedLinePainter extends CustomPainter {
  const _BudgetDashedLinePainter();

  @override
  void paint(Canvas canvas, Size size) {
    final Paint paint = Paint()
      ..color = PayaboColors.primary
      ..strokeWidth = 2
      ..style = PaintingStyle.stroke;

    const double dashWidth = 12;
    const double dashSpace = 10;
    double startX = 0;

    while (startX < size.width) {
      canvas.drawLine(
        Offset(startX, size.height / 2),
        Offset(math.min(startX + dashWidth, size.width), size.height / 2),
        paint,
      );
      startX += dashWidth + dashSpace;
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

class _BudgetPickerTile extends StatelessWidget {
  const _BudgetPickerTile({
    required this.category,
    required this.selected,
    required this.onTap,
  });

  final SpendingBudgetCategory category;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(PayaboRadii.xl),
        child: Ink(
          decoration: BoxDecoration(
            color: selected
                ? PayaboColors.white
                : PayaboColors.spendingCardWarmElevated,
            borderRadius: BorderRadius.circular(PayaboRadii.xl),
            border: Border.all(
              color: selected
                  ? PayaboColors.spendingInsightBorder
                  : PayaboColors.spendingQuickActionBorder,
            ),
          ),
          padding: const EdgeInsets.all(PayaboSpacing.lg),
          child: Row(
            children: <Widget>[
              _BudgetCategoryBadge(category: category),
              const SizedBox(width: PayaboSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      category.name,
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            color: PayaboColors.accentBrown,
                            fontWeight: FontWeight.w700,
                          ),
                    ),
                    const SizedBox(height: PayaboSpacing.xs),
                    Text(
                      formatSpendingBudgetCurrency(category.allocated),
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                            color: PayaboColors.accentBrownMuted,
                          ),
                    ),
                  ],
                ),
              ),
              if (selected)
                const Icon(
                  Icons.check_circle_rounded,
                  color: PayaboColors.primary,
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _BudgetDetailActionBar extends StatelessWidget {
  const _BudgetDetailActionBar({
    required this.onSave,
    required this.onDelete,
  });

  final VoidCallback? onSave;
  final VoidCallback? onDelete;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: PayaboColors.white,
        border: Border(
          top: BorderSide(color: PayaboColors.spendingQuickActionBorder),
        ),
      ),
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.xl,
        PayaboSpacing.lg,
        PayaboSpacing.xl,
        PayaboSpacing.lg,
      ),
      child: SafeArea(
        top: false,
        child: Row(
          children: <Widget>[
            Expanded(
              child: PayaboButton(
                key: const Key('budget-detail-save'),
                label: 'Save',
                size: PayaboButtonSize.lg,
                onPressed: onSave,
              ),
            ),
            const SizedBox(width: PayaboSpacing.lg),
            Expanded(
              child: PayaboButton(
                key: const Key('budget-detail-delete'),
                label: 'Delete',
                variant: PayaboButtonVariant.secondary,
                size: PayaboButtonSize.lg,
                onPressed: onDelete,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _FreshBudgetDetailState extends StatelessWidget {
  const _FreshBudgetDetailState();

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.xl,
        PayaboSpacing.md,
        PayaboSpacing.xl,
        PayaboSpacing.x3,
      ),
      children: <Widget>[
        Container(
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
                'No budgets available yet',
                style: Theme.of(context).textTheme.titleLarge?.copyWith(
                      color: PayaboColors.accentBrown,
                      fontWeight: FontWeight.w700,
                    ),
              ),
              const SizedBox(height: PayaboSpacing.sm),
              Text(
                'Fresh demo mode clears the seeded budget categories, so there is nothing to edit on this detail view yet.',
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: PayaboColors.muted,
                      height: 1.45,
                    ),
              ),
              const SizedBox(height: PayaboSpacing.lg),
              Text(
                'Switch back to Populated demo data in Profile to preview budget editing, category switching, and yearly comparisons.',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: PayaboColors.chatTextSecondary,
                    ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
