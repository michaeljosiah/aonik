import 'dart:developer' as developer;

import 'package:dio/dio.dart';

import '../api/api_exception.dart';
import 'spending_repository.dart';

/// Live implementation of [SpendingRepository] that calls the
/// personal-finance spending BFF endpoints and maps server responses
/// into the existing DTOs.
class LiveSpendingRepository implements SpendingRepository {
  LiveSpendingRepository({required Dio apiClient}) : _apiClient = apiClient;

  final Dio _apiClient;

  @override
  Future<List<SpendingAccountCard>> getAccounts() async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/personal-finance/account-links/summary',
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      return raw
          .whereType<Map<Object?, Object?>>()
          .map((item) =>
              _mapAccountCardFromSummary(Map<String, dynamic>.from(item)))
          .where((card) => card != null)
          .cast<SpendingAccountCard>()
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('getAccounts', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<List<SpendingTransaction>> getTransactions(String accountId) async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/personal-finance/spending/accounts/$accountId/transactions',
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      return raw
          .whereType<Map<Object?, Object?>>()
          .map((item) => _mapTransaction(Map<String, dynamic>.from(item)))
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('getTransactions', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<SpendingOverviewData> getOverview() async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/personal-finance/spending/overview',
      );

      final Map<String, dynamic> data = response.data ?? const {};
      return _mapOverview(data);
    } on DioException catch (exception) {
      _logDioFailure('getOverview', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<SpendingMerchantHistory> getMerchantHistory(
    String merchantName,
  ) async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/personal-finance/spending/merchants/history',
        queryParameters: <String, dynamic>{
          'merchant': merchantName,
        },
      );

      final Map<String, dynamic> data = response.data ?? const {};
      return _mapMerchantHistory(data);
    } on DioException catch (exception) {
      _logDioFailure('getMerchantHistory', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<SpendingTransaction> addTransaction(
    String accountId,
    CreateTransactionRequest request,
  ) async {
    try {
      final response = await _apiClient.post<Map<String, dynamic>>(
        '/personal-finance/spending/accounts/$accountId/transactions',
        data: <String, dynamic>{
          'merchant': request.merchant,
          'amount': request.amount,
          'currency': request.currency,
          'category': request.category,
          'isCredit': request.isCredit,
          'date': request.date.toIso8601String(),
          if (request.notes != null) 'notes': request.notes,
        },
      );

      final Map<String, dynamic> data = response.data ?? const {};
      return _mapTransaction(data);
    } on DioException catch (exception) {
      _logDioFailure('addTransaction', exception);
      throw mapDioException(exception);
    }
  }

  // ═══════════════════════════════════════════════════════════════
  // Mapping helpers — accounts & transactions
  // ═══════════════════════════════════════════════════════════════

  // Icons.account_balance_outlined
  static const int _iconAccountBalanceOutlined = 0xee2f;
  // Icons.edit_outlined
  static const int _iconEditOutlined = 0xef4b;

  static String _currencySymbolFromCode(String code) {
    switch (code.toUpperCase()) {
      case 'GBP':
        return '\u00A3';
      case 'USD':
        return '\$';
      case 'EUR':
        return '\u20AC';
      case 'NGN':
        return '\u20A6';
      case 'KES':
        return 'KSh';
      case 'GHS':
        return 'GH\u20B5';
      case 'ZAR':
        return 'R';
      case 'CAD':
        return 'CA\$';
      case 'INR':
        return '\u20B9';
      default:
        return code;
    }
  }

  /// Maps an account-links summary item to a [SpendingAccountCard].
  /// Returns null for archived accounts so they can be filtered out.
  SpendingAccountCard? _mapAccountCardFromSummary(Map<String, dynamic> json) {
    final String rawStatus = _readString(json['status']) ?? 'Active';
    if (rawStatus.trim().toLowerCase() == 'archived') return null;

    final String id = _readString(json['linkedAccountId']) ??
        _readString(json['personalAccountId']) ??
        '';
    final String name = _readString(json['name']) ?? 'Untitled account';
    final String? sourceType = _readString(json['sourceType']);
    final bool isLinked = sourceType?.toLowerCase() == 'linked';
    final String? provider = _readString(json['provider']);
    final String? institutionName = _readString(json['institutionName']);
    final String currencyCode =
        (_readString(json['currency']) ?? 'GBP').toUpperCase();
    final String symbol = _currencySymbolFromCode(currencyCode);

    // Provider name: use provider for linked accounts, 'Manual' for manual.
    final String providerName =
        isLinked ? (provider ?? institutionName ?? 'Connected bank') : 'Manual';

    // Icon: bank icon for linked, edit icon for manual.
    final int iconCodePoint =
        isLinked ? _iconAccountBalanceOutlined : _iconEditOutlined;

    // Balance is not available from the summary endpoint, so default to
    // a placeholder showing the currency.
    final String balanceLabel = '${symbol}0.00';
    const String balanceMajor = '0';
    const String balanceMinor = '.00';

    return SpendingAccountCard(
      id: id,
      accountName: name,
      providerName: providerName,
      providerIconCodePoint: iconCodePoint,
      providerIconFontFamily: 'MaterialIcons',
      balanceLabel: balanceLabel,
      balanceMajor: balanceMajor,
      balanceMinor: balanceMinor,
      currencySymbol: symbol,
      currencyCode: currencyCode,
      connectionId: _readString(json['connectionId']),
      isManual: !isLinked,
    );
  }

  SpendingTransaction _mapTransaction(Map<String, dynamic> json) {
    return SpendingTransaction(
      id: _readString(json['id']) ?? '',
      merchant: _readString(json['merchant']) ?? '',
      category: _readString(json['category']) ?? '',
      subCategory: _readString(json['subCategory']),
      amountLabel: _readString(json['amountLabel']) ?? '',
      amountMajor: _readString(json['amountMajor']) ?? '0',
      amountMinor: _readString(json['amountMinor']) ?? '.00',
      currencySymbol: _readString(json['currencySymbol']) ?? '',
      isCredit: json['isCredit'] == true,
      date: _parseDate(json['date']) ?? DateTime.now(),
      iconText: _readString(json['iconText']),
      iconCodePoint: (json['iconCodePoint'] as num?)?.toInt(),
      iconFontFamily: _readString(json['iconFontFamily']),
      connectionId: _readString(json['connectionId']),
      notes: _readString(json['notes']),
    );
  }

  // ═══════════════════════════════════════════════════════════════
  // Mapping helpers — overview
  // ═══════════════════════════════════════════════════════════════

  SpendingOverviewData _mapOverview(Map<String, dynamic> json) {
    final snapshotsJson =
        json['accountSnapshots'] as List<dynamic>? ?? const [];
    final breakdownJson = json['breakdownSlices'] as List<dynamic>? ?? const [];
    final trendSpotsJson = json['trendSpots'] as List<dynamic>? ?? const [];
    final trendLabelsJson =
        json['trendBottomLabels'] as List<dynamic>? ?? const [];
    final allocationJson =
        json['allocationSlices'] as List<dynamic>? ?? const [];
    final recentTxnsJson =
        json['recentTransactions'] as List<dynamic>? ?? const [];
    final totalBalanceJson =
        json['totalBalanceMetric'] as Map<String, dynamic>? ?? const {};
    final netWorthJson =
        json['netWorthMetric'] as Map<String, dynamic>? ?? const {};

    return SpendingOverviewData(
      accountSnapshots: snapshotsJson
          .whereType<Map<Object?, Object?>>()
          .map((s) => _mapSnapshot(Map<String, dynamic>.from(s)))
          .toList(growable: false),
      totalBalanceMetric: _mapMetric(totalBalanceJson),
      netWorthMetric: _mapMetric(netWorthJson),
      safeToSpendLabel: _readString(json['safeToSpendLabel']) ?? '',
      safeToSpendSubtitle: _readString(json['safeToSpendSubtitle']) ?? '',
      breakdownSlices: breakdownJson
          .whereType<Map<Object?, Object?>>()
          .map((s) => _mapBreakdownSlice(Map<String, dynamic>.from(s)))
          .toList(growable: false),
      breakdownTotalLabel: _readString(json['breakdownTotalLabel']) ?? '',
      trendSummaryLabel: _readString(json['trendSummaryLabel']) ?? '',
      trendSpots: trendSpotsJson.whereType<Map<Object?, Object?>>().map((s) {
        final spot = Map<String, dynamic>.from(s);
        return SpendingTrendSpot(
          x: (spot['x'] as num?)?.toDouble() ?? 0,
          y: (spot['y'] as num?)?.toDouble() ?? 0,
        );
      }).toList(growable: false),
      trendBottomLabels:
          trendLabelsJson.whereType<String>().toList(growable: false),
      insightTitle: _readString(json['insightTitle']) ?? '',
      insightBody: _readString(json['insightBody']) ?? '',
      allocationSlices: allocationJson
          .whereType<Map<Object?, Object?>>()
          .map((s) => _mapAllocationSlice(Map<String, dynamic>.from(s)))
          .toList(growable: false),
      allocationMonthLabel: _readString(json['allocationMonthLabel']) ?? '',
      allocationYearLabel: _readString(json['allocationYearLabel']) ?? '',
      allocationChipLabel: _readString(json['allocationChipLabel']) ?? '',
      recentTransactions: recentTxnsJson
          .whereType<Map<Object?, Object?>>()
          .map((t) => _mapRecentTransaction(Map<String, dynamic>.from(t)))
          .toList(growable: false),
    );
  }

  SpendingAccountSnapshot _mapSnapshot(Map<String, dynamic> json) {
    return SpendingAccountSnapshot(
      label: _readString(json['label']) ?? '',
      balanceLabel: _readString(json['balanceLabel']) ?? '',
      statusLabel: _readString(json['statusLabel']) ?? '',
      changeLabel: _readString(json['changeLabel']) ?? '',
      gradientKey: _readString(json['gradientKey']) ?? 'primary',
      iconCodePoint: (json['iconCodePoint'] as num?)?.toInt() ?? 0xee33,
      iconFontFamily: _readString(json['iconFontFamily']) ?? 'MaterialIcons',
      connectionId: _readString(json['connectionId']),
    );
  }

  SpendingBreakdownSlice _mapBreakdownSlice(Map<String, dynamic> json) {
    return SpendingBreakdownSlice(
      label: _readString(json['label']) ?? '',
      amountLabel: _readString(json['amountLabel']) ?? '',
      value: (json['value'] as num?)?.toDouble() ?? 0,
      colorKey: _readString(json['colorKey']) ?? 'primary',
    );
  }

  SpendingAllocationSlice _mapAllocationSlice(Map<String, dynamic> json) {
    return SpendingAllocationSlice(
      label: _readString(json['label']) ?? '',
      amountLabel: _readString(json['amountLabel']) ?? '',
      value: (json['value'] as num?)?.toDouble() ?? 0,
      colorKey: _readString(json['colorKey']) ?? 'primary',
    );
  }

  SpendingRecentTransaction _mapRecentTransaction(Map<String, dynamic> json) {
    return SpendingRecentTransaction(
      merchant: _readString(json['merchant']) ?? '',
      category: _readString(json['category']) ?? '',
      subCategory: _readString(json['subCategory']),
      amountLabel: _readString(json['amountLabel']) ?? '',
      iconText: _readString(json['iconText']) ?? '',
      iconBackgroundKey: _readString(json['iconBackgroundKey']) ?? 'dark',
      iconForegroundKey:
          _readString(json['iconForegroundKey']) ?? 'surfaceBase',
    );
  }

  SpendingMetric _mapMetric(Map<String, dynamic> json) {
    return SpendingMetric(
      label: _readString(json['label']) ?? '',
      amountLabel: _readString(json['amountLabel']) ?? '',
      trendLabel: _readString(json['trendLabel']) ?? '',
      iconCodePoint: (json['iconCodePoint'] as num?)?.toInt() ?? 0xe5f7,
      iconFontFamily: _readString(json['iconFontFamily']) ?? 'MaterialIcons',
    );
  }

  // ═══════════════════════════════════════════════════════════════
  // Mapping helpers — merchant history
  // ═══════════════════════════════════════════════════════════════

  SpendingMerchantHistory _mapMerchantHistory(Map<String, dynamic> json) {
    return SpendingMerchantHistory(
      transactionCountLabel: _readString(json['transactionCountLabel']) ?? '0',
      averageSpendLabel: _readString(json['averageSpendLabel']) ?? '',
      totalSpentLabel: _readString(json['totalSpentLabel']) ?? '',
    );
  }

  // ═══════════════════════════════════════════════════════════════
  // Utilities
  // ═══════════════════════════════════════════════════════════════

  DateTime? _parseDate(Object? value) {
    final String? raw = _readString(value);
    if (raw == null) return null;
    return DateTime.tryParse(raw);
  }

  String? _readString(Object? value) {
    if (value is! String) return null;
    final trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

  void _logDioFailure(String operation, DioException exception) {
    final request = exception.requestOptions;
    final statusCode = exception.response?.statusCode;

    developer.log(
      '$operation failed for ${request.method} ${request.path}'
      '${statusCode != null ? ' (HTTP $statusCode)' : ''} '
      '[${exception.type.name}].',
      name: 'Payabo.LiveSpendingRepository',
      stackTrace: exception.stackTrace,
    );
  }
}
