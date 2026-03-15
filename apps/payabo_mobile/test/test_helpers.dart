import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/app/demo/demo_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/shared/theme/payabo_theme.dart';
import 'package:shared_preferences/shared_preferences.dart';

Widget buildTestApp(
  Widget child, {
  DemoDataMode demoDataMode = DemoDataMode.populated,
  bool? isDemo,
  ThemeMode themeMode = ThemeMode.light,
  AppEnvironment environment = const AppEnvironment(
    flavor: AppFlavor.dev,
    useMocks: true,
    apiBaseUrl: 'https://api.dev.payabo.local',
  ),
}) {
  SharedPreferences.setMockInitialValues(<String, Object>{});
  final resolvedIsDemo = isDemo ?? environment.useMocks;

  return ProviderScope(
    overrides: [
      appEnvironmentProvider.overrideWithValue(environment),
      isDemoProvider.overrideWith((Ref ref) => resolvedIsDemo),
      initialDemoDataModeProvider.overrideWithValue(demoDataMode),
    ],
    child: MaterialApp(
      theme: buildPayaboTheme(),
      darkTheme: buildPayaboDarkTheme(),
      themeMode: themeMode,
      home: child,
    ),
  );
}
