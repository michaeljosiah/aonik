import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/pay_activity_repository.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_profile_avatar.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import '../../profile/presentation/profile_state.dart';
import '../application/pay_activity_providers.dart';

// ═══════════════════════════════════════════════════════════════════════════
// Pay Dashboard — mirrors the Home dashboard architecture exactly.
//
// Scaffold: dark charcoal gradient, status-bar overlay, bottom nav.
// Stack:    hero banner  →  pinned header  →  DraggableScrollableSheet.
// Sheet:    two payment-option cards  +  quick send / recent activities.
// ═══════════════════════════════════════════════════════════════════════════

class PayDashboardScreen extends StatefulWidget {
  const PayDashboardScreen({super.key});

  @override
  State<PayDashboardScreen> createState() => _PayDashboardScreenState();
}

class _PayDashboardScreenState extends State<PayDashboardScreen> {
  final ValueNotifier<double> _statusBarProgress = ValueNotifier<double>(0.0);

  static double _extentToStatusBarProgress(double extent) {
    const double fadeZone = 0.05;
    const double fadeStart = _PayHeroSheetSection._maxSheetSize - fadeZone;
    return Curves.easeOut.transform(
      ((extent - fadeStart) / fadeZone).clamp(0.0, 1.0).toDouble(),
    );
  }

  void _handleSheetExtentChanged(double extent) {
    final double progress = _extentToStatusBarProgress(extent);
    if ((_statusBarProgress.value - progress).abs() > 0.001) {
      _statusBarProgress.value = progress;
    }
  }

  @override
  void dispose() {
    _statusBarProgress.dispose();
    super.dispose();
  }

  // Same dark charcoal gradient as the Home dashboard.
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
    return PayaboWarmScaffold(
      backgroundDecoration: const BoxDecoration(gradient: _backgroundGradient),
      statusBarColorNotifier: _statusBarProgress,
      bottomNavigationBar: const PayaboPrimaryAppShell(
        destination: PayaboPrimaryDestination.pay,
      ),
      body: _PayHeroSheetSection(
        onSheetExtentChanged: _handleSheetExtentChanged,
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Hero + Sheet section — LayoutBuilder → Stack(hero, header, sheet)
// ═══════════════════════════════════════════════════════════════════════════

class _PayHeroSheetSection extends StatefulWidget {
  const _PayHeroSheetSection({this.onSheetExtentChanged});

  final ValueChanged<double>? onSheetExtentChanged;

  static const double _minHeroHeight = 200;
  static const double _maxHeroHeight = 248;
  static const double _maxSheetSize = 1.0;
  static const double _pinnedHeaderHeight = 76;
  static const double _sheetTopGap = 10;

  @override
  State<_PayHeroSheetSection> createState() => _PayHeroSheetSectionState();
}

class _PayHeroSheetSectionState extends State<_PayHeroSheetSection> {
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
        return;
      }
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted || !_sheetController.isAttached) return;
        if ((_sheetExtentNotifier.value - next).abs() > 0.001) {
          _sheetExtentNotifier.value = next;
          widget.onSheetExtentChanged?.call(next);
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
          _PayHeroSheetSection._maxHeroHeight,
          math.max(
            _PayHeroSheetSection._minHeroHeight,
            viewportHeight * 0.37,
          ),
        );

        const double pinnedHeaderHeight =
            _PayHeroSheetSection._pinnedHeaderHeight;
        const double pinnedSheetTop =
            pinnedHeaderHeight + _PayHeroSheetSection._sheetTopGap;

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
            // LAYER 1 — Hero banner (text on dark gradient)
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: heroHeight,
              child: _PayHeroBanner(bottomPadding: heroBottomPadding),
            ),

            // LAYER 2 — Pinned header (profile + notification bell)
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
                            _PayHeroSheetSection._maxSheetSize,
                          )
                          .toDouble();
                  const double fadeZone = 0.05;
                  final double fadeStart = math.max(
                    0.0,
                    _PayHeroSheetSection._maxSheetSize - fadeZone,
                  );
                  final double bgProgress = Curves.easeOut.transform(
                    ((eff - fadeStart) / fadeZone).clamp(0.0, 1.0).toDouble(),
                  );
                  return _PayPinnedHeader(backgroundProgress: bgProgress);
                },
              ),
            ),

            // LAYER 3 — Draggable sheet
            Positioned(
              top: pinnedHeaderHeight,
              left: 0,
              right: 0,
              bottom: 0,
              child: DraggableScrollableSheet(
                controller: _sheetController,
                initialChildSize: initialSheetSize,
                minChildSize: minSheetSize,
                maxChildSize: _PayHeroSheetSection._maxSheetSize,
                snap: true,
                snapSizes: <double>[
                  initialSheetSize,
                  _PayHeroSheetSection._maxSheetSize,
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
                                  (_PayHeroSheetSection._maxSheetSize -
                                      fadeZone)) /
                              fadeZone)
                          .clamp(0.0, 1.0);
                      return _PayStatsSheet(
                        scrollController: scrollController,
                        topBorderRadius: 24.0 * (1.0 - fadeFraction),
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

// ═══════════════════════════════════════════════════════════════════════════
// Hero banner — updated to match design mockup
// ═══════════════════════════════════════════════════════════════════════════

class _PayHeroBanner extends StatelessWidget {
  const _PayHeroBanner({this.bottomPadding = 40});

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
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 360),
            child: LayoutBuilder(
              builder: (BuildContext context, BoxConstraints box) {
                final bool compact = box.maxHeight < 190;
                return Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text.rich(
                      TextSpan(
                        style: (compact
                                ? textTheme.headlineMedium
                                : textTheme.headlineLarge)
                            ?.copyWith(
                          color: Colors.white,
                          fontWeight: FontWeight.w700,
                          height: 1.15,
                        ),
                        children: const <InlineSpan>[
                          TextSpan(
                              text: 'Support your family,\nwherever they are.'),
                        ],
                      ),
                      maxLines: compact ? 2 : 3,
                      overflow: TextOverflow.ellipsis,
                    ),
                    SizedBox(
                        height: compact ? PayaboSpacing.sm : PayaboSpacing.md),
                    Text(
                      'Send money, pay bills, track everything.',
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: textTheme.bodyLarge?.copyWith(
                        fontSize: 15,
                        color: Colors.white.withValues(alpha: 0.60),
                        height: 1.4,
                      ),
                    ),
                  ],
                );
              },
            ),
          ),
        ),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Pinned header — profile summary + notification bell (same as Home)
// ═══════════════════════════════════════════════════════════════════════════

class _PayPinnedHeader extends ConsumerWidget {
  const _PayPinnedHeader({required this.backgroundProgress});

  final double backgroundProgress;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.colors;
    final profileState = ref.watch(profileHeaderProvider);
    final displayName = profileState.displayName.trim().isEmpty
        ? 'Your account'
        : profileState.displayName.trim();

    return Stack(
      children: <Widget>[
        // Background fade
        Positioned.fill(
          child: Opacity(
            opacity: backgroundProgress,
            child: ColoredBox(color: c.surfaceBase),
          ),
        ),
        // Foreground
        Padding(
          padding: const EdgeInsets.fromLTRB(
            PayaboSpacing.xl,
            PayaboSpacing.md,
            PayaboSpacing.xl,
            0,
          ),
          child: Row(
            children: <Widget>[
              Expanded(
                child: _PayProfileSummary(
                  photoUrl: profileState.photoUrl,
                  displayName: displayName,
                  onTap: () => context.go('/profile'),
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),
              _PayNotificationButton(
                onTap: () => context.push('/notifications'),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _PayProfileSummary extends StatelessWidget {
  const _PayProfileSummary({
    required this.photoUrl,
    required this.displayName,
    required this.onTap,
  });

  final String? photoUrl;
  final String displayName;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Row(
      children: <Widget>[
        Material(
          color: c.surfaceBase,
          shape: const CircleBorder(),
          child: Container(
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              border: Border.all(color: c.primary, width: 1.5),
            ),
            child: InkWell(
              onTap: onTap,
              customBorder: const CircleBorder(),
              child: Padding(
                padding: const EdgeInsets.all(1.5),
                child: PayaboProfileAvatar(
                  photoUrl: photoUrl,
                  size: 42,
                  backgroundColor: c.background,
                  placeholderIcon: Icons.person_outline_rounded,
                  placeholderIconSize: 20,
                ),
              ),
            ),
          ),
        ),
        const SizedBox(width: PayaboSpacing.md),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                'Welcome back',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: c.headerSubtitle,
                      fontWeight: FontWeight.w500,
                    ),
              ),
              Text(
                displayName,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: c.headerTitle,
                      fontWeight: FontWeight.w700,
                    ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _PayNotificationButton extends StatelessWidget {
  const _PayNotificationButton({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: Ink(
          width: 44,
          height: 44,
          decoration: BoxDecoration(
            color: c.headerIconSurface,
            shape: BoxShape.circle,
            border: Border.all(color: c.headerIconBorder),
          ),
          child: Stack(
            clipBehavior: Clip.none,
            children: <Widget>[
              Center(
                child: Icon(
                  Icons.notifications_none_rounded,
                  color: c.headerIconAccent,
                  size: 22,
                ),
              ),
              Positioned(
                right: 10,
                top: 9,
                child: Container(
                  width: 8,
                  height: 8,
                  decoration: BoxDecoration(
                    color: c.headerNotificationDot,
                    shape: BoxShape.circle,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Stats sheet — payment option cards + quick send + recent activities
// ═══════════════════════════════════════════════════════════════════════════

class _PayStatsSheet extends StatelessWidget {
  const _PayStatsSheet({
    required this.scrollController,
    required this.topBorderRadius,
  });

  final ScrollController scrollController;
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
                  color: Colors.black.withValues(alpha: c.isDark ? 0.22 : 0.08),
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
          // ── Drag handle ────────────────────────────────────
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

          // ── Payment option cards ───────────────────────────
          const _PaymentOptionsRow(),
          const SizedBox(height: PayaboSpacing.x2),

          // ── Quick send header with "View all activity" link ─
          const _QuickSendHeader(),
          const SizedBox(height: PayaboSpacing.md),

          // ── Activity rows ──────────────────────────────────
          const _RecentActivityList(),
        ],
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Payment option cards — two cards side by side
// ═══════════════════════════════════════════════════════════════════════════

class _PaymentOptionsRow extends StatelessWidget {
  const _PaymentOptionsRow();

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 210,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Expanded(
            child: _PaymentOptionCard(
              icon: Icons.receipt_long_outlined,
              iconColor: context.colors.insightAccent,
              title: 'Pay a bill',
              subtitle: 'Utilities, TV, airtime and household essentials.',
              actionLabel: 'Start',
              onTap: () => context.go('/payments/country'),
            ),
          ),
          const SizedBox(width: PayaboSpacing.md),
          Expanded(
            child: _PaymentOptionCard(
              icon: Icons.send_rounded,
              iconColor: const Color(0xFF2465E8),
              title: 'Send money',
              subtitle: 'Transfer funds to family and friends in a few taps.',
              actionLabel: 'Start',
              onTap: () => context.go('/payments/friends'),
            ),
          ),
        ],
      ),
    );
  }
}

class _PaymentOptionCard extends StatelessWidget {
  const _PaymentOptionCard({
    required this.icon,
    required this.iconColor,
    required this.title,
    required this.subtitle,
    required this.actionLabel,
    required this.onTap,
  });

  final IconData icon;
  final Color iconColor;
  final String title;
  final String subtitle;
  final String actionLabel;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    // Matches the _InsightCarouselCardShell from the Home dashboard.
    final Color bg = c.cardWarmBackground;
    final Color borderColor = c.cardWarmBorder;

    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(PayaboSpacing.lg),
        decoration: BoxDecoration(
          color: bg,
          borderRadius: PayaboRadii.radiusSm,
          border: Border.all(color: borderColor, width: 0.5),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            // Icon circle
            Container(
              width: 38,
              height: 38,
              decoration: BoxDecoration(
                color: iconColor.withValues(alpha: 0.14),
                shape: BoxShape.circle,
              ),
              child: Icon(icon, size: 20, color: iconColor),
            ),
            const SizedBox(height: PayaboSpacing.md),

            // Title
            Text(
              title,
              style: textTheme.titleMedium?.copyWith(
                color: c.accentBrown,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: PayaboSpacing.xs),

            // Subtitle
            Text(
              subtitle,
              maxLines: 3,
              overflow: TextOverflow.ellipsis,
              style: textTheme.bodyMedium?.copyWith(
                color: c.muted,
                height: 1.35,
              ),
            ),
            const SizedBox(height: PayaboSpacing.md),

            // Action label
            Text(
              actionLabel,
              style: textTheme.labelLarge?.copyWith(
                color: c.primary,
                fontWeight: FontWeight.w700,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Quick send header — "Quick send" + "View all activity >"
// ═══════════════════════════════════════════════════════════════════════════

class _QuickSendHeader extends StatelessWidget {
  const _QuickSendHeader();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;
    final Color titleColor = c.accentBrown;

    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            'Quick send',
            style: textTheme.headlineMedium?.copyWith(
              fontSize: 18,
              fontWeight: FontWeight.w500,
              color: titleColor,
            ),
          ),
        ),
        GestureDetector(
          onTap: () => context.push('/payments/activity'),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                'View all activity',
                style: textTheme.titleSmall?.copyWith(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                  color: c.primary,
                ),
              ),
              const SizedBox(width: 2),
              Icon(
                Icons.chevron_right_rounded,
                size: 18,
                color: c.primary,
              ),
            ],
          ),
        ),
      ],
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Recent activity list — shows last 2-3 transactions with avatars/icons
// ═══════════════════════════════════════════════════════════════════════════

class _RecentActivityList extends ConsumerWidget {
  const _RecentActivityList();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final activityAsync = ref.watch(payActivitySummaryProvider);

    return activityAsync.when(
      loading: () => const Padding(
        padding: EdgeInsets.symmetric(vertical: PayaboSpacing.x2),
        child: Center(
          child: SizedBox(
            width: 24,
            height: 24,
            child: CircularProgressIndicator(strokeWidth: 2),
          ),
        ),
      ),
      error: (_, __) => Padding(
        padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.lg),
        child: Text(
          'Unable to load recent activity',
          style: theme.textTheme.bodyMedium?.copyWith(
            color: theme.colorScheme.error,
          ),
        ),
      ),
      data: (PayActivitySummary summary) {
        // Show at most 2 recent items on the dashboard.
        final items = summary.transactions.take(2).toList();

        if (items.isEmpty) {
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.lg),
            child: Text(
              'No recent activity yet',
              style: theme.textTheme.bodyMedium?.copyWith(
                color: theme.colorScheme.onSurface.withValues(alpha: 0.5),
              ),
            ),
          );
        }

        return Column(
          children: <Widget>[
            for (int i = 0; i < items.length; i++) ...<Widget>[
              PayActivityRow(
                item: items[i],
                onTap: () => context.push(
                  '/payments/transaction-details/${items[i].id}',
                ),
              ),
              if (i < items.length - 1)
                Divider(
                  height: 1,
                  color:
                      theme.colorScheme.outlineVariant.withValues(alpha: 0.3),
                ),
            ],
          ],
        );
      },
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Shared activity row — used by both dashboard and activity screen.
// Renders a single PayActivityTransaction from the repository.
// ═══════════════════════════════════════════════════════════════════════════

class PayActivityRow extends StatelessWidget {
  const PayActivityRow({
    super.key,
    required this.item,
    this.onTap,
  });

  final PayActivityTransaction item;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;
    final theme = Theme.of(context);
    final Color statusColor = resolveStatusColor(c, item.status);

    final IconData icon = item.type == PayActivityTransactionType.transfer
        ? Icons.send_rounded
        : Icons.receipt_long_outlined;

    return InkWell(
      onTap: onTap,
      borderRadius: PayaboRadii.radiusSm,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.md),
        child: Row(
          children: <Widget>[
            // Icon
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(10),
                color: c.isDark
                    ? theme.colorScheme.surfaceContainerHighest
                    : c.primary.withValues(alpha: 0.08),
              ),
              child: Icon(icon, size: 18, color: theme.colorScheme.primary),
            ),
            const SizedBox(width: PayaboSpacing.md),

            // Title + subtitle
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    item.title,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w600,
                      color: theme.colorScheme.onSurface,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    item.subtitle,
                    style: textTheme.bodySmall?.copyWith(
                      color: c.textMuted,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: PayaboSpacing.md),

            // Amount + status badge
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: <Widget>[
                Text(
                  item.amountLabel,
                  style: textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: theme.colorScheme.onSurface,
                  ),
                ),
                const SizedBox(height: 4),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
                  decoration: BoxDecoration(
                    color:
                        statusColor.withValues(alpha: c.isDark ? 0.22 : 0.12),
                    borderRadius: PayaboRadii.radiusPill,
                  ),
                  child: Text(
                    item.status,
                    style: textTheme.labelSmall?.copyWith(
                      color: statusColor,
                      fontWeight: FontWeight.w700,
                    ),
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

// ═══════════════════════════════════════════════════════════════════════════
// Helpers
// ═══════════════════════════════════════════════════════════════════════════

Color resolveStatusColor(PayaboColorResolver c, String status) {
  switch (status.toLowerCase()) {
    case 'completed':
    case 'sent':
      return c.success;
    case 'processing':
      return c.warning;
    case 'failed':
      return c.danger;
    default:
      return c.primary;
  }
}
