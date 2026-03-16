enum AccountLinkSource {
  linked,
  manual,
}

enum AccountLinkStatus {
  connected,
  syncing,
  actionRequired,
  manual,
  archived,
}

class AccountLinkItem {
  const AccountLinkItem({
    required this.id,
    required this.name,
    required this.institutionName,
    required this.accountTypeLabel,
    required this.currencyCode,
    required this.source,
    required this.status,
    required this.statusLabel,
    required this.statusDetail,
    required this.sourceLabel,
    this.connectionId,
    this.providerCode,
    this.balanceLabel,
    this.maskedIdentifier,
    this.providerLabel,
    this.lastSyncedLabel,
  });

  final String id;
  final String name;
  final String institutionName;
  final String accountTypeLabel;
  final String currencyCode;
  final AccountLinkSource source;
  final AccountLinkStatus status;
  final String statusLabel;
  final String statusDetail;
  final String sourceLabel;
  final String? connectionId;
  final String? providerCode;
  final String? balanceLabel;
  final String? maskedIdentifier;
  final String? providerLabel;
  final String? lastSyncedLabel;

  bool get needsReconnect => status == AccountLinkStatus.actionRequired;

  bool get hasProvider => (providerCode?.trim().isNotEmpty ?? false);

  bool get canReconnect =>
      source == AccountLinkSource.linked &&
      connectionId != null &&
      hasProvider &&
      needsReconnect;

  bool get canRefresh =>
      source == AccountLinkSource.linked &&
      connectionId != null &&
      status != AccountLinkStatus.archived &&
      !needsReconnect;

  bool get canDisconnect =>
      source == AccountLinkSource.linked && connectionId != null;

  AccountLinkItem copyWith({
    String? id,
    String? name,
    String? institutionName,
    String? accountTypeLabel,
    String? currencyCode,
    AccountLinkSource? source,
    AccountLinkStatus? status,
    String? statusLabel,
    String? statusDetail,
    String? sourceLabel,
    Object? connectionId = _copySentinel,
    Object? providerCode = _copySentinel,
    Object? balanceLabel = _copySentinel,
    Object? maskedIdentifier = _copySentinel,
    Object? providerLabel = _copySentinel,
    Object? lastSyncedLabel = _copySentinel,
  }) {
    return AccountLinkItem(
      id: id ?? this.id,
      name: name ?? this.name,
      institutionName: institutionName ?? this.institutionName,
      accountTypeLabel: accountTypeLabel ?? this.accountTypeLabel,
      currencyCode: currencyCode ?? this.currencyCode,
      source: source ?? this.source,
      status: status ?? this.status,
      statusLabel: statusLabel ?? this.statusLabel,
      statusDetail: statusDetail ?? this.statusDetail,
      sourceLabel: sourceLabel ?? this.sourceLabel,
      connectionId: connectionId == _copySentinel
          ? this.connectionId
          : connectionId as String?,
      providerCode: providerCode == _copySentinel
          ? this.providerCode
          : providerCode as String?,
      balanceLabel: balanceLabel == _copySentinel
          ? this.balanceLabel
          : balanceLabel as String?,
      maskedIdentifier: maskedIdentifier == _copySentinel
          ? this.maskedIdentifier
          : maskedIdentifier as String?,
      providerLabel: providerLabel == _copySentinel
          ? this.providerLabel
          : providerLabel as String?,
      lastSyncedLabel: lastSyncedLabel == _copySentinel
          ? this.lastSyncedLabel
          : lastSyncedLabel as String?,
    );
  }
}

class AccountLinksSummary {
  const AccountLinksSummary({
    required this.accounts,
  });

  final List<AccountLinkItem> accounts;

  int get linkedCount => accounts
      .where((AccountLinkItem item) => item.source == AccountLinkSource.linked)
      .length;

  int get manualCount => accounts
      .where((AccountLinkItem item) => item.source == AccountLinkSource.manual)
      .length;

  int get attentionCount => accounts
      .where(
        (AccountLinkItem item) =>
            item.status == AccountLinkStatus.actionRequired,
      )
      .length;

  bool get hasAccounts => accounts.isNotEmpty;
}

class AccountLinkSession {
  const AccountLinkSession({
    required this.sessionId,
    required this.provider,
    required this.providerDisplayName,
    required this.mode,
    this.connectionId,
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
}

class AccountLinkConnectionResult {
  const AccountLinkConnectionResult({
    required this.connectionId,
    required this.provider,
    required this.providerDisplayName,
    required this.institutionName,
    required this.linkedAccountCount,
    this.status,
  });

  final String connectionId;
  final String provider;
  final String providerDisplayName;
  final String institutionName;
  final int linkedAccountCount;
  final String? status;
}

typedef AccountLinkExchangeResult = AccountLinkConnectionResult;
typedef AccountLinkActionResult = AccountLinkConnectionResult;

abstract class AccountLinksRepository {
  Future<AccountLinksSummary> getSummary();

  Future<AccountLinkSession> createSession({
    String provider = 'Plaid',
    String mode = 'connect',
    String? connectionId,
    String? androidPackageName,
    String? redirectUri,
    String? countryCode,
  });

  Future<AccountLinkExchangeResult> exchangeSession({
    required String sessionId,
    required String temporaryCode,
  });

  Future<AccountLinkActionResult> refreshConnection({
    required String connectionId,
  });

  Future<AccountLinkActionResult> disconnectConnection({
    required String connectionId,
  });
}

const Object _copySentinel = Object();
