import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../shared/theme/payabo_color_resolver.dart';
import '../../shared/theme/payabo_spacing.dart';
import '../../shared/widgets/payabo_button.dart';
import '../../shared/widgets/payabo_wordmark.dart';
import 'app_lock_controller.dart';

class BiometricLockScreen extends ConsumerStatefulWidget {
  const BiometricLockScreen({super.key});

  @override
  ConsumerState<BiometricLockScreen> createState() =>
      _BiometricLockScreenState();
}

class _BiometricLockScreenState extends ConsumerState<BiometricLockScreen> {
  bool _prompted = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!_prompted) {
        _prompted = true;
        ref.read(appLockControllerProvider.notifier).attemptBiometricUnlock();
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final lockState = ref.watch(appLockControllerProvider);

    return Scaffold(
      backgroundColor: c.surfaceBase,
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: PayaboSpacing.xl),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                const PayaboWordmark(width: 200),
                const SizedBox(height: PayaboSpacing.x3),
                Icon(
                  Icons.fingerprint,
                  size: 64,
                  color: c.primary,
                ),
                const SizedBox(height: PayaboSpacing.lg),
                Text(
                  'Unlock Payabo',
                  style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                        color: c.textPrimary,
                      ),
                ),
                const SizedBox(height: PayaboSpacing.sm),
                Text(
                  'Verify your identity to continue',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: c.muted,
                      ),
                ),
                if (lockState.errorMessage != null) ...<Widget>[
                  const SizedBox(height: PayaboSpacing.lg),
                  Text(
                    lockState.errorMessage!,
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: c.danger,
                        ),
                  ),
                ],
                const SizedBox(height: PayaboSpacing.x3),
                if (lockState.isCheckingBiometric)
                  const SizedBox(
                    width: 24,
                    height: 24,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                else
                  PayaboButton(
                    label: 'Unlock',
                    leading: const Icon(Icons.fingerprint, size: 20),
                    onPressed: () => ref
                        .read(appLockControllerProvider.notifier)
                        .attemptBiometricUnlock(),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
