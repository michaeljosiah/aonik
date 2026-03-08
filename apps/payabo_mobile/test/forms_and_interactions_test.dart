import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/data/api/api_exception.dart';
import 'package:payabo_mobile/data/repositories/auth_repository.dart';
import 'package:payabo_mobile/data/repositories/repository_providers.dart';
import 'package:payabo_mobile/features/auth/presentation/login_screen.dart';
import 'package:payabo_mobile/features/auth/presentation/phone_code_screen.dart';
import 'package:payabo_mobile/features/payments/presentation/provider_list_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('login validation enables button only for valid credentials',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const LoginScreen()));

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
      buildTestApp(
        const LoginScreen(),
        overrides: <Override>[
          authRepositoryProvider.overrideWithValue(
            const _FailingAuthRepository(
              ApiException(
                message: 'Wrong email or password.',
                statusCode: 401,
              ),
            ),
          ),
        ],
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

  testWidgets('phone code disabled state shows countdown then unlocks',
      (WidgetTester tester) async {
    await tester
        .pumpWidget(buildTestApp(const PhoneCodeScreen(initialDisabled: true)));

    expect(find.textContaining('Request new code in'), findsOneWidget);
    await tester.pump(const Duration(seconds: 6));
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
  Future<void> registerIndividual(RegisterIndividualRequest request) async {}

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
}
