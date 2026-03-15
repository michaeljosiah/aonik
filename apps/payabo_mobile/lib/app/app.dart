import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../shared/theme/payabo_theme.dart';
import '../shared/theme/theme_mode_provider.dart';
import 'errors/api_error_listener.dart';
import 'router/app_router.dart';

class PayaboApp extends ConsumerWidget {
  const PayaboApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(appRouterProvider);
    final themeMode = ref.watch(themeModeProvider);

    return MaterialApp.router(
      title: 'Payabo Mobile',
      debugShowCheckedModeBanner: false,
      theme: buildPayaboTheme(),
      darkTheme: buildPayaboDarkTheme(),
      themeMode: themeMode,
      routerConfig: router,
      builder: (context, child) => ApiErrorListener(
        child: child ?? const SizedBox.shrink(),
      ),
    );
  }
}
