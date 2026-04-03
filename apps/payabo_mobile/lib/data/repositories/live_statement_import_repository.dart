import 'dart:developer' as developer;

import 'package:dio/dio.dart';

import '../api/api_exception.dart';
import 'statement_import_repository.dart';

class LiveStatementImportRepository implements StatementImportRepository {
  LiveStatementImportRepository({required Dio apiClient})
      : _apiClient = apiClient;

  final Dio _apiClient;

  // ─── Upload ──────────────────────────────────────────────
  @override
  Future<StatementImportItem> uploadStatement({
    required String personalAccountId,
    required String filePath,
    required String fileName,
  }) async {
    try {
      final formData = FormData.fromMap(<String, dynamic>{
        'personalAccountId': personalAccountId,
        'files': await MultipartFile.fromFile(filePath, filename: fileName),
      });

      final response = await _apiClient.post<Map<String, dynamic>>(
        '/personal-finance/imports/statements',
        data: formData,
      );

      return _mapImportItem(response.data ?? const <String, dynamic>{});
    } on DioException catch (exception) {
      _logDioFailure('uploadStatement', exception);
      throw mapDioException(exception);
    }
  }

  // ─── Get ─────────────────────────────────────────────────
  @override
  Future<StatementImportItem?> getImport(String statementImportId) async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/personal-finance/imports/statements/$statementImportId',
      );

      if (response.data == null) return null;
      return _mapImportItem(response.data!);
    } on DioException catch (exception) {
      if (exception.response?.statusCode == 404) return null;
      _logDioFailure('getImport', exception);
      throw mapDioException(exception);
    }
  }

  // ─── List ────────────────────────────────────────────────
  @override
  Future<List<StatementImportItem>> listImports() async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/personal-finance/imports/statements',
      );

      final data = response.data ?? const <dynamic>[];
      return data
          .cast<Map<String, dynamic>>()
          .map(_mapImportItem)
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('listImports', exception);
      throw mapDioException(exception);
    }
  }

  // ─── List Rows ───────────────────────────────────────────
  @override
  Future<List<StatementImportRowItem>> listImportRows(
    String statementImportId,
  ) async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/personal-finance/imports/statements/$statementImportId/rows',
      );

      final data = response.data ?? const <dynamic>[];
      return data
          .cast<Map<String, dynamic>>()
          .map(_mapRowItem)
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('listImportRows', exception);
      throw mapDioException(exception);
    }
  }

  // ─── Apply ───────────────────────────────────────────────
  @override
  Future<StatementImportApplyResult> applyImport(
    String statementImportId,
  ) async {
    try {
      final response = await _apiClient.post<Map<String, dynamic>>(
        '/personal-finance/imports/statements/$statementImportId/apply',
      );

      final data = response.data ?? const <String, dynamic>{};
      return StatementImportApplyResult(
        statementImportId: _str(data, 'statementImportId'),
        rowsImported: _int(data, 'rowsImported'),
        rowsDuplicate: _int(data, 'rowsDuplicate'),
        rowsFailed: _int(data, 'rowsFailed'),
        status: _str(data, 'status'),
        completedAt: _dateTimeOrNull(data, 'completedAt'),
      );
    } on DioException catch (exception) {
      _logDioFailure('applyImport', exception);
      throw mapDioException(exception);
    }
  }

  // ─── Mapping Helpers ─────────────────────────────────────

  static StatementImportItem _mapImportItem(Map<String, dynamic> data) {
    return StatementImportItem(
      statementImportId: _str(data, 'statementImportId'),
      personalAccountId: _str(data, 'personalAccountId'),
      fileName: _str(data, 'fileName'),
      format: _str(data, 'format'),
      status: _str(data, 'status'),
      rowsTotal: _int(data, 'rowsTotal'),
      rowsParsed: _int(data, 'rowsParsed'),
      rowsImported: _int(data, 'rowsImported'),
      rowsDuplicate: _int(data, 'rowsDuplicate'),
      rowsFailed: _int(data, 'rowsFailed'),
      failureReason: data['failureReason'] as String?,
      startedAt: _dateTimeOrNull(data, 'startedAt'),
      completedAt: _dateTimeOrNull(data, 'completedAt'),
      createdAt:
          _dateTimeOrNull(data, 'createdAt') ?? DateTime.now().toUtc(),
      updatedAt: _dateTimeOrNull(data, 'updatedAt'),
    );
  }

  static StatementImportRowItem _mapRowItem(Map<String, dynamic> data) {
    return StatementImportRowItem(
      statementImportRowId: _str(data, 'statementImportRowId'),
      statementImportId: _str(data, 'statementImportId'),
      rowNumber: _int(data, 'rowNumber'),
      occurredAtRaw: data['occurredAtRaw'] as String?,
      amountRaw: data['amountRaw'] as String?,
      descriptionRaw: data['descriptionRaw'] as String?,
      merchantRaw: data['merchantRaw'] as String?,
      currencyRaw: data['currencyRaw'] as String?,
      normalizedOccurredAt: _dateTimeOrNull(data, 'normalizedOccurredAt'),
      normalizedAmount: _doubleOrNull(data, 'normalizedAmount'),
      normalizedCurrency: data['normalizedCurrency'] as String?,
      normalizedDescription: data['normalizedDescription'] as String?,
      parseStatus: _str(data, 'parseStatus'),
      errorMessage: data['errorMessage'] as String?,
      fingerprint: data['fingerprint'] as String?,
      createdAt:
          _dateTimeOrNull(data, 'createdAt') ?? DateTime.now().toUtc(),
      updatedAt: _dateTimeOrNull(data, 'updatedAt'),
    );
  }

  // ─── Primitive Helpers ───────────────────────────────────

  static String _str(Map<String, dynamic> data, String key) =>
      (data[key]?.toString()) ?? '';

  static int _int(Map<String, dynamic> data, String key) {
    final value = data[key];
    if (value is int) return value;
    if (value is num) return value.toInt();
    return int.tryParse(value?.toString() ?? '') ?? 0;
  }

  static double? _doubleOrNull(Map<String, dynamic> data, String key) {
    final value = data[key];
    if (value == null) return null;
    if (value is double) return value;
    if (value is num) return value.toDouble();
    return double.tryParse(value.toString());
  }

  static DateTime? _dateTimeOrNull(Map<String, dynamic> data, String key) {
    final value = data[key];
    if (value == null) return null;
    if (value is DateTime) return value;
    return DateTime.tryParse(value.toString());
  }

  static void _logDioFailure(String operation, DioException exception) {
    developer.log(
      'StatementImportRepository.$operation failed: '
      '${exception.message} '
      '(status=${exception.response?.statusCode})',
    );
  }
}
