import 'dart:developer' as developer;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';

import '../../../app/auth/auth_controller.dart';
import '../../../data/api/api_exception.dart';
import '../../../shared/theme/payabo_colors.dart';
import '../../../shared/theme/payabo_shadows.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
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
  final ImagePicker _picker = ImagePicker();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      try {
        await ref.read(profileControllerProvider.notifier).ensureLoaded();
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
    final state = ref.watch(profileControllerProvider);

    return ProfileScaffold(
      title: state.loaded ? state.displayName : '',
      backRoute: '/dashboard',
      child: state.loaded
          ? _buildContent(context, state)
          : const Center(
              child: Padding(
                padding: EdgeInsets.only(top: 80),
                child: CircularProgressIndicator(color: PayaboColors.primary),
              ),
            ),
    );
  }

  Widget _buildContent(BuildContext context, ProfileState state) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Center(
          child: Column(
            children: <Widget>[
              _ProfileAvatar(photoUrl: state.photoUrl),
              const SizedBox(height: PayaboSpacing.sm),
              GestureDetector(
                onTap: _openPhotoPicker,
                child: Text(
                  state.photoLabel,
                  style: const TextStyle(
                    color: PayaboColors.primary,
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
        const SizedBox(height: PayaboSpacing.lg),
        InkWell(
          onTap: () async {
            await ref.read(authControllerProvider.notifier).signOut();

            if (!context.mounted) {
              return;
            }

            context.go('/intro');
          },
          child: const Padding(
            padding: EdgeInsets.symmetric(vertical: PayaboSpacing.md),
            child: Center(
              child: Text(
                'LOG OUT',
                style: TextStyle(
                  color: PayaboColors.primary,
                  fontWeight: FontWeight.w700,
                  fontSize: 16,
                ),
              ),
            ),
          ),
        ),
        const SizedBox(height: PayaboSpacing.xs),
        const Center(
          child: Text(
            'Version 22.0001.01',
            style: TextStyle(color: PayaboColors.muted, fontSize: 12),
          ),
        ),
      ],
    );
  }

  Future<void> _openPhotoPicker() async {
    final state = ref.read(profileControllerProvider);
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
          const Divider(height: 1, color: PayaboColors.border),
          _ModalOption(
            label: 'CHOOSE PHOTO',
            onTap: () {
              Navigator.of(context).pop();
              _choosePhoto();
            },
          ),
          if (hasPhoto) ...<Widget>[
            const Divider(height: 1, color: PayaboColors.border),
            _ModalOption(
              label: 'DELETE PHOTO',
              color: PayaboColors.primary,
              onTap: () async {
                Navigator.of(context).pop();
                try {
                  await ref
                      .read(profileControllerProvider.notifier)
                      .deletePhoto();
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

  Future<void> _takePhoto() async {
    try {
      final XFile? image =
          await _picker.pickImage(source: ImageSource.camera, maxWidth: 800);
      if (image != null && mounted) {
        await ref
            .read(profileControllerProvider.notifier)
            .uploadPhoto(image.path);
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
        await ref
            .read(profileControllerProvider.notifier)
            .uploadPhoto(image.path);
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

/// 100x100 circular avatar with drop-shadow and camera icon placeholder.
class _ProfileAvatar extends StatelessWidget {
  const _ProfileAvatar({this.photoUrl});

  final String? photoUrl;

  @override
  Widget build(BuildContext context) {
    if (photoUrl != null && photoUrl!.isNotEmpty) {
      developer.log(
        'Attempting to render profile image from $photoUrl',
        name: 'Payabo.ProfileAvatar',
      );
    }

    return Container(
      width: 100,
      height: 100,
      decoration: const BoxDecoration(
        shape: BoxShape.circle,
        color: PayaboColors.background,
        boxShadow: PayaboShadows.soft,
      ),
      clipBehavior: Clip.antiAlias,
      child: photoUrl != null
          ? Image.network(
              photoUrl!,
              fit: BoxFit.cover,
              errorBuilder: (_, error, stackTrace) {
                developer.log(
                  'Profile image failed to render from $photoUrl',
                  name: 'Payabo.ProfileAvatar',
                  error: error,
                  stackTrace: stackTrace,
                );
                return _placeholder();
              },
            )
          : _placeholder(),
    );
  }

  static Widget _placeholder() {
    return const Center(
      child: Icon(
        Icons.camera_alt_outlined,
        size: 36,
        color: PayaboColors.muted,
      ),
    );
  }
}

/// Consistent gray icon wrapper for profile menu rows.
class _MenuIcon extends StatelessWidget {
  const _MenuIcon(this.icon);

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Icon(icon, size: 24, color: PayaboColors.muted);
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
              color: color ?? PayaboColors.ink,
            ),
          ),
        ),
      ),
    );
  }
}
