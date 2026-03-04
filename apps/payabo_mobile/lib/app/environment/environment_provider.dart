import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app_environment.dart';

final Provider<AppEnvironment> appEnvironmentProvider =
    Provider<AppEnvironment>(
  (Ref ref) => AppEnvironment.fromDefines(),
);
