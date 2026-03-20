import 'dart:developer' as developer;

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';

import '../../features/spending/presentation/spending_budget_data.dart';
import '../api/api_exception.dart';
import 'budget_repository.dart';

/// Live implementation of [BudgetRepository] that calls the
/// personal-finance budget BFF endpoints and maps server responses
/// into the existing DTOs.
class LiveBudgetRepository implements BudgetRepository {
  LiveBudgetRepository({required Dio apiClient}) : _apiClient = apiClient;

  final Dio _apiClient;

  @override
  Future<List<SpendingBudgetCategory>> getBudgets() async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/personal-finance/budgets',
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      return raw
          .whereType<Map<Object?, Object?>>()
          .map((item) => _mapBudgetCategory(Map<String, dynamic>.from(item)))
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('getBudgets', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<SpendingBudgetCategory> createBudget({String? categoryId}) async {
    try {
      final response = await _apiClient.post<Map<String, dynamic>>(
        '/personal-finance/budgets',
        data: <String, dynamic>{
          if (categoryId != null) 'categoryId': categoryId,
        },
      );

      final Map<String, dynamic> data = response.data ?? const {};
      return _mapBudgetCategory(data);
    } on DioException catch (exception) {
      _logDioFailure('createBudget', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<List<SpendingBudgetCategory>> saveBudgetAmount({
    required String budgetId,
    required double totalAllocated,
  }) async {
    try {
      final response = await _apiClient.put<List<dynamic>>(
        '/personal-finance/budgets/$budgetId/amount',
        data: <String, dynamic>{
          'totalAllocated': totalAllocated,
        },
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      return raw
          .whereType<Map<Object?, Object?>>()
          .map((item) => _mapBudgetCategory(Map<String, dynamic>.from(item)))
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('saveBudgetAmount', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<List<SpendingBudgetCategory>> deleteBudget(String budgetId) async {
    try {
      final response = await _apiClient.delete<List<dynamic>>(
        '/personal-finance/budgets/$budgetId',
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      return raw
          .whereType<Map<Object?, Object?>>()
          .map((item) => _mapBudgetCategory(Map<String, dynamic>.from(item)))
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('deleteBudget', exception);
      throw mapDioException(exception);
    }
  }

  // ═══════════════════════════════════════════════════════════════
  // Mapping helpers
  // ═══════════════════════════════════════════════════════════════

  SpendingBudgetCategory _mapBudgetCategory(Map<String, dynamic> json) {
    final lineItemsJson =
        json['lineItems'] as List<dynamic>? ?? const [];
    final historyJson =
        json['history'] as List<dynamic>? ?? const [];

    return SpendingBudgetCategory(
      id: _readString(json['id']) ?? '',
      name: _readString(json['name']) ?? '',
      description: _readString(json['description']),
      icon: _parseIconData(json),
      accentRole: _parseAccentRole(json['accentRole']),
      linkedSpendingCategoryId: _readString(json['linkedSpendingCategoryId']),
      lineItems: lineItemsJson
          .whereType<Map<Object?, Object?>>()
          .map((li) => _mapLineItem(Map<String, dynamic>.from(li)))
          .toList(growable: false),
      history: historyJson
          .whereType<Map<Object?, Object?>>()
          .map((h) => _mapHistoryPoint(Map<String, dynamic>.from(h)))
          .toList(growable: false),
    );
  }

  SpendingBudgetLineItem _mapLineItem(Map<String, dynamic> json) {
    return SpendingBudgetLineItem(
      id: _readString(json['id']) ?? '',
      name: _readString(json['name']) ?? '',
      allocated: (json['allocated'] as num?)?.toDouble() ?? 0,
      spent: (json['spent'] as num?)?.toDouble() ?? 0,
    );
  }

  SpendingBudgetHistoryPoint _mapHistoryPoint(Map<String, dynamic> json) {
    return SpendingBudgetHistoryPoint(
      label: _readString(json['label']) ?? '',
      amount: (json['amount'] as num?)?.toDouble() ?? 0,
      isCurrent: json['isCurrent'] == true,
    );
  }

  // ═══════════════════════════════════════════════════════════════
  // Parsing helpers
  // ═══════════════════════════════════════════════════════════════

  IconData _parseIconData(Map<String, dynamic> json) {
    final int codePoint =
        (json['iconCodePoint'] as num?)?.toInt() ?? 0xef8f;
    final String fontFamily =
        _readString(json['iconFontFamily']) ?? 'MaterialIcons';
    return IconData(codePoint, fontFamily: fontFamily);
  }

  SpendingBudgetColorRole _parseAccentRole(Object? value) {
    final String? role = _readString(value);
    switch (role?.toLowerCase()) {
      case 'success':
        return SpendingBudgetColorRole.success;
      case 'warning':
        return SpendingBudgetColorRole.warning;
      case 'danger':
        return SpendingBudgetColorRole.danger;
      case 'info':
        return SpendingBudgetColorRole.info;
      case 'accent':
        return SpendingBudgetColorRole.accent;
      case 'primary':
      default:
        return SpendingBudgetColorRole.primary;
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
      name: 'Payabo.LiveBudgetRepository',
      stackTrace: exception.stackTrace,
    );
  }
}
