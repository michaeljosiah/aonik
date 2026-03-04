import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_text_field.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

class NotificationsEmailScreen extends ConsumerStatefulWidget {
  const NotificationsEmailScreen({super.key});

  @override
  ConsumerState<NotificationsEmailScreen> createState() =>
      _NotificationsEmailScreenState();
}

class _NotificationsEmailScreenState
    extends ConsumerState<NotificationsEmailScreen> {
  late final TextEditingController _emailController;

  @override
  void initState() {
    super.initState();
    _emailController = TextEditingController(
        text: ref.read(profileControllerProvider).notificationsEmail);
  }

  @override
  void dispose() {
    _emailController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ProfileScaffold(
      title: 'Email for notifications',
      backRoute: '/profile/notifications',
      footer: PayaboButton(
        label: 'Save changes',
        onPressed: () {
          ref
              .read(profileControllerProvider.notifier)
              .setNotificationsEmail(_emailController.text);
          context.go('/profile/notifications');
        },
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            'You can set a different email to receive your notifications, this will not affect your login details.',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboTextField(
            label: 'Email address for notifications',
            variant: PayaboInputVariant.floating,
            controller: _emailController,
            keyboardType: TextInputType.emailAddress,
          ),
        ],
      ),
    );
  }
}
