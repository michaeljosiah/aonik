import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/app/demo/demo_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/data/repositories/account_links_repository.dart';
import 'package:payabo_mobile/data/repositories/repository_providers.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_accounts_screen.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_accounts_state.dart';
import 'package:payabo_mobile/shared/theme/payabo_theme.dart';
import 'package:shared_preferences/shared_preferences.dart';

class _RecordingAccountLinksRepository implements AccountLinksRepository {
  _RecordingAccountLinksRepository({
    List<AccountLinkItem> accounts = const <AccountLinkItem>[],
  }) : _accounts = List<AccountLinkItem>.from(accounts);

  final List<AccountLinkItem> _accounts;
  String? lastCreateSessionProvider;
  String? lastCreateSessionMode;
  String? lastCreateSessionConnectionId;
  String? lastCreateSessionAndroidPackageName;
  String? lastCreateSessionCountryCode;

  @override
  Future<AccountLinksSummary> getSummary() async {
    return AccountLinksSummary(
      accounts: List<AccountLinkItem>.unmodifiable(_accounts),
    );
  }

  @override
  Future<AccountLinkSession> createSession({
    String provider = 'Plaid',
    String mode = 'connect',
    String? connectionId,
    String? androidPackageName,
    String? redirectUri,
    String? countryCode,
  }) async {
    lastCreateSessionProvider = provider;
    lastCreateSessionMode = mode;
    lastCreateSessionConnectionId = connectionId;
    lastCreateSessionAndroidPackageName = androidPackageName;
    lastCreateSessionCountryCode = countryCode;

    return AccountLinkSession(
      sessionId: 'test-session',
      provider: provider,
      providerDisplayName: provider,
      mode: mode,
      connectionId: connectionId,
      launchToken: 'test-launch-token',
      expiresAt: DateTime.now().add(const Duration(minutes: 30)),
    );
  }

  @override
  Future<AccountLinkExchangeResult> exchangeSession({
    required String sessionId,
    required String temporaryCode,
  }) async {
    if (lastCreateSessionMode == 'update') {
      final AccountLinkItem source = _accounts.firstWhere(
        (AccountLinkItem item) =>
            item.connectionId == lastCreateSessionConnectionId,
      );

      return AccountLinkExchangeResult(
        connectionId: lastCreateSessionConnectionId ?? 'test-connection',
        provider: lastCreateSessionProvider ?? 'Unknown',
        providerDisplayName: lastCreateSessionProvider ?? 'Unknown',
        institutionName: source.institutionName,
        linkedAccountCount: 1,
        status: 'Connected',
      );
    }

    return AccountLinkExchangeResult(
      connectionId: 'test-connection',
      provider: lastCreateSessionProvider ?? 'Unknown',
      providerDisplayName: lastCreateSessionProvider ?? 'Unknown',
      institutionName: 'Test Bank',
      linkedAccountCount: 1,
      status: 'Connected',
    );
  }

  @override
  Future<AccountLinkActionResult> refreshConnection({
    required String connectionId,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<AccountLinkActionResult> disconnectConnection({
    required String connectionId,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<CreateManualAccountResult> createManualAccount(
    CreateManualAccountRequest request,
  ) {
    throw UnimplementedError();
  }

  @override
  Future<void> deleteManualAccount(String accountId) {
    throw UnimplementedError();
  }
}

class _DelayedRefreshAccountLinksRepository
    extends _RecordingAccountLinksRepository {
  _DelayedRefreshAccountLinksRepository({
    required this.refreshedSummary,
  });

  final Completer<AccountLinksSummary> refreshedSummary;
  int _summaryRequestCount = 0;

  @override
  Future<AccountLinksSummary> getSummary() async {
    _summaryRequestCount += 1;
    if (_summaryRequestCount == 1) {
      return const AccountLinksSummary(accounts: <AccountLinkItem>[]);
    }

    return refreshedSummary.future;
  }
}

class _ImmediateAccountLinkLauncher implements AccountLinkLauncher {
  const _ImmediateAccountLinkLauncher({this.native = false});

  final bool native;

  @override
  bool get isNativeProviderFlow => native;

  @override
  bool get supportsOAuthResume => false;

  @override
  bool get supportsEmbeddedLink => false;

  @override
  String get experienceLabel => native ? 'Plaid Link' : 'Test handoff';

  @override
  Future<AccountLinkLaunchResult?> launch(
    AccountLinkLaunchRequest request,
  ) async {
    return const AccountLinkLaunchResult(temporaryCode: 'temporary-code');
  }

  @override
  Future<AccountLinkLaunchResult?> resume(
    AccountLinkResumeRequest request,
  ) {
    throw UnimplementedError();
  }
}

class _ThrowingAccountLinkLauncher implements AccountLinkLauncher {
  const _ThrowingAccountLinkLauncher(this.message);

  final String message;

  @override
  bool get isNativeProviderFlow => false;

  @override
  bool get supportsOAuthResume => false;

  @override
  bool get supportsEmbeddedLink => false;

  @override
  String get experienceLabel => 'Test handoff';

  @override
  Future<AccountLinkLaunchResult?> launch(
    AccountLinkLaunchRequest request,
  ) async {
    throw AccountLinkLaunchException(message);
  }

  @override
  Future<AccountLinkLaunchResult?> resume(
    AccountLinkResumeRequest request,
  ) {
    throw UnimplementedError();
  }
}

Widget _buildProviderTestApp({
  required AccountLinksRepository repository,
  required AccountLinkLauncher launcher,
  required AppEnvironment environment,
  DemoDataMode demoDataMode = DemoDataMode.populated,
}) {
  SharedPreferences.setMockInitialValues(<String, Object>{});

  return ProviderScope(
    overrides: [
      appEnvironmentProvider.overrideWithValue(environment),
      initialDemoDataModeProvider.overrideWithValue(demoDataMode),
      accountLinksRepositoryProvider.overrideWithValue(repository),
      accountLinkLauncherProvider.overrideWithValue(launcher),
    ],
    child: MaterialApp(
      theme: buildPayaboTheme(),
      home: const SpendingAccountsScreen(),
    ),
  );
}

void main() {
  testWidgets('connect flow uses the configured account-link provider', (
    WidgetTester tester,
  ) async {
    final repository = _RecordingAccountLinksRepository();

    await tester.pumpWidget(
      _buildProviderTestApp(
        repository: repository,
        launcher: const _ImmediateAccountLinkLauncher(),
        demoDataMode: DemoDataMode.fresh,
        environment: const AppEnvironment(
          flavor: AppFlavor.dev,
          useMocks: true,
          apiBaseUrl: 'https://api.dev.payabo.local',
          accountLinkProvider: 'TrueLayer',
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('accounts-connect-primary')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('accounts-connect-continue')));
    await tester.pumpAndSettle();

    expect(repository.lastCreateSessionProvider, 'TrueLayer');
    expect(repository.lastCreateSessionMode, 'connect');
  });

  testWidgets('provider-less action-required items do not show reconnect', (
    WidgetTester tester,
  ) async {
    final repository = _RecordingAccountLinksRepository(
      accounts: const <AccountLinkItem>[
        AccountLinkItem(
          id: 'unresolved-provider-account',
          name: 'Needs reconnect',
          institutionName: 'Fallback Bank',
          accountTypeLabel: 'Current',
          currencyCode: 'GBP',
          source: AccountLinkSource.linked,
          status: AccountLinkStatus.actionRequired,
          statusLabel: 'Action required',
          statusDetail: 'Reconnect required.',
          sourceLabel: 'Linked account',
          connectionId: 'connection-1',
          providerCode: null,
          providerLabel: null,
        ),
      ],
    );

    await tester.pumpWidget(
      _buildProviderTestApp(
        repository: repository,
        launcher: const _ImmediateAccountLinkLauncher(),
        environment: const AppEnvironment(
          flavor: AppFlavor.dev,
          useMocks: true,
          apiBaseUrl: 'https://api.dev.payabo.local',
          accountLinkProvider: 'TrueLayer',
        ),
      ),
    );
    await tester.pumpAndSettle();

    final Finder primaryScrollable = find.byType(Scrollable).last;
    final Finder accountCard =
        find.byKey(const Key('account-card-unresolved-provider-account'));

    await tester.scrollUntilVisible(
      accountCard,
      220,
      scrollable: primaryScrollable,
    );

    final Finder reconnectButton = find.descendant(
      of: accountCard,
      matching: find.text('RECONNECT'),
    );

    expect(reconnectButton, findsNothing);
    expect(repository.lastCreateSessionProvider, isNull);
  });

  testWidgets('connect flow shows a loading indicator while summary refreshes',
      (
    WidgetTester tester,
  ) async {
    final Completer<AccountLinksSummary> refreshedSummary =
        Completer<AccountLinksSummary>();
    final repository = _DelayedRefreshAccountLinksRepository(
      refreshedSummary: refreshedSummary,
    );

    await tester.pumpWidget(
      _buildProviderTestApp(
        repository: repository,
        launcher: const _ImmediateAccountLinkLauncher(),
        demoDataMode: DemoDataMode.fresh,
        environment: const AppEnvironment(
          flavor: AppFlavor.dev,
          useMocks: true,
          apiBaseUrl: 'https://api.dev.payabo.local',
          accountLinkProvider: 'TrueLayer',
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('accounts-connect-primary')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('accounts-connect-continue')));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));

    expect(find.text('Updating linked accounts...'), findsOneWidget);

    refreshedSummary.complete(
      const AccountLinksSummary(accounts: <AccountLinkItem>[]),
    );
    await tester.pumpAndSettle();
  });

  testWidgets('connect flow surfaces launcher errors in the sheet', (
    WidgetTester tester,
  ) async {
    final repository = _RecordingAccountLinksRepository();

    await tester.pumpWidget(
      _buildProviderTestApp(
        repository: repository,
        launcher: const _ThrowingAccountLinkLauncher(
          'The secure bank-link session could not start.',
        ),
        demoDataMode: DemoDataMode.fresh,
        environment: const AppEnvironment(
          flavor: AppFlavor.dev,
          useMocks: true,
          apiBaseUrl: 'https://api.dev.payabo.local',
          accountLinkProvider: 'TrueLayer',
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('accounts-connect-primary')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('accounts-connect-continue')));
    await tester.pumpAndSettle();

    expect(
      find.text('The secure bank-link session could not start.'),
      findsOneWidget,
    );
    expect(find.text('Connect bank account'), findsOneWidget);
  });

  test('native launcher sessions include the configured Android package name',
      () async {
    final TargetPlatform? previousPlatform = debugDefaultTargetPlatformOverride;
    debugDefaultTargetPlatformOverride = TargetPlatform.android;
    addTearDown(() {
      debugDefaultTargetPlatformOverride = previousPlatform;
    });

    final repository = _RecordingAccountLinksRepository();
    final ProviderContainer container = ProviderContainer(
      overrides: [
        appEnvironmentProvider.overrideWithValue(
          const AppEnvironment(
            flavor: AppFlavor.dev,
            useMocks: false,
            apiBaseUrl: 'https://api.dev.payabo.local',
            accountLinkProvider: 'Plaid',
            accountLinkAndroidPackageName: 'com.example.payabo',
          ),
        ),
        isDemoProvider.overrideWith((ref) => false),
        accountLinksRepositoryProvider.overrideWithValue(repository),
        accountLinkLauncherProvider.overrideWithValue(
          const _ImmediateAccountLinkLauncher(native: true),
        ),
      ],
    );
    addTearDown(container.dispose);

    final AccountLinkFlowController controller =
        container.read(accountLinkFlowControllerProvider.notifier);

    await controller.connect(provider: 'Plaid');

    expect(
      repository.lastCreateSessionAndroidPackageName,
      'com.example.payabo',
    );
  });

  test('selected country is forwarded when creating a Plaid session', () async {
    final repository = _RecordingAccountLinksRepository();
    final ProviderContainer container = ProviderContainer(
      overrides: [
        appEnvironmentProvider.overrideWithValue(
          const AppEnvironment(
            flavor: AppFlavor.dev,
            useMocks: false,
            apiBaseUrl: 'https://api.dev.payabo.local',
            accountLinkProvider: 'Plaid',
          ),
        ),
        isDemoProvider.overrideWith((ref) => false),
        accountLinksRepositoryProvider.overrideWithValue(repository),
        accountLinkLauncherProvider.overrideWithValue(
          const _ImmediateAccountLinkLauncher(native: true),
        ),
      ],
    );
    addTearDown(container.dispose);

    final AccountLinkFlowController controller =
        container.read(accountLinkFlowControllerProvider.notifier);

    await controller.connect(provider: 'Plaid', countryCode: 'NG');

    expect(repository.lastCreateSessionCountryCode, 'NG');
  });

  test('native Plaid launcher is selected on Android when not in demo mode',
      () {
    final TargetPlatform? previousPlatform = debugDefaultTargetPlatformOverride;
    debugDefaultTargetPlatformOverride = TargetPlatform.android;
    addTearDown(() {
      debugDefaultTargetPlatformOverride = previousPlatform;
    });

    final ProviderContainer container = ProviderContainer(
      overrides: [
        appEnvironmentProvider.overrideWithValue(
          const AppEnvironment(
            flavor: AppFlavor.dev,
            useMocks: false,
            apiBaseUrl: 'https://api.dev.payabo.local',
            accountLinkProvider: 'Plaid',
          ),
        ),
        isDemoProvider.overrideWith((ref) => false),
      ],
    );
    addTearDown(container.dispose);

    expect(
      container.read(accountLinkLauncherProvider),
      isA<PlaidAccountLinkLauncher>(),
    );
  });
}
