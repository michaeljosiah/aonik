import 'dart:developer' as developer;

import 'package:firebase_analytics/firebase_analytics.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/date_symbol_data_local.dart';

import 'app/app.dart';
import 'app/demo/demo_data_mode.dart';
import 'app/environment/app_environment.dart';
import 'app/environment/environment_provider.dart';
import 'app/network/dev_http_overrides.dart';
import 'firebase_options.dart';
import 'shared/theme/theme_mode_provider.dart';

const String _firebaseLogName = 'Payabo.Firebase';

bool get _isAndroid =>
    !kIsWeb && defaultTargetPlatform == TargetPlatform.android;

@pragma('vm:entry-point')
Future<void> _firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  await Firebase.initializeApp(
    options: DefaultFirebaseOptions.currentPlatform,
  );

  developer.log(
    'Background message received: id=${message.messageId}, data=${message.data}',
    name: _firebaseLogName,
  );
}

bool get _supportsAnalytics {
  if (kIsWeb) {
    return true;
  }

  switch (defaultTargetPlatform) {
    case TargetPlatform.android:
    case TargetPlatform.iOS:
    case TargetPlatform.macOS:
      return true;
    case TargetPlatform.fuchsia:
    case TargetPlatform.linux:
    case TargetPlatform.windows:
      return false;
  }
}

bool get _supportsCrashlytics {
  if (kIsWeb) {
    return false;
  }

  switch (defaultTargetPlatform) {
    case TargetPlatform.android:
    case TargetPlatform.iOS:
    case TargetPlatform.macOS:
      return true;
    case TargetPlatform.fuchsia:
    case TargetPlatform.linux:
    case TargetPlatform.windows:
      return false;
  }
}

Future<void> _configureFirebaseMessaging() async {
  FirebaseMessaging.onBackgroundMessage(_firebaseMessagingBackgroundHandler);

  final messaging = FirebaseMessaging.instance;
  final settings = await messaging.requestPermission(
    alert: true,
    announcement: false,
    badge: true,
    carPlay: false,
    criticalAlert: false,
    provisional: false,
    sound: true,
  );

  developer.log(
    'Messaging permission status: ${settings.authorizationStatus.name}',
    name: _firebaseLogName,
  );

  FirebaseMessaging.onMessage.listen((message) {
    developer.log(
      'Foreground message received: id=${message.messageId}, data=${message.data}',
      name: _firebaseLogName,
    );
  });
}

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Firebase.initializeApp(
    options: DefaultFirebaseOptions.currentPlatform,
  );
  if (_isAndroid) {
    await _configureFirebaseMessaging();
  }
  if (_supportsAnalytics) {
    await FirebaseAnalytics.instance.setAnalyticsCollectionEnabled(!kDebugMode);
  }
  if (_supportsCrashlytics) {
    await FirebaseCrashlytics.instance
        .setCrashlyticsCollectionEnabled(!kDebugMode);
    FlutterError.onError = FirebaseCrashlytics.instance.recordFlutterFatalError;
    PlatformDispatcher.instance.onError = (error, stack) {
      FirebaseCrashlytics.instance.recordError(error, stack, fatal: true);
      return true;
    };
  }
  await initializeDateFormatting();
  await SystemChrome.setPreferredOrientations([
    DeviceOrientation.portraitUp,
  ]);

  final AppEnvironment environment = AppEnvironment.fromDefines();
  final DemoDataMode initialDemoDataMode = await loadInitialDemoDataMode();
  final ThemeMode initialThemeMode = await loadInitialThemeMode();
  configureDevHttpOverrides(environment);
  if (kDebugMode) {
    developer.log(
      'Starting Payabo with baseUrl=${environment.runtimeApiBaseUrl}, flavor=${environment.label}, useMocks=${environment.useMocks}',
      name: 'Payabo.Main',
    );
  }

  runApp(
    ProviderScope(
      overrides: [
        appEnvironmentProvider.overrideWithValue(environment),
        initialDemoDataModeProvider.overrideWithValue(initialDemoDataMode),
        initialThemeModeProvider.overrideWithValue(initialThemeMode),
      ],
      child: const PayaboApp(),
    ),
  );
}
