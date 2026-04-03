import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_warm_scaffold.dart';
import '../notification_data.dart';

// ─────────────────────────────────────────────────────────
//  Notification data provider (backed by NotificationRepository)
// ─────────────────────────────────────────────────────────

final _notificationSectionsFutureProvider =
    FutureProvider<List<NotificationSection>>((Ref ref) async {
  ref.watch(demoDataModeProvider);
  final repository = ref.watch(notificationRepositoryProvider);
  return repository.getSections();
});

class NotificationCenterScreen extends ConsumerWidget {
  const NotificationCenterScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.colors;
    final sections = ref.watch(_notificationSectionsFutureProvider).when(
          data: (List<NotificationSection> data) => data,
          loading: () => const <NotificationSection>[],
          error: (_, __) => const <NotificationSection>[],
        );

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
                  onPressed: sections.isEmpty
                      ? null
                      : () {
                          ScaffoldMessenger.of(context)
                            ..hideCurrentSnackBar()
                            ..showSnackBar(
                              const SnackBar(
                                content:
                                    Text('Mark all read coming soon.'),
                              ),
                            );
                        },
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
                          (NotificationSection section) =>
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

  final NotificationSection section;

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
            (NotificationItem item) => Padding(
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

  final NotificationItem item;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final Color iconColor = Color(item.iconColorValue);
    final IconData icon = IconData(
      item.iconCodePoint,
      fontFamily: item.iconFontFamily,
    );

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
              color: iconColor.withValues(alpha: 0.14),
              shape: BoxShape.circle,
            ),
            child: Icon(icon, color: iconColor, size: 20),
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
