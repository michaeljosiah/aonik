import 'package:dio/dio.dart';

import '../../app/auth/auth_session_store.dart';
import '../api/api_exception.dart';
import 'auth_repository.dart';

class LiveAuthRepository implements AuthRepository {
  LiveAuthRepository({
    required Dio apiClient,
    required AuthSessionStore authSessionStore,
    required String tenantId,
    required String authClientId,
  })  : _apiClient = apiClient,
        _authSessionStore = authSessionStore,
        _tenantId = tenantId,
        _authClientId = authClientId;

  final Dio _apiClient;
  final AuthSessionStore _authSessionStore;
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
  Future<AuthOnboardingSnapshot?> registerIndividual(
    RegisterIndividualRequest request,
  ) async {
    try {
      final response = await _apiClient.post<Map<String, dynamic>>(
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

      final payload = response.data ?? const <String, dynamic>{};
      final onboarding = payload['onboarding'];
      if (onboarding is Map) {
        return _mapOnboardingSnapshot(
          Map<String, dynamic>.from(onboarding),
        );
      }

      return null;
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
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/identity/userinfo',
        options: await _authorizedOptions(),
      );
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

  @override
  Future<AuthOnboardingSnapshot?> getOnboardingSnapshot() async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/v1/onboarding/me',
        options: await _authorizedOptions(),
      );

      return _mapOnboardingSnapshot(
        response.data ?? const <String, dynamic>{},
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

  AuthOnboardingSnapshot _mapOnboardingSnapshot(
    Map<String, dynamic> payload,
  ) {
    final gatesPayload = payload['gates'];
    final nextActionsPayload = payload['nextActions'];

    return AuthOnboardingSnapshot(
      userId: _readString(payload['userId']),
      partyId: _readNullableString(payload['partyId']),
      gates: gatesPayload is List
          ? gatesPayload
              .whereType<Map<Object?, Object?>>()
              .map(
                (Map<Object?, Object?> gate) => _mapOnboardingGate(
                  Map<String, dynamic>.from(gate),
                ),
              )
              .toList(growable: false)
          : const <AuthOnboardingGate>[],
      nextActions: nextActionsPayload is List
          ? nextActionsPayload
              .map((dynamic action) => action.toString().trim())
              .where((String action) => action.isNotEmpty)
              .toList(growable: false)
          : const <String>[],
    );
  }

  AuthOnboardingGate _mapOnboardingGate(Map<String, dynamic> payload) {
    final actionsPayload = payload['requiredActions'];

    return AuthOnboardingGate(
      gate: _readString(payload['gate']),
      isSatisfied: payload['isSatisfied'] == true,
      isRequired: payload['isRequired'] == true,
      requiredActions: actionsPayload is List
          ? actionsPayload
              .map((dynamic action) => action.toString().trim())
              .where((String action) => action.isNotEmpty)
              .toList(growable: false)
          : const <String>[],
    );
  }

  String _readString(dynamic value) {
    return value?.toString().trim() ?? '';
  }

  String? _readNullableString(dynamic value) {
    final normalized = value?.toString().trim();
    if (normalized == null || normalized.isEmpty) {
      return null;
    }

    return normalized;
  }

  Future<Options?> _authorizedOptions() async {
    final session = await _authSessionStore.read();
    if (session == null || !session.hasAccessToken || session.isExpired) {
      return null;
    }

    return Options(
      headers: <String, String>{
        'Authorization': '${session.tokenType} ${session.accessToken}',
      },
    );
  }
}
