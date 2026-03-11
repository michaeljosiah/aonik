import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/environment/environment_provider.dart';
import '../../app/startup/offline_mode_provider.dart';
import '../../mock/repositories/mock_auth_repository.dart';
import '../../mock/repositories/mock_catalog_repository.dart';
import '../../mock/repositories/mock_dashboard_repository.dart';
import '../../mock/repositories/mock_order_repository.dart';
import '../../mock/repositories/mock_payment_repository.dart';
import '../../mock/repositories/mock_profile_repository.dart';
import '../api/api_client.dart';
import 'auth_repository.dart';
import 'catalog_repository.dart';
import 'dashboard_repository.dart';
import 'live_auth_repository.dart';
import 'live_profile_repository.dart';
import 'order_repository.dart';
import 'payment_repository.dart';
import 'profile_repository.dart';

/// True when the app should use mock implementations -- either because the
/// compile-time USE_MOCKS flag is set or because the API was unreachable at
/// startup and we fell back to demo mode.
bool _shouldMock(Ref ref) {
  return ref.watch(appEnvironmentProvider).useMocks ||
      ref.watch(offlineModeProvider);
}

final Provider<AuthRepository> authRepositoryProvider =
    Provider<AuthRepository>(
  (Ref ref) {
    if (_shouldMock(ref)) {
      return MockAuthRepository();
    }

    final environment = ref.watch(appEnvironmentProvider);
    final apiClient = ref.watch(apiClientProvider);

    return LiveAuthRepository(
      apiClient: apiClient,
      tenantId: environment.tenantId,
      authClientId: environment.authClientId,
    );
  },
);

final Provider<CatalogRepository> catalogRepositoryProvider =
    Provider<CatalogRepository>(
  (Ref ref) {
    if (_shouldMock(ref)) {
      return MockCatalogRepository();
    }

    return MockCatalogRepository();
  },
);

final Provider<DashboardRepository> dashboardRepositoryProvider =
    Provider<DashboardRepository>(
  (Ref ref) {
    if (_shouldMock(ref)) {
      return MockDashboardRepository();
    }

    return MockDashboardRepository();
  },
);

final Provider<OrderRepository> orderRepositoryProvider =
    Provider<OrderRepository>(
  (Ref ref) {
    if (_shouldMock(ref)) {
      return MockOrderRepository();
    }

    return MockOrderRepository();
  },
);

final Provider<PaymentRepository> paymentRepositoryProvider =
    Provider<PaymentRepository>(
  (Ref ref) {
    if (_shouldMock(ref)) {
      return MockPaymentRepository();
    }

    return MockPaymentRepository();
  },
);

final Provider<ProfileRepository> profileRepositoryProvider =
    Provider<ProfileRepository>(
  (Ref ref) {
    if (_shouldMock(ref)) {
      return MockProfileRepository();
    }

    final apiClient = ref.watch(apiClientProvider);
    return LiveProfileRepository(apiClient: apiClient);
  },
);
