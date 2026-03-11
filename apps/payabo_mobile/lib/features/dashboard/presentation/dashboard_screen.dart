// ignore_for_file: unused_element, unused_element_parameter

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../data/repositories/dashboard_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_gradients.dart';
import '../../../shared/theme/payabo_radii.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_bottom_nav.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_card.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import '../../../shared/widgets/payabo_modal_sheet.dart';
import '../../../shared/widgets/payabo_profile_avatar.dart';
import '../../profile/presentation/profile_state.dart';

final FutureProvider<DashboardSummary> dashboardSummaryProvider =
    FutureProvider<DashboardSummary>((Ref ref) async {
  final repository = ref.watch(dashboardRepositoryProvider);
  return repository.getSummary();
});

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
  int _navIndex = 0;

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
    final profileState = ref.watch(profileControllerProvider);

    return Scaffold(
      backgroundColor: PayaboColors.surfaceWarm,
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: PayaboGradients.warmScreen,
        ),
        child: SafeArea(
          child: Column(
            children: <Widget>[
              _DashboardHeader(
                onProfileTap: () => context.go('/profile'),
                onNotificationsTap: _showNotificationsMessage,
                photoUrl: profileState.photoUrl,
              ),
              Expanded(
                child: summaryValue.when(
                  data: (summary) {
                    final isEmpty = widget.showEmptyState;

                    return RefreshIndicator(
                      onRefresh: () async =>
                          ref.refresh(dashboardSummaryProvider.future),
                      child: _DashboardContent(
                        summary: summary,
                        isEmpty: isEmpty,
                      ),
                    );
                  },
                  loading: () =>
                      const Center(child: CircularProgressIndicator()),
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
        ),
      ),
      bottomNavigationBar: PayaboBottomNav(
        items: const <PayaboBottomNavItem>[
          PayaboBottomNavItem(icon: Icons.home_outlined, label: 'Home'),
          PayaboBottomNavItem(
              icon: Icons.receipt_long_outlined, label: 'Bills'),
          PayaboBottomNavItem(
              icon: Icons.show_chart_outlined, label: 'Spending'),
          PayaboBottomNavItem(icon: Icons.chat_bubble_outline, label: 'Chat'),
        ],
        currentIndex: _navIndex,
        onTap: _handleNavTap,
        onCenterTap: _showQuickActions,
      ),
    );
  }

  Future<void> _ensureProfileLoaded() async {
    try {
      await ref.read(profileControllerProvider.notifier).ensureLoaded();
    } catch (_) {}
  }

  void _showNotificationsMessage() {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        const SnackBar(content: Text('Notifications are coming soon.')),
      );
  }

  void _handleNavTap(int index) {
    setState(() {
      _navIndex = index;
    });

    switch (index) {
      case 0:
        context.go('/dashboard');
        return;
      case 1:
        context.go('/payments/country');
        return;
      case 2:
        context.go('/spending');
        return;
      case 3:
        context.go('/chat');
        return;
    }
  }

  Future<void> _showQuickActions() async {
    await showPayaboModalSheet<void>(
      context: context,
      title: 'Quick Actions',
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          PayaboListRow(
            title: 'Pay a bill',
            subtitle: 'Start a bill payment now',
            leading: const Icon(Icons.receipt_long_outlined),
            onTap: () {
              Navigator.of(context).pop();
              context.go('/payments/country');
            },
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Transfer',
            subtitle: 'Send money to another account',
            leading: const Icon(Icons.compare_arrows_outlined),
            onTap: () => Navigator.of(context).pop(),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Account',
            subtitle: 'Manage your account details',
            leading: const Icon(Icons.account_balance_outlined),
            onTap: () => Navigator.of(context).pop(),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Income',
            subtitle: 'Track and categorize income',
            leading: const Icon(Icons.trending_up_outlined),
            onTap: () => Navigator.of(context).pop(),
          ),
        ],
      ),
    );
  }
}

class _DashboardHeader extends StatelessWidget {
  const _DashboardHeader({
    required this.onProfileTap,
    required this.onNotificationsTap,
    required this.photoUrl,
  });

  final VoidCallback onProfileTap;
  final VoidCallback onNotificationsTap;
  final String? photoUrl;

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
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: <Widget>[
          _DashboardProfileAvatar(
            onTap: onProfileTap,
            photoUrl: photoUrl,
          ),
          _DashboardNotificationButton(onTap: onNotificationsTap),
        ],
      ),
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
    return Material(
      color: const Color(0xFFFFFBF8),
      shape: const CircleBorder(),
      child: Container(
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          border: Border.all(color: PayaboColors.primary, width: 1.5),
        ),
        child: InkWell(
          onTap: onTap,
          customBorder: const CircleBorder(),
          child: Padding(
            padding: const EdgeInsets.all(1.5),
            child: PayaboProfileAvatar(
              photoUrl: photoUrl,
              size: 42,
              backgroundColor: const Color(0xFFF4ECDE),
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
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: Ink(
          width: 44,
          height: 44,
          decoration: BoxDecoration(
            color: const Color(0xFFFFFCF6),
            shape: BoxShape.circle,
            border: Border.all(color: const Color(0xFFDCCDB7)),
          ),
          child: Stack(
            clipBehavior: Clip.none,
            children: <Widget>[
              const Center(
                child: Icon(
                  Icons.notifications_none_rounded,
                  color: Color(0xFF9B7A43),
                  size: 22,
                ),
              ),
              Positioned(
                right: 10,
                top: 9,
                child: Container(
                  width: 8,
                  height: 8,
                  decoration: const BoxDecoration(
                    color: Color(0xFFD7A14E),
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
  });

  final DashboardSummary summary;
  final bool isEmpty;

  @override
  Widget build(BuildContext context) {
    final bills =
        isEmpty ? const <DashboardUpcomingBill>[] : summary.upcomingBills;

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.xl,
        0,
        PayaboSpacing.xl,
        PayaboSpacing.x2,
      ),
      children: <Widget>[
        _InsightCard(isEmpty: isEmpty),
        const SizedBox(height: PayaboSpacing.md),
        const _InsightPageIndicator(),
        const SizedBox(height: PayaboSpacing.md),
        _DashboardFeatureRow(isEmpty: isEmpty),
        const SizedBox(height: PayaboSpacing.md),
        _DashboardBalanceCard(
          dueBillCount: bills.length,
          isEmpty: isEmpty,
        ),
        const SizedBox(height: PayaboSpacing.xl),
        _DashboardListHeader(
          title: 'Upcoming bills',
          actionLabel: isEmpty ? null : 'View all',
        ),
        const SizedBox(height: PayaboSpacing.md),
        if (bills.isEmpty)
          const _DashboardEmptyBillsCard()
        else
          _UpcomingBillsCardV2(items: bills),
      ],
    );
  }
}

class _InsightCard extends StatelessWidget {
  const _InsightCard({required this.isEmpty});

  final bool isEmpty;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;

    return Container(
      decoration: BoxDecoration(
        color: PayaboColors.white,
        borderRadius: BorderRadius.circular(28),
        border: Border.all(color: const Color(0xFFE7DCCB)),
        boxShadow: PayaboShadows.soft,
      ),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(18, 18, 18, 16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                Container(
                  width: 34,
                  height: 34,
                  decoration: const BoxDecoration(
                    color: Color(0xFFF7EFD9),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(
                    Icons.tips_and_updates_rounded,
                    color: Color(0xFFD3A04B),
                    size: 19,
                  ),
                ),
                const SizedBox(width: PayaboSpacing.md),
                Expanded(
                  child: Text(
                    "Today's Insight",
                    overflow: TextOverflow.ellipsis,
                    style: textTheme.titleLarge?.copyWith(
                      fontSize: 18,
                      fontWeight: FontWeight.w700,
                      color: const Color(0xFF2A231D),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            Text.rich(
              TextSpan(
                style: textTheme.headlineMedium?.copyWith(
                  fontSize: 16,
                  height: 1.25,
                  color: const Color(0xFF231F1B),
                  fontWeight: FontWeight.w400,
                ),
                children: <InlineSpan>[
                  TextSpan(
                    text: isEmpty
                        ? 'You are ready to start tracking your bills.'
                        : "You're likely to overspend today. ",
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                  TextSpan(
                    text: isEmpty
                        ? 'Add a bill to unlock daily insights and spending guidance.'
                        : 'Dining category is trending higher than overall.',
                  ),
                ],
              ),
            ),
            const SizedBox(height: 16),
            const Wrap(
              spacing: PayaboSpacing.sm,
              runSpacing: PayaboSpacing.sm,
              children: <Widget>[
                _InsightActionChip(
                  label: 'Set a dining alert',
                  leadingIcon: Icons.notifications_none_rounded,
                ),
                _InsightActionChip(
                  label: 'Adjust dining budget',
                  trailingIcon: Icons.arrow_forward_rounded,
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _InsightActionChip extends StatelessWidget {
  const _InsightActionChip({
    required this.label,
    this.leadingIcon,
    this.trailingIcon,
  });

  final String label;
  final IconData? leadingIcon;
  final IconData? trailingIcon;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: const Color(0xFFF2F3F4),
        borderRadius: BorderRadius.circular(18),
      ),
      child: FittedBox(
        fit: BoxFit.scaleDown,
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            if (leadingIcon != null) ...<Widget>[
              Icon(leadingIcon, size: 15, color: const Color(0xFF4D4F5C)),
              const SizedBox(width: 6),
            ],
            Text(
              label,
              style: Theme.of(context).textTheme.labelLarge?.copyWith(
                    fontSize: 12,
                    fontWeight: FontWeight.w500,
                    color: const Color(0xFF27231E),
                  ),
            ),
            if (trailingIcon != null) ...<Widget>[
              const SizedBox(width: 6),
              Icon(trailingIcon, size: 15, color: const Color(0xFF6F6F76)),
            ],
          ],
        ),
      ),
    );
  }
}

class _InsightPageIndicator extends StatelessWidget {
  const _InsightPageIndicator();

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Container(
          width: 30,
          height: 6,
          decoration: BoxDecoration(
            color: const Color(0xFFC99144),
            borderRadius: BorderRadius.circular(999),
          ),
        ),
        const SizedBox(width: 8),
        _buildDot(const Color(0xFFD6CCBD)),
        const SizedBox(width: 8),
        _buildDot(const Color(0xFFD6CCBD)),
        const SizedBox(width: 8),
        _buildDot(const Color(0xFFD6CCBD)),
      ],
    );
  }

  Widget _buildDot(Color color) {
    return Container(
      width: 8,
      height: 8,
      decoration: BoxDecoration(
        color: color,
        shape: BoxShape.circle,
      ),
    );
  }
}

class _DashboardFeatureRow extends StatelessWidget {
  const _DashboardFeatureRow({required this.isEmpty});

  final bool isEmpty;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 196,
      child: Row(
        children: <Widget>[
          Expanded(child: _GoalsShowcaseCard(isEmpty: isEmpty)),
          const SizedBox(width: PayaboSpacing.md),
          Expanded(child: _PayaboReminderCard(isEmpty: isEmpty)),
        ],
      ),
    );
  }
}

class _GoalsShowcaseCard extends StatelessWidget {
  const _GoalsShowcaseCard({required this.isEmpty});

  final bool isEmpty;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: const Color(0x3BE8E8E8)),
        gradient: const LinearGradient(
          colors: <Color>[
            Color(0xFFB8B6AA),
            Color(0xFF5D6659),
            Color(0xFF263223),
          ],
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
        ),
        boxShadow: PayaboShadows.soft,
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(24),
        child: Stack(
          children: <Widget>[
            Positioned(
              top: -24,
              right: -16,
              child: Container(
                width: 98,
                height: 98,
                decoration: const BoxDecoration(
                  shape: BoxShape.circle,
                  color: Color(0x1EFFFFFF),
                ),
              ),
            ),
            Positioned(
              left: -24,
              right: -18,
              bottom: -46,
              child: Container(
                height: 116,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(120),
                  gradient: const LinearGradient(
                    colors: <Color>[Color(0xAA4B5C49), Color(0xFF142215)],
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                  ),
                ),
              ),
            ),
            Positioned(
              left: -18,
              right: 56,
              bottom: 10,
              child: Container(
                height: 72,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(80),
                  gradient: const LinearGradient(
                    colors: <Color>[Color(0x99384C38), Color(0x00384C38)],
                    begin: Alignment.bottomCenter,
                    end: Alignment.topCenter,
                  ),
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(14),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    'Goals',
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          fontSize: 18,
                          fontWeight: FontWeight.w500,
                          color: PayaboColors.white,
                        ),
                  ),
                  _GoalProgressStat(
                    label: 'Goal\nProgress',
                    progress: isEmpty ? 0.0 : 0.5,
                    progressLabel: isEmpty ? '0%' : '50%',
                    ringColor: PayaboColors.white,
                  ),
                  const SizedBox(height: 14),
                  _GoalProgressStat(
                    label: 'Visual\nProgress',
                    progress: isEmpty ? 0.0 : 0.7,
                    progressLabel: isEmpty ? '0%' : '70%',
                    ringColor: const Color(0xFFC78D4A),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _GoalProgressStat extends StatelessWidget {
  const _GoalProgressStat({
    required this.label,
    required this.progress,
    required this.progressLabel,
    required this.ringColor,
  });

  final String label;
  final double progress;
  final String progressLabel;
  final Color ringColor;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            label,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  height: 1.1,
                  fontSize: 13,
                  fontWeight: FontWeight.w400,
                  color: PayaboColors.white,
                ),
          ),
        ),
        _GoalProgressRing(
          progress: progress,
          label: progressLabel,
          ringColor: ringColor,
        ),
      ],
    );
  }
}

class _GoalProgressRing extends StatelessWidget {
  const _GoalProgressRing({
    required this.progress,
    required this.label,
    required this.ringColor,
  });

  final double progress;
  final String label;
  final Color ringColor;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 50,
      height: 50,
      child: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          SizedBox.expand(
            child: CircularProgressIndicator(
              value: progress,
              strokeWidth: 3.5,
              backgroundColor: const Color(0x4DFFFFFF),
              valueColor: AlwaysStoppedAnimation<Color>(ringColor),
            ),
          ),
          Text(
            label,
            style: Theme.of(context).textTheme.labelLarge?.copyWith(
                  fontSize: 10,
                  fontWeight: FontWeight.w500,
                  color: PayaboColors.white,
                ),
          ),
        ],
      ),
    );
  }
}

class _PayaboReminderCard extends StatelessWidget {
  const _PayaboReminderCard({required this.isEmpty});

  final bool isEmpty;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: const Color(0xFFE2B47D)),
        gradient: const LinearGradient(
          colors: <Color>[Color(0xFFF2CCA4), Color(0xFFEAB783)],
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
        ),
        boxShadow: PayaboShadows.soft,
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            const Row(
              children: <Widget>[
                Expanded(child: _PayaboBadge()),
                SizedBox(width: 8),
                _AssistantOrb(),
              ],
            ),
            Text(
              isEmpty
                  ? 'Payabo helps you stay ahead of upcoming bills.'
                  : 'Mum\'s\nelectricity bill\nis due in 2 days. Or\npay it now?',
              maxLines: 4,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontSize: 14,
                    height: 1.18,
                    fontWeight: FontWeight.w500,
                    color: const Color(0xFF4B2E18),
                  ),
            ),
            Container(
              width: double.infinity,
              height: 38,
              decoration: BoxDecoration(
                color: const Color(0xFFC69A70),
                borderRadius: BorderRadius.circular(24),
              ),
              alignment: Alignment.center,
              child: Text(
                'Pay now',
                style: Theme.of(context).textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w500,
                      color: const Color(0xFFFFF6EB),
                    ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _PayaboBadge extends StatelessWidget {
  const _PayaboBadge();

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          width: 18,
          height: 18,
          decoration: BoxDecoration(
            color: const Color(0xFFC27B34),
            borderRadius: BorderRadius.circular(5),
          ),
          child: const Icon(
            Icons.currency_pound_rounded,
            size: 11,
            color: Color(0xFFFFE9C9),
          ),
        ),
        const SizedBox(width: 8),
        Flexible(
          child: Text(
            'Payabo',
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                  color: const Color(0xFF5A3217),
                ),
          ),
        ),
      ],
    );
  }
}

class _AssistantOrb extends StatelessWidget {
  const _AssistantOrb();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 34,
      height: 34,
      decoration: const BoxDecoration(
        shape: BoxShape.circle,
        gradient: LinearGradient(
          colors: <Color>[Color(0xFFFCF8F0), Color(0xFFD8E4F2)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
      ),
      child: Center(
        child: Container(
          width: 22,
          height: 14,
          decoration: BoxDecoration(
            color: const Color(0xFF6D4322),
            borderRadius: BorderRadius.circular(12),
          ),
          child: const Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Icon(Icons.circle, size: 3.4, color: Colors.white),
              SizedBox(width: 4),
              Icon(Icons.circle, size: 3.4, color: Colors.white),
            ],
          ),
        ),
      ),
    );
  }
}

class _DashboardBalanceCard extends StatelessWidget {
  const _DashboardBalanceCard({
    required this.dueBillCount,
    required this.isEmpty,
  });

  final int dueBillCount;
  final bool isEmpty;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: PayaboColors.white,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: const Color(0xFFE7DDCF)),
        boxShadow: PayaboShadows.soft,
      ),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(20, 18, 20, 18),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(
              'Available to spend',
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontSize: 17,
                    fontWeight: FontWeight.w500,
                    color: const Color(0xFF413226),
                  ),
            ),
            const SizedBox(height: 6),
            Text(
              isEmpty ? '£0.00' : '£1,285.00',
              style: Theme.of(context).textTheme.displayMedium?.copyWith(
                    fontSize: 46,
                    height: 1,
                    fontWeight: FontWeight.w700,
                    color: const Color(0xFF4A2F1B),
                  ),
            ),
            const SizedBox(height: 14),
            ClipRRect(
              borderRadius: BorderRadius.circular(999),
              child: LinearProgressIndicator(
                minHeight: 10,
                value: isEmpty ? 0 : 0.78,
                backgroundColor: const Color(0xFFF0E7D8),
                valueColor: const AlwaysStoppedAnimation<Color>(
                  Color(0xFFC78933),
                ),
              ),
            ),
            const SizedBox(height: 14),
            Text(
              isEmpty
                  ? 'No bills due this week'
                  : '$dueBillCount bills due this week',
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    fontSize: 17,
                    fontWeight: FontWeight.w500,
                    color: const Color(0xFF33261C),
                  ),
            ),
            const SizedBox(height: 6),
            Row(
              children: <Widget>[
                Container(
                  width: 22,
                  height: 22,
                  decoration: const BoxDecoration(
                    color: Color(0xFFF3DFBA),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(
                    Icons.check_rounded,
                    size: 14,
                    color: Color(0xFF9C6C26),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    isEmpty
                        ? 'Start by adding your first bill'
                        : 'You\'re on track',
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(
                          fontWeight: FontWeight.w500,
                          color: const Color(0xFF32261D),
                        ),
                  ),
                ),
                const _DashboardStatusPill(label: 'on track'),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _DashboardStatusPill extends StatelessWidget {
  const _DashboardStatusPill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
      decoration: BoxDecoration(
        color: const Color(0xFFF3E4C8),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelLarge?.copyWith(
              fontWeight: FontWeight.w500,
              color: const Color(0xFF7C5B25),
            ),
      ),
    );
  }
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
    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            title,
            style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                  fontSize: 21,
                  fontWeight: FontWeight.w500,
                  color: const Color(0xFF463020),
                ),
          ),
        ),
        if (actionLabel != null)
          Text(
            actionLabel!,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  fontSize: 16,
                  fontWeight: FontWeight.w500,
                  color: const Color(0xFFB48642),
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
    return Container(
      decoration: BoxDecoration(
        color: PayaboColors.white,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: const Color(0xFFE7DDCF)),
        boxShadow: PayaboShadows.soft,
      ),
      child: Column(
        children: items
            .asMap()
            .entries
            .map(
              (entry) => _UpcomingBillRow(
                item: entry.value,
                showDivider: entry.key != items.length - 1,
              ),
            )
            .toList(growable: false),
      ),
    );
  }
}

class _UpcomingBillRow extends StatelessWidget {
  const _UpcomingBillRow({
    required this.item,
    required this.showDivider,
  });

  final DashboardUpcomingBill item;
  final bool showDivider;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 14),
      decoration: showDivider
          ? const BoxDecoration(
              border: Border(
                bottom: BorderSide(color: Color(0xFFF0E5D7)),
              ),
            )
          : null,
      child: Row(
        children: <Widget>[
          Container(
            width: 42,
            height: 42,
            decoration: BoxDecoration(
              color: const Color(0xFFFBF1E2),
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Icon(
              Icons.description_outlined,
              color: Color(0xFFC79448),
              size: 22,
            ),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  item.biller,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontSize: 17,
                        fontWeight: FontWeight.w600,
                        color: const Color(0xFF2B241E),
                      ),
                ),
                const SizedBox(height: 2),
                Text(
                  item.dueDateLabel,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        fontSize: 14,
                        color: const Color(0xFF948576),
                      ),
                ),
              ],
            ),
          ),
          Text(
            item.amountLabel,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontSize: 16,
                  fontWeight: FontWeight.w700,
                  color: const Color(0xFF4A3219),
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
        color: PayaboColors.white,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: const Color(0xFFE7DDCF)),
        boxShadow: PayaboShadows.soft,
      ),
      padding: const EdgeInsets.all(20),
      child: Row(
        children: <Widget>[
          Container(
            width: 42,
            height: 42,
            decoration: BoxDecoration(
              color: const Color(0xFFFBF1E2),
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Icon(
              Icons.receipt_long_outlined,
              color: Color(0xFFC79448),
            ),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Text(
              'No upcoming bills yet. Add a bill to start tracking due dates.',
              style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                    color: const Color(0xFF4A3524),
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
    final String dueText = isEmpty
        ? 'No bills due this week'
        : upcomingBillCount == 1
            ? '1 bill due this week'
            : '$upcomingBillCount bills due this week';

    return Container(
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: <Color>[Color(0xFFFFAE58), Color(0xFFF37920)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: const BorderRadius.all(Radius.circular(28)),
        border: Border.all(color: const Color(0xFFF29D49)),
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
                    color: PayaboColors.accentBrown,
                    fontWeight: FontWeight.w500,
                  ),
            ),
            const SizedBox(height: PayaboSpacing.xs),
            FittedBox(
              fit: BoxFit.scaleDown,
              alignment: Alignment.centerLeft,
              child: Text(
                isEmpty ? '£0.00' : '£1,285.00',
                style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                      color: const Color(0xFF4F220F),
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
                    color: PayaboColors.accentBrown,
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
                  decoration: const BoxDecoration(
                    color: Color(0xFF7C320E),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(
                    Icons.check,
                    color: PayaboColors.white,
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
                          color: PayaboColors.accentBrown,
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
                    color: PayaboColors.accentBrown,
                  ),
            ),
          ),
          if (actionLabel != null)
            TextButton(
              onPressed: onActionTap,
              child: Text(
                actionLabel!,
                style: Theme.of(context).textTheme.labelLarge?.copyWith(
                      color: PayaboColors.primary,
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
    return Padding(
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Container(
        padding: const EdgeInsets.all(PayaboSpacing.x3),
        decoration: BoxDecoration(
          color: const Color(0xFFFFFBF8),
          borderRadius: PayaboRadii.radiusLg,
          border: Border.all(color: const Color(0xFFF1DEC9)),
          boxShadow: PayaboShadows.soft,
        ),
        child: Column(
          children: <Widget>[
            Icon(icon, size: 56, color: PayaboColors.primary),
            const SizedBox(height: PayaboSpacing.md),
            Text(
              message,
              textAlign: TextAlign.center,
              style: Theme.of(context)
                  .textTheme
                  .titleSmall
                  ?.copyWith(color: PayaboColors.accentBrownMuted),
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

class _OrganisationCarousel extends StatelessWidget {
  const _OrganisationCarousel();

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 230,
      child: PageView(
        controller: PageController(viewportFraction: 0.88),
        children: const <Widget>[
          _OrganisationCard(name: 'Volunteer of the World', sponsored: true),
          _OrganisationCard(name: 'Organisation name'),
          _OrganisationCard(name: 'Organisation name'),
        ],
      ),
    );
  }
}

class _OrganisationCard extends StatelessWidget {
  const _OrganisationCard({
    required this.name,
    this.sponsored = false,
  });

  final String name;
  final bool sponsored;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(PayaboSpacing.xl, PayaboSpacing.lg,
          PayaboSpacing.sm, PayaboSpacing.x2),
      child: PayaboCard(
        padding: EdgeInsets.zero,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Expanded(
              child: Container(
                decoration: const BoxDecoration(
                  borderRadius: BorderRadius.only(
                    topLeft: Radius.circular(PayaboRadii.sm),
                    topRight: Radius.circular(PayaboRadii.sm),
                  ),
                  gradient: LinearGradient(
                    colors: <Color>[Color(0xFFFFC48F), Color(0xFFF37920)],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                ),
                child: Stack(
                  children: <Widget>[
                    if (sponsored)
                      Positioned(
                        left: 12,
                        top: 12,
                        child: Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 10, vertical: 4),
                          decoration: BoxDecoration(
                            color: PayaboColors.info,
                            borderRadius: BorderRadius.circular(4),
                          ),
                          child: Text(
                            'SPONSORED',
                            style:
                                Theme.of(context).textTheme.bodySmall?.copyWith(
                                      color: PayaboColors.white,
                                      fontWeight: FontWeight.w700,
                                      fontSize: 10,
                                    ),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(PayaboSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(name, style: Theme.of(context).textTheme.titleSmall),
                  const SizedBox(height: 4),
                  Text('Line for extra info',
                      style: Theme.of(context).textTheme.bodySmall),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _BillList extends StatelessWidget {
  const _BillList({required this.items});

  final List<DashboardUpcomingBill> items;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xl),
      child: PayaboCard(
        backgroundColor: const Color(0xFFFFFBF8),
        padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.lg),
        child: Column(
          children: items
              .map(
                (bill) => _ProductRow(
                  title: bill.biller,
                  subtitle: bill.dueDateLabel,
                  amountLabel: bill.amountLabel,
                ),
              )
              .toList(growable: false),
        ),
      ),
    );
  }
}

class _TransactionList extends StatelessWidget {
  const _TransactionList({required this.items});

  final List<DashboardTransaction> items;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xl),
      child: PayaboCard(
        backgroundColor: const Color(0xFFFFFBF8),
        padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.lg),
        child: Column(
          children: items
              .map(
                (transaction) => _ProductRow(
                  title: transaction.title,
                  subtitle: transaction.status,
                  amountLabel: transaction.amountLabel,
                ),
              )
              .toList(growable: false),
        ),
      ),
    );
  }
}

class _ProductRow extends StatelessWidget {
  const _ProductRow({
    required this.title,
    required this.subtitle,
    required this.amountLabel,
  });

  final String title;
  final String subtitle;
  final String amountLabel;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.md),
      decoration: const BoxDecoration(
        border:
            Border(bottom: BorderSide(color: PayaboColors.border, width: 1)),
      ),
      child: Row(
        children: <Widget>[
          Container(
            width: 42,
            height: 42,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(8),
              color: const Color(0xFFFFF3E8),
            ),
            child: const Icon(Icons.description_outlined,
                color: PayaboColors.primary),
          ),
          const SizedBox(width: PayaboSpacing.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(title, style: Theme.of(context).textTheme.titleSmall),
                const SizedBox(height: 2),
                Text(subtitle, style: Theme.of(context).textTheme.bodySmall),
              ],
            ),
          ),
          Text(
            amountLabel,
            style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                  fontWeight: FontWeight.w600,
                  color: PayaboColors.accentBrown,
                ),
          ),
        ],
      ),
    );
  }
}

class _BudgetCarousel extends StatelessWidget {
  const _BudgetCarousel();

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 196,
      child: PageView(
        controller: PageController(viewportFraction: 0.88),
        children: const <Widget>[
          _BudgetCard(month: 'July 2022', progress: 0.25),
          _BudgetCard(month: 'June 2022', progress: 0.42),
          _BudgetCard(month: 'May 2022', progress: 0.35),
        ],
      ),
    );
  }
}

class _BudgetCard extends StatelessWidget {
  const _BudgetCard({
    required this.month,
    required this.progress,
  });

  final String month;
  final double progress;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(PayaboSpacing.xl, PayaboSpacing.lg,
          PayaboSpacing.sm, PayaboSpacing.x2),
      child: PayaboCard(
        backgroundColor: const Color(0xFFFFFBF8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(month, style: Theme.of(context).textTheme.titleSmall),
            const SizedBox(height: PayaboSpacing.md),
            ClipRRect(
              borderRadius: BorderRadius.circular(5),
              child: LinearProgressIndicator(
                minHeight: 10,
                value: progress,
                backgroundColor: PayaboColors.border,
                valueColor:
                    const AlwaysStoppedAnimation<Color>(PayaboColors.primary),
              ),
            ),
            const SizedBox(height: PayaboSpacing.md),
            Row(
              children: <Widget>[
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text('SPENT',
                          style: Theme.of(context).textTheme.bodySmall),
                      const SizedBox(height: 2),
                      Text('N 0,000.00',
                          style: Theme.of(context).textTheme.titleSmall),
                    ],
                  ),
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: <Widget>[
                    Text('BUDGET',
                        style: Theme.of(context).textTheme.bodySmall),
                    const SizedBox(height: 2),
                    Text('N 0,000.00',
                        style: Theme.of(context).textTheme.titleSmall),
                  ],
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _AssistRequestCarousel extends StatelessWidget {
  const _AssistRequestCarousel();

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 186,
      child: PageView(
        controller: PageController(viewportFraction: 0.88),
        children: const <Widget>[
          _AssistRequestCard(
              friendName: 'Alicia Keys', amountLabel: 'N 0,000.00'),
          _AssistRequestCard(
              friendName: 'Friend Name', amountLabel: 'N 0,000.00'),
          _AssistRequestCard(
              friendName: 'Friend Name', amountLabel: 'N 0,000.00'),
        ],
      ),
    );
  }
}

class _AssistRequestCard extends StatelessWidget {
  const _AssistRequestCard({
    required this.friendName,
    required this.amountLabel,
  });

  final String friendName;
  final String amountLabel;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(PayaboSpacing.xl, PayaboSpacing.lg,
          PayaboSpacing.sm, PayaboSpacing.x2),
      child: PayaboCard(
        backgroundColor: const Color(0xFFFFFBF8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                const CircleAvatar(
                  radius: 18,
                  backgroundColor: PayaboColors.background,
                  child:
                      Icon(Icons.person_outline, color: PayaboColors.primary),
                ),
                const SizedBox(width: PayaboSpacing.md),
                Text(friendName, style: Theme.of(context).textTheme.titleSmall),
              ],
            ),
            const SizedBox(height: PayaboSpacing.lg),
            Row(
              children: <Widget>[
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text('Bill name',
                          style: Theme.of(context).textTheme.titleSmall),
                      Text('Line for extra info',
                          style: Theme.of(context).textTheme.bodySmall),
                    ],
                  ),
                ),
                Text(amountLabel, style: Theme.of(context).textTheme.bodyLarge),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
