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

  void _showError(String message) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(message)));
  }

  Future<void> _togglePush({
    bool? newBills,
    bool? billUpdates,
    bool? billAssist,
    bool? mbaMessages,
    bool? orgMessages,
    bool? friendsMessages,
  }) async {
    try {
      await ref.read(profileControllerProvider.notifier).setPushToggle(
            newBills: newBills,
            billUpdates: billUpdates,
            billAssist: billAssist,
            mbaMessages: mbaMessages,
            orgMessages: orgMessages,
            friendsMessages: friendsMessages,
          );
    } catch (_) {
      if (mounted) {
        _showError('Unable to update notification settings right now.');
      }
    }
  }

  Future<void> _toggleEmail({
    bool? newBills,
    bool? billUpdates,
    bool? billAssist,
    bool? mbaMessages,
    bool? orgMessages,
  }) async {
    try {
      await ref
          .read(profileControllerProvider.notifier)
          .setEmailNotificationToggle(
            newBills: newBills,
            billUpdates: billUpdates,
            billAssist: billAssist,
            mbaMessages: mbaMessages,
            orgMessages: orgMessages,
          );
    } catch (_) {
      if (mounted) {
        _showError('Unable to update notification settings right now.');
      }
    }
  }

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
                  isFirst: true,
                ),
              ),
              Expanded(
                child: _TabButton(
                  label: 'EMAIL',
                  selected: _tabIndex == 1,
                  onTap: () => setState(() => _tabIndex = 1),
                  isLast: true,
                ),
              ),
            ],
          ),
          const SizedBox(height: PayaboSpacing.md),
          if (_tabIndex == 0) ...<Widget>[
            _ToggleCard(
              label: 'New bills',
              value: state.newBillsPush,
              onChanged: (v) => _togglePush(newBills: v),
            ),
            _ToggleCard(
              label: 'Bills updates',
              value: state.billUpdatesPush,
              onChanged: (v) => _togglePush(billUpdates: v),
            ),
            _ToggleCard(
              label: 'Bill pay assist requests',
              value: state.billAssistPush,
              onChanged: (v) => _togglePush(billAssist: v),
            ),
            _ToggleCard(
              label: 'Bill MBA messages',
              value: state.mbaMessagesPush,
              onChanged: (v) => _togglePush(mbaMessages: v),
            ),
            _ToggleCard(
              label: 'Organisations messages',
              value: state.orgMessagesPush,
              onChanged: (v) => _togglePush(orgMessages: v),
            ),
            _ToggleCard(
              label: 'Friends messages',
              value: state.friendsMessagesPush,
              onChanged: (v) => _togglePush(friendsMessages: v),
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
              onChanged: (v) => _toggleEmail(newBills: v),
            ),
            _ToggleCard(
              label: 'Bills updates',
              value: state.billUpdatesEmail,
              onChanged: (v) => _toggleEmail(billUpdates: v),
            ),
            _ToggleCard(
              label: 'Bill pay assist requests',
              value: state.billAssistEmail,
              onChanged: (v) => _toggleEmail(billAssist: v),
            ),
            _ToggleCard(
              label: 'Bill MBA messages',
              value: state.mbaMessagesEmail,
              onChanged: (v) => _toggleEmail(mbaMessages: v),
            ),
            _ToggleCard(
              label: 'Organisations messages',
              value: state.orgMessagesEmail,
              onChanged: (v) => _toggleEmail(orgMessages: v),
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
            SizedBox(
              width: 60,
              height: 30,
              child: FittedBox(
                fit: BoxFit.fill,
                child: Switch.adaptive(
                  value: value,
                  onChanged: onChanged,
                  activeThumbColor: PayaboColors.white,
                  activeTrackColor: PayaboColors.success,
                  inactiveThumbColor: PayaboColors.white,
                  inactiveTrackColor: PayaboColors.background,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Segmented tab control with squared inner edges matching the HTML
/// `.nav-tabs` reference: orange active background, gray inactive.
class _TabButton extends StatelessWidget {
  const _TabButton({
    required this.label,
    required this.selected,
    required this.onTap,
    this.isFirst = false,
    this.isLast = false,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;
  final bool isFirst;
  final bool isLast;

  @override
  Widget build(BuildContext context) {
    final borderRadius = BorderRadius.only(
      topLeft: isFirst ? const Radius.circular(6) : Radius.zero,
      bottomLeft: isFirst ? const Radius.circular(6) : Radius.zero,
      topRight: isLast ? const Radius.circular(6) : Radius.zero,
      bottomRight: isLast ? const Radius.circular(6) : Radius.zero,
    );

    return InkWell(
      onTap: onTap,
      borderRadius: borderRadius,
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 10),
        decoration: BoxDecoration(
          color: selected ? PayaboColors.primary : PayaboColors.background,
          borderRadius: borderRadius,
        ),
        child: Text(
          label,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.labelLarge?.copyWith(
                color: selected ? PayaboColors.white : PayaboColors.ink,
                fontWeight: FontWeight.w700,
              ),
        ),
      ),
    );
  }
}
