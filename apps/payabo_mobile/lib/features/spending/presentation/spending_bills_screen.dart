import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/commitments_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_app_header.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import 'widgets/spending_section_pills.dart';

// ─────────────────────────────────────────────────────────
//  Section visibility
// ─────────────────────────────────────────────────────────

const List<SpendingSection> _visibleSpendingSections = <SpendingSection>[
  SpendingSection.transactions,
  SpendingSection.budgets,
  SpendingSection.bills,
  SpendingSection.accounts,
];

// ─────────────────────────────────────────────────────────
//  Provider
// ─────────────────────────────────────────────────────────

final _commitmentsPageProvider =
    FutureProvider<CommitmentListPage>((Ref ref) async {
  return ref.watch(commitmentsRepositoryProvider).listCommitments();
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
  CommitmentFilter _filter = CommitmentFilter.all;

  @override
  void dispose() {
    _statusBarProgress.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final pageValue = ref.watch(_commitmentsPageProvider);

    return pageValue.when(
      data: (CommitmentListPage page) {
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
          body: _CommitmentsHeroAndSheet(
            page: page,
            filter: _filter,
            onFilterChanged: (f) => setState(() => _filter = f),
            onSectionSelected: _handleSectionSelected,
            onConfirm: _confirm,
            onReject: _reject,
            onRefresh: () async {
              ref.invalidate(_commitmentsPageProvider);
              await ref.read(_commitmentsPageProvider.future);
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
                Icon(Icons.error_outline_rounded, size: 48, color: c.muted),
                const SizedBox(height: PayaboSpacing.md),
                Text(
                  'Unable to load commitments.',
                  style: Theme.of(context)
                      .textTheme
                      .bodyMedium
                      ?.copyWith(color: c.muted),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: PayaboSpacing.lg),
                TextButton(
                  onPressed: () => ref.invalidate(_commitmentsPageProvider),
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

  Future<void> _confirm(String id) async {
    await ref.read(commitmentsRepositoryProvider).confirmCommitment(id);
    ref.invalidate(_commitmentsPageProvider);
  }

  Future<void> _reject(String id) async {
    await ref.read(commitmentsRepositoryProvider).rejectCommitment(id);
    ref.invalidate(_commitmentsPageProvider);
  }

  void _handleSectionSelected(SpendingSection section) {
    switch (section) {
      case SpendingSection.overview:
        context.go('/spending/overview');
      case SpendingSection.transactions:
        context.go('/spending');
      case SpendingSection.budgets:
        context.go('/spending/budgets');
      case SpendingSection.bills:
        return;
      case SpendingSection.accounts:
        context.go('/spending/accounts');
    }
  }
}

// ─────────────────────────────────────────────────────────
//  Hero + Pinned Header + DraggableScrollableSheet
// ─────────────────────────────────────────────────────────

class _CommitmentsHeroAndSheet extends StatefulWidget {
  const _CommitmentsHeroAndSheet({
    required this.page,
    required this.filter,
    required this.onFilterChanged,
    required this.onSectionSelected,
    required this.onConfirm,
    required this.onReject,
    required this.onRefresh,
    this.onSheetExtentChanged,
  });

  static const double _maxSheetSize = 1.0;
  static const double _pinnedHeaderHeight = 76;
  static const double _sheetTopGap = 10;
  static const double _minHeroHeight = 200;
  static const double _maxHeroHeight = 248;

  final CommitmentListPage page;
  final CommitmentFilter filter;
  final ValueChanged<CommitmentFilter> onFilterChanged;
  final ValueChanged<SpendingSection> onSectionSelected;
  final Future<void> Function(String id) onConfirm;
  final Future<void> Function(String id) onReject;
  final Future<void> Function() onRefresh;
  final ValueChanged<double>? onSheetExtentChanged;

  @override
  State<_CommitmentsHeroAndSheet> createState() =>
      _CommitmentsHeroAndSheetState();
}

class _CommitmentsHeroAndSheetState extends State<_CommitmentsHeroAndSheet> {
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
    final double next = _sheetController.size;
    if ((_sheetExtentNotifier.value - next).abs() > 0.001) {
      final SchedulerPhase phase = WidgetsBinding.instance.schedulerPhase;
      if (phase == SchedulerPhase.idle ||
          phase == SchedulerPhase.postFrameCallbacks) {
        _sheetExtentNotifier.value = next;
        widget.onSheetExtentChanged?.call(next);
      } else {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (!mounted || !_sheetController.isAttached) return;
          if ((_sheetExtentNotifier.value - next).abs() > 0.001) {
            _sheetExtentNotifier.value = next;
            widget.onSheetExtentChanged?.call(next);
          }
        });
      }
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
          _CommitmentsHeroAndSheet._maxHeroHeight,
          math.max(
            _CommitmentsHeroAndSheet._minHeroHeight,
            viewportHeight * 0.37,
          ),
        );

        const double pinnedHeaderHeight =
            _CommitmentsHeroAndSheet._pinnedHeaderHeight;
        const double pinnedSheetTop =
            pinnedHeaderHeight + _CommitmentsHeroAndSheet._sheetTopGap;

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

        final double heroBottomPadding =
            math.max(40, heroHeight - collapsedSheetTop + 28);

        return Stack(
          children: <Widget>[
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: heroHeight,
              child: _CommitmentsHeroBanner(
                totals: widget.page.totals,
                bottomPadding: heroBottomPadding,
              ),
            ),
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: pinnedHeaderHeight,
              child: ValueListenableBuilder<double>(
                valueListenable: _sheetExtentNotifier,
                builder: (BuildContext ctx, double sheetExtent, Widget? _) {
                  final double eff =
                      (sheetExtent <= 0 ? initialSheetSize : sheetExtent)
                          .clamp(
                            minSheetSize,
                            _CommitmentsHeroAndSheet._maxSheetSize,
                          )
                          .toDouble();
                  const double fadeZone = 0.05;
                  final double fadeStart = math.max(
                    0.0,
                    _CommitmentsHeroAndSheet._maxSheetSize - fadeZone,
                  );
                  final double bgProgress = Curves.easeOut.transform(
                    ((eff - fadeStart) / fadeZone).clamp(0.0, 1.0).toDouble(),
                  );
                  return _CommitmentsPinnedHeader(
                    backgroundProgress: bgProgress,
                  );
                },
              ),
            ),
            Positioned(
              top: pinnedHeaderHeight,
              left: 0,
              right: 0,
              bottom: 0,
              child: DraggableScrollableSheet(
                controller: _sheetController,
                initialChildSize: initialSheetSize,
                minChildSize: minSheetSize,
                maxChildSize: _CommitmentsHeroAndSheet._maxSheetSize,
                snap: true,
                snapSizes: <double>[
                  initialSheetSize,
                  _CommitmentsHeroAndSheet._maxSheetSize,
                ],
                builder: (
                  BuildContext context,
                  ScrollController scrollController,
                ) {
                  return ValueListenableBuilder<double>(
                    valueListenable: _sheetExtentNotifier,
                    builder: (BuildContext ctx, double extent, Widget? child) {
                      const double fadeZone = 0.05;
                      final double fadeFraction = ((extent -
                                  (_CommitmentsHeroAndSheet._maxSheetSize -
                                      fadeZone)) /
                              fadeZone)
                          .clamp(0.0, 1.0);
                      return _CommitmentsSheet(
                        scrollController: scrollController,
                        topBorderRadius: 24.0 * (1.0 - fadeFraction),
                        page: widget.page,
                        filter: widget.filter,
                        onFilterChanged: widget.onFilterChanged,
                        onSectionSelected: widget.onSectionSelected,
                        onConfirm: widget.onConfirm,
                        onReject: widget.onReject,
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
//  Pinned header
// ─────────────────────────────────────────────────────────

class _CommitmentsPinnedHeader extends StatelessWidget {
  const _CommitmentsPinnedHeader({required this.backgroundProgress});

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
//  Hero banner
// ─────────────────────────────────────────────────────────

class _CommitmentsHeroBanner extends StatelessWidget {
  const _CommitmentsHeroBanner({
    required this.totals,
    this.bottomPadding = 40,
  });

  final CommitmentTotals totals;
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
                  Text(
                    'Bills & commitments',
                    style: textTheme.bodyMedium?.copyWith(
                      color: Colors.white.withValues(alpha: 0.6),
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                  SizedBox(height: compact ? PayaboSpacing.sm : PayaboSpacing.md),
                  Text(
                    totals.totalUpcomingAmountLabel,
                    style: (compact
                            ? textTheme.headlineLarge
                            : textTheme.displaySmall)
                        ?.copyWith(
                      color: Colors.white,
                      fontWeight: FontWeight.w800,
                      height: 1,
                    ),
                  ),
                  SizedBox(height: compact ? PayaboSpacing.sm : PayaboSpacing.md),
                  Row(
                    children: <Widget>[
                      Text(
                        '${totals.totalCount} commitment${totals.totalCount == 1 ? '' : 's'}',
                        style: textTheme.bodyMedium?.copyWith(
                          color: Colors.white.withValues(alpha: 0.5),
                        ),
                      ),
                      if (totals.dueSoonCount > 0) ...<Widget>[
                        const SizedBox(width: PayaboSpacing.lg),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 3,
                          ),
                          decoration: BoxDecoration(
                            color: Colors.orange.withValues(alpha: 0.25),
                            borderRadius: BorderRadius.circular(PayaboRadii.pill),
                          ),
                          child: Text(
                            '${totals.dueSoonCount} due soon',
                            style: textTheme.bodySmall?.copyWith(
                              color: Colors.orange.shade200,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                      ],
                      if (totals.detectedCount > 0) ...<Widget>[
                        const SizedBox(width: PayaboSpacing.sm),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 3,
                          ),
                          decoration: BoxDecoration(
                            color: Colors.white.withValues(alpha: 0.15),
                            borderRadius: BorderRadius.circular(PayaboRadii.pill),
                          ),
                          child: Text(
                            '${totals.detectedCount} to review',
                            style: textTheme.bodySmall?.copyWith(
                              color: Colors.white.withValues(alpha: 0.8),
                              fontWeight: FontWeight.w600,
                            ),
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
//  Sheet — pills + filter chips + commitment list
// ─────────────────────────────────────────────────────────

class _CommitmentsSheet extends StatelessWidget {
  const _CommitmentsSheet({
    required this.scrollController,
    required this.page,
    required this.filter,
    required this.onFilterChanged,
    required this.onSectionSelected,
    required this.onConfirm,
    required this.onReject,
    this.topBorderRadius = 24.0,
  });

  final ScrollController scrollController;
  final CommitmentListPage page;
  final CommitmentFilter filter;
  final ValueChanged<CommitmentFilter> onFilterChanged;
  final ValueChanged<SpendingSection> onSectionSelected;
  final Future<void> Function(String id) onConfirm;
  final Future<void> Function(String id) onReject;
  final double topBorderRadius;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final items = page.filtered(filter);

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

          // ── Filter chips ──
          _FilterChipRow(
            selected: filter,
            onSelected: onFilterChanged,
            totals: page.totals,
          ),
          const SizedBox(height: PayaboSpacing.lg),

          // ── Section heading ──
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  filter == CommitmentFilter.all
                      ? 'All commitments'
                      : filter.label,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        color: c.accentBrown,
                        fontWeight: FontWeight.w700,
                      ),
                ),
              ),
              _CountPill(
                label: '${items.length}',
                foregroundColor: c.primary,
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.sm),

          // ── Commitment rows or empty state ──
          if (items.isEmpty)
            _EmptyState(filter: filter)
          else
            for (int i = 0; i < items.length; i++) ...<Widget>[
              _CommitmentRow(
                item: items[i],
                onConfirm: () => onConfirm(items[i].id),
                onReject: () => onReject(items[i].id),
              ),
              if (i < items.length - 1)
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
//  Filter chip row
// ─────────────────────────────────────────────────────────

class _FilterChipRow extends StatelessWidget {
  const _FilterChipRow({
    required this.selected,
    required this.onSelected,
    required this.totals,
  });

  final CommitmentFilter selected;
  final ValueChanged<CommitmentFilter> onSelected;
  final CommitmentTotals totals;

  bool _hasItems(CommitmentFilter f) {
    switch (f) {
      case CommitmentFilter.all:
        return totals.totalCount > 0;
      case CommitmentFilter.bills:
        return totals.billsCount > 0;
      case CommitmentFilter.subscriptions:
        return totals.subscriptionsCount > 0;
      case CommitmentFilter.loans:
        return totals.debtRepaymentsCount > 0;
      case CommitmentFilter.dueSoon:
        return totals.dueSoonCount > 0;
      case CommitmentFilter.detected:
        return totals.detectedCount > 0;
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: <Widget>[
          for (final CommitmentFilter f in CommitmentFilter.values)
            if (_hasItems(f) || f == CommitmentFilter.all)
              Padding(
                padding: const EdgeInsets.only(right: PayaboSpacing.sm),
                child: GestureDetector(
                  onTap: () => onSelected(f),
                  child: AnimatedContainer(
                    duration: const Duration(milliseconds: 160),
                    padding: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 6,
                    ),
                    decoration: BoxDecoration(
                      color: selected == f
                          ? c.primary
                          : c.primary.withValues(alpha: 0.1),
                      borderRadius:
                          BorderRadius.circular(PayaboRadii.pill),
                    ),
                    child: Text(
                      f.label,
                      style: textTheme.bodySmall?.copyWith(
                        color: selected == f
                            ? Colors.white
                            : c.primary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ),
              ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Commitment row
// ─────────────────────────────────────────────────────────

class _CommitmentRow extends StatefulWidget {
  const _CommitmentRow({
    required this.item,
    required this.onConfirm,
    required this.onReject,
  });

  final CommitmentItem item;
  final Future<void> Function() onConfirm;
  final Future<void> Function() onReject;

  @override
  State<_CommitmentRow> createState() => _CommitmentRowState();
}

class _CommitmentRowState extends State<_CommitmentRow> {
  bool _acting = false;

  Future<void> _run(Future<void> Function() fn) async {
    if (_acting) return;
    setState(() => _acting = true);
    try {
      await fn();
    } finally {
      if (mounted) setState(() => _acting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final theme = Theme.of(context);
    final item = widget.item;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              // ── Type icon ──
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: c.isDark
                      ? theme.colorScheme.surfaceContainerHighest
                      : _typeIconBackground(item.type, c),
                  borderRadius: BorderRadius.circular(10),
                ),
                alignment: Alignment.center,
                child: Icon(
                  _typeIcon(item.type),
                  color: _typeIconColor(item.type, c),
                  size: 18,
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),

              // ── Name + due date ──
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Row(
                      children: <Widget>[
                        Flexible(
                          child: Text(
                            item.displayName,
                            style: theme.textTheme.titleSmall?.copyWith(
                              fontWeight: FontWeight.w600,
                              color: theme.colorScheme.onSurface,
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        const SizedBox(width: PayaboSpacing.sm),
                        _TypeBadge(type: item.type),
                      ],
                    ),
                    const SizedBox(height: 2),
                    Row(
                      children: <Widget>[
                        if (item.dueDateLabel != null)
                          Text(
                            'Due ${item.dueDateLabel}',
                            style: theme.textTheme.bodySmall?.copyWith(
                              color: item.isDueSoon ? Colors.orange : c.muted,
                              fontWeight: item.isDueSoon
                                  ? FontWeight.w600
                                  : FontWeight.normal,
                            ),
                          ),
                        if (item.autopay && item.dueDateLabel != null) ...<Widget>[
                          const SizedBox(width: 6),
                          Text(
                            '· Auto',
                            style: theme.textTheme.bodySmall
                                ?.copyWith(color: c.muted),
                          ),
                        ],
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),

              // ── Amount ──
              Text(
                item.amountLabel ?? '',
                style: theme.textTheme.titleSmall?.copyWith(
                  fontWeight: FontWeight.w700,
                  color: theme.colorScheme.onSurface,
                ),
              ),
            ],
          ),

          // ── Detected: confirm / reject actions ──
          if (item.isDetected) ...<Widget>[
            const SizedBox(height: PayaboSpacing.sm),
            Row(
              children: <Widget>[
                const SizedBox(width: 52),
                Expanded(
                  child: Text(
                    'Detected from transactions${item.confidenceScore != null ? ' · ${(item.confidenceScore! * 100).round()}% confidence' : ''}',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: c.muted,
                      fontStyle: FontStyle.italic,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Row(
              children: <Widget>[
                const SizedBox(width: 52),
                _acting
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Row(
                        children: <Widget>[
                          _ActionButton(
                            label: 'Confirm',
                            primary: true,
                            onTap: () => _run(widget.onConfirm),
                          ),
                          const SizedBox(width: PayaboSpacing.sm),
                          _ActionButton(
                            label: 'Not mine',
                            primary: false,
                            onTap: () => _run(widget.onReject),
                          ),
                        ],
                      ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  static IconData _typeIcon(CommitmentType type) {
    switch (type) {
      case CommitmentType.bill:
        return Icons.receipt_long_rounded;
      case CommitmentType.subscription:
        return Icons.autorenew_rounded;
      case CommitmentType.debtRepayment:
        return Icons.account_balance_rounded;
    }
  }

  static Color _typeIconBackground(CommitmentType type, PayaboColorResolver c) {
    switch (type) {
      case CommitmentType.bill:
        return c.spendingMerchantIconWarmSurface;
      case CommitmentType.subscription:
        return const Color(0xFFEDE9FE);
      case CommitmentType.debtRepayment:
        return const Color(0xFFDCFCE7);
    }
  }

  static Color _typeIconColor(CommitmentType type, PayaboColorResolver c) {
    switch (type) {
      case CommitmentType.bill:
        return c.spendingMerchantIconDark;
      case CommitmentType.subscription:
        return const Color(0xFF7C3AED);
      case CommitmentType.debtRepayment:
        return const Color(0xFF15803D);
    }
  }
}

// ─────────────────────────────────────────────────────────
//  Type badge
// ─────────────────────────────────────────────────────────

class _TypeBadge extends StatelessWidget {
  const _TypeBadge({required this.type});

  final CommitmentType type;

  @override
  Widget build(BuildContext context) {
    final (String label, Color fg, Color bg) = switch (type) {
      CommitmentType.bill => (
          'Bill',
          const Color(0xFFB45309),
          const Color(0xFFFEF3C7),
        ),
      CommitmentType.subscription => (
          'Sub',
          const Color(0xFF7C3AED),
          const Color(0xFFEDE9FE),
        ),
      CommitmentType.debtRepayment => (
          'Loan',
          const Color(0xFF15803D),
          const Color(0xFFDCFCE7),
        ),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: fg,
              fontWeight: FontWeight.w700,
              fontSize: 10,
            ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Confirm / reject action button
// ─────────────────────────────────────────────────────────

class _ActionButton extends StatelessWidget {
  const _ActionButton({
    required this.label,
    required this.primary,
    required this.onTap,
  });

  final String label;
  final bool primary;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
        decoration: BoxDecoration(
          color: primary ? c.primary : Colors.transparent,
          border: Border.all(
            color: primary ? c.primary : c.borderStrong,
          ),
          borderRadius: BorderRadius.circular(PayaboRadii.pill),
        ),
        child: Text(
          label,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: primary ? Colors.white : c.muted,
                fontWeight: FontWeight.w600,
              ),
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Empty state
// ─────────────────────────────────────────────────────────

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.filter});

  final CommitmentFilter filter;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final theme = Theme.of(context);

    final String title = filter == CommitmentFilter.all
        ? 'No commitments yet'
        : 'No ${filter.label.toLowerCase()} found';

    final String body = filter == CommitmentFilter.all
        ? 'Bills, subscriptions, and loan repayments you track will appear here.'
        : 'Try switching to All to see everything.';

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.x4),
      child: Column(
        children: <Widget>[
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: c.isDark
                  ? theme.colorScheme.surfaceContainerHighest
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
            title,
            style: theme.textTheme.titleMedium?.copyWith(
              color: c.accentBrown,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            body,
            style: theme.textTheme.bodyMedium?.copyWith(color: c.muted),
            textAlign: TextAlign.center,
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────
//  Shared small widgets
// ─────────────────────────────────────────────────────────

class _CountPill extends StatelessWidget {
  const _CountPill({
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
