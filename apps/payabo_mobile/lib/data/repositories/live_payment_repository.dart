import 'dart:developer' as developer;

import 'package:dio/dio.dart';

import '../api/api_exception.dart';
import 'payment_repository.dart';

/// Live implementation of [PaymentRepository] that calls the
/// platform payment BFF endpoints and maps server responses into
/// the existing DTOs.
class LivePaymentRepository implements PaymentRepository {
  LivePaymentRepository({required Dio apiClient}) : _apiClient = apiClient;

  final Dio _apiClient;

  @override
  Future<PaymentIntent> createPaymentIntent({
    required String orderId,
    String selectedCardId = '',
    String manualCardNumber = '',
    String manualCardExpiry = '',
    String manualCardCvc = '',
    bool saveCard = true,
  }) async {
    try {
      final Map<String, dynamic> body = <String, dynamic>{
        'orderId': orderId,
      };

      if (selectedCardId.isNotEmpty && selectedCardId != 'manual_card') {
        body['savedCardId'] = selectedCardId;
      }

      if (manualCardNumber.isNotEmpty) {
        body['cardNumber'] = manualCardNumber;
        body['cardExpiry'] = manualCardExpiry;
        body['cardCvc'] = manualCardCvc;
        body['saveCard'] = saveCard;
      }

      final response = await _apiClient.post<Map<String, dynamic>>(
        '/payments/intents',
        data: body,
      );

      final Map<String, dynamic> data = response.data ?? const {};
      return _mapPaymentIntent(data);
    } on DioException catch (exception) {
      _logDioFailure('createPaymentIntent', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<PaymentResult> getPaymentStatus({
    required String paymentIntentId,
  }) async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/payments/intents/$paymentIntentId/status',
      );

      final Map<String, dynamic> data = response.data ?? const {};
      final String? status = _readString(data['status']);
      return _parsePaymentResult(status);
    } on DioException catch (exception) {
      _logDioFailure('getPaymentStatus', exception);
      throw mapDioException(exception);
    }
  }

  // ═══════════════════════════════════════════════════════════════
  // Mapping helpers
  // ═══════════════════════════════════════════════════════════════

  PaymentIntent _mapPaymentIntent(Map<String, dynamic> json) {
    final String? status = _readString(json['status']);

    return PaymentIntent(
      paymentIntentId: _readString(json['paymentIntentId']) ??
          _readString(json['id']) ??
          '',
      orderId: _readString(json['orderId']) ?? '',
      providerReference: _readString(json['providerReference']) ?? '',
      status: _parsePaymentResult(status),
    );
  }

  PaymentResult _parsePaymentResult(String? status) {
    switch (status?.toLowerCase()) {
      case 'success':
      case 'completed':
      case 'succeeded':
        return PaymentResult.success;
      case 'failed':
      case 'rejected':
      case 'cancelled':
        return PaymentResult.failed;
      default:
        return PaymentResult.pending;
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
      name: 'Payabo.LivePaymentRepository',
      stackTrace: exception.stackTrace,
    );
  }
}
