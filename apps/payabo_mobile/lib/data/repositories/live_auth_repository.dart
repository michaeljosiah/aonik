import 'package:dio/dio.dart';

import '../api/api_exception.dart';
import 'auth_repository.dart';

class LiveAuthRepository implements AuthRepository {
  LiveAuthRepository({
    required Dio apiClient,
    required String tenantId,
    required String authClientId,
  })  : _apiClient = apiClient,
        _tenantId = tenantId,
        _authClientId = authClientId;

  final Dio _apiClient;
  final String _tenantId;
  final String _authClientId;

  @override
  Future<AuthTokenResult> signInWithPassword({
    required String email,
    required String password,
  }) async {
    try {
      final response = await _apiClient.post<Map<String, dynamic>>(
        '/auth/token',
        data: <String, dynamic>{
          'grantType': 'password',
          'clientId': _authClientId,
          'username': email.trim(),
          'password': password,
          'scope': 'openid profile email',
        },
      );

      return _mapTokenResponse(
        response.data ?? const <String, dynamic>{},
        errorMessage: 'Login failed because no access token was returned.',
      );
    } on DioException catch (exception) {
      throw mapDioException(exception);
    }
  }

  @override
  Future<AuthTokenResult> refreshAccessToken({
    required String refreshToken,
  }) async {
    try {
      final response = await _apiClient.post<Map<String, dynamic>>(
        '/auth/token',
        data: <String, dynamic>{
          'grantType': 'refresh_token',
          'clientId': _authClientId,
          'refreshToken': refreshToken,
          'scope': 'openid profile email',
        },
      );

      return _mapTokenResponse(
        response.data ?? const <String, dynamic>{},
        errorMessage:
            'Token refresh failed because no access token was returned.',
      );
    } on DioException catch (exception) {
      throw mapDioException(exception);
    }
  }

  @override
  Future<void> registerIndividual(RegisterIndividualRequest request) async {
    try {
      await _apiClient.post<void>(
        '/v1/registrations/individual',
        data: <String, dynamic>{
          'tenantId': _tenantId,
          'registrationCountry': request.registrationCountry,
          'title': request.title,
          'firstName': request.firstName.trim(),
          'lastName': request.lastName.trim(),
          'email': request.email.trim(),
          'phone': request.phone,
          'password': request.password,
        },
      );
    } on DioException catch (exception) {
      throw mapDioException(exception);
    }
  }

  @override
  Future<void> sendPasswordResetEmail(String email) async {
    try {
      await _apiClient.post<void>(
        '/identity/password/forgot',
        data: <String, dynamic>{
          'email': email.trim(),
          'tenantId': _tenantId,
        },
      );
    } on DioException catch (exception) {
      throw mapDioException(exception);
    }
  }

  @override
  Future<AuthUserInfo> getUserInfo() async {
    try {
      final response =
          await _apiClient.get<Map<String, dynamic>>('/identity/userinfo');
      final payload = response.data ?? const <String, dynamic>{};

      final userId = (payload['userId'] as String?)?.trim() ?? '';
      final email = (payload['email'] as String?)?.trim() ?? '';

      if (userId.isEmpty || email.isEmpty) {
        throw const ApiException(
          message: 'User information response is missing required fields.',
        );
      }

      return AuthUserInfo(
        userId: userId,
        email: email,
        firstName: (payload['firstName'] as String?)?.trim(),
        lastName: (payload['lastName'] as String?)?.trim(),
      );
    } on DioException catch (exception) {
      throw mapDioException(exception);
    }
  }

  AuthTokenResult _mapTokenResponse(
    Map<String, dynamic> payload, {
    required String errorMessage,
  }) {
    final accessToken = (payload['accessToken'] as String?)?.trim() ?? '';
    final tokenType = (payload['tokenType'] as String?)?.trim() ?? 'Bearer';
    final expiresIn = (payload['expiresIn'] as num?)?.toInt() ?? 0;

    if (accessToken.isEmpty) {
      throw ApiException(message: errorMessage);
    }

    return AuthTokenResult(
      accessToken: accessToken,
      refreshToken: (payload['refreshToken'] as String?)?.trim(),
      expiresIn: expiresIn,
      tokenType: tokenType,
      idToken: (payload['idToken'] as String?)?.trim(),
    );
  }
}
