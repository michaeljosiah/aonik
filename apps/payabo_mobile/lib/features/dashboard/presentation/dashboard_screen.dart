// ignore_for_file: unused_element, unused_element_parameter

import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../data/repositories/dashboard_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_primary_app_shell.dart';
import '../../../shared/widgets/payabo_profile_avatar.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import '../../profile/presentation/profile_state.dart';

final FutureProvider<DashboardSummary> dashboardSummaryProvider =
    FutureProvider<DashboardSummary>((Ref ref) async {
  ref.watch(demoDataModeProvider);
  final repository = ref.watch(dashboardRepositoryProvider);
  return repository.getSummary();
});

List<_DashboardOverviewSlice> _dashboardOverviewSlices(PayaboColorResolver c) =>
    <_DashboardOverviewSlice>[
      _DashboardOverviewSlice(
        label: 'Income',
        amountLabel: '₵4,232.24',
        value: 4232.24,
        color: c.success,
      ),
      _DashboardOverviewSlice(
        label: 'Expenses',
        amountLabel: '₵2,660.12',
        value: 2660.12,
        color: c.primary,
      ),
      _DashboardOverviewSlice(
        label: 'Investments',
        amountLabel: '₵1,754.64',
        value: 1754.64,
        color: c.info,
      ),
    ];

String _dashboardAvatarLabel(String text) {
  final parts = text
      .trim()
      .split(RegExp(r'\s+'))
      .where((String part) => part.isNotEmpty)
      .toList(growable: false);

  if (parts.isEmpty) {
    return 'NA';
  }

  if (parts.length == 1) {
    final word = parts.first.toUpperCase();
    return word.substring(0, math.min(2, word.length));
  }

  return '${parts[0][0]}${parts[1][0]}'.toUpperCase();
}

String _dashboardFirstName(String displayName) {
  final parts = displayName
      .trim()
      .split(RegExp(r'\s+'))
      .where((String part) => part.isNotEmpty)
      .toList(growable: false);

  if (parts.isEmpty) {
    return 'there';
  }

  return parts.first;
}

String _dashboardGreeting(DateTime now) {
  if (now.hour < 12) {
    return 'Good morning';
  }

  if (now.hour < 17) {
    return 'Good afternoon';
  }

  return 'Good evening';
}

String _dashboardDueBillPhrase(int dueBillCount) {
  if (dueBillCount <= 0) {
    return 'no bills';
  }

  if (dueBillCount == 1) {
    return '1 bill';
  }

  return '$dueBillCount bills';
}

LinearGradient _dashboardBackgroundGradient(BuildContext context) {
  return const LinearGradient(
    colors: <Color>[
      Color(0xFF242223),
      Color(0xFF191718),
      Color(0xFF0F0D0E),
    ],
    stops: <double>[0, 0.46, 1],
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
  );
}

class DashboardScreen extends ConsumerStatefulWidget {
  const DashboardScreen({
    super.key,
    this.showEmptyState = false,
  });

  final bool showEmptyState;

  @override
  ConsumerState<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends ConsumerState<DashboardScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) {
        return;
      }

      unawaited(_ensureProfileLoaded());
    });
  }

  @override
  Widget build(BuildContext context) {
    final summaryValue = ref.watch(dashboardSummaryProvider);
    final profileState = ref.watch(profileHeaderProvider);
    final demoDataMode = ref.watch(demoDataModeProvider);
    final isEmptyDemoMode = demoDataMode == DemoDataMode.fresh;

    return PayaboWarmScaffold(
      body: Column(
        children: <Widget>[
          Expanded(
            child: summaryValue.when(
              data: (summary) {
                final isEmpty = widget.showEmptyState || isEmptyDemoMode;

                return RefreshIndicator(
                  onRefresh: () async =>
                      ref.refresh(dashboardSummaryProvider.future),
                  child: _DashboardContent(
                    summary: summary,
                    isEmpty: isEmpty,
                    displayName: profileState.displayName,
                    photoUrl: profileState.photoUrl,
                    onProfileTap: () => context.go('/profile'),
                    onNotificationsTap: () => context.push('/notifications'),
                  ),
                );
              },
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, stackTrace) {
                return Center(
                  child: Padding(
                    padding: const EdgeInsets.all(PayaboSpacing.xl),
                    child: Text('Unable to load dashboard: $error'),
                  ),
                );
              },
            ),
          ),
        ],
      ),
      bottomNavigationBar: const PayaboPrimaryAppShell(
        destination: PayaboPrimaryDestination.dashboard,
      ),
    );
  }

  Future<void> _ensureProfileLoaded() async {
    try {
      await ref.read(profileDataCoordinatorProvider).ensureLoaded();
    } catch (_) {}
  }
}

class _DashboardHeader extends StatelessWidget {
  const _DashboardHeader({
    required this.onProfileTap,
    required this.onNotificationsTap,
    required this.photoUrl,
    required this.displayName,
  });

  final VoidCallback onProfileTap;
  final VoidCallback onNotificationsTap;
  final String? photoUrl;
  final String displayName;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.xl,
        PayaboSpacing.md,
        PayaboSpacing.xl,
        PayaboSpacing.lg,
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: _DashboardProfileSummary(
              onTap: onProfileTap,
              photoUrl: photoUrl,
              displayName: displayName,
            ),
          ),
          const SizedBox(width: PayaboSpacing.md),
          _DashboardNotificationButton(onTap: onNotificationsTap),
        ],
      ),
    );
  }
}

class _DashboardProfileSummary extends StatelessWidget {
  const _DashboardProfileSummary({
    required this.onTap,
    required this.photoUrl,
    required this.displayName,
  });

  final VoidCallback onTap;
  final String? photoUrl;
  final String displayName;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final resolvedName =
        displayName.trim().isEmpty ? 'Your account' : displayName.trim();

    return Row(
      children: <Widget>[
        _DashboardProfileAvatar(
          onTap: onTap,
          photoUrl: photoUrl,
        ),
        const SizedBox(width: PayaboSpacing.md),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
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
                resolvedName,
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

class _DashboardProfileAvatar extends StatelessWidget {
  const _DashboardProfileAvatar({
    required this.onTap,
    required this.photoUrl,
  });

  final VoidCallback onTap;
  final String? photoUrl;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Material(
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
    );
  }
}

class _DashboardNotificationButton extends StatelessWidget {
  const _DashboardNotificationButton({required this.onTap});

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

class _DashboardContent extends StatelessWidget {
  const _DashboardContent({
    required this.summary,
    required this.isEmpty,
    required this.displayName,
    required this.photoUrl,
    required this.onProfileTap,
    required this.onNotificationsTap,
  });

  static const int _upcomingBillPreviewLimit = 5;
  final DashboardSummary summary;
  final bool isEmpty;
  final String displayName;
  final String? photoUrl;
  final VoidCallback onProfileTap;
  final VoidCallback onNotificationsTap;

  @override
  Widget build(BuildContext context) {
    final allUpcomingBills =
        isEmpty ? const <DashboardUpcomingBill>[] : summary.upcomingBills;
    final bills = allUpcomingBills
        .take(_upcomingBillPreviewLimit)
        .toList(growable: false);
    return _DashboardHeroInsightsSection(
      displayName: displayName,
      photoUrl: photoUrl,
      onProfileTap: onProfileTap,
      onNotificationsTap: onNotificationsTap,
      dueBillCount: allUpcomingBills.length,
      upcomingBills: bills,
      isEmpty: isEmpty,
    );
  }
}

class _DashboardHeroInsightsSection extends StatefulWidget {
  const _DashboardHeroInsightsSection({
    required this.displayName,
    required this.photoUrl,
    required this.onProfileTap,
    required this.onNotificationsTap,
    required this.dueBillCount,
    required this.upcomingBills,
    required this.isEmpty,
  });

  static const double _minHeroHeight = 248;
  static const double _maxHeroHeight = 300;
  static const double _maxSheetSize = 1.0;
  static const double _pinnedHeaderHeight = 76;
  static const double _sheetTopGap = 10;

  final String displayName;
  final String? photoUrl;
  final VoidCallback onProfileTap;
  final VoidCallback onNotificationsTap;
  final int dueBillCount;
  final List<DashboardUpcomingBill> upcomingBills;
  final bool isEmpty;

  @override
  State<_DashboardHeroInsightsSection> createState() =>
      _DashboardHeroInsightsSectionState();
}

class _DashboardHeroInsightsSectionState
    extends State<_DashboardHeroInsightsSection> {
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
    if (!_sheetController.isAttached) {
      return;
    }

    final double nextExtent = _sheetController.size;

    if ((_sheetExtentNotifier.value - nextExtent).abs() > 0.001) {
      final SchedulerPhase schedulerPhase =
          WidgetsBinding.instance.schedulerPhase;

      if (schedulerPhase == SchedulerPhase.idle ||
          schedulerPhase == SchedulerPhase.postFrameCallbacks) {
        _sheetExtentNotifier.value = nextExtent;
        return;
      }

      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted || !_sheetController.isAttached) {
          return;
        }

        if ((_sheetExtentNotifier.value - nextExtent).abs() > 0.001) {
          _sheetExtentNotifier.value = nextExtent;
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
          _DashboardHeroInsightsSection._maxHeroHeight,
          math.max(
            _DashboardHeroInsightsSection._minHeroHeight,
            viewportHeight * 0.37,
          ),
        );
        const double pinnedSheetTop =
            _DashboardHeroInsightsSection._pinnedHeaderHeight +
                _DashboardHeroInsightsSection._sheetTopGap;
        final double sheetViewportHeight = math.max(
          1,
          viewportHeight - pinnedSheetTop,
        );
        final double collapsedSheetTop = math.max(
          pinnedSheetTop + 164,
          heroHeight + PayaboSpacing.sm,
        );
        final double initialSheetSize =
            (1 - ((collapsedSheetTop - pinnedSheetTop) / sheetViewportHeight))
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
            Positioned.fill(
              child: DecoratedBox(
                decoration: BoxDecoration(
                  gradient: _dashboardBackgroundGradient(context),
                ),
              ),
            ),
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: heroHeight,
              child: _DashboardHeroBanner(
                displayName: widget.displayName,
                dueBillCount: widget.dueBillCount,
                isEmpty: widget.isEmpty,
                bottomPadding: heroBottomPadding,
              ),
            ),
            Positioned(
              top: 0,
              left: 0,
              right: 0,
              height: _DashboardHeroInsightsSection._pinnedHeaderHeight,
              child: ValueListenableBuilder<double>(
                valueListenable: _sheetExtentNotifier,
                builder: (
                  BuildContext context,
                  double sheetExtent,
                  Widget? child,
                ) {
                  final double effectiveSheetExtent =
                      (sheetExtent <= 0 ? initialSheetSize : sheetExtent)
                          .clamp(
                            minSheetSize,
                            _DashboardHeroInsightsSection._maxSheetSize,
                          )
                          .toDouble();
                  final double sheetRange = math.max(
                    0.0001,
                    _DashboardHeroInsightsSection._maxSheetSize -
                        initialSheetSize,
                  );
                  final double headerBackgroundProgress =
                      Curves.easeOut.transform(
                    ((effectiveSheetExtent - initialSheetSize) / sheetRange)
                        .clamp(0, 1)
                        .toDouble(),
                  );

                  return _DashboardPinnedHeader(
                    backgroundProgress: headerBackgroundProgress,
                    displayName: widget.displayName,
                    photoUrl: widget.photoUrl,
                    onProfileTap: widget.onProfileTap,
                    onNotificationsTap: widget.onNotificationsTap,
                  );
                },
              ),
            ),
            Positioned(
              top: pinnedSheetTop,
              left: 0,
              right: 0,
              bottom: 0,
              child: DraggableScrollableSheet(
                controller: _sheetController,
                initialChildSize: initialSheetSize,
                minChildSize: minSheetSize,
                maxChildSize: _DashboardHeroInsightsSection._maxSheetSize,
                snap: true,
                snapSizes: <double>[
                  initialSheetSize,
                  _DashboardHeroInsightsSection._maxSheetSize,
                ],
                builder: (
                  BuildContext context,
                  ScrollController scrollController,
                ) {
                  return _DashboardStatsSheet(
                    scrollController: scrollController,
                    dueBillCount: widget.dueBillCount,
                    upcomingBills: widget.upcomingBills,
                    isEmpty: widget.isEmpty,
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

class _DashboardPinnedHeader extends StatelessWidget {
  const _DashboardPinnedHeader({
    required this.backgroundProgress,
    required this.displayName,
    required this.photoUrl,
    required this.onProfileTap,
    required this.onNotificationsTap,
  });

  final double backgroundProgress;
  final String displayName;
  final String? photoUrl;
  final VoidCallback onProfileTap;
  final VoidCallback onNotificationsTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Stack(
      fit: StackFit.expand,
      children: <Widget>[
        IgnorePointer(
          child: Opacity(
            opacity: backgroundProgress,
            child: DecoratedBox(
              decoration: BoxDecoration(
                gradient: c.warmScreenGradient,
                boxShadow: backgroundProgress > 0
                    ? const <BoxShadow>[
                        BoxShadow(
                          color: Color(0x12000000),
                          blurRadius: 10,
                          offset: Offset(0, 2),
                        ),
                      ]
                    : const <BoxShadow>[],
              ),
            ),
          ),
        ),
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
                child: _DashboardProfileSummary(
                  onTap: onProfileTap,
                  photoUrl: photoUrl,
                  displayName: displayName,
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),
              _DashboardNotificationButton(onTap: onNotificationsTap),
            ],
          ),
        ),
      ],
    );
  }
}

class _DashboardHeroBanner extends StatelessWidget {
  const _DashboardHeroBanner({
    required this.displayName,
    required this.dueBillCount,
    required this.isEmpty,
    required this.bottomPadding,
  });

  final String displayName;
  final int dueBillCount;
  final bool isEmpty;
  final double bottomPadding;

  @override
  Widget build(BuildContext context) {
    final TextTheme textTheme = Theme.of(context).textTheme;
    final String firstName = _dashboardFirstName(displayName);
    final String greeting = _dashboardGreeting(DateTime.now());
    final String dueBillPhrase = _dashboardDueBillPhrase(dueBillCount);
    final bool isDark = context.colors.isDark;
    final TextStyle baseMessageStyle = textTheme.bodyLarge?.copyWith(
          fontSize: 17,
          color: Colors.white.withValues(alpha: 0.76),
          height: 1.5,
        ) ??
        const TextStyle(
          color: Colors.white,
          fontSize: 17,
          height: 1.5,
        );
    final TextStyle emphasisStyle = baseMessageStyle.copyWith(
      color: const Color(0xFFF3A85C),
      fontWeight: FontWeight.w700,
    );

    return Container(
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
              builder: (
                BuildContext context,
                BoxConstraints constraints,
              ) {
                final bool compact = constraints.maxHeight < 190;
                final int messageMaxLines = compact ? 4 : 5;

                return Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      '$greeting, $firstName.',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: (compact
                              ? textTheme.headlineMedium
                              : textTheme.headlineLarge)
                          ?.copyWith(
                        color: Colors.white,
                        fontWeight: FontWeight.w700,
                        height: 1.08,
                      ),
                    ),
                    SizedBox(
                      height: compact ? PayaboSpacing.sm : PayaboSpacing.md,
                    ),
                    Text.rich(
                      TextSpan(
                        style: baseMessageStyle,
                        children: isEmpty
                            ? <InlineSpan>[
                                const TextSpan(
                                    text: 'This might interest you. '),
                                const TextSpan(
                                  text:
                                      'Add your first bill to unlock daily insights, spendable balance guidance, and due reminders.',
                                ),
                              ]
                            : <InlineSpan>[
                                const TextSpan(
                                    text: 'This might interest you. '),
                                const TextSpan(text: 'You have '),
                                TextSpan(
                                  text: '₵1,285.00',
                                  style: emphasisStyle,
                                ),
                                const TextSpan(text: ' available to spend, '),
                                TextSpan(
                                  text: dueBillPhrase,
                                  style: emphasisStyle,
                                ),
                                const TextSpan(text: ' due this week, and '),
                                TextSpan(
                                  text: '₵620',
                                  style: emphasisStyle,
                                ),
                                const TextSpan(
                                  text: ' added to your net worth this month.',
                                ),
                              ],
                      ),
                      maxLines: messageMaxLines,
                      overflow: TextOverflow.ellipsis,
                      softWrap: true,
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

class _DashboardStatsSheet extends StatelessWidget {
  const _DashboardStatsSheet({
    required this.scrollController,
    required this.dueBillCount,
    required this.upcomingBills,
    required this.isEmpty,
  });

  final ScrollController scrollController;
  final int dueBillCount;
  final List<DashboardUpcomingBill> upcomingBills;
  final bool isEmpty;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final Color sheetBackground = c.surfaceBase;
    final Color sheetBorder =
        c.isDark ? c.borderStrong.withValues(alpha: 0.52) : c.border;
    final Color handleColor = c.borderStrong;

    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: sheetBackground,
        borderRadius: const BorderRadius.only(
          topLeft: Radius.circular(24),
          topRight: Radius.circular(24),
          bottomLeft: Radius.circular(20),
          bottomRight: Radius.circular(20),
        ),
        border: Border.all(color: sheetBorder),
        boxShadow: <BoxShadow>[
          BoxShadow(
            color: Colors.black.withValues(alpha: c.isDark ? 0.22 : 0.08),
            blurRadius: 18,
            offset: const Offset(0, -4),
          ),
        ],
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
          Center(
            child: Container(
              width: 42,
              height: 5,
              decoration: BoxDecoration(
                color: handleColor,
                borderRadius: BorderRadius.circular(999),
              ),
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          _InsightCarouselSection(
            dueBillCount: dueBillCount,
            isEmpty: isEmpty,
          ),
          const SizedBox(height: PayaboSpacing.xl),
          _DashboardMetricSummary(
            isEmpty: isEmpty,
          ),
          const SizedBox(height: PayaboSpacing.xl),
          const _DashboardOverviewCard(),
          const SizedBox(height: PayaboSpacing.xl),
          _DashboardListHeader(
            title: 'Upcoming bills',
            actionLabel: isEmpty ? null : 'View all',
          ),
          const SizedBox(height: PayaboSpacing.md),
          if (upcomingBills.isEmpty)
            const _DashboardEmptyBillsCard()
          else
            _UpcomingBillsCardV2(items: upcomingBills),
        ],
      ),
    );
  }
}

class _DashboardMetricSummary extends StatelessWidget {
  const _DashboardMetricSummary({
    required this.isEmpty,
  });

  final bool isEmpty;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            Expanded(
              child: Text(
                'Today at a glance',
                style: Theme.of(context).textTheme.titleLarge?.copyWith(
                      color: const Color(0xFF2C2017),
                      fontWeight: FontWeight.w700,
                    ),
              ),
            ),
            _DashboardStatusPill(label: isEmpty ? 'set up' : 'updated today'),
          ],
        ),
        const SizedBox(height: PayaboSpacing.md),
        _DashboardSpendableBalanceCard(isEmpty: isEmpty),
      ],
    );
  }
}

class _DashboardSpendableBalanceCard extends StatelessWidget {
  const _DashboardSpendableBalanceCard({required this.isEmpty});

  final bool isEmpty;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        gradient: c.spendingSafeToSpendGradient,
        borderRadius: PayaboRadii.radiusSm,
        boxShadow: PayaboShadows.medium,
      ),
      child: Padding(
        padding: const EdgeInsets.all(PayaboSpacing.lg),
        child: Row(
          children: <Widget>[
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    'Spendable balance',
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(
                          color: Colors.white,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.xs),
                  Text(
                    isEmpty ? '₵0.00' : '₵1,285.00',
                    style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                          color: Colors.white,
                          fontWeight: FontWeight.w800,
                        ),
                  ),
                  const SizedBox(height: PayaboSpacing.xs),
                  Text(
                    isEmpty
                        ? 'Add bills and budgets to unlock your spendable balance.'
                        : 'After bills, savings, and your weekly safety buffer.',
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: Colors.white.withValues(alpha: 0.82),
                        ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: PayaboSpacing.lg),
            Container(
              width: 56,
              height: 56,
              decoration: BoxDecoration(
                color: Colors.white.withValues(alpha: 0.16),
                borderRadius: BorderRadius.circular(18),
              ),
              child: const Icon(
                Icons.account_balance_wallet_outlined,
                color: Colors.white,
                size: 28,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _InsightCarouselSection extends StatefulWidget {
  const _InsightCarouselSection({
    required this.dueBillCount,
    required this.isEmpty,
  });

  final int dueBillCount;
  final bool isEmpty;

  @override
  State<_InsightCarouselSection> createState() =>
      _InsightCarouselSectionState();
}

class _InsightCarouselSectionState extends State<_InsightCarouselSection> {
  static const Duration _autoScrollDelay = Duration(seconds: 5);
  static const Duration _scrollAnimationDuration = Duration(milliseconds: 420);
  static const int _pageCount = 3;

  late final PageController _pageController;
  Timer? _autoScrollTimer;
  int _currentPage = 0;
  bool _isUserInteracting = false;
  double _dragDeltaX = 0;

  @override
  void initState() {
    super.initState();
    _pageController = PageController();
    _scheduleAutoScroll();
  }

  @override
  void dispose() {
    _autoScrollTimer?.cancel();
    _pageController.dispose();
    super.dispose();
  }

  void _scheduleAutoScroll() {
    _autoScrollTimer?.cancel();
    _autoScrollTimer = Timer(_autoScrollDelay, _advancePage);
  }

  void _pauseAutoScroll() {
    _autoScrollTimer?.cancel();
  }

  void _advancePage() {
    if (!mounted) {
      return;
    }

    if (!_pageController.hasClients) {
      _scheduleAutoScroll();
      return;
    }

    final int nextPage = (_currentPage + 1) % _pageCount;
    _animateToPage(nextPage);
  }

  void _animateToPage(int nextPage) {
    if (!_pageController.hasClients || nextPage == _currentPage) {
      if (!_isUserInteracting) {
        _scheduleAutoScroll();
      }

      return;
    }

    _pageController.animateToPage(
      nextPage,
      duration: _scrollAnimationDuration,
      curve: Curves.easeInOutCubic,
    );
  }

  void _handlePageChanged(int index) {
    if (!mounted) {
      return;
    }

    setState(() {
      _currentPage = index;
    });

    if (!_isUserInteracting) {
      _scheduleAutoScroll();
    }
  }

  @override
  Widget build(BuildContext context) {
    final List<Widget> pages = <Widget>[
      _TodayInsightCard(isEmpty: widget.isEmpty),
      _AvailableToSpendInsightCard(
        dueBillCount: widget.dueBillCount,
        isEmpty: widget.isEmpty,
      ),
      _NetWorthInsightCard(isEmpty: widget.isEmpty),
    ];

    return Column(
      children: <Widget>[
        SizedBox(
          height: 240,
          child: GestureDetector(
            onHorizontalDragStart: (_) {
              _isUserInteracting = true;
              _dragDeltaX = 0;
              _pauseAutoScroll();
            },
            onHorizontalDragUpdate: (DragUpdateDetails details) {
              _dragDeltaX += details.primaryDelta ?? 0;
            },
            onHorizontalDragEnd: (DragEndDetails details) {
              final double velocity = details.primaryVelocity ?? 0;

              if (velocity < 0) {
                _animateToPage((_currentPage + 1).clamp(0, _pageCount - 1));
              } else if (velocity > 0) {
                _animateToPage((_currentPage - 1).clamp(0, _pageCount - 1));
              } else if (_dragDeltaX <= -40) {
                _animateToPage((_currentPage + 1).clamp(0, _pageCount - 1));
              } else if (_dragDeltaX >= 40) {
                _animateToPage((_currentPage - 1).clamp(0, _pageCount - 1));
              }

              _isUserInteracting = false;
              _dragDeltaX = 0;
              _scheduleAutoScroll();
            },
            child: PageView(
              controller: _pageController,
              physics: const NeverScrollableScrollPhysics(),
              onPageChanged: _handlePageChanged,
              children: pages,
            ),
          ),
        ),
        const SizedBox(height: PayaboSpacing.sm),
        _InsightPageIndicator(
          currentPage: _currentPage,
          pageCount: pages.length,
        ),
      ],
    );
  }
}

class _InsightCarouselCardShell extends StatelessWidget {
  const _InsightCarouselCardShell({
    required this.child,
    this.backgroundColor,
    this.borderColor,
    this.gradient,
    this.borderRadius = PayaboRadii.radiusSm,
    this.padding = const EdgeInsets.all(PayaboSpacing.lg),
  });

  final Widget child;
  final Color? backgroundColor;
  final Color? borderColor;
  final Gradient? gradient;
  final BorderRadiusGeometry borderRadius;
  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final Color defaultBackground =
        c.isDark ? c.surfaceCardElevated : const Color(0xFFFFFBF8);
    final Color defaultBorder = c.isDark
        ? c.borderStrong.withValues(alpha: 0.5)
        : const Color(0xFFF1DEC9);

    return Container(
      decoration: BoxDecoration(
        color: backgroundColor ?? defaultBackground,
        gradient: gradient,
        borderRadius: borderRadius,
        border: Border.all(color: borderColor ?? defaultBorder, width: 0.5),
      ),
      child: Padding(
        padding: padding,
        child: child,
      ),
    );
  }
}

class _TodayInsightCard extends StatelessWidget {
  const _TodayInsightCard({required this.isEmpty});

  final bool isEmpty;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;
    final String message = isEmpty
        ? 'Add a bill to unlock daily insights and spending guidance.'
        : 'Dining spend is running 18% above your usual daily pace.';

    return _InsightCarouselCardShell(
      borderRadius: PayaboRadii.radiusSm,
      child: Column(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Container(
                width: 38,
                height: 38,
                decoration: BoxDecoration(
                  color: const Color(0xFFD3A04B).withValues(alpha: 0.14),
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.tips_and_updates_rounded,
                  color: Color(0xFFD3A04B),
                  size: 20,
                ),
              ),
              const SizedBox(width: PayaboSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Row(
                      children: <Widget>[
                        Expanded(
                          child: Text(
                            "Today's Insight",
                            overflow: TextOverflow.ellipsis,
                            style: textTheme.titleMedium?.copyWith(
                              fontWeight: FontWeight.w700,
                              color: c.accentBrown,
                            ),
                          ),
                        ),
                        Container(
                          width: 8,
                          height: 8,
                          margin: const EdgeInsets.only(left: PayaboSpacing.sm),
                          decoration: BoxDecoration(
                            color: c.primary,
                            shape: BoxShape.circle,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(
                      message,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: textTheme.bodyMedium?.copyWith(
                        color: c.muted,
                        height: 1.35,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          Row(
            children: <Widget>[
              Text(
                isEmpty ? 'Set up now' : 'Today',
                style: textTheme.labelMedium?.copyWith(
                  color: c.muted,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const Spacer(),
              Text(
                isEmpty ? 'Add first bill' : 'Review dining',
                style: textTheme.labelLarge?.copyWith(
                  color: c.primary,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _AvailableToSpendInsightCard extends StatelessWidget {
  const _AvailableToSpendInsightCard({
    required this.dueBillCount,
    required this.isEmpty,
  });

  final int dueBillCount;
  final bool isEmpty;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final TextTheme textTheme = Theme.of(context).textTheme;
    final String dueText = isEmpty
        ? 'No bills due'
        : dueBillCount == 1
            ? '1 bill due this week'
            : '$dueBillCount bills due this week';

    return _InsightCarouselCardShell(
      backgroundColor:
          c.isDark ? c.surfaceCardElevated : const Color(0xFFFFFBF8),
      borderColor: c.isDark
          ? c.borderStrong.withValues(alpha: 0.5)
          : const Color(0xFFF1DEC9),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  'Available to spend',
                  overflow: TextOverflow.ellipsis,
                  style: textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: c.accentBrown,
                  ),
                ),
              ),
              _DashboardStatusPill(label: isEmpty ? 'set up' : 'on track'),
            ],
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                isEmpty ? '₵0.00' : '₵1,285.00',
                style: textTheme.headlineMedium?.copyWith(
                  fontSize: 36,
                  height: 1,
                  fontWeight: FontWeight.w800,
                  color: c.accentBrown,
                ),
              ),
              const SizedBox(height: 6),
              Text(
                isEmpty
                    ? 'Add bills and budgets to unlock your spendable balance.'
                    : 'After bills, savings, and your weekly buffer.',
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: textTheme.bodyMedium?.copyWith(
                  color: c.muted,
                  height: 1.3,
                ),
              ),
            ],
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              ClipRRect(
                borderRadius: BorderRadius.circular(999),
                child: LinearProgressIndicator(
                  minHeight: 8,
                  value: isEmpty ? 0 : 0.78,
                  backgroundColor: c.border.withValues(alpha: 0.5),
                  valueColor: AlwaysStoppedAnimation<Color>(c.primary),
                ),
              ),
              const SizedBox(height: 10),
              Row(
                children: <Widget>[
                  Text(
                    isEmpty ? '0% free' : '78% free',
                    style: textTheme.labelLarge?.copyWith(
                      color: c.accentBrown,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const Spacer(),
                  Flexible(
                    child: Text(
                      dueText,
                      textAlign: TextAlign.end,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: textTheme.labelLarge?.copyWith(
                        color: c.muted,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                decoration: BoxDecoration(
                  color: c.isDark
                      ? c.surfaceBase.withValues(alpha: 0.12)
                      : c.primary.withValues(alpha: 0.08),
                  borderRadius: BorderRadius.circular(16),
                ),
                child: Row(
                  children: <Widget>[
                    const Icon(
                      Icons.check_circle_outline_rounded,
                      size: 16,
                      color: Color(0xFF8A571E),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        isEmpty
                            ? 'Start by adding your first bill.'
                            : 'You still have room for planned spending.',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: textTheme.labelLarge?.copyWith(
                          color: c.accentBrown,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _NetWorthInsightCard extends StatelessWidget {
  const _NetWorthInsightCard({required this.isEmpty});

  final bool isEmpty;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final TextTheme textTheme = Theme.of(context).textTheme;

    return _InsightCarouselCardShell(
      backgroundColor:
          c.isDark ? c.surfaceCardElevated : const Color(0xFFFFFBF8),
      borderColor: c.isDark
          ? c.borderStrong.withValues(alpha: 0.5)
          : const Color(0xFFF1DEC9),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  'Net worth',
                  overflow: TextOverflow.ellipsis,
                  style: textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: c.accentBrown,
                  ),
                ),
              ),
              _DashboardStatusPill(label: isEmpty ? 'add data' : 'up 3.5%'),
            ],
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                isEmpty ? '₵0.00' : '₵18,406.20',
                style: textTheme.headlineMedium?.copyWith(
                  fontSize: 34,
                  height: 1,
                  fontWeight: FontWeight.w800,
                  color: c.accentBrown,
                ),
              ),
              const SizedBox(height: 6),
              Text(
                isEmpty
                    ? 'Link balances to see your full financial picture.'
                    : '+₵620 since last month across your linked balances.',
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: textTheme.bodyMedium?.copyWith(
                  color: c.muted,
                  height: 1.3,
                ),
              ),
            ],
          ),
          Row(
            children: <Widget>[
              Expanded(
                child: _InsightStatTile(
                  label: 'Assets',
                  value: isEmpty ? '₵0.00' : '₵20.1k',
                ),
              ),
              const SizedBox(width: PayaboSpacing.sm),
              Expanded(
                child: _InsightStatTile(
                  label: 'Bills',
                  value: isEmpty ? '₵0.00' : '₵1.7k',
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _InsightStatTile extends StatelessWidget {
  const _InsightStatTile({
    required this.label,
    required this.value,
  });

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
      decoration: BoxDecoration(
        color: c.surfaceBase,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: c.borderWarm),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            label,
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: c.muted,
                ),
          ),
          const SizedBox(height: 4),
          Text(
            value,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                  color: c.ink,
                ),
          ),
        ],
      ),
    );
  }
}

class _InsightPageIndicator extends StatelessWidget {
  const _InsightPageIndicator({
    required this.currentPage,
    required this.pageCount,
  });

  final int currentPage;
  final int pageCount;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: List<Widget>.generate(pageCount, (int index) {
        final bool isActive = index == currentPage;

        return Padding(
          padding: EdgeInsets.only(right: index == pageCount - 1 ? 0 : 8),
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 180),
            width: isActive ? 30 : 8,
            height: 8,
            decoration: BoxDecoration(
              color: isActive ? c.primary : c.borderWarm,
              borderRadius: BorderRadius.circular(999),
            ),
          ),
        );
      }),
    );
  }
}

class _DashboardOverviewCard extends StatelessWidget {
  const _DashboardOverviewCard();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final slices = _dashboardOverviewSlices(c);
    final Color cardBackground =
        c.isDark ? c.surfaceCardElevated : const Color(0xFF171D26);
    final Color cardBorder = c.isDark
        ? c.borderStrong.withValues(alpha: 0.5)
        : const Color(0xFF2B3442);
    final Color titleColor = c.isDark ? Colors.white : const Color(0xFFF4F6FA);
    final Color mutedColor = c.isDark ? c.muted : const Color(0xFFB7C0CC);

    return Container(
      decoration: BoxDecoration(
        color: cardBackground,
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: cardBorder, width: 0.5),
      ),
      child: ClipRRect(
        borderRadius: PayaboRadii.radiusSm,
        child: Padding(
          padding: const EdgeInsets.all(PayaboSpacing.xl),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      'Overview',
                      style: Theme.of(context).textTheme.titleLarge?.copyWith(
                            color: titleColor,
                            fontWeight: FontWeight.w700,
                          ),
                    ),
                  ),
                  _DashboardOverviewMonthChip(
                    label: 'Mar',
                    backgroundColor: Colors.white.withValues(
                      alpha: c.isDark ? 0.08 : 0.06,
                    ),
                    borderColor: Colors.white.withValues(
                      alpha: c.isDark ? 0.12 : 0.10,
                    ),
                    textColor: titleColor,
                  ),
                ],
              ),
              const SizedBox(height: PayaboSpacing.lg),
              Center(
                child: _DashboardOverviewRing(
                  slices: slices,
                  trackColor: Colors.white.withValues(alpha: 0.12),
                  titleColor: titleColor,
                  subtitleColor: mutedColor,
                ),
              ),
              const SizedBox(height: PayaboSpacing.xl),
              ...slices.asMap().entries.map(
                    (MapEntry<int, _DashboardOverviewSlice> entry) => Padding(
                      padding: EdgeInsets.only(
                        bottom: entry.key == slices.length - 1
                            ? 0
                            : PayaboSpacing.md,
                      ),
                      child: _DashboardOverviewRow(
                        slice: entry.value,
                        labelColor: titleColor,
                        amountColor: titleColor,
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

class _DashboardOverviewMonthChip extends StatelessWidget {
  const _DashboardOverviewMonthChip({
    required this.label,
    required this.backgroundColor,
    required this.borderColor,
    required this.textColor,
  });

  final String label;
  final Color backgroundColor;
  final Color borderColor;
  final Color textColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: PayaboSpacing.md,
        vertical: 10,
      ),
      decoration: BoxDecoration(
        color: backgroundColor,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: borderColor),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Text(
            label,
            style: Theme.of(context).textTheme.labelLarge?.copyWith(
                  color: textColor,
                  fontWeight: FontWeight.w700,
                ),
          ),
          const SizedBox(width: PayaboSpacing.xs),
          Icon(
            Icons.keyboard_arrow_down_rounded,
            size: 18,
            color: textColor,
          ),
        ],
      ),
    );
  }
}

class _DashboardOverviewRing extends StatelessWidget {
  const _DashboardOverviewRing({
    required this.slices,
    required this.trackColor,
    required this.titleColor,
    required this.subtitleColor,
  });

  final List<_DashboardOverviewSlice> slices;
  final Color trackColor;
  final Color titleColor;
  final Color subtitleColor;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 220,
      height: 220,
      child: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          CustomPaint(
            size: const Size.square(220),
            painter: _DashboardOverviewRingPainter(
              slices: slices,
              trackColor: trackColor,
            ),
          ),
          Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                'March',
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: titleColor,
                      fontWeight: FontWeight.w700,
                    ),
              ),
              const SizedBox(height: 2),
              Text(
                '2026',
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: subtitleColor,
                      fontWeight: FontWeight.w600,
                    ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _DashboardOverviewRingPainter extends CustomPainter {
  const _DashboardOverviewRingPainter({
    required this.slices,
    required this.trackColor,
  });

  static const double _gapRadians = 0.22;
  static const double _strokeWidth = 16;

  final List<_DashboardOverviewSlice> slices;
  final Color trackColor;

  @override
  void paint(Canvas canvas, Size size) {
    final Offset center = Offset(size.width / 2, size.height / 2);
    final double radius = (math.min(size.width, size.height) / 2) - 18;
    final Rect rect = Rect.fromCircle(center: center, radius: radius);
    final Paint trackPaint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = _strokeWidth
      ..strokeCap = StrokeCap.round
      ..color = trackColor;

    canvas.drawArc(rect, 0, math.pi * 2, false, trackPaint);

    final double total = slices.fold<double>(
      0,
      (double sum, _DashboardOverviewSlice slice) => sum + slice.value,
    );

    final double totalSweep = (math.pi * 2) - (slices.length * _gapRadians);
    double startAngle = -math.pi / 2;

    for (final _DashboardOverviewSlice slice in slices) {
      final double sweepAngle =
          total == 0 ? 0 : totalSweep * (slice.value / total).clamp(0.0, 1.0);
      final Paint slicePaint = Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = _strokeWidth
        ..strokeCap = StrokeCap.round
        ..color = slice.color;

      canvas.drawArc(rect, startAngle, sweepAngle, false, slicePaint);
      startAngle += sweepAngle + _gapRadians;
    }
  }

  @override
  bool shouldRepaint(covariant _DashboardOverviewRingPainter oldDelegate) {
    return oldDelegate.slices != slices || oldDelegate.trackColor != trackColor;
  }
}

class _DashboardOverviewRow extends StatelessWidget {
  const _DashboardOverviewRow({
    required this.slice,
    required this.labelColor,
    required this.amountColor,
  });

  final _DashboardOverviewSlice slice;
  final Color labelColor;
  final Color amountColor;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Container(
          width: 12,
          height: 12,
          decoration: BoxDecoration(
            color: slice.color,
            shape: BoxShape.circle,
          ),
        ),
        const SizedBox(width: PayaboSpacing.sm),
        Expanded(
          child: Text(
            slice.label,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  color: labelColor,
                ),
          ),
        ),
        Text(
          slice.amountLabel,
          style: Theme.of(context).textTheme.titleSmall?.copyWith(
                color: amountColor,
                fontWeight: FontWeight.w700,
              ),
        ),
      ],
    );
  }
}

class _DashboardStatusPill extends StatelessWidget {
  const _DashboardStatusPill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
      decoration: BoxDecoration(
        color: c.isDark ? c.surfaceWarmAccent : const Color(0xFFF3E4C8),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelLarge?.copyWith(
              fontWeight: FontWeight.w500,
              color: c.isDark ? c.primary : const Color(0xFF7C5B25),
            ),
      ),
    );
  }
}

class _DashboardOverviewSlice {
  const _DashboardOverviewSlice({
    required this.label,
    required this.amountLabel,
    required this.value,
    required this.color,
  });

  final String label;
  final String amountLabel;
  final double value;
  final Color color;
}

class _DashboardListHeader extends StatelessWidget {
  const _DashboardListHeader({
    required this.title,
    this.actionLabel,
  });

  final String title;
  final String? actionLabel;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final Color titleColor = c.isDark ? Colors.white : const Color(0xFF2C2017);

    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            title,
            style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                  fontSize: 18,
                  fontWeight: FontWeight.w500,
                  color: titleColor,
                ),
          ),
        ),
        if (actionLabel != null)
          Text(
            actionLabel!,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  fontSize: 15,
                  fontWeight: FontWeight.w500,
                  color: const Color(0xFFD97A1D),
                ),
          ),
      ],
    );
  }
}

class _UpcomingBillsCardV2 extends StatelessWidget {
  const _UpcomingBillsCardV2({required this.items});

  final List<DashboardUpcomingBill> items;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Column(
      children: items
          .asMap()
          .entries
          .map(
            (entry) => Column(
              children: <Widget>[
                _UpcomingBillRow(item: entry.value),
                if (entry.key != items.length - 1)
                  Divider(
                    height: 1,
                    color: theme.colorScheme.outlineVariant.withValues(
                      alpha: 0.3,
                    ),
                  ),
              ],
            ),
          )
          .toList(growable: false),
    );
  }
}

class _UpcomingBillRow extends StatelessWidget {
  const _UpcomingBillRow({required this.item});

  final DashboardUpcomingBill item;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final c = context.colors;
    final iconSurface = c.isDark
        ? theme.colorScheme.surfaceContainerHighest
        : c.primary.withValues(alpha: 0.08);

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.md),
      child: Row(
        children: <Widget>[
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: iconSurface,
              borderRadius: const BorderRadius.all(Radius.circular(10)),
            ),
            alignment: Alignment.center,
            child: Icon(
              Icons.receipt_long_outlined,
              size: 18,
              color: theme.colorScheme.primary,
            ),
          ),
          const SizedBox(width: PayaboSpacing.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  item.biller,
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.w600,
                    color: theme.colorScheme.onSurface,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  item.dueDateLabel,
                  style: theme.textTheme.bodySmall,
                ),
              ],
            ),
          ),
          const SizedBox(width: PayaboSpacing.md),
          Text(
            item.amountLabel,
            style: theme.textTheme.titleSmall?.copyWith(
              fontWeight: FontWeight.w700,
              color: theme.colorScheme.onSurface,
            ),
          ),
        ],
      ),
    );
  }
}

class _DashboardEmptyBillsCard extends StatelessWidget {
  const _DashboardEmptyBillsCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFFFFFBF7),
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: const Color(0xFFE6D8C7)),
      ),
      padding: const EdgeInsets.all(20),
      child: Row(
        children: <Widget>[
          Container(
            width: 42,
            height: 42,
            decoration: BoxDecoration(
              color: const Color(0xFFF2E4D2),
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Icon(
              Icons.receipt_long_outlined,
              color: Color(0xFFD97A1D),
            ),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Text(
              'No upcoming bills yet. Add a bill to start tracking due dates.',
              style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                    color: const Color(0xFF5A3217),
                  ),
            ),
          ),
        ],
      ),
    );
  }
}

class _DashboardHeroCard extends StatelessWidget {
  const _DashboardHeroCard({
    required this.upcomingBillCount,
    required this.isEmpty,
  });

  final int upcomingBillCount;
  final bool isEmpty;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    final String dueText = isEmpty
        ? 'No bills due this week'
        : upcomingBillCount == 1
            ? '1 bill due this week'
            : '$upcomingBillCount bills due this week';

    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: c.isDark
              ? <Color>[const Color(0xFF4C341B), const Color(0xFF7A4317)]
              : const <Color>[Color(0xFFFFAE58), Color(0xFFF37920)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: PayaboRadii.radiusSm,
        border: Border.all(color: c.primary),
        boxShadow: PayaboShadows.soft,
      ),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(
          PayaboSpacing.xl,
          PayaboSpacing.xl,
          PayaboSpacing.xl,
          PayaboSpacing.lg,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(
              'Available to spend',
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: c.accentBrown,
                    fontWeight: FontWeight.w500,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.xs),
            FittedBox(
              fit: BoxFit.scaleDown,
              alignment: Alignment.centerLeft,
              child: Text(
                isEmpty ? '₵0.00' : '₵1,285.00',
                style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                      color: c.isDark ? Colors.white : const Color(0xFF4F220F),
                      fontSize: 52,
                      height: 1,
                      fontWeight: FontWeight.w700,
                    ),
              ),
            ),
            const SizedBox(height: PayaboSpacing.sm),
            Text(
              dueText,
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: c.accentBrown,
                    fontWeight: FontWeight.w500,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.lg),
            const Divider(color: Color(0x66A34F12), height: 1),
            const SizedBox(height: PayaboSpacing.lg),
            Row(
              children: <Widget>[
                Container(
                  width: 28,
                  height: 28,
                  decoration: BoxDecoration(
                    color: c.isDark
                        ? c.surfaceBase.withValues(alpha: 0.2)
                        : const Color(0xFF7C320E),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(
                    Icons.check,
                    color: Colors.white,
                    size: 18,
                  ),
                ),
                const SizedBox(width: PayaboSpacing.md),
                Expanded(
                  child: Text(
                    isEmpty
                        ? 'Start by adding your first bill'
                        : "You're on track",
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                          color: c.accentBrown,
                          fontWeight: FontWeight.w600,
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

class _SectionHeading extends StatelessWidget {
  const _SectionHeading({
    required this.title,
    required this.onActionTap,
    this.actionLabel,
  });

  final String title;
  final String? actionLabel;
  final VoidCallback onActionTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Padding(
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.xl,
        PayaboSpacing.md,
        PayaboSpacing.xl,
        PayaboSpacing.sm,
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              title,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontSize: 20,
                    fontWeight: FontWeight.w700,
                    color: c.accentBrown,
                  ),
            ),
          ),
          if (actionLabel != null)
            TextButton(
              onPressed: onActionTap,
              child: Text(
                actionLabel!,
                style: Theme.of(context).textTheme.labelLarge?.copyWith(
                      color: c.primary,
                    ),
              ),
            ),
        ],
      ),
    );
  }
}

class _EmptyPanel extends StatelessWidget {
  const _EmptyPanel({
    required this.icon,
    required this.message,
    this.actionLabel,
  });

  final IconData icon;
  final String message;
  final String? actionLabel;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Padding(
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Container(
        padding: const EdgeInsets.all(PayaboSpacing.x3),
        decoration: BoxDecoration(
          color: c.surfaceCardElevated,
          borderRadius: PayaboRadii.radiusLg,
          border: Border.all(color: c.borderWarm),
          boxShadow: PayaboShadows.soft,
        ),
        child: Column(
          children: <Widget>[
            Icon(icon, size: 56, color: c.primary),
            const SizedBox(height: PayaboSpacing.md),
            Text(
              message,
              textAlign: TextAlign.center,
              style: Theme.of(context)
                  .textTheme
                  .titleSmall
                  ?.copyWith(color: c.accentBrownMuted),
            ),
            if (actionLabel != null) ...<Widget>[
              const SizedBox(height: PayaboSpacing.md),
              PayaboButton(
                label: actionLabel!,
                size: PayaboButtonSize.sm,
                expand: false,
                onPressed: () {},
              ),
            ],
          ],
        ),
      ),
    );
  }
}
