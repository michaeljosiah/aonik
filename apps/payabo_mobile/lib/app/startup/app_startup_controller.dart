import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';

import '../../data/api/dio_transport.dart';
import '../demo/demo_mode.dart';
import '../environment/environment_provider.dart';

class AppStartupState {
  const AppStartupState({
    required this.isChecking,
    required this.isHealthy,
    required this.hasChecked,
    this.message,
  });

  final bool isChecking;
  final bool isHealthy;
  final bool hasChecked;
  final String? message;

  factory AppStartupState.initial() {
    return const AppStartupState(
      isChecking: false,
      isHealthy: false,
      hasChecked: false,
      message: null,
    );
  }

  AppStartupState copyWith({
    bool? isChecking,
    bool? isHealthy,
    bool? hasChecked,
    String? message,
    bool clearMessage = false,
  }) {
    return AppStartupState(
      isChecking: isChecking ?? this.isChecking,
      isHealthy: isHealthy ?? this.isHealthy,
      hasChecked: hasChecked ?? this.hasChecked,
      message: clearMessage ? null : message ?? this.message,
    );
  }
}

class AppStartupController extends StateNotifier<AppStartupState> {
  AppStartupController(this._ref) : super(AppStartupState.initial());

  final Ref _ref;
  bool _isRunning = false;

  Future<void> initialize() async {
    if (_isRunning) {
      return;
    }

    _isRunning = true;
    _ref.read(isDemoProvider.notifier).state = false;
    state = state.copyWith(
      isChecking: true,
      hasChecked: true,
      clearMessage: true,
    );

    final environment = _ref.read(appEnvironmentProvider);
    if (environment.useMocks) {
      state = state.copyWith(
        isChecking: false,
        isHealthy: true,
        message: 'Tap logo to continue',
      );
      _isRunning = false;
      return;
    }

    final baseUrl = environment.runtimeApiBaseUrl;

    final dio = Dio(
      BaseOptions(
        baseUrl: baseUrl,
        connectTimeout: const Duration(seconds: 8),
        receiveTimeout: const Duration(seconds: 8),
        validateStatus: (int? _) => true,
      ),
    );
    configureDioTransport(dio, environment, baseUrl: baseUrl);

    try {
      final response = await dio.get<Map<String, dynamic>>(
        '/health',
        options: Options(
          headers: const <String, String>{
            'Accept': 'application/json',
          },
        ),
      );

      state = state.copyWith(
        isChecking: false,
        isHealthy: true,
        message: response.statusCode != null &&
                response.statusCode! >= 200 &&
                response.statusCode! < 300
            ? 'Tap logo to continue'
            : 'API reachable. Tap logo to continue.',
      );
    } on DioException catch (exception) {
      _ref.read(isDemoProvider.notifier).state = true;
      state = state.copyWith(
        isChecking: false,
        isHealthy: false,
        message: _toStartupMessage(exception, baseUrl),
      );
    } finally {
      dio.close();
      _isRunning = false;
    }
  }

  String _toStartupMessage(DioException exception, String baseUrl) {
    final host = Uri.tryParse(baseUrl)?.host.toLowerCase();

    if (exception.type == DioExceptionType.connectionError ||
        exception.type == DioExceptionType.connectionTimeout ||
        exception.type == DioExceptionType.receiveTimeout ||
        exception.type == DioExceptionType.sendTimeout) {
      if (host == 'localhost' || host == '127.0.0.1') {
        return 'Cannot reach API at $baseUrl. If using Android emulator, use https://10.0.2.2:5001 instead of localhost.';
      }

      if (host == '10.0.2.2') {
        return 'Cannot reach API at $baseUrl. Ensure the API is running and your emulator can access the host machine.';
      }

      return 'Cannot reach API at $baseUrl. Make sure Aonik API is running.';
    }

    if (exception.type == DioExceptionType.badCertificate) {
      return 'TLS certificate trust failed for $baseUrl. Trust the local development certificate and retry.';
    }

    final statusCode = exception.response?.statusCode;
    if (statusCode != null) {
      return 'Startup check failed with HTTP $statusCode.';
    }

    return 'Startup check failed. Please verify API availability and retry.';
  }
}

final StateNotifierProvider<AppStartupController, AppStartupState>
    appStartupControllerProvider =
    StateNotifierProvider<AppStartupController, AppStartupState>(
  AppStartupController.new,
);
