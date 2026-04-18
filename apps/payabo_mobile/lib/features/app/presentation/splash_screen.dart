import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/startup/app_startup_controller.dart';
import '../../../app/startup/splash_warmup.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/widgets/payabo_letter_cascade_loader.dart';

class SplashScreen extends ConsumerStatefulWidget {
  const SplashScreen({super.key});

  @override
  ConsumerState<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends ConsumerState<SplashScreen> {
  static const Duration _minShowDuration = Duration(seconds: 4);

  late final DateTime _shownAt;
  Timer? _navigationTimer;
  bool _warmupDone = false;
  bool _healthCheckDone = false;
  bool _navigationScheduled = false;

  @override
  void initState() {
    super.initState();
    _shownAt = DateTime.now();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(appStartupControllerProvider.notifier).initialize();
      _startWarmup();
    });
  }

  @override
  void dispose() {
    _navigationTimer?.cancel();
    super.dispose();
  }

  Future<void> _startWarmup() async {
    await SplashWarmup.run(context);
    if (!mounted) return;
    _warmupDone = true;
    _maybeNavigate();
  }

  void _maybeNavigate() {
    if (!_warmupDone || !_healthCheckDone || _navigationScheduled) return;
    _navigationScheduled = true;

    final Duration elapsed = DateTime.now().difference(_shownAt);
    final Duration delay = _minShowDuration > elapsed
        ? _minShowDuration - elapsed
        : Duration.zero;

    _navigationTimer = Timer(delay, () {
      if (!mounted) return;
      if (context.mounted) context.go('/intro');
    });
  }

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final AppStartupState startupState =
        ref.watch(appStartupControllerProvider);

    ref.listen<AppStartupState>(appStartupControllerProvider,
        (AppStartupState? prev, AppStartupState next) {
      if (prev?.isChecking == true && !next.isChecking) {
        _healthCheckDone = true;
        _maybeNavigate();
      }
    });

    final String statusText;
    if (startupState.isChecking || !_warmupDone) {
      statusText = 'Starting up\u2026';
    } else if (startupState.isHealthy) {
      statusText = 'Ready';
    } else {
      statusText = 'Continuing in demo mode\u2026';
    }

    return Scaffold(
      backgroundColor: c.surfaceBase,
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                const PayaboLetterCascadeLoader(),
                const SizedBox(height: 18),
                Text(
                  statusText,
                  textAlign: TextAlign.center,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
