import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/startup/app_startup_controller.dart';
import '../../../app/startup/offline_mode_provider.dart';
import '../../../shared/theme/payabo_color_resolver.dart';

class SplashScreen extends ConsumerStatefulWidget {
  const SplashScreen({super.key});

  @override
  ConsumerState<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends ConsumerState<SplashScreen> {
  bool _navigating = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(appStartupControllerProvider.notifier).initialize();
    });
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final startupState = ref.watch(appStartupControllerProvider);

    // React to health-check completion.
    ref.listen<AppStartupState>(appStartupControllerProvider, (prev, next) {
      if (prev == null || prev.isChecking != true || next.isChecking) return;

      if (!next.isHealthy && !_navigating) {
        // API unreachable -- activate offline / demo mode.
        ref.read(offlineModeProvider.notifier).state = true;
        _navigating = true;
        Future<void>.delayed(const Duration(seconds: 2), () {
          if (mounted) {
            if (context.mounted) context.go('/intro');
          }
        });
      }
    });

    final isOfflineFallback =
        !startupState.isChecking && !startupState.isHealthy && _navigating;
    final canContinue =
        startupState.isHealthy && !startupState.isChecking;

    final String statusText;
    if (startupState.isChecking) {
      statusText = 'Checking service availability\u2026';
    } else if (isOfflineFallback) {
      statusText = 'Could not reach services.\nContinuing in demo mode\u2026';
    } else {
      statusText = startupState.message ?? 'Tap logo to continue';
    }

    return Scaffold(
      backgroundColor: c.surfaceBase,
      body: SafeArea(
        child: Center(
          child: InkWell(
            onTap: canContinue ? () => context.go('/intro') : null,
            borderRadius: BorderRadius.circular(12),
            child: Padding(
              padding:
                  const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Image.asset(
                    'assets/images/mba_logo.png',
                    width: 260,
                    fit: BoxFit.contain,
                  ),
                  const SizedBox(height: 18),
                  Text(
                    statusText,
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: isOfflineFallback
                          ? c.primary
                          : null,
                      fontWeight: isOfflineFallback
                          ? FontWeight.w600
                          : FontWeight.normal,
                    ),
                  ),
                  if (startupState.isChecking) ...<Widget>[
                    const SizedBox(height: 12),
                    const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                  ],
                  if (isOfflineFallback) ...<Widget>[
                    const SizedBox(height: 12),
                    const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                  ],
                  // Show retry only when the check failed but we have NOT
                  // started the offline-fallback navigation.
                  if (!startupState.isChecking &&
                      !startupState.isHealthy &&
                      !_navigating) ...<Widget>[
                    const SizedBox(height: 12),
                    TextButton(
                      onPressed: () => ref
                          .read(appStartupControllerProvider.notifier)
                          .initialize(),
                      child: const Text('Retry checks'),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
