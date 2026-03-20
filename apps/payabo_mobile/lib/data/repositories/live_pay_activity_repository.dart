import 'dart:developer' as developer;

import 'package:dio/dio.dart';

import '../api/api_exception.dart';
import 'pay_activity_repository.dart';

/// Live implementation of [PayActivityRepository] that calls the
/// platform pay-activity BFF endpoints and maps server responses
/// into the existing DTOs.
class LivePayActivityRepository implements PayActivityRepository {
  LivePayActivityRepository({required Dio apiClient}) : _apiClient = apiClient;

  final Dio _apiClient;

  @override
  Future<PayActivitySummary> getRecentActivity() async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/payments/activity',
      );

      final Map<String, dynamic> data = response.data ?? const {};
      final transactionsJson =
          data['transactions'] as List<dynamic>? ?? const [];

      final List<PayActivityTransaction> transactions = transactionsJson
          .whereType<Map<Object?, Object?>>()
          .map((item) => _mapTransaction(Map<String, dynamic>.from(item)))
          .toList(growable: false);

      return PayActivitySummary(transactions: transactions);
    } on DioException catch (exception) {
      _logDioFailure('getRecentActivity', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<PayTransactionDetail?> getTransactionDetail(
    String transactionId,
  ) async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/payments/activity/$transactionId',
      );

      if (response.data == null) {
        return null;
      }

      return _mapTransactionDetail(response.data!);
    } on DioException catch (exception) {
      if (exception.response?.statusCode == 404) {
        return null;
      }

      _logDioFailure('getTransactionDetail', exception);
      throw mapDioException(exception);
    }
  }

  // ═══════════════════════════════════════════════════════════════
  // Mapping helpers
  // ═══════════════════════════════════════════════════════════════

  PayActivityTransaction _mapTransaction(Map<String, dynamic> json) {
    return PayActivityTransaction(
      id: _readString(json['id']) ?? '',
      title: _readString(json['title']) ?? '',
      subtitle: _readString(json['subtitle']) ?? '',
      amountLabel: _readString(json['amountLabel']) ?? '',
      status: _readString(json['status']) ?? '',
      type: _parseTransactionType(json['type']),
      dateGroupLabel: _readString(json['dateGroupLabel']) ?? '',
    );
  }

  PayTransactionDetail _mapTransactionDetail(Map<String, dynamic> json) {
    final recipientJson =
        json['recipient'] as Map<String, dynamic>? ?? const {};

    return PayTransactionDetail(
      id: _readString(json['id']) ?? '',
      status: _readString(json['status']) ?? '',
      statusDescription: _readString(json['statusDescription']) ?? '',
      amountLabel: _readString(json['amountLabel']) ?? '',
      feeLabel: _readString(json['feeLabel']) ?? '',
      totalLabel: _readString(json['totalLabel']) ?? '',
      recipient: _mapRecipient(recipientJson),
      orderId: _readString(json['orderId']) ?? '',
      paymentIntentId: _readString(json['paymentIntentId']) ?? '',
      providerReference: _readString(json['providerReference']) ?? '',
      reference: _readString(json['reference']) ?? '',
    );
  }

  PayTransactionRecipient _mapRecipient(Map<String, dynamic> json) {
    final String name = _readString(json['name']) ?? 'Unknown';
    final String initials = _readString(json['initials']) ??
        (name.length >= 2 ? name.substring(0, 2).toUpperCase() : name.toUpperCase());

    return PayTransactionRecipient(
      name: name,
      initials: initials,
      bankName: _readString(json['bankName']) ?? '',
      maskedAccountNumber: _readString(json['maskedAccountNumber']) ?? '',
      country: _readString(json['country']) ?? '',
    );
  }

  PayActivityTransactionType _parseTransactionType(Object? value) {
    final String? type = _readString(value);
    switch (type?.toLowerCase()) {
      case 'bill':
        return PayActivityTransactionType.bill;
      case 'transfer':
      default:
        return PayActivityTransactionType.transfer;
    }
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
      name: 'Payabo.LivePayActivityRepository',
      stackTrace: exception.stackTrace,
    );
  }
}
