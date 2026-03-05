import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../environment/environment_provider.dart';

class AuthSession {
  const AuthSession({
    required this.accessToken,
    required this.tokenType,
    this.refreshToken,
    this.expiresAt,
  });

  final String accessToken;
  final String tokenType;
  final String? refreshToken;
  final DateTime? expiresAt;

  bool get hasAccessToken => accessToken.trim().isNotEmpty;

  bool get isExpired {
    if (expiresAt == null) {
      return false;
    }

    return DateTime.now().isAfter(expiresAt!);
  }
}

abstract class KeyValueStore {
  Future<String?> read(String key);

  Future<void> write(String key, String value);

  Future<void> delete(String key);
}

class InMemoryKeyValueStore implements KeyValueStore {
  final Map<String, String> _values = <String, String>{};

  @override
  Future<String?> read(String key) async {
    return _values[key];
  }

  @override
  Future<void> write(String key, String value) async {
    _values[key] = value;
  }

  @override
  Future<void> delete(String key) async {
    _values.remove(key);
  }
}

class FlutterSecureKeyValueStore implements KeyValueStore {
  FlutterSecureKeyValueStore(this._secureStorage);

  final FlutterSecureStorage _secureStorage;

  @override
  Future<String?> read(String key) {
    return _secureStorage.read(key: key);
  }

  @override
  Future<void> write(String key, String value) {
    return _secureStorage.write(key: key, value: value);
  }

  @override
  Future<void> delete(String key) {
    return _secureStorage.delete(key: key);
  }
}

abstract class AuthSessionStore {
  Future<AuthSession?> read();

  Future<void> write(AuthSession session);

  Future<void> clear();
}

class KeyValueAuthSessionStore implements AuthSessionStore {
  KeyValueAuthSessionStore(this._store);

  static const String _accessTokenKey = 'payabo.auth.access_token';
  static const String _refreshTokenKey = 'payabo.auth.refresh_token';
  static const String _tokenTypeKey = 'payabo.auth.token_type';
  static const String _expiresAtKey = 'payabo.auth.expires_at_ms';

  final KeyValueStore _store;

  @override
  Future<AuthSession?> read() async {
    final accessToken = await _store.read(_accessTokenKey);
    if (accessToken == null || accessToken.trim().isEmpty) {
      return null;
    }

    final refreshToken = await _store.read(_refreshTokenKey);
    final tokenType = await _store.read(_tokenTypeKey) ?? 'Bearer';
    final expiresAtRaw = await _store.read(_expiresAtKey);
    final expiresAtMilliseconds = int.tryParse(expiresAtRaw ?? '');
    final expiresAt = expiresAtMilliseconds == null
        ? null
        : DateTime.fromMillisecondsSinceEpoch(expiresAtMilliseconds);

    return AuthSession(
      accessToken: accessToken,
      tokenType: tokenType,
      refreshToken: refreshToken,
      expiresAt: expiresAt,
    );
  }

  @override
  Future<void> write(AuthSession session) async {
    await _store.write(_accessTokenKey, session.accessToken);
    await _store.write(_tokenTypeKey, session.tokenType);

    if (session.refreshToken == null || session.refreshToken!.trim().isEmpty) {
      await _store.delete(_refreshTokenKey);
    } else {
      await _store.write(_refreshTokenKey, session.refreshToken!);
    }

    if (session.expiresAt == null) {
      await _store.delete(_expiresAtKey);
    } else {
      await _store.write(
        _expiresAtKey,
        session.expiresAt!.millisecondsSinceEpoch.toString(),
      );
    }
  }

  @override
  Future<void> clear() async {
    await _store.delete(_accessTokenKey);
    await _store.delete(_refreshTokenKey);
    await _store.delete(_tokenTypeKey);
    await _store.delete(_expiresAtKey);
  }
}

final Provider<KeyValueStore> keyValueStoreProvider = Provider<KeyValueStore>(
  (Ref ref) {
    final useMocks = ref.watch(appEnvironmentProvider).useMocks;
    if (useMocks) {
      return InMemoryKeyValueStore();
    }

    return FlutterSecureKeyValueStore(
      const FlutterSecureStorage(
        aOptions: AndroidOptions(
          encryptedSharedPreferences: true,
        ),
        iOptions: IOSOptions(
          accessibility: KeychainAccessibility.first_unlock,
        ),
      ),
    );
  },
);

final Provider<AuthSessionStore> authSessionStoreProvider =
    Provider<AuthSessionStore>(
  (Ref ref) {
    final store = ref.watch(keyValueStoreProvider);
    return KeyValueAuthSessionStore(store);
  },
);
