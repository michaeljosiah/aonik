import 'dart:developer' as developer;

import 'package:dio/dio.dart';
import 'package:http_parser/http_parser.dart';
import 'package:path/path.dart' as p;

import '../api/api_exception.dart';
import 'profile_repository.dart';

class LiveProfileRepository implements ProfileRepository {
  LiveProfileRepository({required Dio apiClient}) : _apiClient = apiClient;

  final Dio _apiClient;

  @override
  Future<UserProfile> getProfile() async {
    try {
      final response =
          await _apiClient.get<Map<String, dynamic>>('/profiles/customers/me');
      return _mapProfile(response.data ?? const <String, dynamic>{});
    } on DioException catch (exception) {
      _logDioFailure('getProfile', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<UserProfile> updateProfile(UserProfile profile) async {
    try {
      final response = await _apiClient.put<Map<String, dynamic>>(
        '/profiles/customers/me',
        data: <String, dynamic>{
          'firstName': profile.firstName.trim(),
          'lastName': profile.lastName.trim(),
          'title': null,
          'phone': profile.phone.trim(),
          'countryCode': profile.countryCode.trim(),
        },
      );

      return _mapProfile(response.data ?? const <String, dynamic>{});
    } on DioException catch (exception) {
      _logDioFailure('updateProfile', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<UserProfile> updateEmail({
    required String currentEmail,
    required String newEmail,
    required String password,
  }) async {
    try {
      final response = await _apiClient.put<Map<String, dynamic>>(
        '/profiles/customers/me/email',
        data: <String, dynamic>{
          'currentEmail': currentEmail.trim(),
          'newEmail': newEmail.trim(),
          'password': password,
        },
      );

      return _mapProfile(response.data ?? const <String, dynamic>{});
    } on DioException catch (exception) {
      _logDioFailure('updateEmail', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<void> updatePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    try {
      await _apiClient.put<void>(
        '/profiles/customers/me/password',
        data: <String, dynamic>{
          'currentPassword': currentPassword,
          'newPassword': newPassword,
        },
      );
    } on DioException catch (exception) {
      _logDioFailure('updatePassword', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<String> uploadPhoto(String filePath) async {
    try {
      final filename = p.basename(filePath);
      final ext = p.extension(filePath).toLowerCase();
      final mimeType = switch (ext) {
        '.jpg' || '.jpeg' => 'image/jpeg',
        '.png' => 'image/png',
        '.gif' => 'image/gif',
        '.webp' => 'image/webp',
        _ => 'image/jpeg',
      };

      final formData = FormData.fromMap(<String, dynamic>{
        'photo': await MultipartFile.fromFile(
          filePath,
          filename: filename,
          contentType: MediaType.parse(mimeType),
        ),
      });

      final response = await _apiClient.post<Map<String, dynamic>>(
        '/profiles/customers/me/photo',
        data: formData,
      );

      final data = response.data ?? const <String, dynamic>{};
      final resolvedUrl = _resolveUrl(
        (data['photoUrl'] as String?) ??
            (data['PhotoUrl'] as String?) ??
            (data['url'] as String?) ??
            (data['Url'] as String?),
      );

      developer.log(
        'uploadPhoto response payload: $data | resolvedUrl: ${resolvedUrl ?? '<empty>'}',
        name: 'Payabo.LiveProfileRepository',
      );

      return resolvedUrl ?? '';
    } on DioException catch (exception) {
      _logDioFailure('uploadPhoto', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<void> deletePhoto() async {
    try {
      await _apiClient.delete<void>('/profiles/customers/me/photo');
    } on DioException catch (exception) {
      _logDioFailure('deletePhoto', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<NotificationPreferences> getNotificationPreferences() async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/profiles/customers/me/notifications',
      );

      return _mapNotificationPreferences(
        response.data ?? const <String, dynamic>{},
      );
    } on DioException catch (exception) {
      _logDioFailure('getNotificationPreferences', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<NotificationPreferences> updateNotificationPreferences(
    NotificationPreferences preferences,
  ) async {
    try {
      final response = await _apiClient.put<Map<String, dynamic>>(
        '/profiles/customers/me/notifications',
        data: <String, dynamic>{
          'email': preferences.email.trim(),
          'newBillsPush': preferences.newBillsPush,
          'billUpdatesPush': preferences.billUpdatesPush,
          'billAssistPush': preferences.billAssistPush,
          'mbaMessagesPush': preferences.mbaMessagesPush,
          'orgMessagesPush': preferences.orgMessagesPush,
          'friendsMessagesPush': preferences.friendsMessagesPush,
          'newBillsEmail': preferences.newBillsEmail,
          'billUpdatesEmail': preferences.billUpdatesEmail,
          'billAssistEmail': preferences.billAssistEmail,
          'mbaMessagesEmail': preferences.mbaMessagesEmail,
          'orgMessagesEmail': preferences.orgMessagesEmail,
        },
      );

      return _mapNotificationPreferences(
        response.data ?? const <String, dynamic>{},
      );
    } on DioException catch (exception) {
      _logDioFailure('updateNotificationPreferences', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<MarketingPreferences> getMarketingPreferences() async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/profiles/customers/me/marketing',
      );

      return _mapMarketingPreferences(
        response.data ?? const <String, dynamic>{},
      );
    } on DioException catch (exception) {
      _logDioFailure('getMarketingPreferences', exception);
      throw mapDioException(exception);
    }
  }

  @override
  Future<MarketingPreferences> updateMarketingPreferences(
    MarketingPreferences preferences,
  ) async {
    try {
      final response = await _apiClient.put<Map<String, dynamic>>(
        '/profiles/customers/me/marketing',
        data: <String, dynamic>{
          'email': preferences.email.trim(),
          'news': preferences.news,
          'offers': preferences.offers,
          'surveys': preferences.surveys,
        },
      );

      return _mapMarketingPreferences(
        response.data ?? const <String, dynamic>{},
      );
    } on DioException catch (exception) {
      _logDioFailure('updateMarketingPreferences', exception);
      throw mapDioException(exception);
    }
  }

  void _logDioFailure(String operation, DioException exception) {
    final request = exception.requestOptions;
    final statusCode = exception.response?.statusCode;

    developer.log(
      '$operation failed for ${request.method} ${request.path}${statusCode != null ? ' (HTTP $statusCode)' : ''}.',
      name: 'Payabo.LiveProfileRepository',
      error: exception.response?.data ?? exception.message ?? exception,
      stackTrace: exception.stackTrace,
    );
  }

  UserProfile _mapProfile(Map<String, dynamic> payload) {
    final firstName = (payload['firstName'] as String?)?.trim() ?? '';
    final lastName = (payload['lastName'] as String?)?.trim() ?? '';
    final email = (payload['email'] as String?)?.trim() ?? '';
    final phone = (payload['phone'] as String?)?.trim() ?? '';
    final countryCode =
        ((payload['countryCode'] as String?)?.trim() ?? '').toUpperCase();
    final photoUrl = _resolveUrl(
      (payload['photoUrl'] as String?) ?? (payload['PhotoUrl'] as String?),
    );

    developer.log(
      'Mapped customer profile photoUrl: ${photoUrl ?? '<empty>'}',
      name: 'Payabo.LiveProfileRepository',
    );

    return UserProfile(
      firstName: firstName,
      lastName: lastName,
      email: email,
      phone: phone,
      countryCode: countryCode,
      photoUrl: photoUrl,
    );
  }

  NotificationPreferences _mapNotificationPreferences(
    Map<String, dynamic> payload,
  ) {
    return NotificationPreferences(
      email: (payload['email'] as String?)?.trim() ?? '',
      newBillsPush: payload['newBillsPush'] as bool? ?? true,
      billUpdatesPush: payload['billUpdatesPush'] as bool? ?? true,
      billAssistPush: payload['billAssistPush'] as bool? ?? false,
      mbaMessagesPush: payload['mbaMessagesPush'] as bool? ?? true,
      orgMessagesPush: payload['orgMessagesPush'] as bool? ?? true,
      friendsMessagesPush: payload['friendsMessagesPush'] as bool? ?? false,
      newBillsEmail: payload['newBillsEmail'] as bool? ?? true,
      billUpdatesEmail: payload['billUpdatesEmail'] as bool? ?? true,
      billAssistEmail: payload['billAssistEmail'] as bool? ?? false,
      mbaMessagesEmail: payload['mbaMessagesEmail'] as bool? ?? true,
      orgMessagesEmail: payload['orgMessagesEmail'] as bool? ?? true,
    );
  }

  MarketingPreferences _mapMarketingPreferences(
    Map<String, dynamic> payload,
  ) {
    return MarketingPreferences(
      email: (payload['email'] as String?)?.trim() ?? '',
      news: payload['news'] as bool? ?? true,
      offers: payload['offers'] as bool? ?? true,
      surveys: payload['surveys'] as bool? ?? false,
    );
  }

  String? _resolveUrl(String? value) {
    final trimmed = value?.trim();
    if (trimmed == null || trimmed.isEmpty) {
      return null;
    }

    final uri = Uri.tryParse(trimmed);
    if (uri != null && uri.hasScheme) {
      return trimmed;
    }

    final baseUri = Uri.tryParse(_apiClient.options.baseUrl);
    if (baseUri == null) {
      return trimmed;
    }

    return baseUri.resolve(trimmed).toString();
  }
}
