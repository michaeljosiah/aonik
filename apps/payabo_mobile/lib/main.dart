import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app/app.dart';
import 'app/environment/app_environment.dart';
import 'app/environment/environment_provider.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();

  final AppEnvironment environment = AppEnvironment.fromDefines();

  runApp(
    ProviderScope(
      overrides: <Override>[
        appEnvironmentProvider.overrideWithValue(environment),
      ],
      child: const PayaboApp(),
    ),
  );
}
