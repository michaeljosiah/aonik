import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'package:payabo_mobile/app/demo/demo_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/data/api/api_exception.dart';
import 'package:payabo_mobile/data/repositories/auth_repository.dart';
import 'package:payabo_mobile/data/repositories/repository_providers.dart';
import 'package:payabo_mobile/features/auth/presentation/forgot_password_screen.dart';
import 'package:payabo_mobile/features/auth/presentation/intro_screen.dart';
import 'package:payabo_mobile/features/auth/presentation/login_screen.dart';
import 'package:payabo_mobile/features/auth/presentation/phone_code_screen.dart';
import 'package:payabo_mobile/features/payments/presentation/provider_list_screen.dart';
import 'package:payabo_mobile/shared/theme/payabo_theme.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('login validation enables button only for valid credentials',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const LoginScreen(),
        isDemo: false,
      ),
    );

    final ElevatedButton loginButton = tester.widget<ElevatedButton>(
      find.widgetWithText(ElevatedButton, 'LOGIN'),
    );
    expect(loginButton.onPressed, isNull);

    await tester.enterText(find.byType(TextField).first, 'invalid-email');
    await tester.enterText(find.byType(TextField).last, 'pass123');
    await tester.pump();

    final ElevatedButton stillDisabled = tester.widget<ElevatedButton>(
      find.widgetWithText(ElevatedButton, 'LOGIN'),
    );
    expect(stillDisabled.onPressed, isNull);

    await tester.enterText(find.byType(TextField).first, 'jane@mail.com');
    await tester.pump();

    final ElevatedButton enabled = tester.widget<ElevatedButton>(
      find.widgetWithText(ElevatedButton, 'LOGIN'),
    );
    expect(enabled.onPressed, isNotNull);
  });

  testWidgets('login failure shows friendly snackbar message',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          appEnvironmentProvider.overrideWithValue(
            const AppEnvironment(
              flavor: AppFlavor.dev,
              useMocks: true,
              apiBaseUrl: 'https://api.dev.payabo.local',
            ),
          ),
          authRepositoryProvider.overrideWithValue(
            const _FailingAuthRepository(
              ApiException(
                message: 'Wrong email or password.',
                statusCode: 401,
              ),
            ),
          ),
        ],
        child: MaterialApp(
          theme: buildPayaboTheme(),
          home: const LoginScreen(),
        ),
      ),
    );

    await tester.enterText(find.byType(TextField).first, 'jane@mail.com');
    await tester.enterText(find.byType(TextField).last, 'WrongPass123');
    await tester.pump();

    await tester.tap(find.widgetWithText(ElevatedButton, 'LOGIN'));
    await tester.pumpAndSettle();

    expect(find.text('Wrong email or password.'), findsOneWidget);
    expect(find.text('We couldn\'t sign you in'), findsNothing);
  });

  testWidgets('intro disables registration while demo mode is active',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const IntroScreen(),
        isDemo: true,
      ),
    );
    await tester.pumpAndSettle();

    final OutlinedButton registerButton = tester.widget<OutlinedButton>(
      find.widgetWithText(OutlinedButton, 'CREATE AN ACCOUNT'),
    );

    expect(registerButton.onPressed, isNull);
    expect(
      find.text('Account creation is unavailable in demo mode.'),
      findsOneWidget,
    );
    expect(find.text('Demo mode is active'), findsOneWidget);
  });

  testWidgets(
      'access in demo mode routes to setup when live sign-in is available',
      (WidgetTester tester) async {
    final router = GoRouter(
      initialLocation: '/auth/login',
      routes: <RouteBase>[
        GoRoute(
          path: '/setup',
          builder: (BuildContext context, GoRouterState state) {
            return const Scaffold(body: Text('Setup'));
          },
        ),
        GoRoute(
          path: '/auth/login',
          builder: (BuildContext context, GoRouterState state) {
            return const LoginScreen();
          },
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          appEnvironmentProvider.overrideWithValue(
            const AppEnvironment(
              flavor: AppFlavor.dev,
              useMocks: true,
              apiBaseUrl: 'https://api.dev.payabo.local',
            ),
          ),
        ],
        child: MaterialApp.router(
          theme: buildPayaboTheme(),
          routerConfig: router,
        ),
      ),
    );

    await tester.tap(find.text('ACCESS IN DEMO MODE'));
    await tester.pumpAndSettle();

    expect(find.text('Setup'), findsOneWidget);
  });

  testWidgets('login screen disables live auth controls in demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          appEnvironmentProvider.overrideWithValue(
            const AppEnvironment(
              flavor: AppFlavor.dev,
              useMocks: false,
              apiBaseUrl: 'https://api.dev.payabo.local',
            ),
          ),
          isDemoProvider.overrideWith((Ref ref) => true),
        ],
        child: MaterialApp(
          theme: buildPayaboTheme(),
          home: const LoginScreen(),
        ),
      ),
    );

    final emailField = tester.widget<TextField>(find.byType(TextField).first);
    final passwordField = tester.widget<TextField>(find.byType(TextField).last);
    final ElevatedButton loginButton = tester.widget<ElevatedButton>(
      find.widgetWithText(ElevatedButton, 'LOGIN'),
    );

    expect(emailField.enabled, isFalse);
    expect(passwordField.enabled, isFalse);
    expect(loginButton.onPressed, isNull);
    expect(find.text('Demo mode is active'), findsOneWidget);

    final Material googleButton = tester.widget<Material>(
      find
          .ancestor(
            of: find.text('Continue with Google'),
            matching: find.byType(Material),
          )
          .first,
    );

    expect(googleButton.color, const Color(0xFFF4F4F4));
  });

  testWidgets('forgot password explains demo limitations when active',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const ForgotPasswordScreen(),
        isDemo: true,
      ),
    );
    await tester.pumpAndSettle();

    final emailField = tester.widget<TextField>(find.byType(TextField).first);
    final ElevatedButton recoverButton = tester.widget<ElevatedButton>(
      find.widgetWithText(ElevatedButton, 'RECOVER PASSWORD'),
    );

    expect(emailField.enabled, isFalse);
    expect(recoverButton.onPressed, isNull);
    expect(find.text('Password recovery is unavailable'), findsOneWidget);
  });

  testWidgets('phone code auto-starts 60s countdown then unlocks',
      (WidgetTester tester) async {
    await tester
        .pumpWidget(buildTestApp(const PhoneCodeScreen()));

    expect(find.textContaining('Request new code in'), findsOneWidget);
    // Pump through the full 60-second countdown
    for (var i = 0; i < 60; i++) {
      await tester.pump(const Duration(seconds: 1));
    }
    await tester.pump();
    expect(find.text('Request new code'), findsOneWidget);
  });

  testWidgets('provider search filter narrows provider list',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const ProviderListScreen()));

    await tester.pumpAndSettle();
    expect(find.text('ECG Power'), findsOneWidget);
    expect(find.text('Ghana Water'), findsOneWidget);

    await tester.enterText(find.byType(TextField).first, 'water');
    await tester.pumpAndSettle();

    expect(find.text('ECG Power'), findsNothing);
    expect(find.text('Ghana Water'), findsOneWidget);
  });
}

class _FailingAuthRepository implements AuthRepository {
  const _FailingAuthRepository(this._error);

  final ApiException _error;

  @override
  Future<AuthUserInfo> getUserInfo() async {
    return const AuthUserInfo(
      userId: 'test-user-id',
      email: 'jane@mail.com',
      firstName: 'Jane',
      lastName: 'Doe',
    );
  }

  @override
  Future<AuthOnboardingSnapshot?> registerIndividual(
    RegisterIndividualRequest request,
  ) async {
    return null;
  }

  @override
  Future<void> sendPasswordResetEmail(String email) async {}

  @override
  Future<AuthTokenResult> signInWithPassword({
    required String email,
    required String password,
  }) async {
    throw _error;
  }

  @override
  Future<AuthTokenResult> refreshAccessToken({
    required String refreshToken,
  }) async {
    return const AuthTokenResult(
      accessToken: 'unused-access-token',
      tokenType: 'Bearer',
      expiresIn: 3600,
      refreshToken: 'unused-refresh-token',
      idToken: null,
    );
  }

  @override
  Future<PhoneOtpResult> sendRegistrationPhoneOtp(String phone) async {
    return PhoneOtpResult(
      challengeId: 'test-challenge-id',
      expiresAt: DateTime.now().add(const Duration(minutes: 10)),
    );
  }

  @override
  Future<bool> verifyRegistrationPhoneOtp(
    String challengeId,
    String code,
  ) async {
    return code == '123456';
  }

  @override
  Future<AuthOnboardingSnapshot?> getOnboardingSnapshot() async {
    return null;
  }
}
