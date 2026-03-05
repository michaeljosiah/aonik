import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/auth/auth_session_store.dart';
import '../../app/environment/app_environment.dart';
import '../../app/environment/environment_provider.dart';
import 'dio_transport.dart';

final Provider<Dio> apiClientProvider = Provider<Dio>(
  (Ref ref) {
    final environment = ref.watch(appEnvironmentProvider);
    final authSessionStore = ref.watch(authSessionStoreProvider);
    Future<AuthSession?>? refreshInFlight;

    final baseUrl = environment.runtimeApiBaseUrl;

    final dio = Dio(
      BaseOptions(
        baseUrl: baseUrl,
        connectTimeout: const Duration(seconds: 30),
        receiveTimeout: const Duration(seconds: 30),
      ),
    );
    configureDioTransport(dio, environment, baseUrl: baseUrl);

    dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) async {
          options.headers['Accept'] = 'application/json';

          if (environment.tenantId.isNotEmpty) {
            options.headers['X-Tenant-Id'] = environment.tenantId;
          }

          if (options.data != null &&
              options.data is! FormData &&
              options.headers['Content-Type'] == null) {
            options.headers['Content-Type'] = 'application/json';
          }

          AuthSession? session = await authSessionStore.read();
          final isTokenExchangeRequest =
              options.path.toLowerCase().endsWith('/auth/token');

          if (!isTokenExchangeRequest && session != null && session.isExpired) {
            refreshInFlight ??= _refreshSession(
              environment: environment,
              baseUrl: baseUrl,
              tenantId: environment.tenantId,
              expiredSession: session,
              authSessionStore: authSessionStore,
            );

            try {
              session = await refreshInFlight;
            } finally {
              refreshInFlight = null;
            }
          }

          if (session != null && session.hasAccessToken && !session.isExpired) {
            options.headers['Authorization'] =
                '${session.tokenType} ${session.accessToken}';
          }

          handler.next(options);
        },
      ),
    );

    ref.onDispose(dio.close);

    return dio;
  },
);

Future<AuthSession?> _refreshSession({
  required AppEnvironment environment,
  required String baseUrl,
  required String tenantId,
  required AuthSession expiredSession,
  required AuthSessionStore authSessionStore,
}) async {
  final refreshToken = expiredSession.refreshToken?.trim();
  if (refreshToken == null || refreshToken.isEmpty) {
    await authSessionStore.clear();
    return null;
  }

  final refreshDio = Dio(
    BaseOptions(
      baseUrl: baseUrl,
      connectTimeout: const Duration(seconds: 30),
      receiveTimeout: const Duration(seconds: 30),
    ),
  );
  configureDioTransport(refreshDio, environment, baseUrl: baseUrl);

  try {
    final headers = <String, String>{
      'Accept': 'application/json',
      'Content-Type': 'application/json',
    };

    if (tenantId.trim().isNotEmpty) {
      headers['X-Tenant-Id'] = tenantId.trim();
    }

    final response = await refreshDio.post<Map<String, dynamic>>(
      '/auth/token',
      data: <String, dynamic>{
        'grantType': 'refresh_token',
        'clientId': environment.authClientId,
        'refreshToken': refreshToken,
        'scope': 'openid profile email',
      },
      options: Options(headers: headers),
    );

    final payload = response.data ?? const <String, dynamic>{};
    final accessToken = (payload['accessToken'] as String?)?.trim() ?? '';
    final tokenType = (payload['tokenType'] as String?)?.trim() ?? 'Bearer';
    final refreshedToken = (payload['refreshToken'] as String?)?.trim();
    final expiresIn = (payload['expiresIn'] as num?)?.toInt() ?? 0;

    if (accessToken.isEmpty) {
      await authSessionStore.clear();
      return null;
    }

    final expiresAt =
        expiresIn > 0 ? DateTime.now().add(Duration(seconds: expiresIn)) : null;

    final refreshedSession = AuthSession(
      accessToken: accessToken,
      tokenType: tokenType,
      refreshToken: (refreshedToken == null || refreshedToken.isEmpty)
          ? expiredSession.refreshToken
          : refreshedToken,
      expiresAt: expiresAt,
    );

    await authSessionStore.write(refreshedSession);
    return refreshedSession;
  } on DioException {
    await authSessionStore.clear();
    return null;
  } finally {
    refreshDio.close();
  }
}
