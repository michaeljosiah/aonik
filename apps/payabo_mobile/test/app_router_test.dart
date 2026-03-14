import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/auth/auth_controller.dart';
import 'package:payabo_mobile/app/router/app_router.dart';

void main() {
  group('resolveAppRedirect', () {
    const AuthState unauthenticated = AuthState(
      isInitialized: true,
      isAuthenticated: false,
      isBusy: false,
    );

    const AuthState authenticated = AuthState(
      isInitialized: true,
      isAuthenticated: true,
      isBusy: false,
    );

    test('redirects unauthenticated users to login', () {
      final redirect = resolveAppRedirect(
        authState: unauthenticated,
        location: '/dashboard',
        setupDone: false,
      );

      expect(redirect, '/auth/login');
    });

    test('redirects authenticated auth-area users to setup when incomplete',
        () {
      final redirect = resolveAppRedirect(
        authState: authenticated,
        location: '/auth/login',
        setupDone: false,
      );

      expect(redirect, '/setup');
    });

    test(
        'redirects authenticated users away from dashboard until setup completes',
        () {
      final redirect = resolveAppRedirect(
        authState: authenticated,
        location: '/dashboard',
        setupDone: false,
      );

      expect(redirect, '/setup');
    });

    test(
        'redirects authenticated users in auth area to dashboard when complete',
        () {
      final redirect = resolveAppRedirect(
        authState: authenticated,
        location: '/auth/login',
        setupDone: true,
      );

      expect(redirect, '/dashboard');
    });

    test('prevents revisiting setup after completion', () {
      final redirect = resolveAppRedirect(
        authState: authenticated,
        location: '/setup',
        setupDone: true,
      );

      expect(redirect, '/dashboard');
    });
  });
}
