import 'dart:developer' as developer;

import 'package:dio/dio.dart';

import '../api/api_exception.dart';
import 'catalog_repository.dart';

/// Live implementation of [CatalogRepository] that calls the
/// platform catalog BFF endpoints and maps server responses into
/// the existing DTOs.
class LiveCatalogRepository implements CatalogRepository {
  LiveCatalogRepository({required Dio apiClient}) : _apiClient = apiClient;

  final Dio _apiClient;

  @override
  Future<List<CatalogCountry>> getCountries() async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/catalog/countries',
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      return raw
          .whereType<Map<Object?, Object?>>()
          .map((item) => _mapCountry(Map<String, dynamic>.from(item)))
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('getCountries', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<List<CatalogProvider>> getProviders({
    required String countryCode,
  }) async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/catalog/providers',
        queryParameters: <String, dynamic>{
          'countryCode': countryCode,
        },
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      return raw
          .whereType<Map<Object?, Object?>>()
          .map((item) => _mapProvider(Map<String, dynamic>.from(item)))
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('getProviders', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<List<String>> getServiceTypes() async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/catalog/service-types',
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      return raw
          .whereType<String>()
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('getServiceTypes', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<List<String>> getRecurringFrequencies() async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/catalog/recurring-frequencies',
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      return raw
          .whereType<String>()
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('getRecurringFrequencies', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<List<String>> getProviderCategories() async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/catalog/provider-categories',
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      return raw
          .whereType<String>()
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('getProviderCategories', exception);
      throw mapDioException(exception);
    }
  }

  // ═══════════════════════════════════════════════════════════════
  // Mapping helpers
  // ═══════════════════════════════════════════════════════════════

  CatalogCountry _mapCountry(Map<String, dynamic> json) {
    return CatalogCountry(
      code: _readString(json['code']) ?? '',
      name: _readString(json['name']) ?? '',
      currency: _readString(json['currency']) ??
          _readString(json['currencyCode']) ??
          '',
    );
  }

  CatalogProvider _mapProvider(Map<String, dynamic> json) {
    return CatalogProvider(
      id: _readString(json['id']) ?? '',
      name: _readString(json['name']) ?? '',
      countryCode: _readString(json['countryCode']) ?? '',
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
      name: 'Payabo.LiveCatalogRepository',
      stackTrace: exception.stackTrace,
    );
  }
}
