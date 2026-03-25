import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/app/demo/demo_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/data/repositories/repository_providers.dart';
import 'package:payabo_mobile/features/setup_journey/application/setup_journey_controller.dart';
import 'package:payabo_mobile/features/setup_journey/application/setup_step_configs.dart';
import 'package:payabo_mobile/features/setup_journey/domain/setup_enums.dart';
import 'package:payabo_mobile/features/setup_journey/domain/setup_journey_repository.dart';
import 'package:payabo_mobile/features/setup_journey/domain/setup_models.dart';
import 'package:payabo_mobile/mock/repositories/mock_setup_journey_repository.dart';
import 'package:shared_preferences/shared_preferences.dart';

class _BlockingSetupJourneyRepository implements SetupJourneyRepository {
  @override
  Future<void> clearSetupProfile() async {}

  @override
  Future<PayaboSetupProfile?> loadSetupProfile() async => null;

  @override
  Future<void> saveSetupProfile(PayaboSetupProfile profile) {
    return Completer<void>().future;
  }

  @override
  Future<void> triggerNigeriaAccountLink() async {}

  @override
  Future<void> triggerUkAccountLink() async {}
}

void main() {
  late ProviderContainer container;
  late MockSetupJourneyRepository repository;

  setUp(() {
    SharedPreferences.setMockInitialValues(<String, Object>{});
    repository = MockSetupJourneyRepository();
    container = ProviderContainer(
      overrides: [
        setupJourneyRepositoryProvider.overrideWithValue(repository),
        appEnvironmentProvider.overrideWithValue(
          const AppEnvironment(
            flavor: AppFlavor.dev,
            useMocks: true,
            apiBaseUrl: 'https://api.dev.payabo.local',
          ),
        ),
        isDemoProvider.overrideWith((Ref ref) => false),
      ],
    );
  });

  tearDown(() {
    container.dispose();
  });

  SetupJourneyController controller() =>
      container.read(setupJourneyControllerProvider.notifier);

  SetupJourneyState state() => container.read(setupJourneyControllerProvider);

  group('SetupJourneyController — initial state', () {
    test('starts at step 0 with empty profile', () {
      final s = state();

      expect(s.currentStepIndex, 0);
      expect(s.isFirstStep, isTrue);
      expect(s.isLastStep, isFalse);
      expect(s.isReviewing, isFalse);
      expect(s.profile.completed, isFalse);
      expect(s.profile.selectedUseCases, isEmpty);
      expect(s.profile.accountSourceTypes, isEmpty);
      expect(s.profile.responsibilities, isEmpty);
      expect(s.profile.financialGoals, isEmpty);
      expect(s.profile.connectChoice, isNull);
      expect(s.profile.supportType, isNull);
    });

    test('total steps matches setupSteps length', () {
      expect(state().totalSteps, setupSteps.length);
    });

    test('progress is 1/totalSteps at step 0', () {
      expect(state().progress, closeTo(1 / setupSteps.length, 0.001));
    });
  });

  group('SetupJourneyController — navigation', () {
    test('nextStep advances by one', () {
      controller().nextStep();

      expect(state().currentStepIndex, 1);
      expect(state().isFirstStep, isFalse);
    });

    test('previousStep goes back by one', () {
      controller().nextStep();
      controller().nextStep();
      controller().previousStep();

      expect(state().currentStepIndex, 1);
    });

    test('previousStep does nothing at step 0', () {
      controller().previousStep();

      expect(state().currentStepIndex, 0);
    });

    test('nextStep does not exceed last step', () {
      for (var i = 0; i < setupSteps.length + 5; i++) {
        controller().nextStep();
      }

      expect(state().currentStepIndex, setupSteps.length - 1);
      expect(state().isLastStep, isTrue);
    });

    test('goToStep navigates to arbitrary valid index', () {
      controller().goToStep(3);

      expect(state().currentStepIndex, 3);
    });

    test('goToStep ignores out-of-range indices', () {
      controller().goToStep(-1);
      expect(state().currentStepIndex, 0);

      controller().goToStep(setupSteps.length + 10);
      expect(state().currentStepIndex, 0);
    });

    test('navigation clears isReviewing flag', () {
      controller().enterReview();
      expect(state().isReviewing, isTrue);

      controller().nextStep();
      expect(state().isReviewing, isFalse);
    });
  });

  group('SetupJourneyController — multi-select toggles', () {
    test('toggleUseCase adds then removes', () {
      controller().toggleUseCase(SetupUseCase.trackMoney);
      expect(state().profile.selectedUseCases, [SetupUseCase.trackMoney]);

      controller().toggleUseCase(SetupUseCase.saveForGoals);
      expect(state().profile.selectedUseCases,
          [SetupUseCase.trackMoney, SetupUseCase.saveForGoals]);

      controller().toggleUseCase(SetupUseCase.trackMoney);
      expect(state().profile.selectedUseCases, [SetupUseCase.saveForGoals]);
    });

    test('toggleAccountSource adds then removes', () {
      controller().toggleAccountSource(AccountSourceType.ukBank);
      expect(state().profile.accountSourceTypes, [AccountSourceType.ukBank]);

      controller().toggleAccountSource(AccountSourceType.ukBank);
      expect(state().profile.accountSourceTypes, isEmpty);
    });

    test('toggleResponsibility adds then removes', () {
      controller().toggleResponsibility(ResponsibilityType.rentOrMortgage);
      controller().toggleResponsibility(ResponsibilityType.electricity);
      expect(state().profile.responsibilities,
          [ResponsibilityType.rentOrMortgage, ResponsibilityType.electricity]);

      controller().toggleResponsibility(ResponsibilityType.rentOrMortgage);
      expect(
          state().profile.responsibilities, [ResponsibilityType.electricity]);
    });

    test('toggleFinancialGoal adds then removes', () {
      controller().toggleFinancialGoal(FinancialGoalType.saveMore);
      controller().toggleFinancialGoal(FinancialGoalType.buildEmergencyFund);
      expect(state().profile.financialGoals,
          [FinancialGoalType.saveMore, FinancialGoalType.buildEmergencyFund]);

      controller().toggleFinancialGoal(FinancialGoalType.saveMore);
      expect(state().profile.financialGoals,
          [FinancialGoalType.buildEmergencyFund]);
    });
  });

  group('SetupJourneyController — single-select setters', () {
    test('setConnectChoice updates profile', () {
      controller().setConnectChoice(SetupConnectChoice.connectUkBank);
      expect(state().profile.connectChoice, SetupConnectChoice.connectUkBank);

      controller().setConnectChoice(SetupConnectChoice.skipForNow);
      expect(state().profile.connectChoice, SetupConnectChoice.skipForNow);
    });

    test('setSupportType updates profile', () {
      controller().setSupportType(SupportType.parents);
      expect(state().profile.supportType, SupportType.parents);

      controller().setSupportType(SupportType.noOne);
      expect(state().profile.supportType, SupportType.noOne);
    });
  });

  group('SetupJourneyController — review mode', () {
    test('enterReview and exitReview toggle flag', () {
      controller().enterReview();
      expect(state().isReviewing, isTrue);

      controller().exitReview();
      expect(state().isReviewing, isFalse);
    });
  });

  group('SetupJourneyController — completion', () {
    test('completeSetup marks profile as completed', () async {
      controller().toggleUseCase(SetupUseCase.trackMoney);
      await controller().completeSetup();

      expect(state().profile.completed, isTrue);
    });

    test('completeSetup persists flag in SharedPreferences', () async {
      await controller().completeSetup();

      final prefs = await SharedPreferences.getInstance();
      expect(prefs.getBool('payabo.setup.completed'), isTrue);
    });

    test('completeSetup does not wait for repository persistence', () async {
      final blockingContainer = ProviderContainer(
        overrides: [
          setupJourneyRepositoryProvider
              .overrideWithValue(_BlockingSetupJourneyRepository()),
          appEnvironmentProvider.overrideWithValue(
            const AppEnvironment(
              flavor: AppFlavor.dev,
              useMocks: false,
              apiBaseUrl: 'https://api.dev.payabo.local',
              tenantId: '3dc5a130-fd09-4918-9f61-b738ddc04baf',
            ),
          ),
          isDemoProvider.overrideWith((Ref ref) => false),
        ],
      );
      addTearDown(blockingContainer.dispose);

      final blockingController =
          blockingContainer.read(setupJourneyControllerProvider.notifier);

      await blockingController.completeSetup().timeout(
            const Duration(milliseconds: 250),
          );

      final prefs = await SharedPreferences.getInstance();
      expect(prefs.getBool('payabo.setup.completed'), isTrue);
      expect(
        blockingContainer
            .read(setupJourneyControllerProvider)
            .profile
            .completed,
        isTrue,
      );
    });

    test('completeSetupLocally marks setup complete immediately', () async {
      final blockingContainer = ProviderContainer(
        overrides: [
          setupJourneyRepositoryProvider
              .overrideWithValue(_BlockingSetupJourneyRepository()),
          appEnvironmentProvider.overrideWithValue(
            const AppEnvironment(
              flavor: AppFlavor.dev,
              useMocks: false,
              apiBaseUrl: 'https://api.dev.payabo.local',
              tenantId: '3dc5a130-fd09-4918-9f61-b738ddc04baf',
            ),
          ),
          isDemoProvider.overrideWith((Ref ref) => false),
        ],
      );
      addTearDown(blockingContainer.dispose);

      final blockingController =
          blockingContainer.read(setupJourneyControllerProvider.notifier);

      blockingController.completeSetupLocally();

      expect(
        blockingContainer
            .read(setupJourneyControllerProvider)
            .profile
            .completed,
        isTrue,
      );
    });

    test('setupCompletedProvider reflects SharedPreferences', () async {
      // Before completing setup
      final beforeValue = await container.read(setupCompletedProvider.future);
      expect(beforeValue, isFalse);

      // Complete setup (writes to SharedPreferences)
      await controller().completeSetup();

      // Re-read the provider (refresh to pick up new value)
      container.invalidate(setupCompletedProvider);
      final afterValue = await container.read(setupCompletedProvider.future);
      expect(afterValue, isTrue);
    });
  });

  group('SetupJourneyController — reset', () {
    test('reset returns to initial state', () {
      controller().toggleUseCase(SetupUseCase.trackMoney);
      controller().nextStep();
      controller().nextStep();
      controller().enterReview();

      controller().reset();

      final s = state();
      expect(s.currentStepIndex, 0);
      expect(s.isReviewing, isFalse);
      expect(s.profile.selectedUseCases, isEmpty);
      expect(s.profile.completed, isFalse);
    });
  });

  group('setupDashboardSeedProvider', () {
    test('builds seed from current profile', () {
      controller().toggleUseCase(SetupUseCase.manageBills);
      controller().toggleFinancialGoal(FinancialGoalType.saveMore);

      final seed = container.read(setupDashboardSeedProvider);

      expect(seed.suggestedModules, isNotEmpty);
      expect(seed.greetingVariant, isNotEmpty);
    });
  });

  group('profile re-entry — reset and clear flow', () {
    test('reset clears all profile data after completing setup', () async {
      // Simulate a full setup journey
      controller().toggleUseCase(SetupUseCase.trackMoney);
      controller().toggleAccountSource(AccountSourceType.ukBank);
      controller().setConnectChoice(SetupConnectChoice.connectUkBank);
      controller().toggleResponsibility(ResponsibilityType.rentOrMortgage);
      controller().setSupportType(SupportType.parents);
      controller().toggleFinancialGoal(FinancialGoalType.saveMore);
      controller().nextStep();
      controller().nextStep();
      await controller().completeSetup();

      // Verify setup is marked complete with data
      expect(state().profile.completed, isTrue);
      expect(state().profile.selectedUseCases, isNotEmpty);
      expect(state().currentStepIndex, 2);

      // Simulate profile screen re-entry: reset controller
      controller().reset();

      // Verify full reset
      expect(state().currentStepIndex, 0);
      expect(state().profile.completed, isFalse);
      expect(state().profile.selectedUseCases, isEmpty);
      expect(state().profile.accountSourceTypes, isEmpty);
      expect(state().profile.connectChoice, isNull);
      expect(state().profile.responsibilities, isEmpty);
      expect(state().profile.supportType, isNull);
      expect(state().profile.financialGoals, isEmpty);
      expect(state().isReviewing, isFalse);
    });

    test('SharedPreferences flag can be cleared for re-entry', () async {
      // Complete setup to set the flag
      await controller().completeSetup();
      final prefs = await SharedPreferences.getInstance();
      expect(prefs.getBool('payabo.setup.completed'), isTrue);

      // Simulate _startSetupJourney clearing the flag
      await prefs.remove('payabo.setup.completed');

      // Verify the flag is gone
      expect(prefs.getBool('payabo.setup.completed'), isNull);

      // Invalidate provider and verify it reads as false
      container.invalidate(setupCompletedProvider);
      final result = await container.read(setupCompletedProvider.future);
      expect(result, isFalse);
    });
  });

  group('setupCompletedProvider — fresh demo mode', () {
    test('returns false when demo mode is fresh, even if flag is set',
        () async {
      // Pre-set the completion flag in SharedPreferences
      final prefs = await SharedPreferences.getInstance();
      await prefs.setBool('payabo.setup.completed', true);

      // Create a container with DemoDataMode.fresh
      final freshContainer = ProviderContainer(
        overrides: [
          setupJourneyRepositoryProvider
              .overrideWithValue(MockSetupJourneyRepository()),
          appEnvironmentProvider.overrideWithValue(
            const AppEnvironment(
              flavor: AppFlavor.dev,
              useMocks: true,
              apiBaseUrl: 'https://api.dev.payabo.local',
            ),
          ),
          isDemoProvider.overrideWith((Ref ref) => true),
          initialDemoDataModeProvider.overrideWithValue(DemoDataMode.fresh),
        ],
      );
      addTearDown(freshContainer.dispose);

      final result = await freshContainer.read(setupCompletedProvider.future);
      expect(result, isFalse,
          reason: 'Fresh demo mode should always report setup as incomplete');
    });

    test('returns true when demo mode is populated and flag is set', () async {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setBool('payabo.setup.completed', true);

      // Create a container with DemoDataMode.populated (default)
      final populatedContainer = ProviderContainer(
        overrides: [
          setupJourneyRepositoryProvider
              .overrideWithValue(MockSetupJourneyRepository()),
          appEnvironmentProvider.overrideWithValue(
            const AppEnvironment(
              flavor: AppFlavor.dev,
              useMocks: true,
              apiBaseUrl: 'https://api.dev.payabo.local',
            ),
          ),
          isDemoProvider.overrideWith((Ref ref) => true),
          initialDemoDataModeProvider.overrideWithValue(DemoDataMode.populated),
        ],
      );
      addTearDown(populatedContainer.dispose);

      final result =
          await populatedContainer.read(setupCompletedProvider.future);
      expect(result, isTrue,
          reason: 'Populated mode should respect the persisted flag');
    });

    test('returns false when demo mode is populated and flag is not set',
        () async {
      // SharedPreferences has no setup flag (set by setUp())
      final populatedContainer = ProviderContainer(
        overrides: [
          setupJourneyRepositoryProvider
              .overrideWithValue(MockSetupJourneyRepository()),
          appEnvironmentProvider.overrideWithValue(
            const AppEnvironment(
              flavor: AppFlavor.dev,
              useMocks: true,
              apiBaseUrl: 'https://api.dev.payabo.local',
            ),
          ),
          isDemoProvider.overrideWith((Ref ref) => true),
          initialDemoDataModeProvider.overrideWithValue(DemoDataMode.populated),
        ],
      );
      addTearDown(populatedContainer.dispose);

      final result =
          await populatedContainer.read(setupCompletedProvider.future);
      expect(result, isFalse,
          reason: 'Populated mode without flag should report incomplete');
    });
  });

  group('setupCompletedProvider — live backend mode', () {
    test('returns repository completion state when live mode is enabled',
        () async {
      final liveRepository = MockSetupJourneyRepository();
      await liveRepository.saveSetupProfile(
        const PayaboSetupProfile(
          selectedUseCases: <SetupUseCase>[SetupUseCase.trackMoney],
          completed: true,
        ),
      );

      final liveContainer = ProviderContainer(
        overrides: [
          setupJourneyRepositoryProvider.overrideWithValue(liveRepository),
          appEnvironmentProvider.overrideWithValue(
            const AppEnvironment(
              flavor: AppFlavor.dev,
              useMocks: false,
              apiBaseUrl: 'https://api.dev.payabo.local',
              tenantId: '3dc5a130-fd09-4918-9f61-b738ddc04baf',
            ),
          ),
          isDemoProvider.overrideWith((Ref ref) => false),
          initialDemoDataModeProvider.overrideWithValue(DemoDataMode.populated),
        ],
      );
      addTearDown(liveContainer.dispose);

      final result = await liveContainer.read(setupCompletedProvider.future);
      expect(result, isTrue);
    });

    test('returns false when live mode has no stored setup profile', () async {
      final liveContainer = ProviderContainer(
        overrides: [
          setupJourneyRepositoryProvider
              .overrideWithValue(MockSetupJourneyRepository()),
          appEnvironmentProvider.overrideWithValue(
            const AppEnvironment(
              flavor: AppFlavor.dev,
              useMocks: false,
              apiBaseUrl: 'https://api.dev.payabo.local',
              tenantId: '3dc5a130-fd09-4918-9f61-b738ddc04baf',
            ),
          ),
          isDemoProvider.overrideWith((Ref ref) => false),
          initialDemoDataModeProvider.overrideWithValue(DemoDataMode.populated),
        ],
      );
      addTearDown(liveContainer.dispose);

      final result = await liveContainer.read(setupCompletedProvider.future);
      expect(result, isFalse);
    });
  });
}
