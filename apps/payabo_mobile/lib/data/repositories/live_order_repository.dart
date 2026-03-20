import 'dart:developer' as developer;

import 'package:dio/dio.dart';

import '../api/api_exception.dart';
import 'order_repository.dart';

/// Live implementation of [OrderRepository] that calls the
/// platform order BFF endpoints and maps server responses into
/// the existing DTOs.
class LiveOrderRepository implements OrderRepository {
  LiveOrderRepository({required Dio apiClient}) : _apiClient = apiClient;

  final Dio _apiClient;

  @override
  Future<DraftOrder> createDraftOrder({
    required String billerName,
    required String serviceName,
    required String countryCode,
    required double amount,
    required String currency,
  }) async {
    try {
      final response = await _apiClient.post<Map<String, dynamic>>(
        '/orders/draft',
        data: <String, dynamic>{
          'billerName': billerName,
          'serviceName': serviceName,
          'countryCode': countryCode,
          'amount': amount,
          'currency': currency,
        },
      );

      final Map<String, dynamic> data = response.data ?? const {};
      return _mapDraftOrder(data);
    } on DioException catch (exception) {
      _logDioFailure('createDraftOrder', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<DraftOrder?> getDraftOrder(String orderId) async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/orders/$orderId',
      );

      if (response.data == null) {
        return null;
      }

      return _mapDraftOrder(response.data!);
    } on DioException catch (exception) {
      if (exception.response?.statusCode == 404) {
        return null;
      }

      _logDioFailure('getDraftOrder', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<PricingBreakdown> getPricingBreakdown(String orderId) async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/orders/$orderId/pricing',
      );

      final Map<String, dynamic> data = response.data ?? const {};
      return _mapPricingBreakdown(data);
    } on DioException catch (exception) {
      _logDioFailure('getPricingBreakdown', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<OrderPointsSummary> getPointsSummary(String orderId) async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/orders/$orderId/points',
      );

      final Map<String, dynamic> data = response.data ?? const {};
      return _mapPointsSummary(data);
    } on DioException catch (exception) {
      _logDioFailure('getPointsSummary', exception);
      throw mapDioException(exception);
    }
  }

  // ═══════════════════════════════════════════════════════════════
  // Mapping helpers
  // ═══════════════════════════════════════════════════════════════

  DraftOrder _mapDraftOrder(Map<String, dynamic> json) {
    return DraftOrder(
      orderId: _readString(json['orderId']) ??
          _readString(json['id']) ??
          '',
      billerName: _readString(json['billerName']) ?? '',
      serviceName: _readString(json['serviceName']) ?? '',
      countryCode: _readString(json['countryCode']) ?? '',
      amount: (json['amount'] as num?)?.toDouble() ?? 0.0,
      currency: _readString(json['currency']) ?? '',
    );
  }

  PricingBreakdown _mapPricingBreakdown(Map<String, dynamic> json) {
    final linesJson = json['lines'] as List<dynamic>? ?? const [];

    final List<PricingLine> lines = linesJson
        .whereType<Map<Object?, Object?>>()
        .map((item) {
          final line = Map<String, dynamic>.from(item);
          return PricingLine(
            label: _readString(line['label']) ?? '',
            value: _readString(line['value']) ?? '',
            bold: line['bold'] == true,
            subtle: line['subtle'] == true,
            accent: line['accent'] == true,
            isDivider: line['isDivider'] == true,
          );
        })
        .toList(growable: false);

    return PricingBreakdown(lines: lines);
  }

  OrderPointsSummary _mapPointsSummary(Map<String, dynamic> json) {
    return OrderPointsSummary(
      pointsEarned: (json['pointsEarned'] as num?)?.toInt() ?? 0,
      totalPoints: (json['totalPoints'] as num?)?.toInt() ?? 0,
      pointsLabel: _readString(json['pointsLabel']) ?? '',
    );
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
      name: 'Payabo.LiveOrderRepository',
      stackTrace: exception.stackTrace,
    );
  }
}
