import 'dart:developer' as developer;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app/app.dart';
import 'app/demo/demo_data_mode.dart';
import 'app/environment/app_environment.dart';
import 'app/environment/environment_provider.dart';
import 'app/network/dev_http_overrides.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final AppEnvironment environment = AppEnvironment.fromDefines();
  final DemoDataMode initialDemoDataMode = await loadInitialDemoDataMode();
  configureDevHttpOverrides(environment);
  developer.log(
    'Starting Payabo with baseUrl=${environment.runtimeApiBaseUrl}, flavor=${environment.label}, useMocks=${environment.useMocks}',
    name: 'Payabo.Main',
  );

  runApp(
    ProviderScope(
      overrides: [
        appEnvironmentProvider.overrideWithValue(environment),
        initialDemoDataModeProvider.overrideWithValue(initialDemoDataMode),
      ],
      child: const PayaboApp(),
    ),
  );
}
