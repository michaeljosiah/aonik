import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../shared/theme/payabo_colors.dart';
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
  int _selectedIndex = 0;

  @override
  Widget build(BuildContext context) {
    return ProfileScaffold(
      title: 'Choose photo',
      backRoute: '/profile',
      footer: PayaboButton(
        label: 'Done',
        onPressed: () {
          ref
              .read(profileControllerProvider.notifier)
              .setPhotoLabel('Change photo');
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
                color: PayaboColors.background,
              ),
              child: Center(
                child: Icon(
                  Icons.person,
                  size: 120,
                  color: PayaboColors.muted.withValues(alpha: 0.5),
                ),
              ),
            ),
          ),
          const SizedBox(height: PayaboSpacing.sm),
          TextButton(
            onPressed: () {},
            child: const Text('Library'),
          ),
          GridView.builder(
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
              crossAxisCount: 4,
              crossAxisSpacing: 4,
              mainAxisSpacing: 4,
            ),
            itemCount: 8,
            itemBuilder: (context, index) {
              final selected = _selectedIndex == index;
              return InkWell(
                onTap: () => setState(() => _selectedIndex = index),
                child: Container(
                  decoration: BoxDecoration(
                    border: Border.all(
                      color:
                          selected ? PayaboColors.primary : PayaboColors.border,
                      width: selected ? 2 : 1,
                    ),
                    color: PayaboColors.background,
                  ),
                  child: const Icon(Icons.image_outlined,
                      color: PayaboColors.muted),
                ),
              );
            },
          ),
        ],
      ),
    );
  }
}
