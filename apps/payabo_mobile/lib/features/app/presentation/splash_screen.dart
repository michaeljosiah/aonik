import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/startup/app_startup_controller.dart';

class SplashScreen extends ConsumerStatefulWidget {
  const SplashScreen({super.key});

  @override
  ConsumerState<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends ConsumerState<SplashScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(appStartupControllerProvider.notifier).initialize();
    });
  }

  @override
  Widget build(BuildContext context) {
    final startupState = ref.watch(appStartupControllerProvider);
    final canContinue = startupState.isHealthy && !startupState.isChecking;
    final statusText = startupState.isChecking
        ? 'Checking service availability...'
        : (startupState.message ?? 'Tap logo to continue');

    return Scaffold(
      backgroundColor: Colors.white,
      body: SafeArea(
        child: Center(
          child: InkWell(
            onTap: canContinue ? () => context.go('/intro') : null,
            borderRadius: BorderRadius.circular(12),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Image.asset(
                    'assets/images/mba_logo.png',
                    width: 260,
                    fit: BoxFit.contain,
                  ),
                  const SizedBox(height: 18),
                  Text(statusText, textAlign: TextAlign.center),
                  if (startupState.isChecking) ...<Widget>[
                    const SizedBox(height: 12),
                    const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                  ],
                  if (!startupState.isChecking &&
                      !startupState.isHealthy) ...<Widget>[
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
