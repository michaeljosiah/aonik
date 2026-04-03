import 'dart:developer' as developer;

import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';

import '../../../app/auth/auth_controller.dart';
import '../../../app/demo/demo_data_mode.dart';
import '../../../app/demo/demo_mode.dart';
import '../../../data/api/api_exception.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/theme/theme_mode_provider.dart';
import '../../../shared/widgets/payabo_button.dart';
import '../../../shared/widgets/payabo_list_row.dart';
import '../../../shared/widgets/payabo_modal_sheet.dart';
import '../../../shared/widgets/payabo_profile_avatar.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

class ProfileScreen extends ConsumerStatefulWidget {
  const ProfileScreen({super.key});

  @override
  ConsumerState<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends ConsumerState<ProfileScreen> {
  final ImagePicker _picker = ImagePicker();

  bool get _showCrashlyticsTestAction =>
      kDebugMode && defaultTargetPlatform == TargetPlatform.android;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      try {
        await ref.read(profileDataCoordinatorProvider).ensureLoaded();
      } catch (error, stackTrace) {
        developer.log(
          'Failed to open profile screen.',
          name: 'Payabo.ProfileScreen',
          error: error,
          stackTrace: stackTrace,
        );

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
    final state = ref.watch(profileCoreProvider);
    final demoDataMode = ref.watch(demoDataModeProvider);
    final themeMode = ref.watch(themeModeProvider);
    final isDemo = ref.watch(isDemoProvider);

    return ProfileScaffold(
      title: 'Profile',
      backRoute: '/dashboard',
      child: state.loaded
          ? _buildContent(
              context,
              state,
              showDemoDataPreferences: isDemo,
              demoDataMode: demoDataMode,
              themeMode: themeMode,
            )
          : const Center(
              child: Padding(
                padding: EdgeInsets.only(top: 80),
                child: CircularProgressIndicator(),
              ),
            ),
    );
  }

  Widget _buildContent(
    BuildContext context,
    ProfileCoreState state, {
    required bool showDemoDataPreferences,
    required DemoDataMode demoDataMode,
    required ThemeMode themeMode,
  }) {
    final c = context.colors;
    final isDarkMode = themeMode == ThemeMode.dark;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Center(
          child: Column(
            children: <Widget>[
              PayaboProfileAvatar(
                photoUrl: state.photoUrl,
                showShadow: true,
              ),
              const SizedBox(height: PayaboSpacing.sm),
              GestureDetector(
                onTap: _openPhotoPicker,
                child: Text(
                  state.photoLabel,
                  style: TextStyle(
                    color: c.primary,
                    fontWeight: FontWeight.w700,
                    fontSize: 16,
                  ),
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: PayaboSpacing.md),
        PayaboListRow(
          title: 'My personal details',
          subtitle: 'Edit your name, mobile number ...',
          leading: const _MenuIcon(Icons.person_outline),
          onTap: () => context.go('/profile/personal-details'),
        ),
        const SizedBox(height: PayaboSpacing.sm),
        PayaboListRow(
          title: 'My login details',
          subtitle: 'Edit your email, password ...',
          leading: const _MenuIcon(Icons.lock_outline),
          onTap: () => context.go('/profile/login-details'),
        ),
        const SizedBox(height: PayaboSpacing.sm),
        PayaboListRow(
          title: 'Notification settings',
          subtitle: 'Manage your notifications',
          leading: const _MenuIcon(Icons.notifications_outlined),
          onTap: () => context.go('/profile/notifications'),
        ),
        const SizedBox(height: PayaboSpacing.sm),
        PayaboListRow(
          title: 'Marketing preferences',
          subtitle: 'Manage marketing communication',
          leading: const _MenuIcon(Icons.campaign_outlined),
          onTap: () => context.go('/profile/marketing'),
        ),
        const SizedBox(height: PayaboSpacing.sm),
        PayaboListRow(
          title: 'Community',
          subtitle: 'News, guides and community updates',
          leading: const _MenuIcon(Icons.people_outline),
          onTap: () => context.go('/community'),
        ),
        const SizedBox(height: PayaboSpacing.sm),
        PayaboListRow(
          title: 'Dark theme',
          subtitle: isDarkMode
              ? 'Using the night palette across Payabo'
              : 'Using the warm light palette across Payabo',
          leading: const _MenuIcon(Icons.dark_mode_outlined),
          trailing: Switch.adaptive(
            value: isDarkMode,
            activeThumbColor: c.surfaceBase,
            activeTrackColor: c.primary,
            onChanged: (bool value) {
              ref.read(themeModeProvider.notifier).setMode(
                    value ? ThemeMode.dark : ThemeMode.light,
                  );
            },
          ),
          onTap: () {
            ref.read(themeModeProvider.notifier).setMode(
                  isDarkMode ? ThemeMode.light : ThemeMode.dark,
                );
          },
        ),
        if (showDemoDataPreferences) ...<Widget>[
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Demo data preferences',
            subtitle: demoDataMode.profileMenuSubtitle,
            leading: const _MenuIcon(Icons.storage_outlined),
            onTap: () => context.go('/profile/demo-data'),
          ),
        ],
        if (_showCrashlyticsTestAction) ...<Widget>[
          const SizedBox(height: PayaboSpacing.sm),
          PayaboListRow(
            title: 'Send Crashlytics test event',
            subtitle: 'Record a dev-only non-fatal error in Firebase',
            leading: const _MenuIcon(Icons.bug_report_outlined),
            onTap: _sendCrashlyticsTestEvent,
          ),
        ],
        const SizedBox(height: PayaboSpacing.lg),
        InkWell(
          onTap: () async {
            await ref.read(authControllerProvider.notifier).signOut();

            if (!context.mounted) {
              return;
            }

            context.go('/intro');
          },
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.md),
            child: Center(
              child: Text(
                'LOG OUT',
                style: TextStyle(
                  color: c.primary,
                  fontWeight: FontWeight.w700,
                  fontSize: 16,
                ),
              ),
            ),
          ),
        ),
        const SizedBox(height: PayaboSpacing.xs),
        Center(
          child: Text(
            'Version 1.0.0',
            style: TextStyle(color: c.muted, fontSize: 12),
          ),
        ),
      ],
    );
  }

  Future<void> _openPhotoPicker() async {
    final c = context.colors;
    final state = ref.read(profileCoreProvider);
    final hasPhoto = state.photoUrl != null;

    await showPayaboModalSheet<void>(
      context: context,
      title: 'Photo',
      isDismissible: false,
      enableDrag: false,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          _ModalOption(
            label: 'TAKE PHOTO',
            onTap: () {
              Navigator.of(context).pop();
              _takePhoto();
            },
          ),
          Divider(height: 1, color: c.border),
          _ModalOption(
            label: 'CHOOSE PHOTO',
            onTap: () {
              Navigator.of(context).pop();
              _choosePhoto();
            },
          ),
          if (hasPhoto) ...<Widget>[
            Divider(height: 1, color: c.border),
            _ModalOption(
              label: 'DELETE PHOTO',
              color: c.primary,
              onTap: () async {
                Navigator.of(context).pop();
                try {
                  await ref.read(profileCoreProvider.notifier).deletePhoto();
                } catch (_) {
                  if (mounted) {
                    ScaffoldMessenger.of(context)
                      ..hideCurrentSnackBar()
                      ..showSnackBar(
                        const SnackBar(
                          content: Text('Unable to delete photo right now.'),
                        ),
                      );
                  }
                }
              },
            ),
          ],
          const SizedBox(height: PayaboSpacing.lg),
          PayaboButton(
            label: 'Cancel',
            variant: PayaboButtonVariant.primary,
            onPressed: () => Navigator.of(context).pop(),
          ),
        ],
      ),
    );
  }

  Future<void> _sendCrashlyticsTestEvent() async {
    try {
      await FirebaseCrashlytics.instance.recordError(
        StateError('Payabo Android debug Crashlytics test event'),
        StackTrace.current,
        reason: 'Manual profile menu test event',
        fatal: false,
      );

      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          const SnackBar(
            content: Text('Crashlytics test event sent.'),
          ),
        );
    } catch (_) {
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          const SnackBar(
            content: Text('Unable to send Crashlytics test event.'),
          ),
        );
    }
  }

  Future<void> _takePhoto() async {
    try {
      final XFile? image =
          await _picker.pickImage(source: ImageSource.camera, maxWidth: 800);
      if (image != null && mounted) {
        await ref.read(profileCoreProvider.notifier).uploadPhoto(image.path);
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(
            const SnackBar(content: Text('Unable to capture photo.')),
          );
      }
    }
  }

  Future<void> _choosePhoto() async {
    try {
      final XFile? image =
          await _picker.pickImage(source: ImageSource.gallery, maxWidth: 800);
      if (image != null && mounted) {
        await ref.read(profileCoreProvider.notifier).uploadPhoto(image.path);
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(
            const SnackBar(content: Text('Unable to select photo.')),
          );
      }
    }
  }
}

/// Consistent gray icon wrapper for profile menu rows.
class _MenuIcon extends StatelessWidget {
  const _MenuIcon(this.icon);

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Icon(icon, size: 24, color: context.colors.muted);
  }
}

/// A single option row inside the photo action modal.
class _ModalOption extends StatelessWidget {
  const _ModalOption({
    required this.label,
    required this.onTap,
    this.color,
  });

  final String label;
  final VoidCallback onTap;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: PayaboSpacing.lg),
        child: Center(
          child: Text(
            label,
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w700,
              color: color ?? c.ink,
            ),
          ),
        ),
      ),
    );
  }
}
