import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/auth/auth_controller.dart';
import 'package:payabo_mobile/app/auth/auth_session_store.dart';
import 'package:payabo_mobile/data/repositories/auth_repository.dart';
import 'package:payabo_mobile/data/repositories/repository_providers.dart';

void main() {
  test('auth controller initializes unauthenticated without stored session',
      () async {
    final store = KeyValueAuthSessionStore(InMemoryKeyValueStore());
    final repository = _FakeAuthRepository();

    final container = ProviderContainer(
      overrides: [
        authSessionStoreProvider.overrideWithValue(store),
        authRepositoryProvider.overrideWithValue(repository),
      ],
    );
    addTearDown(container.dispose);

    expect(container.read(authControllerProvider).isInitialized, isFalse);

    await _waitForInitialization(container);

    final state = container.read(authControllerProvider);
    expect(state.isInitialized, isTrue);
    expect(state.isAuthenticated, isFalse);
    expect(state.user, isNull);
  });

  test('auth controller signs in and persists session token', () async {
    final store = KeyValueAuthSessionStore(InMemoryKeyValueStore());
    final repository = _FakeAuthRepository();

    final container = ProviderContainer(
      overrides: [
        authSessionStoreProvider.overrideWithValue(store),
        authRepositoryProvider.overrideWithValue(repository),
      ],
    );
    addTearDown(container.dispose);

    await _waitForInitialization(container);

    await container.read(authControllerProvider.notifier).signInWithPassword(
          email: 'jane@mail.com',
          password: 'Pass1234',
        );

    final state = container.read(authControllerProvider);
    expect(state.isAuthenticated, isTrue);
    expect(state.user?.email, 'jane@mail.com');
    expect(state.onboarding?.hasPendingRequiredActions, isFalse);

    final session = await store.read();
    expect(session, isNotNull);
    expect(session?.accessToken, 'test-access-token');
  });

  test('auth controller clears expired stored session during bootstrap',
      () async {
    final store = KeyValueAuthSessionStore(InMemoryKeyValueStore());
    await store.write(
      AuthSession(
        accessToken: 'expired-token',
        tokenType: 'Bearer',
        expiresAt: DateTime.now().subtract(const Duration(minutes: 1)),
      ),
    );

    final container = ProviderContainer(
      overrides: [
        authSessionStoreProvider.overrideWithValue(store),
        authRepositoryProvider.overrideWithValue(_FakeAuthRepository()),
      ],
    );
    addTearDown(container.dispose);

    await _waitForInitialization(container);

    final state = container.read(authControllerProvider);
    expect(state.isInitialized, isTrue);
    expect(state.isAuthenticated, isFalse);

    final session = await store.read();
    expect(session, isNull);
  });
}

Future<void> _waitForInitialization(ProviderContainer container) async {
  for (var attempt = 0; attempt < 50; attempt++) {
    if (container.read(authControllerProvider).isInitialized) {
      return;
    }

    await Future<void>.delayed(const Duration(milliseconds: 10));
  }
}

class _FakeAuthRepository implements AuthRepository {
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
    return _buildOnboardingSnapshot();
  }

  @override
  Future<void> sendPasswordResetEmail(String email) async {}

  @override
  Future<AuthTokenResult> signInWithPassword({
    required String email,
    required String password,
  }) async {
    return const AuthTokenResult(
      accessToken: 'test-access-token',
      tokenType: 'Bearer',
      expiresIn: 3600,
      refreshToken: 'test-refresh-token',
      idToken: null,
    );
  }

  @override
  Future<AuthTokenResult> refreshAccessToken({
    required String refreshToken,
  }) async {
    return const AuthTokenResult(
      accessToken: 'test-access-token-refreshed',
      tokenType: 'Bearer',
      expiresIn: 3600,
      refreshToken: 'test-refresh-token',
      idToken: null,
    );
  }

  @override
  Future<AuthOnboardingSnapshot?> getOnboardingSnapshot() async {
    return _buildOnboardingSnapshot();
  }

  AuthOnboardingSnapshot _buildOnboardingSnapshot() {
    return const AuthOnboardingSnapshot(
      userId: 'test-user-id',
      partyId: 'test-party-id',
      gates: <AuthOnboardingGate>[
        AuthOnboardingGate(
          gate: 'EmailVerified',
          isSatisfied: true,
          isRequired: true,
          requiredActions: <String>[],
        ),
      ],
      nextActions: <String>[],
    );
  }
}
