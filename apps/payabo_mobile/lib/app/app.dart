import 'dart:async';
import 'dart:developer' as developer;

import 'package:dio/dio.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../data/api/api_client.dart';
import 'auth/auth_controller.dart';
import '../shared/theme/payabo_theme.dart';
import '../shared/theme/theme_mode_provider.dart';
import 'errors/api_error_listener.dart';
import 'router/app_router.dart';

class PayaboApp extends ConsumerStatefulWidget {
  const PayaboApp({super.key});

  @override
  ConsumerState<PayaboApp> createState() => _PayaboAppState();
}

class _PayaboAppState extends ConsumerState<PayaboApp> {
  static const String _firebaseLogName = 'Payabo.Firebase';
  static const String _deviceRegistrationPath =
      '/profiles/customers/me/notification-devices';

  StreamSubscription<String>? _tokenRefreshSubscription;
  StreamSubscription<RemoteMessage>? _messageOpenedSubscription;
  final Set<String> _handledNotificationKeys = <String>{};
  String? _lastRegisteredToken;
  bool _messagingConfigured = false;

  @override
  void initState() {
    super.initState();
    unawaited(_configureFirebaseMessaging());
  }

  @override
  void dispose() {
    _tokenRefreshSubscription?.cancel();
    _messageOpenedSubscription?.cancel();
    super.dispose();
  }

  Future<void> _configureFirebaseMessaging() async {
    if (_messagingConfigured) {
      return;
    }

    _messagingConfigured = true;
    _messageOpenedSubscription =
        FirebaseMessaging.onMessageOpenedApp.listen(_handleNotificationTap);
    _tokenRefreshSubscription =
        FirebaseMessaging.instance.onTokenRefresh.listen((token) {
      developer.log('FCM token refreshed: $token', name: _firebaseLogName);
      _lastRegisteredToken = null;
      unawaited(_registerCurrentToken(tokenOverride: token));
    });

    final initialMessage = await FirebaseMessaging.instance.getInitialMessage();
    if (initialMessage != null) {
      _handleNotificationTap(initialMessage);
    }

    await _registerCurrentToken();
  }

  Future<void> _registerCurrentToken({String? tokenOverride}) async {
    final authState = ref.read(authControllerProvider);
    if (!authState.isAuthenticated) {
      return;
    }

    final token =
        (tokenOverride ?? await FirebaseMessaging.instance.getToken())?.trim();
    if (token == null || token.isEmpty || token == _lastRegisteredToken) {
      return;
    }

    try {
      await ref.read(apiClientProvider).post<void>(
        _deviceRegistrationPath,
        data: <String, Object?>{
          'provider': 'fcm',
          'platform': 'android',
          'deviceToken': token,
        },
      );
      _lastRegisteredToken = token;
      developer.log('Registered FCM token with API.', name: _firebaseLogName);
    } on DioException catch (error) {
      final statusCode = error.response?.statusCode;
      if (statusCode == 401 || statusCode == 403) {
        return;
      }

      if (statusCode == 404 || statusCode == 405 || statusCode == 501) {
        _lastRegisteredToken = token;
        developer.log(
          'Push token registration endpoint is not available yet at $_deviceRegistrationPath.',
          name: _firebaseLogName,
        );
        return;
      }

      developer.log(
        'Failed to register FCM token: ${error.message}',
        name: _firebaseLogName,
      );
    }
  }

  void _handleNotificationTap(RemoteMessage message) {
    developer.log(
      'Notification opened app: id=${message.messageId}, data=${message.data}',
      name: _firebaseLogName,
    );

    final route = _resolveNotificationRoute(message);
    final key = '${message.messageId ?? 'unknown'}:$route';
    if (!_handledNotificationKeys.add(key)) {
      return;
    }

    _navigateToRoute(route);
  }

  String _resolveNotificationRoute(RemoteMessage message) {
    final actionUrl = (message.data['actionUrl'] as String?)?.trim();
    if (actionUrl != null && actionUrl.startsWith('/')) {
      return actionUrl.startsWith('/admin/') ? '/notifications' : actionUrl;
    }

    final route = (message.data['route'] as String?)?.trim();
    if (route != null && route.startsWith('/')) {
      return route;
    }

    return '/notifications';
  }

  void _navigateToRoute(String route, [int attempt = 0]) {
    final context = rootNavigatorKey.currentContext;
    if (context != null) {
      GoRouter.of(context).go(route);
      return;
    }

    if (attempt >= 10) {
      developer.log(
        'Unable to navigate to notification route $route.',
        name: _firebaseLogName,
      );
      return;
    }

    Future<void>.delayed(
      const Duration(milliseconds: 250),
      () => _navigateToRoute(route, attempt + 1),
    );
  }

  @override
  Widget build(BuildContext context) {
    final router = ref.watch(appRouterProvider);
    final themeMode = ref.watch(themeModeProvider);

    ref.listen<AuthState>(authControllerProvider, (previous, next) {
      if (!next.isAuthenticated) {
        _lastRegisteredToken = null;
        return;
      }

      if (previous?.isAuthenticated == true && next.isAuthenticated) {
        return;
      }

      unawaited(_registerCurrentToken());
    });

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
