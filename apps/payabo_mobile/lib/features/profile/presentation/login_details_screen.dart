import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_card.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

class ProfileLoginDetailsScreen extends ConsumerWidget {
  const ProfileLoginDetailsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(profileControllerProvider);

    return ProfileScaffold(
      title: 'Login details',
      backRoute: '/profile',
      child: Column(
        children: <Widget>[
          PayaboListRow(
            title: 'Email',
            subtitle: state.email,
            onTap: () => context.go('/profile/login-details/email'),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Password',
            subtitle: '........',
            onTap: () => context.go('/profile/login-details/password'),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboCard(
            child: Row(
              children: <Widget>[
                const Expanded(child: Text('Touch ID')),
                Switch.adaptive(
                  value: state.touchIdEnabled,
                  onChanged: (value) => ref
                      .read(profileControllerProvider.notifier)
                      .setTouchId(value),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
