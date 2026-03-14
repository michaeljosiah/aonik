import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';

const List<_NotificationSection> _notificationSections = <_NotificationSection>[
  _NotificationSection(
    title: 'Today',
    items: <_NotificationItem>[
      _NotificationItem(
        title: 'Electricity bill reminder',
        message: 'ECG Power is due tomorrow. Pay now to avoid late fees.',
        timeLabel: '09:42 AM',
        icon: Icons.bolt_rounded,
        iconColor: Color(0xFFB35E17),
        unread: true,
      ),
      _NotificationItem(
        title: 'Spend alert',
        message: 'Dining spend is 18% above your monthly pace.',
        timeLabel: '07:15 AM',
        icon: Icons.show_chart_rounded,
        iconColor: Color(0xFF355F3E),
        unread: true,
      ),
    ],
  ),
  _NotificationSection(
    title: 'Yesterday',
    items: <_NotificationItem>[
      _NotificationItem(
        title: 'Transfer completed',
        message: 'Your GHS 300 transfer to Ama Boafo was successful.',
        timeLabel: '08:03 PM',
        icon: Icons.compare_arrows_rounded,
        iconColor: Color(0xFF31518A),
        unread: false,
      ),
      _NotificationItem(
        title: 'Budget milestone',
        message: 'You stayed under groceries budget for 3 weeks straight.',
        timeLabel: '11:20 AM',
        icon: Icons.emoji_events_rounded,
        iconColor: Color(0xFF8A6325),
        unread: false,
      ),
    ],
  ),
  _NotificationSection(
    title: '10 Mar 2026',
    items: <_NotificationItem>[
      _NotificationItem(
        title: 'New insight available',
        message: 'Payabo found two subscriptions you may want to review.',
        timeLabel: '04:45 PM',
        icon: Icons.lightbulb_rounded,
        iconColor: Color(0xFF784A34),
        unread: false,
      ),
    ],
  ),
];

class NotificationCenterScreen extends ConsumerWidget {
  const NotificationCenterScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.colors;
    final bool isFreshDemo =
        ref.watch(demoDataModeProvider) == DemoDataMode.fresh;
    final sections =
        isFreshDemo ? const <_NotificationSection>[] : _notificationSections;

    return PayaboWarmScaffold(
      body: Column(
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.fromLTRB(
              PayaboSpacing.xl,
              PayaboSpacing.md,
              PayaboSpacing.xl,
              PayaboSpacing.lg,
            ),
            child: Row(
              children: <Widget>[
                _HeaderIconButton(
                  icon: Icons.arrow_back_ios_new_rounded,
                  onTap: () {
                    if (context.canPop()) {
                      context.pop();
                      return;
                    }

                    context.go('/dashboard');
                  },
                ),
                const SizedBox(width: PayaboSpacing.md),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        'Notifications',
                        style: Theme.of(context)
                            .textTheme
                            .headlineMedium
                            ?.copyWith(
                              color: c.headerTitle,
                              fontWeight: FontWeight.w700,
                            ),
                      ),
                      Text(
                        'Your latest reminders and updates.',
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              color: c.headerSubtitle,
                            ),
                      ),
                    ],
                  ),
                ),
                TextButton(
                  onPressed: sections.isEmpty ? null : () {},
                  child: const Text('Mark all read'),
                ),
              ],
            ),
          ),
          Expanded(
            child: sections.isEmpty
                ? const Padding(
                    padding: EdgeInsets.fromLTRB(
                      PayaboSpacing.xl,
                      0,
                      PayaboSpacing.xl,
                      PayaboSpacing.xl,
                    ),
                    child: _NotificationEmptyState(),
                  )
                : ListView(
                    padding: const EdgeInsets.fromLTRB(
                      PayaboSpacing.xl,
                      0,
                      PayaboSpacing.xl,
                      PayaboSpacing.xl,
                    ),
                    children: sections
                        .map(
                          (_NotificationSection section) =>
                              _NotificationSectionBlock(
                            section: section,
                          ),
                        )
                        .toList(growable: false),
                  ),
          ),
        ],
      ),
    );
  }
}

class _NotificationEmptyState extends StatelessWidget {
  const _NotificationEmptyState();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: c.surfaceBase.withValues(alpha: 0.86),
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: c.borderWarm),
        boxShadow: PayaboShadows.soft,
      ),
      padding: const EdgeInsets.all(PayaboSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            width: 48,
            height: 48,
            decoration: BoxDecoration(
              color: c.primary.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(16),
            ),
            child: Icon(
              Icons.notifications_none_rounded,
              color: c.primary,
              size: 24,
            ),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          Text(
            'No notifications yet',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  color: c.chatTextPrimary,
                  fontWeight: FontWeight.w700,
                ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          Text(
            'Fresh demo mode clears reminders, alerts, and update cards so this inbox starts empty.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: c.chatTextSecondary,
                  height: 1.45,
                ),
          ),
        ],
      ),
    );
  }
}

class _NotificationSectionBlock extends StatelessWidget {
  const _NotificationSectionBlock({required this.section});

  final _NotificationSection section;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Padding(
      padding: const EdgeInsets.only(bottom: PayaboSpacing.xl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            section.title,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  color: c.chatTextSecondary,
                  fontWeight: FontWeight.w700,
                ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          ...section.items.map(
            (_NotificationItem item) => Padding(
              padding: const EdgeInsets.only(bottom: PayaboSpacing.sm),
              child: _NotificationCard(item: item),
            ),
          ),
        ],
      ),
    );
  }
}

class _NotificationCard extends StatelessWidget {
  const _NotificationCard({required this.item});

  final _NotificationItem item;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Container(
      decoration: BoxDecoration(
        color: c.surfaceBase.withValues(alpha: 0.86),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: c.borderWarm),
        boxShadow: PayaboShadows.soft,
      ),
      padding: const EdgeInsets.all(PayaboSpacing.lg),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            width: 38,
            height: 38,
            decoration: BoxDecoration(
              color: item.iconColor.withValues(alpha: 0.14),
              shape: BoxShape.circle,
            ),
            child: Icon(item.icon, color: item.iconColor, size: 20),
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
                        item.title,
                        style:
                            Theme.of(context).textTheme.titleMedium?.copyWith(
                                  color: c.chatTextPrimary,
                                  fontWeight: item.unread
                                      ? FontWeight.w700
                                      : FontWeight.w600,
                                ),
                      ),
                    ),
                    if (item.unread)
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
                  item.message,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: c.chatTextSecondary,
                        height: 1.35,
                      ),
                ),
                const SizedBox(height: 6),
                Text(
                  item.timeLabel,
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(
                        color: c.muted,
                        fontWeight: FontWeight.w600,
                      ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _HeaderIconButton extends StatelessWidget {
  const _HeaderIconButton({
    required this.icon,
    required this.onTap,
  });

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return Ink(
      width: 42,
      height: 42,
      decoration: BoxDecoration(
        color: c.surfaceBase.withValues(alpha: 0.8),
        shape: BoxShape.circle,
        border: Border.all(color: c.borderWarm),
      ),
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: Icon(icon, size: 18, color: c.headerIconAccent),
      ),
    );
  }
}

class _NotificationSection {
  const _NotificationSection({
    required this.title,
    required this.items,
  });

  final String title;
  final List<_NotificationItem> items;
}

class _NotificationItem {
  const _NotificationItem({
    required this.title,
    required this.message,
    required this.timeLabel,
    required this.icon,
    required this.iconColor,
    required this.unread,
  });

  final String title;
  final String message;
  final String timeLabel;
  final IconData icon;
  final Color iconColor;
  final bool unread;
}
