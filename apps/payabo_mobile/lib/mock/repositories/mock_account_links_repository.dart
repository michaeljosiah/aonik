import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/account_links_repository.dart';
import '../mock_behavior.dart';

class MockAccountLinksRepository implements AccountLinksRepository {
  MockAccountLinksRepository({
    this.demoDataMode = DemoDataMode.populated,
  }) : _accounts = demoDataMode == DemoDataMode.fresh
            ? <AccountLinkItem>[]
            : _seedAccounts().toList(growable: true);

  final DemoDataMode demoDataMode;
  final List<AccountLinkItem> _accounts;
  final Map<String, AccountLinkSession> _sessions =
      <String, AccountLinkSession>{};

  int _connectionSequence = 0;

  @override
  Future<AccountLinksSummary> getSummary() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('accountLinks.getSummary');

    return AccountLinksSummary(
      accounts: List<AccountLinkItem>.from(_accounts),
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
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('accountLinks.createSession');

    final int now = DateTime.now().microsecondsSinceEpoch;
    final String sessionId = 'mock-session-$now';
    final AccountLinkSession session = AccountLinkSession(
      sessionId: sessionId,
      provider: provider,
      providerDisplayName: provider,
      mode: mode,
      connectionId: connectionId,
      launchToken: 'mock-launch-token-$now',
      expiresAt: DateTime.now().add(const Duration(minutes: 30)),
    );

    _sessions[sessionId] = session;
    return session;
  }

  @override
  Future<AccountLinkExchangeResult> exchangeSession({
    required String sessionId,
    required String temporaryCode,
  }) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('accountLinks.exchangeSession');

    final AccountLinkSession? session = _sessions.remove(sessionId);
    if (session == null) {
      throw StateError('Unknown mock session: $sessionId');
    }

    if (session.mode == 'update') {
      final String? connectionId = session.connectionId;
      if (connectionId == null) {
        throw StateError('Reconnect session missing connection id.');
      }

      var updatedCount = 0;
      for (var index = 0; index < _accounts.length; index++) {
        final AccountLinkItem item = _accounts[index];
        if (item.connectionId != connectionId) {
          continue;
        }

        _accounts[index] = item.copyWith(
          status: AccountLinkStatus.connected,
          statusLabel: 'Connected',
          statusDetail:
              'Connection restored. Spend can use fresh transactions and balances again.',
          providerCode: session.provider,
          providerLabel: session.providerDisplayName,
          lastSyncedLabel: 'Synced just now',
        );
        updatedCount += 1;
      }

      final AccountLinkItem? reconnected =
          _accounts.cast<AccountLinkItem?>().firstWhere(
                (AccountLinkItem? item) => item?.connectionId == connectionId,
                orElse: () => null,
              );

      return AccountLinkExchangeResult(
        connectionId: connectionId,
        provider: session.provider,
        providerDisplayName: session.providerDisplayName,
        institutionName: reconnected?.institutionName ?? 'Connected bank',
        linkedAccountCount: updatedCount,
        status: 'Connected',
      );
    }

    final String suffix = temporaryCode.trim().isEmpty
        ? '${DateTime.now().millisecondsSinceEpoch}'
        : temporaryCode
            .trim()
            .replaceAll(RegExp(r'[^a-zA-Z0-9]'), '')
            .toLowerCase();
    final String shortSuffix =
        suffix.length <= 6 ? suffix : suffix.substring(0, 6);
    _connectionSequence += 1;
    final String connectionId = 'mock-connection-$_connectionSequence';

    final List<AccountLinkItem> newAccounts = <AccountLinkItem>[
      AccountLinkItem(
        id: 'linked-current-$_connectionSequence',
        name: 'Connected current',
        institutionName: 'Plaid Sandbox Bank',
        accountTypeLabel: 'Current',
        currencyCode: 'GBP',
        source: AccountLinkSource.linked,
        status: AccountLinkStatus.connected,
        statusLabel: 'Connected',
        statusDetail:
            'Transactions and balances are now flowing into Spend through the secure connection.',
        sourceLabel: 'Linked account',
        connectionId: connectionId,
        providerCode: session.provider,
        balanceLabel: '£2,960.40',
        maskedIdentifier:
            '.... ${_last4FromSuffix(shortSuffix, fallback: '41')}42',
        providerLabel: session.providerDisplayName,
        lastSyncedLabel: 'Synced just now',
      ),
      AccountLinkItem(
        id: 'linked-saver-$_connectionSequence',
        name: 'Connected saver',
        institutionName: 'Plaid Sandbox Bank',
        accountTypeLabel: 'Savings',
        currencyCode: 'GBP',
        source: AccountLinkSource.linked,
        status: AccountLinkStatus.connected,
        statusLabel: 'Connected',
        statusDetail:
            'Savings activity is ready to improve budgets, categories, and account rollups.',
        sourceLabel: 'Linked account',
        connectionId: connectionId,
        providerCode: session.provider,
        balanceLabel: '£7,125.00',
        maskedIdentifier:
            '.... ${_last4FromSuffix(shortSuffix, fallback: '77')}01',
        providerLabel: session.providerDisplayName,
        lastSyncedLabel: 'Synced just now',
      ),
    ];

    _accounts.insertAll(0, newAccounts);

    return AccountLinkExchangeResult(
      connectionId: connectionId,
      provider: session.provider,
      providerDisplayName: session.providerDisplayName,
      institutionName: 'Plaid Sandbox Bank',
      linkedAccountCount: newAccounts.length,
      status: 'Connected',
    );
  }

  @override
  Future<AccountLinkActionResult> refreshConnection({
    required String connectionId,
  }) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('accountLinks.refreshConnection');

    var refreshedCount = 0;
    String institutionName = 'Connected bank';
    for (var index = 0; index < _accounts.length; index++) {
      final AccountLinkItem item = _accounts[index];
      if (item.connectionId != connectionId) {
        continue;
      }

      institutionName = item.institutionName;
      _accounts[index] = item.copyWith(
        status: item.needsReconnect ? item.status : AccountLinkStatus.connected,
        statusLabel: item.needsReconnect ? item.statusLabel : 'Connected',
        statusDetail: item.needsReconnect
            ? item.statusDetail
            : 'Fresh transactions and balances were just pulled into Spend.',
        lastSyncedLabel:
            item.needsReconnect ? item.lastSyncedLabel : 'Synced just now',
      );
      refreshedCount += 1;
    }

    if (refreshedCount == 0) {
      throw StateError('Unknown mock connection: $connectionId');
    }

    return AccountLinkActionResult(
      connectionId: connectionId,
      provider: 'Plaid',
      providerDisplayName: 'Plaid',
      institutionName: institutionName,
      linkedAccountCount: refreshedCount,
      status: 'Connected',
    );
  }

  @override
  Future<AccountLinkActionResult> disconnectConnection({
    required String connectionId,
  }) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('accountLinks.disconnectConnection');

    final List<AccountLinkItem> removedAccounts = _accounts
        .where((AccountLinkItem item) => item.connectionId == connectionId)
        .toList(growable: false);

    if (removedAccounts.isEmpty) {
      throw StateError('Unknown mock connection: $connectionId');
    }

    _accounts.removeWhere(
        (AccountLinkItem item) => item.connectionId == connectionId);

    return AccountLinkActionResult(
      connectionId: connectionId,
      provider: removedAccounts.first.providerCode ?? 'Plaid',
      providerDisplayName: removedAccounts.first.providerLabel ?? 'Plaid',
      institutionName: removedAccounts.first.institutionName,
      linkedAccountCount: removedAccounts.length,
      status: 'Disconnected',
    );
  }

  static Iterable<AccountLinkItem> _seedAccounts() {
    return const <AccountLinkItem>[
      AccountLinkItem(
        id: 'everyday-current',
        name: 'Everyday current',
        institutionName: 'Sterling Bank',
        accountTypeLabel: 'Current',
        currencyCode: 'GBP',
        source: AccountLinkSource.linked,
        status: AccountLinkStatus.connected,
        statusLabel: 'Connected',
        statusDetail:
            'Transactions and balances are flowing into Spend automatically.',
        sourceLabel: 'Linked account',
        connectionId: 'mock-connection-sterling',
        providerCode: 'Plaid',
        balanceLabel: '£3,842.16',
        maskedIdentifier: '.... 1842',
        providerLabel: 'Plaid',
        lastSyncedLabel: 'Synced 2 mins ago',
      ),
      AccountLinkItem(
        id: 'rainy-day-saver',
        name: 'Rainy day saver',
        institutionName: 'Northline Savings',
        accountTypeLabel: 'Savings',
        currencyCode: 'GBP',
        source: AccountLinkSource.linked,
        status: AccountLinkStatus.syncing,
        statusLabel: 'Syncing',
        statusDetail:
            'A fresh balance update is on the way for this savings pocket.',
        sourceLabel: 'Linked account',
        connectionId: 'mock-connection-northline',
        providerCode: 'Plaid',
        balanceLabel: '£6,240.00',
        maskedIdentifier: '.... 8801',
        providerLabel: 'Plaid',
        lastSyncedLabel: 'Sync in progress',
      ),
      AccountLinkItem(
        id: 'bills-card',
        name: 'Bills card',
        institutionName: 'Harbour Credit',
        accountTypeLabel: 'Credit Card',
        currencyCode: 'GBP',
        source: AccountLinkSource.linked,
        status: AccountLinkStatus.actionRequired,
        statusLabel: 'Action required',
        statusDetail:
            'Reconnect this account so bill forecasts and merchant views stay accurate.',
        sourceLabel: 'Linked account',
        connectionId: 'mock-connection-harbour',
        providerCode: 'Plaid',
        balanceLabel: '£1,090.30',
        maskedIdentifier: '.... 9031',
        providerLabel: 'Plaid',
        lastSyncedLabel: 'Reconnect needed',
      ),
      AccountLinkItem(
        id: 'travel-cash',
        name: 'Travel cash wallet',
        institutionName: 'Added in Payabo',
        accountTypeLabel: 'Cash Wallet',
        currencyCode: 'GBP',
        source: AccountLinkSource.manual,
        status: AccountLinkStatus.manual,
        statusLabel: 'Manual',
        statusDetail:
            'Track spending from cash or off-platform balances without linking a bank.',
        sourceLabel: 'Manual account',
        balanceLabel: '£120.00',
        providerLabel: 'Manual entry',
        lastSyncedLabel: 'Added manually',
      ),
    ];
  }

  String _last4FromSuffix(String suffix, {required String fallback}) {
    final String digitsOnly = suffix.replaceAll(RegExp(r'[^0-9]'), '');
    if (digitsOnly.isEmpty) {
      return fallback;
    }

    final String padded = digitsOnly.padLeft(2, '0');
    return padded.substring(0, 2);
  }
}
