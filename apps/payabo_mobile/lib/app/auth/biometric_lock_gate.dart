import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app_lock_controller.dart';
import 'biometric_lock_screen.dart';

class BiometricLockGate extends ConsumerWidget {
  const BiometricLockGate({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final bool isLocked = ref.watch(
      appLockControllerProvider.select((s) => s.isLocked),
    );

    return Stack(
      children: <Widget>[
        child,
        if (isLocked)
          const Positioned.fill(
            child: BiometricLockScreen(),
          ),
      ],
    );
  }
}
