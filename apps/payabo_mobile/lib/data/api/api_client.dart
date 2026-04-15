import 'dart:developer' as developer;

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
          final requestStopwatch = Stopwatch()..start();
          final isAgUiRequest = options.path == '/ai/agui';
          final traceId = options.headers['X-AgUi-Trace-Id']?.toString();

          if (isAgUiRequest) {
            developer.log(
              '[trace:${traceId ?? '-'}] apiClient onRequest start path=${options.path}',
              name: 'ApiClient',
            );
          }

          _applyCommonHeaders(options, tenantId: environment.tenantId);

          AuthSession? session = await authSessionStore.read();
          final bool isTokenExchangeRequest =
              options.path.toLowerCase().endsWith('/auth/token');

          if (isAgUiRequest) {
            developer.log(
              '[trace:${traceId ?? '-'}] auth session read at ${requestStopwatch.elapsedMilliseconds}ms hasSession=${session != null} isExpired=${session?.isExpired ?? false}',
              name: 'ApiClient',
            );
          }

          if (!isTokenExchangeRequest && session != null && session.isExpired) {
            if (isAgUiRequest) {
              developer.log(
                '[trace:${traceId ?? '-'}] starting token refresh at ${requestStopwatch.elapsedMilliseconds}ms',
                name: 'ApiClient',
              );
            }

            refreshInFlight ??= sessionManager.refreshExpiredSession(session);

            try {
              session = await refreshInFlight;
              if (isAgUiRequest) {
                developer.log(
                  '[trace:${traceId ?? '-'}] token refresh completed at ${requestStopwatch.elapsedMilliseconds}ms refreshed=${session != null}',
                  name: 'ApiClient',
                );
              }
            } finally {
              refreshInFlight = null;
            }
          }

          if (session != null && session.hasAccessToken && !session.isExpired) {
            options.headers['Authorization'] =
                '${session.tokenType} ${session.accessToken}';
          }

          if (isAgUiRequest) {
            developer.log(
              '[trace:${traceId ?? '-'}] apiClient onRequest finish at ${requestStopwatch.elapsedMilliseconds}ms hasAuthorization=${options.headers.containsKey('Authorization')}',
              name: 'ApiClient',
            );
          }

          handler.next(options);
        },
        onError: (error, handler) async {
          final bool isTokenExchangeRequest =
              error.requestOptions.path.toLowerCase().endsWith('/auth/token');
          final bool isAgUiRequest = error.requestOptions.path == '/ai/agui';
          final traceId =
              error.requestOptions.headers['X-AgUi-Trace-Id']?.toString();

          final bool alreadyRetried =
              error.requestOptions.extra['_hasRetried401'] == true;

          if (error.response?.statusCode == 401 &&
              !isTokenExchangeRequest &&
              !alreadyRetried) {
            // Token may have been revoked server-side. Attempt a refresh
            // and retry the original request once.
            final AuthSession? session = await authSessionStore.read();
            if (session != null) {
              refreshInFlight ??= sessionManager.refreshExpiredSession(session);

              try {
                final AuthSession? refreshed = await refreshInFlight;
                if (refreshed != null &&
                    refreshed.hasAccessToken &&
                    !refreshed.isExpired) {
                  final retryOptions = error.requestOptions;
                  retryOptions.headers['Authorization'] =
                      '${refreshed.tokenType} ${refreshed.accessToken}';
                  retryOptions.extra['_hasRetried401'] = true;

                  if (isAgUiRequest) {
                    developer.log(
                      '[trace:${traceId ?? '-'}] retrying /ai/agui after 401 refresh',
                      name: 'ApiClient',
                    );
                  }

                  final response = await dio.fetch<dynamic>(retryOptions);
                  return handler.resolve(response);
                }
              } catch (_) {
                // Refresh failed — fall through to original error.
              } finally {
                refreshInFlight = null;
              }
            }
          }

          if (isAgUiRequest) {
            developer.log(
              '[trace:${traceId ?? '-'}] request error status=${error.response?.statusCode} type=${error.type} message=${error.message}',
              name: 'ApiClient',
            );
          }

          handler.next(error);
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
