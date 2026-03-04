enum AppFlavor {
  dev,
  staging,
  prod,
}

class AppEnvironment {
  const AppEnvironment({
    required this.flavor,
    required this.useMocks,
    required this.apiBaseUrl,
  });

  final AppFlavor flavor;
  final bool useMocks;
  final String apiBaseUrl;

  bool get isProduction => flavor == AppFlavor.prod;

  String get label {
    switch (flavor) {
      case AppFlavor.dev:
        return 'DEV';
      case AppFlavor.staging:
        return 'STAGING';
      case AppFlavor.prod:
        return 'PROD';
    }
  }

  static AppEnvironment fromDefines() {
    const String env = String.fromEnvironment('APP_ENV', defaultValue: 'dev');
    const bool useMocks = bool.fromEnvironment('USE_MOCKS', defaultValue: true);
    const String apiBaseUrl = String.fromEnvironment(
      'API_BASE_URL',
      defaultValue: 'https://api.dev.payabo.local',
    );

    return AppEnvironment(
      flavor: _parseFlavor(env),
      useMocks: useMocks,
      apiBaseUrl: apiBaseUrl,
    );
  }

  static AppFlavor _parseFlavor(String rawValue) {
    final value = rawValue.trim().toLowerCase();

    switch (value) {
      case 'staging':
        return AppFlavor.staging;
      case 'prod':
      case 'production':
        return AppFlavor.prod;
      case 'dev':
      default:
        return AppFlavor.dev;
    }
  }
}
