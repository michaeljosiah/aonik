import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../../data/repositories/account_links_repository.dart';

class PersistedAccountLinkSessionSnapshot {
  const PersistedAccountLinkSessionSnapshot({
    required this.sessionId,
    required this.provider,
    required this.providerDisplayName,
    required this.mode,
    required this.connectionId,
    required this.launchToken,
    required this.expiresAt,
  });

  final String sessionId;
  final String provider;
  final String providerDisplayName;
  final String mode;
  final String? connectionId;
  final String launchToken;
  final DateTime expiresAt;

  AccountLinkSession toSession() {
    return AccountLinkSession(
      sessionId: sessionId,
      provider: provider,
      providerDisplayName: providerDisplayName,
      mode: mode,
      connectionId: connectionId,
      launchToken: launchToken,
      expiresAt: expiresAt,
    );
  }

  static PersistedAccountLinkSessionSnapshot fromSession(
    AccountLinkSession session,
  ) {
    return PersistedAccountLinkSessionSnapshot(
      sessionId: session.sessionId,
      provider: session.provider,
      providerDisplayName: session.providerDisplayName,
      mode: session.mode,
      connectionId: session.connectionId,
      launchToken: session.launchToken,
      expiresAt: session.expiresAt,
    );
  }
}

abstract class AccountLinkSessionPersistence {
  Future<PersistedAccountLinkSessionSnapshot?> read();

  Future<void> write(AccountLinkSession session);

  Future<void> clear();
}

class SharedPreferencesAccountLinkSessionPersistence
    implements AccountLinkSessionPersistence {
  SharedPreferencesAccountLinkSessionPersistence({
    DateTime Function()? now,
    Duration minimumValidityWindow = const Duration(seconds: 30),
  })  : _now = now ?? DateTime.now,
        _minimumValidityWindow = minimumValidityWindow;

  static const String _storageKey = 'payabo.account_link.pending_session.v1';
  final DateTime Function() _now;
  final Duration _minimumValidityWindow;

  @override
  Future<PersistedAccountLinkSessionSnapshot?> read() async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    final String? raw = prefs.getString(_storageKey);
    if (raw == null || raw.isEmpty) {
      return null;
    }

    final Object? decoded = jsonDecode(raw);
    if (decoded is! Map<String, dynamic>) {
      return null;
    }

    final DateTime? expiresAt = DateTime.tryParse(
      decoded['expiresAt'] as String? ?? '',
    );
    if (expiresAt == null) {
      return null;
    }

    if (!_isStillValid(expiresAt)) {
      await prefs.remove(_storageKey);
      return null;
    }

    return PersistedAccountLinkSessionSnapshot(
      sessionId: decoded['sessionId'] as String? ?? '',
      provider: decoded['provider'] as String? ?? 'Plaid',
      providerDisplayName: decoded['providerDisplayName'] as String? ?? 'Plaid',
      mode: decoded['mode'] as String? ?? 'connect',
      connectionId: decoded['connectionId'] as String?,
      launchToken: decoded['launchToken'] as String? ?? '',
      expiresAt: expiresAt,
    );
  }

  @override
  Future<void> write(AccountLinkSession session) async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    if (!_isStillValid(session.expiresAt)) {
      await prefs.remove(_storageKey);
      return;
    }

    final Map<String, dynamic> payload = <String, dynamic>{
      'sessionId': session.sessionId,
      'provider': session.provider,
      'providerDisplayName': session.providerDisplayName,
      'mode': session.mode,
      'connectionId': session.connectionId,
      'launchToken': session.launchToken,
      'expiresAt': session.expiresAt.toIso8601String(),
    };

    await prefs.setString(_storageKey, jsonEncode(payload));
  }

  @override
  Future<void> clear() async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.remove(_storageKey);
  }

  bool _isStillValid(DateTime expiresAt) {
    return expiresAt.isAfter(_now().add(_minimumValidityWindow));
  }
}

final Provider<AccountLinkSessionPersistence>
    accountLinkSessionPersistenceProvider =
    Provider<AccountLinkSessionPersistence>(
  (Ref ref) => SharedPreferencesAccountLinkSessionPersistence(),
);
