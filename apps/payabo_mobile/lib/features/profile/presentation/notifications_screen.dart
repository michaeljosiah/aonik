import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_card.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

class NotificationsScreen extends ConsumerStatefulWidget {
  const NotificationsScreen({super.key});

  @override
  ConsumerState<NotificationsScreen> createState() =>
      _NotificationsScreenState();
}

class _NotificationsScreenState extends ConsumerState<NotificationsScreen> {
  int _tabIndex = 0;

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(profileControllerProvider);

    return ProfileScaffold(
      title: 'Notifications',
      backRoute: '/profile',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            'Choose what activities you want to be notified. Keep in mind, all notifications still will appear on your app inbox.',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const SizedBox(height: PayaboSpacing.md),
          Row(
            children: <Widget>[
              Expanded(
                child: _TabButton(
                  label: 'PUSH',
                  selected: _tabIndex == 0,
                  onTap: () => setState(() => _tabIndex = 0),
                ),
              ),
              const SizedBox(width: PayaboSpacing.sm),
              Expanded(
                child: _TabButton(
                  label: 'EMAIL',
                  selected: _tabIndex == 1,
                  onTap: () => setState(() => _tabIndex = 1),
                ),
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.md),
          if (_tabIndex == 0) ...<Widget>[
            _ToggleCard(
              label: 'New bills',
              value: state.newBillsPush,
              onChanged: (v) => ref
                  .read(profileControllerProvider.notifier)
                  .setPushToggle(newBills: v),
            ),
            _ToggleCard(
              label: 'Bills updates',
              value: state.billUpdatesPush,
              onChanged: (v) => ref
                  .read(profileControllerProvider.notifier)
                  .setPushToggle(billUpdates: v),
            ),
            _ToggleCard(
              label: 'Bill pay assist requests',
              value: state.billAssistPush,
              onChanged: (v) => ref
                  .read(profileControllerProvider.notifier)
                  .setPushToggle(billAssist: v),
            ),
            _ToggleCard(
              label: 'Bill MBA messages',
              value: state.mbaMessagesPush,
              onChanged: (v) => ref
                  .read(profileControllerProvider.notifier)
                  .setPushToggle(mbaMessages: v),
            ),
            _ToggleCard(
              label: 'Organisations messages',
              value: state.orgMessagesPush,
              onChanged: (v) => ref
                  .read(profileControllerProvider.notifier)
                  .setPushToggle(orgMessages: v),
            ),
            _ToggleCard(
              label: 'Friends messages',
              value: state.friendsMessagesPush,
              onChanged: (v) => ref
                  .read(profileControllerProvider.notifier)
                  .setPushToggle(friendsMessages: v),
            ),
          ] else ...<Widget>[
            InkWell(
              onTap: () => context.go('/profile/notifications/email'),
              child: PayaboCard(
                child: Row(
                  children: <Widget>[
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          Text('Email for notifications',
                              style: Theme.of(context).textTheme.titleSmall),
                          Text(state.notificationsEmail,
                              style: Theme.of(context).textTheme.bodySmall),
                        ],
                      ),
                    ),
                    const Icon(Icons.chevron_right, color: PayaboColors.muted),
                  ],
                ),
              ),
            ),
            const SizedBox(height: PayaboSpacing.sm),
            _ToggleCard(
              label: 'New bills',
              value: state.newBillsEmail,
              onChanged: (v) => ref
                  .read(profileControllerProvider.notifier)
                  .setEmailNotificationToggle(newBills: v),
            ),
            _ToggleCard(
              label: 'Bills updates',
              value: state.billUpdatesEmail,
              onChanged: (v) => ref
                  .read(profileControllerProvider.notifier)
                  .setEmailNotificationToggle(billUpdates: v),
            ),
            _ToggleCard(
              label: 'Bill pay assist requests',
              value: state.billAssistEmail,
              onChanged: (v) => ref
                  .read(profileControllerProvider.notifier)
                  .setEmailNotificationToggle(billAssist: v),
            ),
            _ToggleCard(
              label: 'Bill MBA messages',
              value: state.mbaMessagesEmail,
              onChanged: (v) => ref
                  .read(profileControllerProvider.notifier)
                  .setEmailNotificationToggle(mbaMessages: v),
            ),
            _ToggleCard(
              label: 'Organisations messages',
              value: state.orgMessagesEmail,
              onChanged: (v) => ref
                  .read(profileControllerProvider.notifier)
                  .setEmailNotificationToggle(orgMessages: v),
            ),
          ],
        ],
      ),
    );
  }
}

class _ToggleCard extends StatelessWidget {
  const _ToggleCard(
      {required this.label, required this.value, required this.onChanged});

  final String label;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: PayaboSpacing.sm),
      child: PayaboCard(
        child: Row(
          children: <Widget>[
            Expanded(child: Text(label)),
            Switch.adaptive(value: value, onChanged: onChanged),
          ],
        ),
      ),
    );
  }
}

class _TabButton extends StatelessWidget {
  const _TabButton({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(8),
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.sm),
        decoration: BoxDecoration(
          color: selected ? PayaboColors.primary : PayaboColors.background,
          borderRadius: BorderRadius.circular(8),
        ),
        child: Text(
          label,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.labelLarge?.copyWith(
                color: selected ? PayaboColors.white : PayaboColors.ink,
              ),
        ),
      ),
    );
  }
}
