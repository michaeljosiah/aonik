import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/auth/auth_session_manager.dart';
import '../../app/auth/auth_session_store.dart';
import '../../app/environment/app_environment.dart';
import '../../app/environment/environment_provider.dart';
import 'dio_transport.dart';

final Provider<Dio> authApiClientProvider = Provider<Dio>(
  (Ref ref) {
    final AppEnvironment environment = ref.watch(appEnvironmentProvider);
    final String baseUrl = environment.runtimeApiBaseUrl;
    final Dio dio = _createConfiguredDio(environment, baseUrl: baseUrl);

    dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) {
          _applyCommonHeaders(options, tenantId: environment.tenantId);
          handler.next(options);
        },
      ),
    );

    ref.onDispose(dio.close);

    return dio;
  },
);

final Provider<Dio> apiClientProvider = Provider<Dio>(
  (Ref ref) {
    final AppEnvironment environment = ref.watch(appEnvironmentProvider);
    final AuthSessionStore authSessionStore =
        ref.watch(authSessionStoreProvider);
    final AuthSessionManager sessionManager =
        ref.watch(authSessionManagerProvider);
    Future<AuthSession?>? refreshInFlight;

    final String baseUrl = environment.runtimeApiBaseUrl;
    final Dio dio = _createConfiguredDio(environment, baseUrl: baseUrl);

    dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) async {
          _applyCommonHeaders(options, tenantId: environment.tenantId);

          AuthSession? session = await authSessionStore.read();
          final bool isTokenExchangeRequest =
              options.path.toLowerCase().endsWith('/auth/token');

          if (!isTokenExchangeRequest && session != null && session.isExpired) {
            refreshInFlight ??= sessionManager.refreshExpiredSession(session);

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

Dio _createConfiguredDio(
  AppEnvironment environment, {
  required String baseUrl,
}) {
  final Dio dio = Dio(
    BaseOptions(
      baseUrl: baseUrl,
      connectTimeout: const Duration(seconds: 30),
      receiveTimeout: const Duration(seconds: 30),
    ),
  );
  configureDioTransport(dio, environment, baseUrl: baseUrl);
  return dio;
}

void _applyCommonHeaders(
  RequestOptions options, {
  required String tenantId,
}) {
  options.headers.putIfAbsent('Accept', () => 'application/json');

  if (tenantId.isNotEmpty) {
    options.headers['X-Tenant-Id'] = tenantId;
  }

  if (options.data != null &&
      options.data is! FormData &&
      options.headers['Content-Type'] == null) {
    options.headers['Content-Type'] = 'application/json';
  }
}
