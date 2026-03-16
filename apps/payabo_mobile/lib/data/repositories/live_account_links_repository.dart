import 'dart:developer' as developer;

import 'package:dio/dio.dart';
import 'package:intl/intl.dart';

import '../api/api_exception.dart';
import 'account_links_repository.dart';

class LiveAccountLinksRepository implements AccountLinksRepository {
  LiveAccountLinksRepository({
    required Dio apiClient,
    String? dateLocale,
  })  : _apiClient = apiClient,
        _dateFormat = _createDateFormat(dateLocale);

  final Dio _apiClient;
  final DateFormat _dateFormat;

  @override
  Future<AccountLinksSummary> getSummary() async {
    try {
      final List<AccountLinkItem> accounts =
          await _loadSummaryFromAccountLinksEndpoint();

      return AccountLinksSummary(accounts: accounts);
    } on DioException catch (exception) {
      _logDioFailure('getSummary', exception);
      throw mapDioException(exception);
    }
  }

  Future<List<AccountLinkItem>> _loadSummaryFromAccountLinksEndpoint() async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/personal-finance/account-links/summary',
      );

      return (response.data ?? const <dynamic>[])
          .whereType<Map<Object?, Object?>>()
          .map(
            (Map<Object?, Object?> item) =>
                _mapSummaryItem(Map<String, dynamic>.from(item)),
          )
          .toList(growable: false)
        ..sort(_sortAccounts);
    } on DioException catch (exception) {
      final int? statusCode = exception.response?.statusCode;
      if (statusCode != 404) {
        rethrow;
      }

      final fallbackResponse = await _apiClient.get<List<dynamic>>(
        '/personal-finance/accounts',
      );

      return (fallbackResponse.data ?? const <dynamic>[])
          .whereType<Map<Object?, Object?>>()
          .map(
            (Map<Object?, Object?> account) =>
                _mapAccount(Map<String, dynamic>.from(account)),
          )
          .toList(growable: false)
        ..sort(_sortAccounts);
    }
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
    try {
      final response = await _apiClient.post<Map<String, dynamic>>(
        '/personal-finance/account-links/sessions',
        data: <String, dynamic>{
          'provider': provider,
          'mode': mode,
          if (connectionId != null) 'connectionId': connectionId,
          if (androidPackageName != null)
            'androidPackageName': androidPackageName,
          if (redirectUri != null && redirectUri.isNotEmpty)
            'redirectUri': redirectUri,
          if (countryCode != null && countryCode.isNotEmpty)
            'countryCode': countryCode,
        },
      );

      final Map<String, dynamic> payload =
          response.data ?? const <String, dynamic>{};

      return AccountLinkSession(
        sessionId: _readString(payload['accountLinkSessionId']) ?? '',
        provider: _readString(payload['provider']) ?? provider,
        providerDisplayName:
            _readString(payload['providerDisplayName']) ?? provider,
        mode: _readString(payload['mode']) ?? mode,
        connectionId: _readString(payload['connectionId']),
        launchToken: _readString(payload['launchToken']) ?? '',
        expiresAt: _parseDate(payload['expiresAt']) ??
            DateTime.now().add(const Duration(minutes: 30)),
      );
    } on DioException catch (exception) {
      _logDioFailure('createSession', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<AccountLinkExchangeResult> exchangeSession({
    required String sessionId,
    required String temporaryCode,
  }) async {
    try {
      final response = await _apiClient.post<Map<String, dynamic>>(
        '/personal-finance/account-links/exchanges',
        data: <String, dynamic>{
          'accountLinkSessionId': sessionId,
          'temporaryCode': temporaryCode,
        },
      );

      final Map<String, dynamic> payload =
          response.data ?? const <String, dynamic>{};
      final Map<String, dynamic> connection =
          (payload['connection'] as Map?)?.cast<String, dynamic>() ??
              const <String, dynamic>{};
      final List<dynamic> accounts =
          connection['accounts'] as List<dynamic>? ?? const <dynamic>[];

      return AccountLinkExchangeResult(
        connectionId: _readString(connection['connectionId']) ?? '',
        provider: _readString(connection['provider']) ?? 'Plaid',
        providerDisplayName:
            _readString(connection['providerDisplayName']) ?? 'Plaid',
        institutionName:
            _readString(connection['institutionName']) ?? 'Connected bank',
        linkedAccountCount: accounts.length,
        status: _readString(connection['status']),
      );
    } on DioException catch (exception) {
      _logDioFailure('exchangeSession', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<AccountLinkActionResult> refreshConnection({
    required String connectionId,
  }) async {
    try {
      final response = await _apiClient.post<Map<String, dynamic>>(
        '/personal-finance/account-links/$connectionId/refresh',
      );

      return _mapActionResponse(
        response.data ?? const <String, dynamic>{},
        defaultProvider: 'Plaid',
      );
    } on DioException catch (exception) {
      _logDioFailure('refreshConnection', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<AccountLinkActionResult> disconnectConnection({
    required String connectionId,
  }) async {
    try {
      final response = await _apiClient.post<Map<String, dynamic>>(
        '/personal-finance/account-links/$connectionId/disconnect',
      );

      return _mapActionResponse(
        response.data ?? const <String, dynamic>{},
        defaultProvider: 'Plaid',
      );
    } on DioException catch (exception) {
      _logDioFailure('disconnectConnection', exception);
      throw mapDioException(exception);
    }
  }

  AccountLinkActionResult _mapActionResponse(
    Map<String, dynamic> payload, {
    required String defaultProvider,
  }) {
    final Map<String, dynamic> connection =
        (payload['connection'] as Map?)?.cast<String, dynamic>() ??
            const <String, dynamic>{};
    final List<dynamic> accounts =
        connection['accounts'] as List<dynamic>? ?? const <dynamic>[];

    return AccountLinkActionResult(
      connectionId: _readString(connection['connectionId']) ?? '',
      provider: _readString(connection['provider']) ?? defaultProvider,
      providerDisplayName:
          _readString(connection['providerDisplayName']) ?? defaultProvider,
      institutionName:
          _readString(connection['institutionName']) ?? 'Connected bank',
      linkedAccountCount: accounts.length,
      status: _readString(connection['status']),
    );
  }

  AccountLinkItem _mapSummaryItem(Map<String, dynamic> payload) {
    final String id = _readString(payload['linkedAccountId']) ??
        _readString(payload['personalAccountId']) ??
        'account-${payload.hashCode}';
    final String name = _readString(payload['name']) ?? 'Untitled account';
    final String? sourceType = _readString(payload['sourceType']);
    final bool isLinked = sourceType?.toLowerCase() == 'linked';
    final String? provider = _readString(payload['provider']);
    final String? institutionName = _readString(payload['institutionName']);
    final String? subtype = _readString(payload['accountSubtype']);
    final String accountType = _readString(payload['accountType']) ?? 'Account';
    final String currencyCode =
        (_readString(payload['currency']) ?? 'GBP').toUpperCase();
    final String? last4 = _readString(payload['last4']);
    final String rawStatus = _readString(payload['status']) ?? 'Active';
    final String? lastSyncStatus = _readString(payload['lastSyncStatus']);
    final String? lastError = _readString(payload['lastError']);
    final DateTime? createdAt = _parseDate(payload['createdAt']);
    final DateTime? updatedAt = _parseDate(payload['updatedAt']);
    final DateTime? lastSyncedAt = _parseDate(payload['lastSyncedAt']);
    final AccountLinkStatus status = _mapStatusFromSummary(
      rawStatus,
      lastSyncStatus: lastSyncStatus,
      lastError: lastError,
      isLinked: isLinked,
    );

    return AccountLinkItem(
      id: id,
      name: name,
      institutionName: institutionName ?? 'Added in Payabo',
      accountTypeLabel: _formatAccountType(subtype ?? accountType),
      currencyCode: currencyCode,
      source: isLinked ? AccountLinkSource.linked : AccountLinkSource.manual,
      status: status,
      statusLabel: _statusLabel(status),
      statusDetail: _statusDetail(
        status,
        isLinked: isLinked,
        lastError: lastError,
        provider: provider,
      ),
      sourceLabel: isLinked ? 'Linked account' : 'Manual account',
      connectionId: _readString(payload['connectionId']),
      providerCode: provider,
      maskedIdentifier: last4 == null ? null : '.... $last4',
      providerLabel:
          isLinked ? (provider ?? 'Secure connection') : 'Manual entry',
      lastSyncedLabel: _lastSyncedLabelFromSummary(
        status,
        isLinked: isLinked,
        lastSyncedAt: lastSyncedAt,
        updatedAt: updatedAt,
        createdAt: createdAt,
      ),
    );
  }

  AccountLinkItem _mapAccount(Map<String, dynamic> payload) {
    final String id = _readString(payload['personalAccountId']) ??
        _readString(payload['id']) ??
        'account-${payload.hashCode}';
    final String name = _readString(payload['name']) ?? 'Untitled account';
    final String? institutionName = _readString(payload['institutionName']);
    final String? externalReference = _readString(payload['externalReference']);
    final String? subtype = _readString(payload['accountSubtype']);
    final String accountType = _readString(payload['accountType']) ?? 'Account';
    final String currencyCode =
        (_readString(payload['currency']) ?? 'GBP').toUpperCase();
    final String? last4 = _readString(payload['last4']);
    final String rawStatus = _readString(payload['status']) ?? 'Active';
    final bool isArchived = payload['isArchived'] as bool? ?? false;
    final bool isLinked = institutionName != null || externalReference != null;
    final AccountLinkStatus status = _mapStatus(
      rawStatus,
      isArchived: isArchived,
      isLinked: isLinked,
    );
    final DateTime? updatedAt = _parseDate(payload['updatedAt']);
    final DateTime? createdAt = _parseDate(payload['createdAt']);

    return AccountLinkItem(
      id: id,
      name: name,
      institutionName: institutionName ?? 'Added in Payabo',
      accountTypeLabel: _formatAccountType(subtype ?? accountType),
      currencyCode: currencyCode,
      source: isLinked ? AccountLinkSource.linked : AccountLinkSource.manual,
      status: status,
      statusLabel: _statusLabel(status),
      statusDetail: _statusDetail(status, isLinked: isLinked),
      sourceLabel: isLinked ? 'Linked account' : 'Manual account',
      connectionId: null,
      providerCode: null,
      maskedIdentifier: last4 == null ? null : '.... $last4',
      providerLabel: isLinked ? 'Secure connection' : null,
      lastSyncedLabel: _lastSyncedLabel(
        status,
        updatedAt: updatedAt,
        createdAt: createdAt,
        isLinked: isLinked,
      ),
    );
  }

  int _sortAccounts(AccountLinkItem left, AccountLinkItem right) {
    final int leftRank = _statusRank(left.status);
    final int rightRank = _statusRank(right.status);

    if (leftRank != rightRank) {
      return leftRank.compareTo(rightRank);
    }

    if (left.source != right.source) {
      return left.source == AccountLinkSource.linked ? -1 : 1;
    }

    return left.name.toLowerCase().compareTo(right.name.toLowerCase());
  }

  int _statusRank(AccountLinkStatus status) {
    switch (status) {
      case AccountLinkStatus.actionRequired:
        return 0;
      case AccountLinkStatus.syncing:
        return 1;
      case AccountLinkStatus.connected:
        return 2;
      case AccountLinkStatus.manual:
        return 3;
      case AccountLinkStatus.archived:
        return 4;
    }
  }

  AccountLinkStatus _mapStatus(
    String value, {
    required bool isArchived,
    required bool isLinked,
  }) {
    if (isArchived) {
      return AccountLinkStatus.archived;
    }

    final String normalized = value.trim().toLowerCase();

    if (normalized == 'syncing' ||
        normalized == 'refreshing' ||
        normalized == 'pending') {
      return AccountLinkStatus.syncing;
    }

    if (normalized == 'actionrequired' ||
        normalized == 'action_required' ||
        normalized == 'reconnectrequired' ||
        normalized == 'reconnect_required' ||
        normalized == 'needsreauth' ||
        normalized == 'needs_reauth' ||
        normalized == 'reauthrequired' ||
        normalized == 'reauth_required') {
      return AccountLinkStatus.actionRequired;
    }

    if (!isLinked) {
      return AccountLinkStatus.manual;
    }

    return AccountLinkStatus.connected;
  }

  String _statusLabel(AccountLinkStatus status) {
    switch (status) {
      case AccountLinkStatus.connected:
        return 'Connected';
      case AccountLinkStatus.syncing:
        return 'Syncing';
      case AccountLinkStatus.actionRequired:
        return 'Action required';
      case AccountLinkStatus.manual:
        return 'Manual';
      case AccountLinkStatus.archived:
        return 'Archived';
    }
  }

  String _statusDetail(
    AccountLinkStatus status, {
    required bool isLinked,
    String? lastError,
    String? provider,
  }) {
    switch (status) {
      case AccountLinkStatus.connected:
        return isLinked
            ? 'This account is ready for live transaction and balance sync.'
            : 'This account is tracked directly in Payabo.';
      case AccountLinkStatus.syncing:
        return 'We are refreshing the latest activity for this account.';
      case AccountLinkStatus.actionRequired:
        if (lastError != null) {
          return lastError;
        }

        if (provider != null) {
          return 'Reconnect this $provider account to keep insights and budgets current.';
        }

        return 'Reconnect this account to keep insights and budgets current.';
      case AccountLinkStatus.manual:
        return 'Added directly in Payabo for manual tracking and imports.';
      case AccountLinkStatus.archived:
        return 'This account is archived and no longer updates current spend.';
    }
  }

  AccountLinkStatus _mapStatusFromSummary(
    String status, {
    required String? lastSyncStatus,
    required String? lastError,
    required bool isLinked,
  }) {
    final String normalizedStatus = status.trim().toLowerCase();
    final String? normalizedSyncStatus = lastSyncStatus?.trim().toLowerCase();

    if (normalizedStatus == 'archived') {
      return AccountLinkStatus.archived;
    }

    if (lastError != null && lastError.isNotEmpty) {
      return AccountLinkStatus.actionRequired;
    }

    if (normalizedSyncStatus == 'initialsyncpending' ||
        normalizedSyncStatus == 'syncing' ||
        normalizedSyncStatus == 'refreshing') {
      return AccountLinkStatus.syncing;
    }

    return _mapStatus(status, isArchived: false, isLinked: isLinked);
  }

  String? _lastSyncedLabelFromSummary(
    AccountLinkStatus status, {
    required bool isLinked,
    required DateTime? lastSyncedAt,
    required DateTime? updatedAt,
    required DateTime? createdAt,
  }) {
    return _lastSyncedLabel(
      status,
      updatedAt: lastSyncedAt ?? updatedAt,
      createdAt: createdAt,
      isLinked: isLinked,
    );
  }

  String _formatAccountType(String value) {
    final String normalized = value.replaceAll(RegExp(r'[_-]+'), ' ').trim();
    if (normalized.isEmpty) {
      return 'Account';
    }

    final String withSpaces = normalized.replaceAllMapped(
      RegExp(r'(?<=[a-z])(?=[A-Z])'),
      (_) => ' ',
    );

    return withSpaces
        .split(RegExp(r'\s+'))
        .where((String part) => part.isNotEmpty)
        .map(
          (String part) =>
              '${part[0].toUpperCase()}${part.substring(1).toLowerCase()}',
        )
        .join(' ');
  }

  String? _lastSyncedLabel(
    AccountLinkStatus status, {
    required DateTime? updatedAt,
    required DateTime? createdAt,
    required bool isLinked,
  }) {
    if (status == AccountLinkStatus.actionRequired) {
      return 'Reconnect needed';
    }

    if (status == AccountLinkStatus.syncing) {
      return 'Sync in progress';
    }

    final DateTime? reference = updatedAt ?? createdAt;
    if (reference == null) {
      return isLinked ? 'Awaiting first sync' : 'Added manually';
    }

    final String prefix = isLinked ? 'Updated' : 'Added';
    return '$prefix ${_dateFormat.format(reference.toLocal())}';
  }

  static DateFormat _createDateFormat(String? locale) {
    final String normalizedLocale = locale?.trim() ?? '';

    try {
      if (normalizedLocale.isNotEmpty) {
        return DateFormat.MMMd(normalizedLocale);
      }

      return DateFormat.MMMd();
    } catch (_) {
      return DateFormat('d MMM', 'en_GB');
    }
  }

  DateTime? _parseDate(Object? value) {
    final String? raw = _readString(value);
    if (raw == null) {
      return null;
    }

    return DateTime.tryParse(raw);
  }

  String? _readString(Object? value) {
    if (value is! String) {
      return null;
    }

    final String trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

  void _logDioFailure(String operation, DioException exception) {
    final RequestOptions request = exception.requestOptions;
    final int? statusCode = exception.response?.statusCode;

    developer.log(
      '$operation failed for ${request.method} ${request.path}${statusCode != null ? ' (HTTP $statusCode)' : ''} [${exception.type.name}]. Response payload omitted to avoid leaking provider metadata.',
      name: 'Payabo.LiveAccountLinksRepository',
      stackTrace: exception.stackTrace,
    );
  }
}
