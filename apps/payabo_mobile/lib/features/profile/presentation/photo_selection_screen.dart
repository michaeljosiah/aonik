import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/widgets/payabo_button.dart';
import 'profile_scaffold.dart';
import 'profile_state.dart';

class PhotoSelectionScreen extends ConsumerStatefulWidget {
  const PhotoSelectionScreen({super.key});

  @override
  ConsumerState<PhotoSelectionScreen> createState() =>
      _PhotoSelectionScreenState();
}

class _PhotoSelectionScreenState extends ConsumerState<PhotoSelectionScreen> {
  @override
  Widget build(BuildContext context) {
    final c = context.colors;

    return ProfileScaffold(
      title: 'Choose photo',
      backRoute: '/profile',
      footer: PayaboButton(
        label: 'Done',
        onPressed: () {
          ref.read(profileCoreProvider.notifier).setPhotoLabel('Change photo');
          context.go('/profile');
        },
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          AspectRatio(
            aspectRatio: 1,
            child: Container(
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(10),
                color: c.background,
              ),
              child: Center(
                child: Icon(
                  Icons.person,
                  size: 120,
                  color: c.muted.withValues(alpha: 0.5),
                ),
              ),
            ),
          ),
          const SizedBox(height: PayaboSpacing.xl),
          Center(
            child: Column(
              children: <Widget>[
                Icon(
                  Icons.photo_library_outlined,
                  size: 48,
                  color: c.muted.withValues(alpha: 0.4),
                ),
                const SizedBox(height: PayaboSpacing.md),
                Text(
                  'Photo library coming soon',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: c.muted,
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
