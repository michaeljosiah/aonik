import 'dart:convert';

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
  final errorPayload = _ApiErrorPayload.from(payload);
  final friendlyDescription = _sanitizeMessage(errorPayload.message);

  final normalizedMessage = errorPayload.message?.toLowerCase();

  if (errorPayload.errorCode == 'invalid_grant' ||
      _containsAny(normalizedMessage, <String>[
        'invalid email or password',
        'wrong email or password',
        'incorrect email or password',
        'invalid username or password',
        'wrong username or password',
      ])) {
    return ApiException(
      message: friendlyDescription ?? 'Wrong email or password.',
      statusCode: 401,
      details: payload,
    );
  }

  if (statusCode == 429 ||
      errorPayload.errorCode == 'too_many_requests' ||
      _containsAny(normalizedMessage, <String>['too many', 'rate limit'])) {
    return ApiException(
      message: friendlyDescription ??
          'Too many attempts right now. Please wait a moment and try again.',
      statusCode: statusCode ?? 429,
      details: payload,
    );
  }

  if (statusCode == 401) {
    return ApiException(
      message: friendlyDescription ??
          'Your session is no longer valid. Please sign in again.',
      statusCode: 401,
      details: payload,
    );
  }

  if (statusCode == 403) {
    return ApiException(
      message: friendlyDescription ??
          'We could not sign you in with this account right now.',
      statusCode: 403,
      details: payload,
    );
  }

  if (exception.type == DioExceptionType.connectionError ||
      exception.type == DioExceptionType.connectionTimeout ||
      exception.type == DioExceptionType.receiveTimeout ||
      exception.type == DioExceptionType.sendTimeout) {
    return const ApiException(
      message:
          'We could not reach Payabo right now. Check your connection and try again.',
    );
  }

  if (statusCode != null && statusCode >= 500) {
    return ApiException(
      message: 'Payabo is having trouble right now. Please try again shortly.',
      statusCode: statusCode,
      details: payload,
    );
  }

  final message = friendlyDescription ??
      (statusCode == null
          ? 'Unable to reach Payabo services right now.'
          : 'We couldn\'t complete that request right now. Please try again.');

  return ApiException(
    message: message,
    statusCode: statusCode,
    details: payload,
  );
}

class _ApiErrorPayload {
  const _ApiErrorPayload({
    this.errorCode,
    this.message,
  });

  final String? errorCode;
  final String? message;

  factory _ApiErrorPayload.from(Object? payload) {
    if (payload is String) {
      return _ApiErrorPayload.from(_extractEmbeddedPayload(payload));
    }

    if (payload is! Map) {
      return const _ApiErrorPayload();
    }

    final rawError = _readString(payload['error']);
    final embeddedPayload = _extractEmbeddedPayload(rawError);
    final embeddedError = _readString(embeddedPayload?['error']);
    final rawMessage = _firstNonEmpty(<String?>[
      _readString(payload['message']),
      _readString(payload['error_description']),
      _readString(payload['errorDescription']),
      _readString(payload['detail']),
      _readString(payload['title']),
      _readString(embeddedPayload?['message']),
      _readString(embeddedPayload?['error_description']),
      _readString(embeddedPayload?['errorDescription']),
      _readString(embeddedPayload?['detail']),
      _readString(embeddedPayload?['title']),
    ]);

    final errorCode = _firstNonEmpty(<String?>[
      _normalizeErrorCode(rawError),
      _normalizeErrorCode(embeddedError),
    ]);
    final message = _firstNonEmpty(<String?>[
      rawMessage,
      if (rawError != null && !_looksLikeErrorCode(rawError)) rawError,
      if (embeddedError != null && !_looksLikeErrorCode(embeddedError))
        embeddedError,
    ]);

    return _ApiErrorPayload(
      errorCode: errorCode,
      message: message,
    );
  }
}

Map<String, dynamic>? _extractEmbeddedPayload(String? value) {
  final trimmed = value?.trim();
  if (trimmed == null || trimmed.isEmpty) {
    return null;
  }

  try {
    final decoded = jsonDecode(trimmed);
    if (decoded is Map<String, dynamic>) {
      return decoded;
    }
  } catch (_) {
    // Ignore and fall back to embedded JSON detection.
  }

  final startIndex = trimmed.indexOf('{');
  final endIndex = trimmed.lastIndexOf('}');
  if (startIndex < 0 || endIndex <= startIndex) {
    return null;
  }

  final candidate = trimmed.substring(startIndex, endIndex + 1);

  try {
    final decoded = jsonDecode(candidate);
    if (decoded is Map<String, dynamic>) {
      return decoded;
    }
  } catch (_) {
    return null;
  }

  return null;
}

bool _containsAny(String? value, List<String> candidates) {
  if (value == null || value.isEmpty) {
    return false;
  }

  for (final candidate in candidates) {
    if (value.contains(candidate)) {
      return true;
    }
  }

  return false;
}

String? _sanitizeMessage(String? message) {
  final trimmed = message?.trim();
  if (trimmed == null || trimmed.isEmpty) {
    return null;
  }

  final normalized = trimmed.toLowerCase();
  if (_containsAny(normalized, <String>[
    'auth0',
    'azure ad',
    'oauth',
    'token exchange',
    'grant_type',
    'client id',
    'redirect uri',
    'tenant',
    'x-tenant-id',
    'api host',
    'status code',
    'http/',
  ])) {
    return null;
  }

  if (trimmed.contains('{') || trimmed.contains('}')) {
    return null;
  }

  return trimmed;
}

String? _firstNonEmpty(List<String?> values) {
  for (final value in values) {
    if (value != null && value.isNotEmpty) {
      return value;
    }
  }

  return null;
}

String? _readString(Object? value) {
  if (value is! String) {
    return null;
  }

  final trimmed = value.trim();
  return trimmed.isEmpty ? null : trimmed;
}

String? _normalizeErrorCode(String? value) {
  if (!_looksLikeErrorCode(value)) {
    return null;
  }

  return value!.toLowerCase();
}

bool _looksLikeErrorCode(String? value) {
  if (value == null || value.isEmpty) {
    return false;
  }

  return RegExp(r'^[A-Za-z0-9_.-]+$').hasMatch(value);
}
