import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:payabo_mobile/data/repositories/account_links_repository.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_account_link_persistence.dart';
import 'package:shared_preferences/shared_preferences.dart';

const String _storageKey = 'payabo.account_link.pending_session.v1';

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues(<String, Object>{});
  });

  test('write persists a session with enough remaining lifetime', () async {
    final DateTime now = DateTime.utc(2026, 3, 15, 12);
    final SharedPreferencesAccountLinkSessionPersistence persistence =
        SharedPreferencesAccountLinkSessionPersistence(
      now: () => now,
      minimumValidityWindow: const Duration(seconds: 30),
    );

    await persistence.write(
      AccountLinkSession(
        sessionId: 'session-1',
        provider: 'Plaid',
        providerDisplayName: 'Plaid',
        mode: 'connect',
        connectionId: 'connection-1',
        launchToken: 'launch-token',
        expiresAt: now.add(const Duration(minutes: 5)),
      ),
    );

    final PersistedAccountLinkSessionSnapshot? snapshot =
        await persistence.read();

    expect(snapshot, isNotNull);
    expect(snapshot!.sessionId, 'session-1');
    expect(snapshot.provider, 'Plaid');
    expect(snapshot.connectionId, 'connection-1');
  });

  test('write skips sessions that are already expired or too close to expiry',
      () async {
    final DateTime now = DateTime.utc(2026, 3, 15, 12);
    final SharedPreferencesAccountLinkSessionPersistence persistence =
        SharedPreferencesAccountLinkSessionPersistence(
      now: () => now,
      minimumValidityWindow: const Duration(seconds: 30),
    );

    await persistence.write(
      AccountLinkSession(
        sessionId: 'session-2',
        provider: 'Plaid',
        providerDisplayName: 'Plaid',
        mode: 'connect',
        launchToken: 'launch-token',
        expiresAt: now.add(const Duration(seconds: 10)),
      ),
    );

    final SharedPreferences prefs = await SharedPreferences.getInstance();

    expect(await persistence.read(), isNull);
    expect(prefs.getString(_storageKey), isNull);
  });

  test('read clears expired sessions from storage', () async {
    final DateTime now = DateTime.utc(2026, 3, 15, 12);
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.setString(
      _storageKey,
      jsonEncode(<String, dynamic>{
        'sessionId': 'session-3',
        'provider': 'Plaid',
        'providerDisplayName': 'Plaid',
        'mode': 'connect',
        'connectionId': 'connection-3',
        'launchToken': 'launch-token',
        'expiresAt': now.subtract(const Duration(minutes: 1)).toIso8601String(),
      }),
    );

    final SharedPreferencesAccountLinkSessionPersistence persistence =
        SharedPreferencesAccountLinkSessionPersistence(
      now: () => now,
      minimumValidityWindow: const Duration(seconds: 30),
    );

    expect(await persistence.read(), isNull);
    expect(prefs.getString(_storageKey), isNull);
  });
}
