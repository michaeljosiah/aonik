import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

class ProfilePersonalDetailsScreen extends ConsumerWidget {
  const ProfilePersonalDetailsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(profilePersonalDetailsProvider);

    return ProfileScaffold(
      title: 'Personal details',
      backRoute: '/profile',
      child: Column(
        children: <Widget>[
          PayaboListRow(
            title: 'Name',
            subtitle: state.displayName,
            onTap: () => context.go('/profile/personal-details/name'),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Mobile number',
            subtitle: state.phone,
            onTap: () => context.go('/profile/personal-details/contact'),
          ),
        ],
      ),
    );
  }
}
