import 'dart:developer' as developer;

import 'package:dio/dio.dart';

import '../api/api_exception.dart';
import 'spending_category_repository.dart';

/// Live implementation of [SpendingCategoryRepository] that calls the
/// personal-finance spending-category BFF endpoint and maps server
/// responses into the existing DTOs.
class LiveSpendingCategoryRepository implements SpendingCategoryRepository {
  LiveSpendingCategoryRepository({required Dio apiClient})
      : _apiClient = apiClient;

  final Dio _apiClient;

  @override
  Future<SpendingCategoryDetail?> getCategoryDetail(
    String categoryId,
  ) async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/personal-finance/spending/categories/$categoryId',
      );

      if (response.data == null) {
        return null;
      }

      return _mapCategoryDetail(response.data!);
    } on DioException catch (exception) {
      if (exception.response?.statusCode == 404) {
        return null;
      }

      _logDioFailure('getCategoryDetail', exception);
      throw mapDioException(exception);
    }
  }

  // ═══════════════════════════════════════════════════════════════
  // Mapping helpers
  // ═══════════════════════════════════════════════════════════════

  SpendingCategoryDetail _mapCategoryDetail(Map<String, dynamic> json) {
    final transactionsJson =
        json['transactions'] as List<dynamic>? ?? const [];
    final currentSpotsJson =
        json['chartCurrentMonthSpots'] as List<dynamic>? ?? const [];
    final previousSpotsJson =
        json['chartPreviousMonthSpots'] as List<dynamic>? ?? const [];

    return SpendingCategoryDetail(
      categoryId: _readString(json['categoryId']) ??
          _readString(json['id']) ??
          '',
      title: _readString(json['title']) ?? '',
      iconCodePoint:
          (json['iconCodePoint'] as num?)?.toInt() ?? 0xf37b,
      iconFontFamily:
          _readString(json['iconFontFamily']) ?? 'MaterialIcons',
      monthLabel: _readString(json['monthLabel']) ?? '',
      totalAmount: _readString(json['totalAmount']) ?? '',
      deltaAmount: _readString(json['deltaAmount']) ?? '',
      deltaReference: _readString(json['deltaReference']) ?? '',
      isDecrease: json['isDecrease'] == true,
      activeAlertCount:
          (json['activeAlertCount'] as num?)?.toInt() ?? 0,
      transactionCountLabel:
          _readString(json['transactionCountLabel']) ?? '',
      transactions: transactionsJson
          .whereType<Map<Object?, Object?>>()
          .map((t) => _mapTransaction(Map<String, dynamic>.from(t)))
          .toList(growable: false),
      chartCurrentMonthSpots: _mapChartSpots(currentSpotsJson),
      chartPreviousMonthSpots: _mapChartSpots(previousSpotsJson),
    );
  }

  SpendingCategoryTransaction _mapTransaction(Map<String, dynamic> json) {
    return SpendingCategoryTransaction(
      dateLabel: _readString(json['dateLabel']) ?? '',
      merchant: _readString(json['merchant']) ?? '',
      amount: _readString(json['amount']) ?? '',
      time: _readString(json['time']) ?? '',
      accountName: _readString(json['accountName']) ?? '',
      accountBadge: _readString(json['accountBadge']) ?? '',
      avatarLabel: _readString(json['avatarLabel']) ?? '',
      avatarBackgroundValue:
          (json['avatarBackgroundValue'] as num?)?.toInt() ?? 0xFF1A1C20,
      avatarForegroundValue:
          (json['avatarForegroundValue'] as num?)?.toInt() ?? 0xFF4ACB64,
      connectionId: _readString(json['connectionId']),
    );
  }

  List<List<double>> _mapChartSpots(List<dynamic> raw) {
    return raw
        .whereType<List<dynamic>>()
        .map((pair) => pair
            .whereType<num>()
            .map((n) => n.toDouble())
            .toList(growable: false))
        .where((pair) => pair.length >= 2)
        .toList(growable: false);
  }

  // ═══════════════════════════════════════════════════════════════
  // Utilities
  // ═══════════════════════════════════════════════════════════════

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
      name: 'Payabo.LiveSpendingCategoryRepository',
      stackTrace: exception.stackTrace,
    );
  }
}
