import 'dart:io';

import 'package:dio/dio.dart';
import 'package:dio/io.dart';

import '../../app/environment/app_environment.dart';

void configureDioTransportImpl(
  Dio dio,
  AppEnvironment environment, {
  required String baseUrl,
}) {
  if (environment.isProduction) {
    return;
  }

  final uri = Uri.tryParse(baseUrl);
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

  dio.httpClientAdapter = IOHttpClientAdapter(
    createHttpClient: () {
      final client = HttpClient();
      client.badCertificateCallback =
          (X509Certificate cert, String certHost, int port) {
        final normalizedHost = certHost.toLowerCase();
        return normalizedHost == 'localhost' ||
            normalizedHost == '127.0.0.1' ||
            normalizedHost == '10.0.2.2' ||
            normalizedHost == '10.0.3.2';
      };
      return client;
    },
  );
}
