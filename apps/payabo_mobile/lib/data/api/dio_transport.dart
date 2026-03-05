import 'package:dio/dio.dart';

import '../../app/environment/app_environment.dart';
import 'dio_transport_stub.dart' if (dart.library.io) 'dio_transport_io.dart';

void configureDioTransport(
  Dio dio,
  AppEnvironment environment, {
  required String baseUrl,
}) {
  configureDioTransportImpl(dio, environment, baseUrl: baseUrl);
}
