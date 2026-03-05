import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/auth/auth_controller.dart';
import '../../../data/api/api_exception.dart';
import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_card.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import '../../../shared/widgets/payabo_modal_sheet.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

class ProfileScreen extends ConsumerStatefulWidget {
  const ProfileScreen({super.key});

  @override
  ConsumerState<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends ConsumerState<ProfileScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      try {
        await ref.read(profileControllerProvider.notifier).ensureLoaded();
      } catch (error) {
        if (!mounted) {
          return;
        }

        final message = error is ApiException
            ? error.message
            : 'Unable to load your profile right now.';

        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(message)),
        );
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(profileControllerProvider);

    return ProfileScaffold(
      title: state.displayName,
      backRoute: '/dashboard',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Center(
            child: Column(
              children: <Widget>[
                const CircleAvatar(
                  radius: 48,
                  backgroundColor: PayaboColors.background,
                  child: Icon(Icons.person_outline, size: 40),
                ),
                const SizedBox(height: PayaboSpacing.sm),
                TextButton(
                  onPressed: _openPhotoPicker,
                  child: Text(state.photoLabel),
                ),
              ],
            ),
          ),
          const SizedBox(height: PayaboSpacing.md),
          PayaboListRow(
            title: 'My personal details',
            subtitle: 'Edit your name, mobile number ...',
            leading: const Icon(Icons.person_outline),
            onTap: () => context.go('/profile/personal-details'),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'My login details',
            subtitle: 'Edit your email, password ...',
            leading: const Icon(Icons.lock_outline),
            onTap: () => context.go('/profile/login-details'),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Notification settings',
            subtitle: 'Manage your notifications',
            leading: const Icon(Icons.notifications_outlined),
            onTap: () => context.go('/profile/notifications'),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Marketing preferences',
            subtitle: 'Manage marketing communication',
            leading: const Icon(Icons.campaign_outlined),
            onTap: () => context.go('/profile/marketing'),
          ),
          const SizedBox(height: PayaboSpacing.lg),
          PayaboCard(
            child: Column(
              children: <Widget>[
                TextButton(
                  onPressed: () async {
                    await ref.read(authControllerProvider.notifier).signOut();

                    if (!context.mounted) {
                      return;
                    }

                    context.go('/intro');
                  },
                  child: const Text(
                    'LOG OUT',
                    style: TextStyle(fontWeight: FontWeight.w700),
                  ),
                ),
                const SizedBox(height: 4),
                const Text(
                  'Version 22.0001.01',
                  style: TextStyle(color: PayaboColors.muted),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _openPhotoPicker() async {
    await showPayaboModalSheet<void>(
      context: context,
      title: 'Photo',
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          ListTile(
            title: const Text('TAKE PHOTO'),
            onTap: () {
              ref
                  .read(profileControllerProvider.notifier)
                  .setPhotoLabel('Photo selected');
              Navigator.of(context).pop();
            },
          ),
          ListTile(
            title: const Text('CHOOSE PHOTO'),
            onTap: () {
              Navigator.of(context).pop();
              context.go('/profile/photo');
            },
          ),
          ListTile(
            title: const Text('CANCEL'),
            onTap: () => Navigator.of(context).pop(),
          ),
        ],
      ),
    );
  }
}
