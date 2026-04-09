import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/dashboard_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
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

class SpendingBillsScreen extends ConsumerStatefulWidget {
  const SpendingBillsScreen({super.key});

  @override
  ConsumerState<SpendingBillsScreen> createState() =>
      _SpendingBillsScreenState();
}

class _SpendingBillsScreenState extends ConsumerState<SpendingBillsScreen> {
  final ValueNotifier<double> _statusBarProgress = ValueNotifier<double>(0.0);

  @override
  void dispose() {
    _statusBarProgress.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final AsyncValue<List<DashboardUpcomingBill>> billsValue =
        ref.watch(_spendingBillsProvider);

    return billsValue.when(
      data: (List<DashboardUpcomingBill> bills) {
        final String nextDueLabel = bills.isNotEmpty ? bills.first.dueDateLabel : '';

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
          body: _BillsHeroAndSheet(
            bills: bills,
            nextDueLabel: nextDueLabel,
            billCount: bills.length,
            onSectionSelected: _handleSectionSelected,
            onOpenBill: (String id) => context.push('/dashboard/bills/$id'),
            onRefresh: () async {
              ref.invalidate(_spendingBillsProvider);
              await ref.read(_spendingBillsProvider.future);
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
      error: (Object error, _) => Scaffold(
        backgroundColor: c.surfaceWarm,
        body: Center(
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
                  onPressed: () => ref.invalidate(_spendingBillsProvider),
                  child: const Text('Try again'),
                ),
              ],
            ),
          ),
        ),
        bottomNavigationBar: const PayaboPrimaryAppShell(
          destination: PayaboPrimaryDestination.spending,
        ),
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
//  Hero + Pinned Header + DraggableScrollableSheet
// ─────────────────────────────────────────────────────────

class _BillsHeroAndSheet extends StatefulWidget {
  const _BillsHeroAndSheet({
    required this.bills,
    required this.nextDueLabel,
    required this.billCount,
    required this.onSectionSelected,
    required this.onOpenBill,
    required this.onRefresh,
    this.onSheetExtentChanged,
  });

  static const double _maxSheetSize = 1.0;
  static const double _pinnedHeaderHeight = 76;
  static const double _sheetTopGap = 10;
  static const double _minHeroHeight = 200;
  static const double _maxHeroHeight = 248;

  final List<DashboardUpcomingBill> bills;
  final String nextDueLabel;
  final int billCount;
  final ValueChanged<SpendingSection> onSectionSelected;
  final ValueChanged<String> onOpenBill;
  final Future<void> Function() onRefresh;
  final ValueChanged<double>? onSheetExtentChanged;

  @override
  State<_BillsHeroAndSheet> createState() => _BillsHeroAndSheetState();
}

class _BillsHeroAndSheetState extends State<_BillsHeroAndSheet> {
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
          _BillsHeroAndSheet._maxHeroHeight,
          math.max(
            _BillsHeroAndSheet._minHeroHeight,
            viewportHeight * 0.37,
          ),
        );

        const double pinnedHeaderHeight =
            _BillsHeroAndSheet._pinnedHeaderHeight;
        const double pinnedSheetTop =
            pinnedHeaderHeight + _BillsHeroAndSheet._sheetTopGap;

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
            // ── LAYER 1: Hero banner ──
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: heroHeight,
              child: _BillsHeroBanner(
                nextDueLabel: widget.nextDueLabel,
                billCount: widget.billCount,
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
                            _BillsHeroAndSheet._maxSheetSize,
                          )
                          .toDouble();
                  const double fadeZone = 0.05;
                  final double fadeStart = math.max(
                    0.0,
                    _BillsHeroAndSheet._maxSheetSize - fadeZone,
                  );
                  final double bgProgress = Curves.easeOut.transform(
                    ((eff - fadeStart) / fadeZone).clamp(0.0, 1.0).toDouble(),
                  );
                  return _BillsPinnedHeader(
                    backgroundProgress: bgProgress,
                  );
                },
              ),
            ),

            // ── LAYER 3: Draggable sheet — pills + bill list ──
            Positioned(
              top: pinnedHeaderHeight,
              left: 0,
              right: 0,
              bottom: 0,
              child: DraggableScrollableSheet(
                controller: _sheetController,
                initialChildSize: initialSheetSize,
                minChildSize: minSheetSize,
                maxChildSize: _BillsHeroAndSheet._maxSheetSize,
                snap: true,
                snapSizes: <double>[
                  initialSheetSize,
                  _BillsHeroAndSheet._maxSheetSize,
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
                                  (_BillsHeroAndSheet._maxSheetSize -
                                      fadeZone)) /
                              fadeZone)
                          .clamp(0.0, 1.0);
                      return _BillsSheet(
                        scrollController: scrollController,
                        topBorderRadius: 24.0 * (1.0 - fadeFraction),
                        bills: widget.bills,
                        onSectionSelected: widget.onSectionSelected,
                        onOpenBill: widget.onOpenBill,
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

class _BillsPinnedHeader extends StatelessWidget {
  const _BillsPinnedHeader({required this.backgroundProgress});

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
//  Hero banner — bills summary on dark gradient
// ─────────────────────────────────────────────────────────

class _BillsHeroBanner extends StatelessWidget {
  const _BillsHeroBanner({
    required this.nextDueLabel,
    required this.billCount,
    this.bottomPadding = 40,
  });

  final String nextDueLabel;
  final int billCount;
  final double bottomPadding;

  @override
  Widget build(BuildContext context) {
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
                  // ── Upcoming label ──
                  Text(
                    'Upcoming bills',
                    style: textTheme.bodyMedium?.copyWith(
                      color: Colors.white.withValues(alpha: 0.6),
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                  SizedBox(
                    height: compact ? PayaboSpacing.sm : PayaboSpacing.md,
                  ),

                  // ── Bill count as hero number ──
                  Text(
                    '$billCount',
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

                  // ── Summary line ──
                  Row(
                    children: <Widget>[
                      Text(
                        'bill${billCount == 1 ? '' : 's'} due',
                        style: textTheme.bodyMedium?.copyWith(
                          color: Colors.white.withValues(alpha: 0.5),
                        ),
                      ),
                      if (nextDueLabel.isNotEmpty) ...<Widget>[
                        const SizedBox(width: PayaboSpacing.lg),
                        Text(
                          'Next $nextDueLabel',
                          style: textTheme.bodyMedium?.copyWith(
                            color: Colors.white.withValues(alpha: 0.7),
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
                    ],
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
//  Sheet — section pills + bill list
// ─────────────────────────────────────────────────────────

class _BillsSheet extends StatelessWidget {
  const _BillsSheet({
    required this.scrollController,
    required this.bills,
    required this.onSectionSelected,
    required this.onOpenBill,
    this.topBorderRadius = 24.0,
  });

  final ScrollController scrollController;
  final List<DashboardUpcomingBill> bills;
  final ValueChanged<SpendingSection> onSectionSelected;
  final ValueChanged<String> onOpenBill;
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
            selectedSection: SpendingSection.bills,
            sections: _visibleSpendingSections,
            onSelected: onSectionSelected,
          ),
          const SizedBox(height: PayaboSpacing.lg),

          // ── Section heading ──
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  'Upcoming bills',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ),
              _BillStatusPill(
                label: '${bills.length} due',
                foregroundColor: c.primary,
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.sm),

          // ── Bill rows or empty state ──
          if (bills.isEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.x4),
              child: Column(
                children: <Widget>[
                  Container(
                    width: 56,
                    height: 56,
                    decoration: BoxDecoration(
                      color: c.isDark
                          ? Theme.of(context).colorScheme.surfaceContainerHighest
                          : c.spendingMerchantIconWarmSurface,
                      borderRadius: BorderRadius.circular(14),
                    ),
                    child: Icon(
                      Icons.receipt_long_rounded,
                      color: c.spendingMerchantIconDark,
                      size: 26,
                    ),
                  ),
                  const SizedBox(height: PayaboSpacing.lg),
                  Text(
                    'No upcoming bills',
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                          color: c.accentBrown,
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.sm),
                  Text(
                    'Bills you add will appear here.',
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: c.muted,
                        ),
                    textAlign: TextAlign.center,
                  ),
                ],
              ),
            )
          else
            for (int i = 0; i < bills.length; i++) ...[
              _BillRow(
                bill: bills[i],
                onTap: () => onOpenBill(bills[i].id),
              ),
              if (i < bills.length - 1)
                Divider(
                  height: 1,
                  color: c.borderStrong.withValues(alpha: 0.3),
                ),
            ],
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Bill row — mirrors transaction row styling
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

    return InkWell(
      onTap: onTap,
      borderRadius: PayaboRadii.radiusSm,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.md),
        child: Row(
          children: <Widget>[
            // ── Icon ────────────────────────────────
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
                Icons.receipt_long_rounded,
                color: c.spendingMerchantIconDark,
                size: 18,
              ),
            ),
            const SizedBox(width: PayaboSpacing.md),

            // ── Biller name + due date ───────────────
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    bill.biller,
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w600,
                      color: theme.colorScheme.onSurface,
                    ),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  const SizedBox(height: 2),
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

            // ── Amount ───────────────────────────────
            Text(
              bill.amountLabel,
              style: theme.textTheme.titleSmall?.copyWith(
                fontWeight: FontWeight.w700,
                color: theme.colorScheme.onSurface,
              ),
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

class _BillStatusPill extends StatelessWidget {
  const _BillStatusPill({
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
