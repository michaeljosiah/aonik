import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../shared/theme/payabo_theme.dart';
import 'router/app_router.dart';

class PayaboApp extends ConsumerWidget {
  const PayaboApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(appRouterProvider);

    return MaterialApp.router(
      title: 'Payabo Mobile',
      debugShowCheckedModeBanner: false,
      theme: buildPayaboTheme(),
      routerConfig: router,
    );
  }
}
