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
        '/personal-finance/transactions',
        queryParameters: <String, dynamic>{
          'PersonalAccountId': accountId,
          'PageSize': 200,
        },
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
      // The backend expects the amount with its sign: negative for expenses,
      // positive for income.
      final double signedAmount =
          request.isCredit ? request.amount.abs() : -(request.amount.abs());

      final response = await _apiClient.post<Map<String, dynamic>>(
        '/personal-finance/transactions',
        data: <String, dynamic>{
          'personalAccountId': accountId,
          'occurredAt': request.date.toIso8601String(),
          'amount': signedAmount,
          'currency': request.currency,
          'merchant': request.merchant,
          'description': request.merchant,
          'category': request.category,
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

  /// Maps a [PersonalTransactionResponse] JSON object from the backend into
  /// a display-ready [SpendingTransaction].
  SpendingTransaction _mapTransaction(Map<String, dynamic> json) {
    final String id = _readString(json['personalTransactionId']) ??
        _readString(json['id']) ??
        '';
    final String merchant =
        _readString(json['merchant']) ?? _readString(json['description']) ?? '';
    final String category = _readString(json['category']) ?? '';
    final String? subCategory = _readString(json['subCategory']);
    final String? notes = _readString(json['notes']);

    // Parse amount — negative = expense, positive = income/credit.
    final num rawAmount = (json['amount'] as num?) ?? 0;
    final double amount = rawAmount.toDouble();
    final bool isCredit = amount >= 0;
    final double absAmount = amount.abs();

    // Currency
    final String currencyCode =
        (_readString(json['currency']) ?? 'GBP').toUpperCase();
    final String symbol = _currencySymbolFromCode(currencyCode);

    // Format amount parts.
    final String sign = isCredit ? '+' : '-';
    final int wholePart = absAmount.truncate();
    final String fractional =
        '.${(absAmount * 100).round().remainder(100).toString().padLeft(2, '0')}';
    final String majorFormatted = _formatWithCommas(wholePart);
    final String amountLabel = '$sign$symbol$majorFormatted$fractional';

    // Date
    final DateTime date = _parseDate(json['occurredAt']) ?? DateTime.now();

    // Icon: use first letter of merchant, or category icon.
    final String? iconText =
        merchant.isNotEmpty ? merchant[0].toUpperCase() : null;

    return SpendingTransaction(
      id: id,
      merchant: merchant,
      category: category,
      subCategory: subCategory,
      amountLabel: amountLabel,
      amountMajor: majorFormatted,
      amountMinor: fractional,
      currencySymbol: symbol,
      isCredit: isCredit,
      date: date,
      iconText: iconText,
      notes: notes,
    );
  }

  /// Formats an integer with comma thousand-separators (e.g. 1450 → "1,450").
  static String _formatWithCommas(int value) {
    final String digits = value.toString();
    final StringBuffer buf = StringBuffer();
    for (int i = 0; i < digits.length; i++) {
      if (i > 0 && (digits.length - i) % 3 == 0) buf.write(',');
      buf.write(digits[i]);
    }
    return buf.toString();
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
