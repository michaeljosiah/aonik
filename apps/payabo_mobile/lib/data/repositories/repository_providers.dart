import 'dart:ui';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/auth/auth_session_store.dart';
import '../../app/demo/demo_data_mode.dart';
import '../../app/demo/demo_mode.dart';
import '../../app/environment/environment_provider.dart';
import '../../features/setup_journey/domain/setup_journey_repository.dart';
import '../../features/support_planning/domain/support_planning_repository.dart';
import '../../mock/repositories/mock_account_links_repository.dart';
import '../../mock/repositories/mock_auth_repository.dart';
import '../../mock/repositories/mock_budget_repository.dart';
import '../../mock/repositories/mock_catalog_repository.dart';
import '../../mock/repositories/mock_chat_repository.dart';
import '../../mock/repositories/mock_community_repository.dart';
import '../../mock/repositories/mock_dashboard_repository.dart';
import '../../mock/repositories/mock_notification_repository.dart';
import '../../mock/repositories/mock_order_repository.dart';
import '../../mock/repositories/mock_payment_repository.dart';
import '../../mock/repositories/mock_personal_transactions_repository.dart';
import '../../mock/repositories/mock_profile_repository.dart';
import '../../mock/repositories/mock_setup_journey_repository.dart';
import '../../mock/repositories/mock_spending_category_repository.dart';
import '../../mock/repositories/mock_spending_repository.dart';
import '../../mock/repositories/mock_support_planning_repository.dart';
import '../api/api_client.dart';
import 'account_links_repository.dart';
import 'auth_repository.dart';
import 'budget_repository.dart';
import 'catalog_repository.dart';
import 'chat_repository.dart';
import 'community_repository.dart';
import 'dashboard_repository.dart';
import 'live_account_links_repository.dart';
import 'live_auth_repository.dart';
import 'live_personal_transactions_repository.dart';
import 'live_profile_repository.dart';
import 'live_setup_journey_repository.dart';
import 'notification_repository.dart';
import 'order_repository.dart';
import 'payment_repository.dart';
import 'personal_transactions_repository.dart';
import 'profile_repository.dart';
import 'spending_category_repository.dart';
import 'spending_repository.dart';

/// True when the app should use mock implementations for unfinished modules or
/// because the current session is running in demo mode.
bool _shouldMock(Ref ref) {
  return ref.watch(appEnvironmentProvider).useMocks ||
      ref.watch(isDemoProvider);
}

/// Returns a callback that resolves the current set of active connection IDs
/// from the [MockAccountLinksRepository] instance, or `null` if the account
/// links repository is not a mock. This enables cross-repository coordination
/// in demo mode: when an account link is disconnected, spending/category/
/// transactions repositories automatically filter out that connection's data.
Set<String> Function()? _activeConnectionIdsGetter(Ref ref) {
  final AccountLinksRepository accountLinksRepo =
      ref.watch(accountLinksRepositoryProvider);
  if (accountLinksRepo is MockAccountLinksRepository) {
    return accountLinksRepo.getActiveConnectionIds;
  }
  return null;
}

final Provider<AuthRepository> authRepositoryProvider =
    Provider<AuthRepository>(
  (Ref ref) {
    if (ref.watch(isDemoProvider)) {
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

final Provider<ChatRepository> chatRepositoryProvider =
    Provider<ChatRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    // Chat remains mock-backed until a live repository is implemented.
    return MockChatRepository(demoDataMode: demoDataMode);
  },
);

final Provider<CommunityRepository> communityRepositoryProvider =
    Provider<CommunityRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    // Community remains mock-backed until a live repository is implemented.
    return MockCommunityRepository(demoDataMode: demoDataMode);
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
    return LiveAccountLinksRepository(
      apiClient: apiClient,
      dateLocale: PlatformDispatcher.instance.locale.toLanguageTag(),
    );
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

final Provider<NotificationRepository> notificationRepositoryProvider =
    Provider<NotificationRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    // Notifications remain mock-backed until a live repository is implemented.
    return MockNotificationRepository(demoDataMode: demoDataMode);
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

final Provider<PersonalTransactionsRepository>
    personalTransactionsRepositoryProvider =
    Provider<PersonalTransactionsRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    if (_shouldMock(ref)) {
      return MockPersonalTransactionsRepository(
        demoDataMode: demoDataMode,
        activeConnectionIdsGetter: _activeConnectionIdsGetter(ref),
      );
    }

    final apiClient = ref.watch(apiClientProvider);
    return LivePersonalTransactionsRepository(apiClient: apiClient);
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

final Provider<SetupJourneyRepository> setupJourneyRepositoryProvider =
    Provider<SetupJourneyRepository>(
  (Ref ref) {
    if (_shouldMock(ref)) {
      return MockSetupJourneyRepository();
    }

    final apiClient = ref.watch(apiClientProvider);
    return LiveSetupJourneyRepository(apiClient: apiClient);
  },
);

final Provider<SpendingCategoryRepository> spendingCategoryRepositoryProvider =
    Provider<SpendingCategoryRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    // Spending categories remain mock-backed until a live repository is
    // implemented.
    return MockSpendingCategoryRepository(
      demoDataMode: demoDataMode,
      activeConnectionIdsGetter: _activeConnectionIdsGetter(ref),
    );
  },
);

final Provider<SpendingRepository> spendingRepositoryProvider =
    Provider<SpendingRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    // Spending remains mock-backed until a live repository is implemented.
    return MockSpendingRepository(
      demoDataMode: demoDataMode,
      activeConnectionIdsGetter: _activeConnectionIdsGetter(ref),
    );
  },
);

final Provider<SupportPlanningRepository> supportPlanningRepositoryProvider =
    Provider<SupportPlanningRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    // Support planning remains mock-backed until a live repository is
    // implemented.
    return MockSupportPlanningRepository(demoDataMode: demoDataMode);
  },
);
