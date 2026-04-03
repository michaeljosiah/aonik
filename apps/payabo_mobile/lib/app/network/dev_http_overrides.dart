import '../environment/app_environment.dart';
import 'dev_http_overrides_stub.dart'
    if (dart.library.io) 'dev_http_overrides_io.dart';

void configureDevHttpOverrides(AppEnvironment environment) {
  configureDevHttpOverridesImpl(environment);
}
