import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/environment/environment_provider.dart';
import '../../mock/repositories/mock_catalog_repository.dart';
import '../../mock/repositories/mock_dashboard_repository.dart';
import '../../mock/repositories/mock_order_repository.dart';
import '../../mock/repositories/mock_payment_repository.dart';
import '../../mock/repositories/mock_profile_repository.dart';
import 'catalog_repository.dart';
import 'dashboard_repository.dart';
import 'order_repository.dart';
import 'payment_repository.dart';
import 'profile_repository.dart';

final Provider<CatalogRepository> catalogRepositoryProvider =
    Provider<CatalogRepository>(
  (Ref ref) {
    final useMocks = ref.watch(appEnvironmentProvider).useMocks;
    if (useMocks) {
      return MockCatalogRepository();
    }

    return MockCatalogRepository();
  },
);

final Provider<DashboardRepository> dashboardRepositoryProvider =
    Provider<DashboardRepository>(
  (Ref ref) {
    final useMocks = ref.watch(appEnvironmentProvider).useMocks;
    if (useMocks) {
      return MockDashboardRepository();
    }

    return MockDashboardRepository();
  },
);

final Provider<OrderRepository> orderRepositoryProvider =
    Provider<OrderRepository>(
  (Ref ref) {
    final useMocks = ref.watch(appEnvironmentProvider).useMocks;
    if (useMocks) {
      return MockOrderRepository();
    }

    return MockOrderRepository();
  },
);

final Provider<PaymentRepository> paymentRepositoryProvider =
    Provider<PaymentRepository>(
  (Ref ref) {
    final useMocks = ref.watch(appEnvironmentProvider).useMocks;
    if (useMocks) {
      return MockPaymentRepository();
    }

    return MockPaymentRepository();
  },
);

final Provider<ProfileRepository> profileRepositoryProvider =
    Provider<ProfileRepository>(
  (Ref ref) {
    final useMocks = ref.watch(appEnvironmentProvider).useMocks;
    if (useMocks) {
      return MockProfileRepository();
    }

    return MockProfileRepository();
  },
);
