import 'dart:ui';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';

import '../../app/auth/auth_session_store.dart';
import '../../app/demo/demo_data_mode.dart';
import '../../app/demo/demo_mode.dart';
import '../../app/environment/environment_provider.dart';
import '../../features/chat/domain/chat_controller.dart';
import '../../features/setup_journey/domain/setup_journey_repository.dart';
import '../../features/support_planning/domain/support_planning_repository.dart';
import '../../mock/repositories/mock_account_links_repository.dart';
import '../../mock/repositories/mock_attachment_repository.dart';
import '../../mock/repositories/mock_auth_repository.dart';
import '../../mock/repositories/mock_budget_repository.dart';
import '../../mock/repositories/mock_commitments_repository.dart';
import '../../mock/repositories/mock_catalog_repository.dart';
import '../../mock/repositories/mock_chat_repository.dart';
import '../../mock/repositories/mock_community_repository.dart';
import '../../mock/repositories/mock_dashboard_repository.dart';
import '../../mock/repositories/mock_notification_repository.dart';
import '../../mock/repositories/mock_order_repository.dart';
import '../../mock/repositories/mock_pay_activity_repository.dart';
import '../../mock/repositories/mock_payment_repository.dart';
import '../../mock/repositories/mock_personal_transactions_repository.dart';
import '../../mock/repositories/mock_profile_repository.dart';
import '../../mock/repositories/mock_setup_journey_repository.dart';
import '../../mock/repositories/mock_spending_category_repository.dart';
import '../../mock/repositories/mock_spending_repository.dart';
import '../../mock/repositories/mock_statement_import_repository.dart';
import '../../mock/repositories/mock_support_planning_repository.dart';
import '../agui/agui_client.dart';
import '../api/api_client.dart';
import 'account_links_repository.dart';
import 'attachment_repository.dart';
import 'auth_repository.dart';
import 'budget_repository.dart';
import 'commitments_repository.dart';
import 'live_commitments_repository.dart';
import 'catalog_repository.dart';
import 'chat_repository.dart';
import 'community_repository.dart';
import 'dashboard_repository.dart';
import 'live_account_links_repository.dart';
import 'live_attachment_repository.dart';
import 'live_auth_repository.dart';
import 'live_budget_repository.dart';
import 'live_catalog_repository.dart';
import 'live_chat_repository.dart';
import 'live_community_repository.dart';
import 'live_dashboard_repository.dart';
import 'live_order_repository.dart';
import 'live_pay_activity_repository.dart';
import 'live_payment_repository.dart';
import 'live_personal_transactions_repository.dart';
import 'live_profile_repository.dart';
import 'live_setup_journey_repository.dart';
import 'live_spending_category_repository.dart';
import 'live_spending_repository.dart';
import 'live_statement_import_repository.dart';
import 'notification_repository.dart';
import 'order_repository.dart';
import 'pay_activity_repository.dart';
import 'payment_repository.dart';
import 'personal_transactions_repository.dart';
import 'profile_repository.dart';
import 'spending_category_repository.dart';
import 'spending_repository.dart';
import 'statement_import_repository.dart';

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

/// Returns a callback that resolves all runtime-created accounts (both linked
/// via open-banking and added manually) from the [MockAccountLinksRepository]
/// instance, or `null` if the account links repository is not a mock. Runtime
/// accounts have no seed representation in [MockSpendingRepository] and must
/// be synthesised into spending cards, overview snapshots, etc.
List<AccountLinkItem> Function()? _runtimeAccountsGetter(Ref ref) {
  final AccountLinksRepository accountLinksRepo =
      ref.watch(accountLinksRepositoryProvider);
  if (accountLinksRepo is MockAccountLinksRepository) {
    return accountLinksRepo.getRuntimeAccounts;
  }
  return null;
}

// ─────────────────────────────────────────────────────────
//  AG-UI Client
// ─────────────────────────────────────────────────────────

final Provider<AgUiClient> agUiClientProvider = Provider<AgUiClient>(
  (Ref ref) {
    final dio = ref.watch(apiClientProvider);
    return AgUiClient(dio: dio);
  },
);

// ─────────────────────────────────────────────────────────
//  Chat Controller (StateNotifier)
// ─────────────────────────────────────────────────────────

final StateNotifierProvider<ChatController, ChatState> chatControllerProvider =
    StateNotifierProvider<ChatController, ChatState>(
  (Ref ref) {
    final repository = ref.watch(chatRepositoryProvider);
    return ChatController(repository: repository);
  },
);

// ─────────────────────────────────────────────────────────
//  Repositories
// ─────────────────────────────────────────────────────────

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
    if (_shouldMock(ref)) {
      return MockCatalogRepository();
    }

    final apiClient = ref.watch(apiClientProvider);
    return LiveCatalogRepository(apiClient: apiClient);
  },
);

final Provider<ChatRepository> chatRepositoryProvider =
    Provider<ChatRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    if (_shouldMock(ref)) {
      return MockChatRepository(demoDataMode: demoDataMode);
    }

    final agUiClient = ref.watch(agUiClientProvider);
    final apiClient = ref.watch(apiClientProvider);
    return LiveChatRepository(agUiClient: agUiClient, apiClient: apiClient);
  },
);

final Provider<CommunityRepository> communityRepositoryProvider =
    Provider<CommunityRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    if (_shouldMock(ref)) {
      return MockCommunityRepository(demoDataMode: demoDataMode);
    }

    final apiClient = ref.watch(apiClientProvider);
    return LiveCommunityRepository(apiClient: apiClient);
  },
);

final Provider<CommitmentsRepository> commitmentsRepositoryProvider =
    Provider<CommitmentsRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    if (_shouldMock(ref)) {
      return MockCommitmentsRepository(demoDataMode: demoDataMode);
    }

    final apiClient = ref.watch(apiClientProvider);
    return LiveCommitmentsRepository(apiClient: apiClient);
  },
);

final Provider<BudgetRepository> budgetRepositoryProvider =
    Provider<BudgetRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    if (_shouldMock(ref)) {
      return MockBudgetRepository(
        demoDataMode: demoDataMode,
        activeConnectionIdsGetter: _activeConnectionIdsGetter(ref),
        runtimeAccountsGetter: _runtimeAccountsGetter(ref),
      );
    }

    final apiClient = ref.watch(apiClientProvider);
    return LiveBudgetRepository(apiClient: apiClient);
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

final Provider<AttachmentRepository> attachmentRepositoryProvider =
    Provider<AttachmentRepository>(
  (Ref ref) {
    if (_shouldMock(ref)) {
      return MockAttachmentRepository();
    }

    final apiClient = ref.watch(apiClientProvider);
    return LiveAttachmentRepository(apiClient: apiClient);
  },
);

final Provider<DashboardRepository> dashboardRepositoryProvider =
    Provider<DashboardRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    if (_shouldMock(ref)) {
      return MockDashboardRepository(
        demoDataMode: demoDataMode,
        activeConnectionIdsGetter: _activeConnectionIdsGetter(ref),
        runtimeAccountsGetter: _runtimeAccountsGetter(ref),
      );
    }

    final apiClient = ref.watch(apiClientProvider);
    return LiveDashboardRepository(apiClient: apiClient);
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
    if (_shouldMock(ref)) {
      return MockOrderRepository();
    }

    final apiClient = ref.watch(apiClientProvider);
    return LiveOrderRepository(apiClient: apiClient);
  },
);

final Provider<PayActivityRepository> payActivityRepositoryProvider =
    Provider<PayActivityRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    if (_shouldMock(ref)) {
      return MockPayActivityRepository(demoDataMode: demoDataMode);
    }

    final apiClient = ref.watch(apiClientProvider);
    return LivePayActivityRepository(apiClient: apiClient);
  },
);

final Provider<PaymentRepository> paymentRepositoryProvider =
    Provider<PaymentRepository>(
  (Ref ref) {
    if (_shouldMock(ref)) {
      return MockPaymentRepository();
    }

    final apiClient = ref.watch(apiClientProvider);
    return LivePaymentRepository(apiClient: apiClient);
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
        runtimeAccountsGetter: _runtimeAccountsGetter(ref),
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

    if (_shouldMock(ref)) {
      return MockSpendingCategoryRepository(
        demoDataMode: demoDataMode,
        activeConnectionIdsGetter: _activeConnectionIdsGetter(ref),
        runtimeAccountsGetter: _runtimeAccountsGetter(ref),
      );
    }

    final apiClient = ref.watch(apiClientProvider);
    return LiveSpendingCategoryRepository(apiClient: apiClient);
  },
);

final Provider<SpendingRepository> spendingRepositoryProvider =
    Provider<SpendingRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    if (_shouldMock(ref)) {
      return MockSpendingRepository(
        demoDataMode: demoDataMode,
        activeConnectionIdsGetter: _activeConnectionIdsGetter(ref),
        runtimeAccountsGetter: _runtimeAccountsGetter(ref),
      );
    }

    final apiClient = ref.watch(apiClientProvider);
    return LiveSpendingRepository(apiClient: apiClient);
  },
);

final Provider<StatementImportRepository> statementImportRepositoryProvider =
    Provider<StatementImportRepository>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);

    if (_shouldMock(ref)) {
      return MockStatementImportRepository(demoDataMode: demoDataMode);
    }

    final apiClient = ref.watch(apiClientProvider);
    return LiveStatementImportRepository(apiClient: apiClient);
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
