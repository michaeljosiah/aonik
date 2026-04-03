import 'dart:io';

import '../environment/app_environment.dart';

void configureDevHttpOverridesImpl(AppEnvironment environment) {
  if (environment.isProduction) {
    return;
  }

  final uri = Uri.tryParse(environment.runtimeApiBaseUrl);
  if (uri == null || uri.scheme.toLowerCase() != 'https') {
    return;
  }

  final host = uri.host.toLowerCase();
  final allowInsecureDevCertificate = host == 'localhost' ||
      host == '127.0.0.1' ||
      host == '10.0.2.2' ||
      host == '10.0.3.2';

  if (!allowInsecureDevCertificate) {
    return;
  }

  HttpOverrides.global = _DevHttpOverrides();
}

class _DevHttpOverrides extends HttpOverrides {
  @override
  HttpClient createHttpClient(SecurityContext? context) {
    final client = super.createHttpClient(context);
    client.badCertificateCallback =
        (X509Certificate cert, String host, int port) {
      final normalizedHost = host.toLowerCase();
      return normalizedHost == 'localhost' ||
          normalizedHost == '127.0.0.1' ||
          normalizedHost == '10.0.2.2' ||
          normalizedHost == '10.0.3.2';
    };
    return client;
  }
}
