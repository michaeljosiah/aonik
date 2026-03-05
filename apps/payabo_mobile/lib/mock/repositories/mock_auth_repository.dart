import '../../data/repositories/auth_repository.dart';

class MockAuthRepository implements AuthRepository {
  AuthUserInfo? _currentUser;

  @override
  Future<AuthTokenResult> signInWithPassword({
    required String email,
    required String password,
  }) async {
    final normalizedEmail = email.trim().toLowerCase();
    final namePart = normalizedEmail.split('@').first;
    final firstName = namePart.isEmpty ? 'Payabo' : _capitalize(namePart);

    _currentUser = AuthUserInfo(
      userId: 'mock-user-id',
      email: normalizedEmail,
      firstName: firstName,
      lastName: 'User',
    );

    return const AuthTokenResult(
      accessToken: 'mock-access-token',
      refreshToken: 'mock-refresh-token',
      expiresIn: 3600,
      tokenType: 'Bearer',
      idToken: null,
    );
  }

  @override
  Future<AuthTokenResult> refreshAccessToken({
    required String refreshToken,
  }) async {
    return const AuthTokenResult(
      accessToken: 'mock-access-token-refreshed',
      refreshToken: 'mock-refresh-token',
      expiresIn: 3600,
      tokenType: 'Bearer',
      idToken: null,
    );
  }

  @override
  Future<void> registerIndividual(RegisterIndividualRequest request) async {
    _currentUser = AuthUserInfo(
      userId: 'mock-user-id',
      email: request.email.trim().toLowerCase(),
      firstName: request.firstName.trim(),
      lastName: request.lastName.trim(),
    );
  }

  @override
  Future<void> sendPasswordResetEmail(String email) async {}

  @override
  Future<AuthUserInfo> getUserInfo() async {
    return _currentUser ??
        const AuthUserInfo(
          userId: 'mock-user-id',
          email: 'johndoe@mail.com',
          firstName: 'John',
          lastName: 'Doe',
        );
  }

  static String _capitalize(String rawValue) {
    if (rawValue.isEmpty) {
      return rawValue;
    }

    final normalized = rawValue.trim();
    if (normalized.isEmpty) {
      return normalized;
    }

    final first = normalized.substring(0, 1).toUpperCase();
    final rest = normalized.substring(1).toLowerCase();
    return '$first$rest';
  }
}
