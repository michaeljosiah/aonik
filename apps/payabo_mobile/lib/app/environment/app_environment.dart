import 'package:flutter/foundation.dart';

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
    this.tenantId = '',
    this.authClientId = 'Xw3xY2u7FhoLcdc1VjfS0J7Zz6o0jN3R',
    this.accountLinkProvider = 'Plaid',
    this.accountLinkAndroidPackageName = 'com.payabo.mobile',
    this.accountLinkRedirectUri = '',
  });

  final AppFlavor flavor;
  final bool useMocks;
  final String apiBaseUrl;
  final String tenantId;
  final String authClientId;
  final String accountLinkProvider;
  final String accountLinkAndroidPackageName;
  final String accountLinkRedirectUri;

  String get resolvedAccountLinkProvider {
    final String normalized = accountLinkProvider.trim();
    return normalized.isEmpty ? 'Plaid' : normalized;
  }

  bool get usesPlaidAccountLinkProvider =>
      resolvedAccountLinkProvider.toLowerCase() == 'plaid';

  String get resolvedAccountLinkAndroidPackageName {
    final String normalized = accountLinkAndroidPackageName.trim();
    return normalized.isEmpty ? 'com.payabo.mobile' : normalized;
  }

  String? get configuredAccountLinkRedirectUri {
    final String normalized = accountLinkRedirectUri.trim();
    return normalized.isEmpty ? null : normalized;
  }

  String resolveAccountLinkProvider(String? providerCode) {
    final String normalized = providerCode?.trim() ?? '';
    return normalized.isEmpty ? resolvedAccountLinkProvider : normalized;
  }

  String get runtimeApiBaseUrl {
    final normalized = apiBaseUrl.trim().replaceAll(RegExp(r'/+$'), '');
    final uri = Uri.tryParse(normalized);
    if (uri == null || uri.host.isEmpty || kIsWeb) {
      return normalized;
    }

    final host = uri.host.toLowerCase();
    final isLoopback =
        host == 'localhost' || host == '127.0.0.1' || host == '::1';

    if (defaultTargetPlatform == TargetPlatform.android && isLoopback) {
      return uri.replace(host: '10.0.2.2').toString();
    }

    return normalized;
  }

  static final RegExp _guidPattern = RegExp(
    r'^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
    caseSensitive: false,
  );

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
    const bool useMocks =
        bool.fromEnvironment('USE_MOCKS', defaultValue: false);
    const String apiBaseUrl = String.fromEnvironment(
      'API_BASE_URL',
      defaultValue:
          'https://aonik-dev-api.delightfulisland-9fd7c1e7.uksouth.azurecontainerapps.io',
    );
    const String tenantId = String.fromEnvironment(
      'PAYABO_TENANT_ID',
      defaultValue: '7fa21f8e-e433-4077-bd66-4c2af3c67da8',
    );
    const String authClientId = String.fromEnvironment(
      'AUTH0_CLIENT_ID',
      defaultValue: 'Xw3xY2u7FhoLcdc1VjfS0J7Zz6o0jN3R',
    );
    const String accountLinkProvider = String.fromEnvironment(
      'ACCOUNT_LINK_PROVIDER',
      defaultValue: 'Plaid',
    );
    const String accountLinkAndroidPackageName = String.fromEnvironment(
      'ACCOUNT_LINK_ANDROID_PACKAGE_NAME',
      defaultValue: 'com.payabo.mobile',
    );
    const String accountLinkRedirectUri = String.fromEnvironment(
      'ACCOUNT_LINK_REDIRECT_URI',
      defaultValue: '',
    );

    final normalizedTenantId = tenantId.trim();
    final normalizedAuthClientId = authClientId.trim();
    final normalizedAccountLinkProvider = accountLinkProvider.trim();
    final normalizedAccountLinkAndroidPackageName =
        accountLinkAndroidPackageName.trim();
    final normalizedAccountLinkRedirectUri = accountLinkRedirectUri.trim();

    if (!useMocks && !_guidPattern.hasMatch(normalizedTenantId)) {
      throw StateError(
        'Invalid or missing PAYABO_TENANT_ID. Set a valid tenant GUID to match apps/Payabo/.env (VITE_PAYABO_TENANT_ID).',
      );
    }

    if (!useMocks && normalizedAuthClientId.isEmpty) {
      throw StateError(
        'Invalid or missing AUTH0_CLIENT_ID for live auth token exchange.',
      );
    }

    return AppEnvironment(
      flavor: _parseFlavor(env),
      useMocks: useMocks,
      apiBaseUrl: apiBaseUrl,
      tenantId: normalizedTenantId,
      authClientId: normalizedAuthClientId,
      accountLinkProvider: normalizedAccountLinkProvider.isEmpty
          ? 'Plaid'
          : normalizedAccountLinkProvider,
      accountLinkAndroidPackageName:
          normalizedAccountLinkAndroidPackageName.isEmpty
              ? 'com.payabo.mobile'
              : normalizedAccountLinkAndroidPackageName,
      accountLinkRedirectUri: normalizedAccountLinkRedirectUri,
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
