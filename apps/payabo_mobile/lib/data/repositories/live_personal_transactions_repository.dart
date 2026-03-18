import 'dart:developer' as developer;

import 'package:dio/dio.dart';

import '../api/api_exception.dart';
import 'personal_transactions_repository.dart';

class LivePersonalTransactionsRepository
    implements PersonalTransactionsRepository {
  LivePersonalTransactionsRepository({required Dio apiClient})
      : _apiClient = apiClient;

  final Dio _apiClient;

  @override
  Future<PersonalTransactionsPage> listTransactions(
    PersonalTransactionsQuery query,
  ) async {
    try {
      final Map<String, dynamic> params = <String, dynamic>{
        'page': query.page,
        'pageSize': query.pageSize,
        if (query.personalAccountId != null)
          'personalAccountId': query.personalAccountId,
        if (query.from != null) 'from': query.from!.toIso8601String(),
        if (query.to != null) 'to': query.to!.toIso8601String(),
        if (query.category != null && query.category!.isNotEmpty)
          'category': query.category,
        if (query.search != null && query.search!.isNotEmpty)
          'search': query.search,
      };

      final response = await _apiClient.get<List<dynamic>>(
        '/personal-finance/transactions',
        queryParameters: params,
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      final List<PersonalTransactionItem> items = raw
          .whereType<Map<Object?, Object?>>()
          .map(
            (Map<Object?, Object?> item) =>
                _mapItem(Map<String, dynamic>.from(item)),
          )
          .toList(growable: false);

      return PersonalTransactionsPage(
        items: items,
        page: query.page,
        pageSize: query.pageSize,
        hasMore: items.length >= query.pageSize,
      );
    } on DioException catch (exception) {
      _logDioFailure('listTransactions', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<PersonalTransactionItem?> getTransaction(
    String transactionId,
  ) async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/personal-finance/transactions/$transactionId',
      );

      if (response.data == null) {
        return null;
      }

      return _mapItem(response.data!);
    } on DioException catch (exception) {
      if (exception.response?.statusCode == 404) {
        return null;
      }

      _logDioFailure('getTransaction', exception);
      throw mapDioException(exception);
    }
  }

  PersonalTransactionItem _mapItem(Map<String, dynamic> payload) {
    final String id = _readString(payload['personalTransactionId']) ??
        _readString(payload['id']) ??
        'tx-${payload.hashCode}';

    final double amount =
        (payload['amount'] as num?)?.toDouble() ?? 0.0;

    final String currency =
        (_readString(payload['currency']) ?? 'GBP').toUpperCase();

    final String? merchant = _readString(payload['merchant']);
    final String? description = _readString(payload['description']);

    final String displayMerchant =
        merchant ?? description ?? 'Unknown';

    final String category =
        _readString(payload['category']) ?? 'Uncategorised';

    final DateTime occurredAt =
        _parseDate(payload['occurredAt']) ?? DateTime.now();

    final String? accountId =
        _readString(payload['personalAccountId']);

    return PersonalTransactionItem(
      id: id,
      merchant: displayMerchant,
      category: category,
      amount: amount,
      currency: currency,
      isCredit: amount >= 0,
      occurredAt: occurredAt,
      description: description,
      personalAccountId: accountId,
    );
  }

  DateTime? _parseDate(Object? value) {
    final String? raw = _readString(value);
    if (raw == null) return null;

    return DateTime.tryParse(raw);
  }

  String? _readString(Object? value) {
    if (value is! String) return null;

    final String trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

  void _logDioFailure(String operation, DioException exception) {
    final RequestOptions request = exception.requestOptions;
    final int? statusCode = exception.response?.statusCode;

    developer.log(
      '$operation failed for ${request.method} ${request.path}'
      '${statusCode != null ? ' (HTTP $statusCode)' : ''} '
      '[${exception.type.name}].',
      name: 'Payabo.LivePersonalTransactionsRepository',
      stackTrace: exception.stackTrace,
    );
  }
}
