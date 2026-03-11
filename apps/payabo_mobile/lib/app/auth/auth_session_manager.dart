import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/repositories/auth_repository.dart';
import '../../data/repositories/repository_providers.dart';
import 'auth_session_store.dart';

class AuthSessionManager {
  AuthSessionManager(this._ref);

  final Ref _ref;

  Future<AuthSession?> refreshExpiredSession(AuthSession expiredSession) async {
    final String? refreshToken = expiredSession.refreshToken?.trim();
    if (refreshToken == null || refreshToken.isEmpty) {
      await _ref.read(authSessionStoreProvider).clear();
      return null;
    }

    try {
      final AuthTokenResult token =
          await _ref.read(authRepositoryProvider).refreshAccessToken(
                refreshToken: refreshToken,
              );

      final String? refreshedToken = token.refreshToken?.trim();
      final AuthSession refreshedSession = AuthSession(
        accessToken: token.accessToken,
        tokenType: token.tokenType,
        refreshToken: refreshedToken == null || refreshedToken.isEmpty
            ? expiredSession.refreshToken
            : refreshedToken,
        expiresAt: token.expiresIn <= 0
            ? null
            : DateTime.now().add(Duration(seconds: token.expiresIn)),
      );

      await _ref.read(authSessionStoreProvider).write(refreshedSession);
      return refreshedSession;
    } catch (_) {
      await _ref.read(authSessionStoreProvider).clear();
      return null;
    }
  }
}

final Provider<AuthSessionManager> authSessionManagerProvider =
    Provider<AuthSessionManager>(
  AuthSessionManager.new,
);
