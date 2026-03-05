import 'package:dio/dio.dart';

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
      throw mapDioException(exception);
    }
  }

  UserProfile _mapProfile(Map<String, dynamic> payload) {
    final firstName = (payload['firstName'] as String?)?.trim() ?? '';
    final lastName = (payload['lastName'] as String?)?.trim() ?? '';
    final email = (payload['email'] as String?)?.trim() ?? '';
    final phone = (payload['phone'] as String?)?.trim() ?? '';
    final countryCode =
        ((payload['countryCode'] as String?)?.trim() ?? '').toUpperCase();

    return UserProfile(
      firstName: firstName,
      lastName: lastName,
      email: email,
      phone: phone,
      countryCode: countryCode,
    );
  }
}
