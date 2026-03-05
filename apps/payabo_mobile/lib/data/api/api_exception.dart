import 'package:dio/dio.dart';

class ApiException implements Exception {
  const ApiException({
    required this.message,
    this.statusCode,
    this.details,
  });

  final String message;
  final int? statusCode;
  final Object? details;

  @override
  String toString() {
    if (statusCode == null) {
      return message;
    }

    return '$message (HTTP $statusCode)';
  }
}

ApiException mapDioException(DioException exception) {
  final statusCode = exception.response?.statusCode;
  final payload = exception.response?.data;

  String? errorCode;
  String? payloadError;
  String? payloadMessage;

  if (payload is Map<String, dynamic>) {
    final error = payload['error'];
    final message = payload['message'];

    if (error is String && error.trim().isNotEmpty) {
      errorCode = error.trim();
      payloadError = errorCode;
    }

    if (message is String && message.trim().isNotEmpty) {
      payloadMessage = message.trim();
    }
  }

  if ((statusCode == 400 || statusCode == 401) &&
      errorCode?.toLowerCase() == 'invalid_grant') {
    return const ApiException(
      message: 'Invalid email or password.',
      statusCode: 401,
    );
  }

  if (statusCode == 401) {
    return const ApiException(
      message: 'Your session is no longer valid. Please sign in again.',
      statusCode: 401,
    );
  }

  if (exception.type == DioExceptionType.connectionError ||
      exception.type == DioExceptionType.connectionTimeout ||
      exception.type == DioExceptionType.receiveTimeout ||
      exception.type == DioExceptionType.sendTimeout) {
    return const ApiException(
      message:
          'Cannot reach the API right now. Check your connection and API host.',
    );
  }

  final message = payloadError ??
      payloadMessage ??
      exception.message ??
      'Unable to reach Payabo services right now.';

  return ApiException(
    message: message,
    statusCode: statusCode,
    details: payload,
  );
}
