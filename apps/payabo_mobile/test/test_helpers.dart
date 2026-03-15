import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/shared/theme/payabo_theme.dart';
import 'package:shared_preferences/shared_preferences.dart';

Widget buildTestApp(
  Widget child, {
  DemoDataMode demoDataMode = DemoDataMode.populated,
  AppEnvironment environment = const AppEnvironment(
    flavor: AppFlavor.dev,
    useMocks: true,
    apiBaseUrl: 'https://api.dev.payabo.local',
  ),
}) {
  SharedPreferences.setMockInitialValues(<String, Object>{});

  return ProviderScope(
    overrides: [
      appEnvironmentProvider.overrideWithValue(environment),
      initialDemoDataModeProvider.overrideWithValue(demoDataMode),
    ],
    child: MaterialApp(
      theme: buildPayaboTheme(),
      home: child,
    ),
  );
}
