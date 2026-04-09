import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import 'spending_budget_data.dart';
import 'spending_budget_state.dart';
import 'widgets/budget_category_picker.dart';
import 'widgets/spending_budget_empty_state.dart';
import 'widgets/spending_section_pills.dart';

const List<SpendingSection> _visibleSpendingSections = <SpendingSection>[
  SpendingSection.transactions,
  SpendingSection.budgets,
  SpendingSection.bills,
  SpendingSection.accounts,
];

// ─────────────────────────────────────────────────────────
//  Screen
// ─────────────────────────────────────────────────────────

class SpendingBudgetScreen extends ConsumerStatefulWidget {
  const SpendingBudgetScreen({super.key});

  @override
  ConsumerState<SpendingBudgetScreen> createState() =>
      _SpendingBudgetScreenState();
}

class _SpendingBudgetScreenState extends ConsumerState<SpendingBudgetScreen> {
  bool _isCreatingBudget = false;

  final ValueNotifier<double> _statusBarProgress = ValueNotifier<double>(0.0);

  @override
  void dispose() {
    _statusBarProgress.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final AsyncValue<List<SpendingBudgetCategory>> budgetsValue =
        ref.watch(spendingBudgetsProvider);

    return budgetsValue.when(
      data: (List<SpendingBudgetCategory> categories) {
        if (categories.isEmpty) {
          return Scaffold(
            backgroundColor: c.surfaceWarm,
            body: _BudgetEmptyLayout(
              isCreatingBudget: _isCreatingBudget,
              onCreateBudget: _handleCreateBudget,
              onSectionSelected: _handleSectionSelected,
            ),
            bottomNavigationBar: const PayaboPrimaryAppShell(
              destination: PayaboPrimaryDestination.spending,
            ),
          );
        }

        final SpendingBudgetSummary summary =
            SpendingBudgetSummary.fromCategories(
          monthLabel: spendingBudgetMonthLabel,
          categories: categories,
        );

        return PayaboWarmScaffold(
          backgroundDecoration: BoxDecoration(
            gradient: LinearGradient(
              colors: c.isDark
                  ? const <Color>[Color(0xFF1A1A1A), Color(0xFF121212)]
                  : const <Color>[Color(0xFF2C1810), Color(0xFF1A0E08)],
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
            ),
          ),
          statusBarColorNotifier: _statusBarProgress,
          bottomNavigationBar: const PayaboPrimaryAppShell(
            destination: PayaboPrimaryDestination.spending,
          ),
          body: _BudgetHeroAndSheet(
            summary: summary,
            categories: categories,
            isCreatingBudget: _isCreatingBudget,
            onSectionSelected: _handleSectionSelected,
            onCreateBudget: _handleCreateBudget,
            onOpenCategory: (String id) => context.push(
              '/spending/budgets/$id',
            ),
            onRefresh: () async {
              ref.invalidate(spendingBudgetsProvider);
              await ref.read(spendingBudgetsProvider.future);
            },
            onSheetExtentChanged: (double extent) {
              _statusBarProgress.value = extent;
            },
          ),
        );
      },
      loading: () => Scaffold(
        backgroundColor: c.surfaceWarm,
        body: const Center(child: CircularProgressIndicator()),
        bottomNavigationBar: const PayaboPrimaryAppShell(
          destination: PayaboPrimaryDestination.spending,
        ),
      ),
      error: (Object error, StackTrace stackTrace) {
        return Scaffold(
          backgroundColor: c.surfaceWarm,
          body: Center(
            child: Padding(
              padding: const EdgeInsets.all(PayaboSpacing.xl),
              child: Text('Unable to load budgets: $error'),
            ),
          ),
          bottomNavigationBar: const PayaboPrimaryAppShell(
            destination: PayaboPrimaryDestination.spending,
          ),
        );
      },
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
      case SpendingSection.bills:
        context.go('/spending/bills');
        return;
      case SpendingSection.accounts:
        context.go('/spending/accounts');
        return;
    }
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
      await ref.read(spendingBudgetsProvider.future);

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

// ─────────────────────────────────────────────────────────
//  Empty state — full screen, no hero/sheet
// ─────────────────────────────────────────────────────────

class _BudgetEmptyLayout extends StatelessWidget {
  const _BudgetEmptyLayout({
    required this.isCreatingBudget,
    required this.onCreateBudget,
    required this.onSectionSelected,
  });

  final bool isCreatingBudget;
  final VoidCallback onCreateBudget;
  final ValueChanged<SpendingSection> onSectionSelected;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return DecoratedBox(
      decoration: BoxDecoration(gradient: c.warmScreenGradient),
      child: SafeArea(
        child: Column(
          children: <Widget>[
            PayaboAppHeader(
              title: 'Spend',
              titleStyle: Theme.of(context).textTheme.headlineLarge?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: c.accentBrown,
                  ),
              bottom: SpendingSectionPills(
                selectedSection: SpendingSection.budgets,
                sections: _visibleSpendingSections,
                onSelected: onSectionSelected,
              ),
            ),
            Expanded(
              child: LayoutBuilder(
                builder: (BuildContext context, BoxConstraints constraints) {
                  return SingleChildScrollView(
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
                        busy: isCreatingBudget,
                        onPressed: onCreateBudget,
                      ),
                    ),
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

// ─────────────────────────────────────────────────────────
//  Hero + Pinned Header + DraggableScrollableSheet
// ─────────────────────────────────────────────────────────

class _BudgetHeroAndSheet extends StatefulWidget {
  const _BudgetHeroAndSheet({
    required this.summary,
    required this.categories,
    required this.isCreatingBudget,
    required this.onSectionSelected,
    required this.onCreateBudget,
    required this.onOpenCategory,
    required this.onRefresh,
    this.onSheetExtentChanged,
  });

  static const double _maxSheetSize = 1.0;
  static const double _pinnedHeaderHeight = 76;
  static const double _sheetTopGap = 10;
  static const double _minHeroHeight = 200;
  static const double _maxHeroHeight = 248;

  final SpendingBudgetSummary summary;
  final List<SpendingBudgetCategory> categories;
  final bool isCreatingBudget;
  final ValueChanged<SpendingSection> onSectionSelected;
  final VoidCallback onCreateBudget;
  final ValueChanged<String> onOpenCategory;
  final Future<void> Function() onRefresh;
  final ValueChanged<double>? onSheetExtentChanged;

  @override
  State<_BudgetHeroAndSheet> createState() => _BudgetHeroAndSheetState();
}

class _BudgetHeroAndSheetState extends State<_BudgetHeroAndSheet> {
  late final DraggableScrollableController _sheetController;
  late final ValueNotifier<double> _sheetExtentNotifier;

  @override
  void initState() {
    super.initState();
    _sheetController = DraggableScrollableController();
    _sheetExtentNotifier = ValueNotifier<double>(0);
    _sheetController.addListener(_syncSheetExtent);
  }

  void _syncSheetExtent() {
    if (!_sheetController.isAttached) return;

    final double nextExtent = _sheetController.size;
    if ((_sheetExtentNotifier.value - nextExtent).abs() > 0.001) {
      final SchedulerPhase phase = WidgetsBinding.instance.schedulerPhase;

      if (phase == SchedulerPhase.idle ||
          phase == SchedulerPhase.postFrameCallbacks) {
        _sheetExtentNotifier.value = nextExtent;
        widget.onSheetExtentChanged?.call(nextExtent);
        return;
      }

      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted || !_sheetController.isAttached) return;
        if ((_sheetExtentNotifier.value - nextExtent).abs() > 0.001) {
          _sheetExtentNotifier.value = nextExtent;
          widget.onSheetExtentChanged?.call(nextExtent);
        }
      });
    }
  }

  @override
  void dispose() {
    _sheetController.removeListener(_syncSheetExtent);
    _sheetController.dispose();
    _sheetExtentNotifier.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (BuildContext context, BoxConstraints constraints) {
        final double viewportHeight =
            constraints.maxHeight.isFinite ? constraints.maxHeight : 640;

        final double heroHeight = math.min(
          _BudgetHeroAndSheet._maxHeroHeight,
          math.max(
            _BudgetHeroAndSheet._minHeroHeight,
            viewportHeight * 0.37,
          ),
        );

        const double pinnedHeaderHeight =
            _BudgetHeroAndSheet._pinnedHeaderHeight;
        const double pinnedSheetTop =
            pinnedHeaderHeight + _BudgetHeroAndSheet._sheetTopGap;

        final double sheetViewportHeight =
            math.max(1, viewportHeight - pinnedHeaderHeight);

        final double collapsedSheetTop = math.max(
          pinnedSheetTop + 164,
          heroHeight + PayaboSpacing.sm,
        );

        final double initialSheetSize = (1 -
                ((collapsedSheetTop - pinnedHeaderHeight) /
                    sheetViewportHeight))
            .clamp(0.62, 0.76)
            .toDouble();
        final double minSheetSize =
            (initialSheetSize - 0.10).clamp(0.56, initialSheetSize).toDouble();

        final double heroBottomPadding = math.max(
          40,
          heroHeight - collapsedSheetTop + 28,
        );

        return Stack(
          children: <Widget>[
            // ── LAYER 1: Hero banner — budget summary on dark gradient ──
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: heroHeight,
              child: _BudgetHeroBanner(
                summary: widget.summary,
                bottomPadding: heroBottomPadding,
              ),
            ),

            // ── LAYER 2: Pinned header (profile + bell, 76px) ──
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: pinnedHeaderHeight,
              child: ValueListenableBuilder<double>(
                valueListenable: _sheetExtentNotifier,
                builder: (
                  BuildContext context,
                  double sheetExtent,
                  Widget? _,
                ) {
                  final double eff =
                      (sheetExtent <= 0 ? initialSheetSize : sheetExtent)
                          .clamp(
                            minSheetSize,
                            _BudgetHeroAndSheet._maxSheetSize,
                          )
                          .toDouble();
                  const double fadeZone = 0.05;
                  final double fadeStart = math.max(
                    0.0,
                    _BudgetHeroAndSheet._maxSheetSize - fadeZone,
                  );
                  final double bgProgress = Curves.easeOut.transform(
                    ((eff - fadeStart) / fadeZone).clamp(0.0, 1.0).toDouble(),
                  );
                  return _BudgetPinnedHeader(
                    backgroundProgress: bgProgress,
                  );
                },
              ),
            ),

            // ── LAYER 3: Draggable sheet — pills + category list ──
            Positioned(
              top: pinnedHeaderHeight,
              left: 0,
              right: 0,
              bottom: 0,
              child: DraggableScrollableSheet(
                controller: _sheetController,
                initialChildSize: initialSheetSize,
                minChildSize: minSheetSize,
                maxChildSize: _BudgetHeroAndSheet._maxSheetSize,
                snap: true,
                snapSizes: <double>[
                  initialSheetSize,
                  _BudgetHeroAndSheet._maxSheetSize,
                ],
                builder: (
                  BuildContext context,
                  ScrollController scrollController,
                ) {
                  return ValueListenableBuilder<double>(
                    valueListenable: _sheetExtentNotifier,
                    builder: (
                      BuildContext context,
                      double extent,
                      Widget? child,
                    ) {
                      const double fadeZone = 0.05;
                      final double fadeFraction = ((extent -
                                  (_BudgetHeroAndSheet._maxSheetSize -
                                      fadeZone)) /
                              fadeZone)
                          .clamp(0.0, 1.0);
                      return _BudgetSheet(
                        scrollController: scrollController,
                        topBorderRadius: 24.0 * (1.0 - fadeFraction),
                        summary: widget.summary,
                        categories: widget.categories,
                        isCreatingBudget: widget.isCreatingBudget,
                        onSectionSelected: widget.onSectionSelected,
                        onCreateBudget: widget.onCreateBudget,
                        onOpenCategory: widget.onOpenCategory,
                      );
                    },
                  );
                },
              ),
            ),
          ],
        );
      },
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Pinned header — profile + notification bell (76px)
// ─────────────────────────────────────────────────────────

class _BudgetPinnedHeader extends StatelessWidget {
  const _BudgetPinnedHeader({required this.backgroundProgress});

  final double backgroundProgress;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Stack(
      children: <Widget>[
        Positioned.fill(
          child: Opacity(
            opacity: backgroundProgress,
            child: ColoredBox(color: c.surfaceBase),
          ),
        ),
        const Positioned.fill(
          child: PayaboAppHeader(
            padding: EdgeInsets.fromLTRB(
              PayaboSpacing.xl,
              PayaboSpacing.md,
              PayaboSpacing.xl,
              0,
            ),
          ),
        ),
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Hero banner — budget summary on dark gradient
// ─────────────────────────────────────────────────────────

class _BudgetHeroBanner extends StatelessWidget {
  const _BudgetHeroBanner({
    required this.summary,
    this.bottomPadding = 40,
  });

  final SpendingBudgetSummary summary;
  final double bottomPadding;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return SizedBox(
      width: double.infinity,
      child: Padding(
        padding: EdgeInsets.fromLTRB(
          PayaboSpacing.xl,
          0,
          PayaboSpacing.xl,
          bottomPadding,
        ),
        child: Align(
          alignment: Alignment.bottomLeft,
          child: LayoutBuilder(
            builder: (BuildContext context, BoxConstraints box) {
              final bool compact = box.maxHeight < 190;

              return Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  // ── Month label + status pill ──
                  Row(
                    children: <Widget>[
                      Text(
                        summary.monthLabel,
                        style: textTheme.bodyMedium?.copyWith(
                          color: Colors.white.withValues(alpha: 0.6),
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                      const SizedBox(width: PayaboSpacing.sm),
                      _BudgetStatusPill(
                        label: summary.statusLabel,
                        foregroundColor:
                            summary.statusColorRole.resolve(c),
                      ),
                    ],
                  ),
                  SizedBox(
                    height: compact ? PayaboSpacing.sm : PayaboSpacing.md,
                  ),

                  // ── Total budget amount ──
                  Text(
                    formatSpendingBudgetCurrency(summary.totalBudget),
                    style: (compact
                            ? textTheme.headlineLarge
                            : textTheme.displaySmall)
                        ?.copyWith(
                      color: Colors.white,
                      fontWeight: FontWeight.w800,
                      height: 1,
                    ),
                  ),
                  SizedBox(
                    height: compact ? PayaboSpacing.sm : PayaboSpacing.md,
                  ),

                  // ── Left to spend · Used so far ──
                  Row(
                    children: <Widget>[
                      Text(
                        summary.leftToSpendLabel,
                        style: textTheme.bodyMedium?.copyWith(
                          color: summary.leftToSpendColorRole
                              .resolve(c)
                              .withValues(alpha: 0.9),
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      Text(
                        '  left',
                        style: textTheme.bodyMedium?.copyWith(
                          color: Colors.white.withValues(alpha: 0.5),
                        ),
                      ),
                      const SizedBox(width: PayaboSpacing.lg),
                      Text(
                        formatSpendingBudgetCurrency(summary.totalSpent),
                        style: textTheme.bodyMedium?.copyWith(
                          color: Colors.white.withValues(alpha: 0.7),
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      Text(
                        '  used',
                        style: textTheme.bodyMedium?.copyWith(
                          color: Colors.white.withValues(alpha: 0.5),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: PayaboSpacing.md),

                  // ── Progress bar ──
                  _BudgetProgressBar(
                    value: summary.progress,
                    color: summary.statusColorRole.resolve(c),
                    trackColor: Colors.white.withValues(alpha: 0.15),
                  ),
                ],
              );
            },
          ),
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Sheet — section pills + category list
// ─────────────────────────────────────────────────────────

class _BudgetSheet extends StatelessWidget {
  const _BudgetSheet({
    required this.scrollController,
    required this.summary,
    required this.categories,
    required this.isCreatingBudget,
    required this.onSectionSelected,
    required this.onCreateBudget,
    required this.onOpenCategory,
    this.topBorderRadius = 24.0,
  });

  final ScrollController scrollController;
  final SpendingBudgetSummary summary;
  final List<SpendingBudgetCategory> categories;
  final bool isCreatingBudget;
  final ValueChanged<SpendingSection> onSectionSelected;
  final VoidCallback onCreateBudget;
  final ValueChanged<String> onOpenCategory;
  final double topBorderRadius;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: c.surfaceBase,
        borderRadius: BorderRadius.only(
          topLeft: Radius.circular(topBorderRadius),
          topRight: Radius.circular(topBorderRadius),
        ),
        boxShadow: topBorderRadius > 0
            ? <BoxShadow>[
                BoxShadow(
                  color:
                      Colors.black.withValues(alpha: c.isDark ? 0.22 : 0.08),
                  blurRadius: 18,
                  offset: const Offset(0, -4),
                ),
              ]
            : const <BoxShadow>[],
      ),
      child: ListView(
        controller: scrollController,
        physics: const BouncingScrollPhysics(
          parent: AlwaysScrollableScrollPhysics(),
        ),
        padding: const EdgeInsets.fromLTRB(
          PayaboSpacing.xl,
          PayaboSpacing.md,
          PayaboSpacing.xl,
          PayaboSpacing.x4,
        ),
        children: <Widget>[
          // ── Drag handle ──
          Center(
            child: Container(
              width: 42,
              height: 5,
              decoration: BoxDecoration(
                color: c.borderStrong,
                borderRadius: BorderRadius.circular(999),
              ),
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),

          // ── Section pills ──
          SpendingSectionPills(
            selectedSection: SpendingSection.budgets,
            sections: _visibleSpendingSections,
            onSelected: onSectionSelected,
          ),
          const SizedBox(height: PayaboSpacing.lg),

          // ── Section heading ──
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  'Category budgets',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ),
              _BudgetStatusPill(
                label: '${summary.categoryCount} active',
                foregroundColor: c.primary,
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.sm),

          // ── Category rows ──
          for (int i = 0; i < categories.length; i++) ...[
            _BudgetCategoryCard(
              category: categories[i],
              onOpen: () => onOpenCategory(categories[i].id),
            ),
            if (i < categories.length - 1)
              Divider(
                height: 1,
                color: c.borderStrong.withValues(alpha: 0.3),
              ),
          ],
          const SizedBox(height: PayaboSpacing.lg),

          // ── Create button ──
          Center(
            child: PayaboButton(
              key: const Key('budget-create-new'),
              label: isCreatingBudget ? 'Creating\u2026' : 'Create new budget',
              variant: PayaboButtonVariant.secondary,
              size: PayaboButtonSize.lg,
              leading: const Icon(Icons.add_rounded, size: 20),
              onPressed: isCreatingBudget ? null : onCreateBudget,
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Category row — mirrors transaction row styling
// ─────────────────────────────────────────────────────────

class _BudgetCategoryCard extends StatelessWidget {
  const _BudgetCategoryCard({
    required this.category,
    required this.onOpen,
  });

  final SpendingBudgetCategory category;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final SpendingBudgetState state = SpendingBudgetState.fromBudget(
      allocated: category.allocated,
      spent: category.spent,
    );

    final theme = Theme.of(context);

    return InkWell(
      key: Key('budget-card-${category.id}'),
      onTap: onOpen,
      borderRadius: PayaboRadii.radiusSm,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.md),
        child: Row(
          children: <Widget>[
            // ── Icon ─────────────────────────────
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: c.isDark
                    ? theme.colorScheme.surfaceContainerHighest
                    : c.spendingMerchantIconWarmSurface,
                borderRadius: BorderRadius.circular(10),
              ),
              alignment: Alignment.center,
              child: Icon(
                category.icon,
                color: c.spendingMerchantIconDark,
                size: 18,
              ),
            ),

            const SizedBox(width: PayaboSpacing.md),

            // ── Name + progress bar ──────────────
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    category.name,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w600,
                      color: theme.colorScheme.onSurface,
                    ),
                  ),
                  const SizedBox(height: 6),
                  _BudgetProgressBar(
                    value: state.progress,
                    color: state.progressColorRole.resolve(c),
                    slim: true,
                  ),
                ],
              ),
            ),

            const SizedBox(width: PayaboSpacing.md),

            // ── Amount + remaining ───────────────
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: <Widget>[
                Text(
                  formatSpendingBudgetCurrency(category.allocated),
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: theme.colorScheme.onSurface,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  state.remainingLabel,
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: state.remainingColorRole.resolve(c),
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Shared small widgets
// ─────────────────────────────────────────────────────────

class _BudgetProgressBar extends StatelessWidget {
  const _BudgetProgressBar({
    required this.value,
    required this.color,
    this.slim = false,
    this.trackColor,
  });

  final double value;
  final Color color;
  final bool slim;
  final Color? trackColor;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return ClipRRect(
      borderRadius: BorderRadius.circular(PayaboRadii.pill),
      child: LinearProgressIndicator(
        minHeight: slim ? 6 : 10,
        value: value.clamp(0, 1),
        backgroundColor: trackColor ?? c.border,
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
