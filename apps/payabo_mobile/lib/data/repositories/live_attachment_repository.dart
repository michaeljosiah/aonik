import 'dart:developer' as developer;

import 'package:dio/dio.dart';

import '../api/api_exception.dart';
import 'attachment_repository.dart';
import 'spending_repository.dart';

/// Live implementation of [AttachmentRepository] that calls the
/// personal-finance attachment endpoints via multipart upload.
class LiveAttachmentRepository implements AttachmentRepository {
  LiveAttachmentRepository({required Dio apiClient}) : _apiClient = apiClient;

  final Dio _apiClient;

  @override
  Future<List<Attachment>> getTransactionAttachments(
    String transactionId,
  ) async {
    try {
      final response = await _apiClient.get<List<dynamic>>(
        '/personal-finance/spending/transactions/$transactionId/attachments',
      );

      final List<dynamic> raw = response.data ?? const <dynamic>[];
      return raw
          .whereType<Map<Object?, Object?>>()
          .map((item) => _mapAttachment(Map<String, dynamic>.from(item)))
          .toList(growable: false);
    } on DioException catch (exception) {
      _logDioFailure('getTransactionAttachments', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<Attachment> addTransactionAttachment(
    String transactionId,
    String filePath,
    String fileName,
  ) async {
    try {
      final formData = FormData.fromMap(<String, dynamic>{
        'file': await MultipartFile.fromFile(filePath, filename: fileName),
      });

      final response = await _apiClient.post<Map<String, dynamic>>(
        '/personal-finance/spending/transactions/$transactionId/attachments',
        data: formData,
      );

      final Map<String, dynamic> data = response.data ?? const {};
      return _mapAttachment(data);
    } on DioException catch (exception) {
      _logDioFailure('addTransactionAttachment', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<void> deleteAttachment(String attachmentId) async {
    try {
      await _apiClient.delete<void>(
        '/personal-finance/spending/attachments/$attachmentId',
      );
    } on DioException catch (exception) {
      _logDioFailure('deleteAttachment', exception);
      throw mapDioException(exception);
    }
  }

  // ═══════════════════════════════════════════════════════════════
  // Mapping helpers
  // ═══════════════════════════════════════════════════════════════

  Attachment _mapAttachment(Map<String, dynamic> json) {
    return Attachment(
      id: _readString(json['id']) ?? '',
      fileName: _readString(json['fileName']) ?? 'unknown',
      mimeType: _readString(json['mimeType']) ?? 'application/octet-stream',
      url: _readString(json['url']) ?? '',
      thumbnailUrl: _readString(json['thumbnailUrl']),
      fileSizeBytes: (json['fileSizeBytes'] as num?)?.toInt() ?? 0,
      createdAt: _parseDate(json['createdAt']) ?? DateTime.now(),
    );
  }

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
      name: 'Payabo.LiveAttachmentRepository',
      stackTrace: exception.stackTrace,
    );
  }
}
