import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:payabo_mobile/app/auth/auth_controller.dart';
import 'package:payabo_mobile/app/auth/auth_session_store.dart';
import 'package:payabo_mobile/data/repositories/auth_repository.dart';
import 'package:payabo_mobile/data/repositories/profile_repository.dart';
import 'package:payabo_mobile/data/repositories/repository_providers.dart';
import 'package:payabo_mobile/features/profile/presentation/profile_state.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() {
    SharedPreferences.setMockInitialValues(<String, Object>{});
  });

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

  test('sign out clears stale profile state before next user logs in',
      () async {
    final store = KeyValueAuthSessionStore(InMemoryKeyValueStore());
    final repository = _SwitchingAuthRepository();
    final profileRepository = _SwitchingProfileRepository();

    final container = ProviderContainer(
      overrides: [
        authSessionStoreProvider.overrideWithValue(store),
        authRepositoryProvider.overrideWithValue(repository),
        profileRepositoryProvider.overrideWithValue(profileRepository),
      ],
    );
    addTearDown(container.dispose);

    await _waitForInitialization(container);

    await container.read(authControllerProvider.notifier).signInWithPassword(
          email: 'jayden.josiah@mailinator.com',
          password: 'Pass1234',
        );
    await container.read(profileCoreProvider.notifier).ensureLoaded();

    expect(container.read(profileCoreProvider).displayName, 'Jayden Josiah');
    expect(container.read(profileCoreProvider).loaded, isTrue);

    await container.read(authControllerProvider.notifier).signOut();

    final clearedProfileState = container.read(profileCoreProvider);
    expect(clearedProfileState.loaded, isFalse);
    expect(clearedProfileState.displayName, isEmpty);
    expect(clearedProfileState.email, isEmpty);

    repository.setActiveUser(
      email: 'ethan.josiah@mailinator.com',
      userId: 'ethan-user-id',
      firstName: 'Ethan',
      lastName: 'Josiah',
    );
    profileRepository.setActiveProfile(
      const UserProfile(
        firstName: 'Ethan',
        lastName: 'Josiah',
        email: 'ethan.josiah@mailinator.com',
        phone: '+447700900124',
        countryCode: 'GB',
      ),
    );

    await container.read(authControllerProvider.notifier).registerIndividual(
          const RegisterIndividualRequest(
            firstName: 'Ethan',
            lastName: 'Josiah',
            email: 'ethan.josiah@mailinator.com',
            password: 'Pass1234',
            registrationCountry: 'GB',
          ),
        );

    final postRegistrationProfileState = container.read(profileCoreProvider);
    expect(postRegistrationProfileState.loaded, isFalse);

    await container.read(profileCoreProvider.notifier).ensureLoaded();

    final refreshedProfileState = container.read(profileCoreProvider);
    expect(refreshedProfileState.displayName, 'Ethan Josiah');
    expect(refreshedProfileState.email, 'ethan.josiah@mailinator.com');
    expect(container.read(authControllerProvider).user?.email,
        'ethan.josiah@mailinator.com');
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
  Future<List<String>> getRegistrationCountries() async {
    return const <String>['GB', 'GH', 'NG'];
  }

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

class _SwitchingAuthRepository implements AuthRepository {
  _SwitchingAuthRepository()
      : _userInfo = const AuthUserInfo(
          userId: 'jayden-user-id',
          email: 'jayden.josiah@mailinator.com',
          firstName: 'Jayden',
          lastName: 'Josiah',
        );

  AuthUserInfo _userInfo;

  void setActiveUser({
    required String email,
    required String userId,
    required String firstName,
    required String lastName,
  }) {
    _userInfo = AuthUserInfo(
      userId: userId,
      email: email,
      firstName: firstName,
      lastName: lastName,
    );
  }

  @override
  Future<List<String>> getRegistrationCountries() async {
    return const <String>['GB', 'GH', 'NG'];
  }

  @override
  Future<AuthUserInfo> getUserInfo() async {
    return _userInfo;
  }

  @override
  Future<AuthOnboardingSnapshot?> registerIndividual(
    RegisterIndividualRequest request,
  ) async {
    return AuthOnboardingSnapshot(
      userId: _userInfo.userId,
      partyId: 'party-${_userInfo.userId}',
      gates: const <AuthOnboardingGate>[],
      nextActions: const <String>[],
    );
  }

  @override
  Future<void> sendPasswordResetEmail(String email) async {}

  @override
  Future<AuthTokenResult> signInWithPassword({
    required String email,
    required String password,
  }) async {
    return AuthTokenResult(
      accessToken: 'token-for-${email.toLowerCase()}',
      tokenType: 'Bearer',
      expiresIn: 3600,
      refreshToken: 'refresh-for-${email.toLowerCase()}',
      idToken: null,
    );
  }

  @override
  Future<AuthTokenResult> refreshAccessToken({
    required String refreshToken,
  }) async {
    return const AuthTokenResult(
      accessToken: 'refreshed-token',
      tokenType: 'Bearer',
      expiresIn: 3600,
      refreshToken: 'refreshed-refresh-token',
      idToken: null,
    );
  }

  @override
  Future<PhoneOtpResult> sendRegistrationPhoneOtp(String phone) async {
    return PhoneOtpResult(
      challengeId: 'challenge',
      expiresAt: DateTime.now().add(const Duration(minutes: 10)),
    );
  }

  @override
  Future<bool> verifyRegistrationPhoneOtp(
    String challengeId,
    String code,
  ) async {
    return true;
  }

  @override
  Future<AuthOnboardingSnapshot?> getOnboardingSnapshot() async {
    return AuthOnboardingSnapshot(
      userId: _userInfo.userId,
      partyId: 'party-${_userInfo.userId}',
      gates: const <AuthOnboardingGate>[],
      nextActions: const <String>[],
    );
  }
}

class _SwitchingProfileRepository implements ProfileRepository {
  _SwitchingProfileRepository()
      : _profile = const UserProfile(
          firstName: 'Jayden',
          lastName: 'Josiah',
          email: 'jayden.josiah@mailinator.com',
          phone: '+447700900123',
          countryCode: 'GB',
        );

  UserProfile _profile;

  void setActiveProfile(UserProfile profile) {
    _profile = profile;
  }

  @override
  Future<UserProfile> getProfile() async {
    return _profile;
  }

  @override
  Future<UserProfile> updateProfile(UserProfile profile) async {
    _profile = profile;
    return _profile;
  }

  @override
  Future<UserProfile> updateEmail({
    required String currentEmail,
    required String newEmail,
    required String password,
  }) async {
    _profile = UserProfile(
      firstName: _profile.firstName,
      lastName: _profile.lastName,
      email: newEmail,
      phone: _profile.phone,
      countryCode: _profile.countryCode,
      photoUrl: _profile.photoUrl,
    );
    return _profile;
  }

  @override
  Future<void> updatePassword({
    required String currentPassword,
    required String newPassword,
  }) async {}

  @override
  Future<String> uploadPhoto(String filePath) async {
    _profile = UserProfile(
      firstName: _profile.firstName,
      lastName: _profile.lastName,
      email: _profile.email,
      phone: _profile.phone,
      countryCode: _profile.countryCode,
      photoUrl: filePath,
    );
    return filePath;
  }

  @override
  Future<void> deletePhoto() async {
    _profile = UserProfile(
      firstName: _profile.firstName,
      lastName: _profile.lastName,
      email: _profile.email,
      phone: _profile.phone,
      countryCode: _profile.countryCode,
    );
  }

  @override
  Future<NotificationPreferences> getNotificationPreferences() async {
    return NotificationPreferences(
      email: _profile.email,
      newBillsPush: true,
      billUpdatesPush: true,
      billAssistPush: false,
      mbaMessagesPush: true,
      orgMessagesPush: true,
      friendsMessagesPush: false,
      newBillsEmail: true,
      billUpdatesEmail: true,
      billAssistEmail: false,
      mbaMessagesEmail: true,
      orgMessagesEmail: true,
    );
  }

  @override
  Future<NotificationPreferences> updateNotificationPreferences(
    NotificationPreferences preferences,
  ) async {
    return preferences;
  }

  @override
  Future<MarketingPreferences> getMarketingPreferences() async {
    return MarketingPreferences(
      email: _profile.email,
      news: true,
      offers: true,
      surveys: false,
    );
  }

  @override
  Future<MarketingPreferences> updateMarketingPreferences(
    MarketingPreferences preferences,
  ) async {
    return preferences;
  }
}
