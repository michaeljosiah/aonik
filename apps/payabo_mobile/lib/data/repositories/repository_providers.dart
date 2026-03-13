import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/auth/auth_session_store.dart';
import '../../app/demo/demo_data_mode.dart';
import '../../app/environment/environment_provider.dart';
import '../../app/startup/offline_mode_provider.dart';
import '../../mock/repositories/mock_account_links_repository.dart';
import '../../mock/repositories/mock_auth_repository.dart';
import '../../mock/repositories/mock_budget_repository.dart';
import '../../mock/repositories/mock_catalog_repository.dart';
import '../../mock/repositories/mock_dashboard_repository.dart';
import '../../mock/repositories/mock_order_repository.dart';
import '../../mock/repositories/mock_payment_repository.dart';
import '../../mock/repositories/mock_profile_repository.dart';
import '../api/api_client.dart';
import 'account_links_repository.dart';
import 'auth_repository.dart';
import 'budget_repository.dart';
import 'catalog_repository.dart';
import 'dashboard_repository.dart';
import 'live_account_links_repository.dart';
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
    final apiClient = ref.watch(authApiClientProvider);
    final authSessionStore = ref.watch(authSessionStoreProvider);

    return LiveAuthRepository(
      apiClient: apiClient,
      authSessionStore: authSessionStore,
      tenantId: environment.tenantId,
      authClientId: environment.authClientId,
    );
  },
);

final Provider<CatalogRepository> catalogRepositoryProvider =
    Provider<CatalogRepository>(
  (Ref ref) {
    // Catalog remains mock-backed until a live repository is implemented.
    return MockCatalogRepository();
  },
);

final Provider<BudgetRepository> budgetRepositoryProvider =
    Provider<BudgetRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    // Budgets remain mock-backed until a live repository is implemented.
    return MockBudgetRepository(demoDataMode: demoDataMode);
  },
);

final Provider<AccountLinksRepository> accountLinksRepositoryProvider =
    Provider<AccountLinksRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    if (_shouldMock(ref)) {
      return MockAccountLinksRepository(demoDataMode: demoDataMode);
    }

    final apiClient = ref.watch(apiClientProvider);
    return LiveAccountLinksRepository(apiClient: apiClient);
  },
);

final Provider<DashboardRepository> dashboardRepositoryProvider =
    Provider<DashboardRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    // Dashboard remains mock-backed until a live repository is implemented.
    return MockDashboardRepository(demoDataMode: demoDataMode);
  },
);

final Provider<OrderRepository> orderRepositoryProvider =
    Provider<OrderRepository>(
  (Ref ref) {
    // Orders remain mock-backed until a live repository is implemented.
    return MockOrderRepository();
  },
);

final Provider<PaymentRepository> paymentRepositoryProvider =
    Provider<PaymentRepository>(
  (Ref ref) {
    // Payments remain mock-backed until a live repository is implemented.
    return MockPaymentRepository();
  },
);

final Provider<ProfileRepository> profileRepositoryProvider =
    Provider<ProfileRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    if (_shouldMock(ref)) {
      return MockProfileRepository(demoDataMode: demoDataMode);
    }

    final apiClient = ref.watch(apiClientProvider);
    return LiveProfileRepository(apiClient: apiClient);
  },
);
