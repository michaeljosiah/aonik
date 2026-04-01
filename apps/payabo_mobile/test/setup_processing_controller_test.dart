import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/app/demo/demo_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/data/repositories/repository_providers.dart';
import 'package:payabo_mobile/features/setup_journey/application/setup_journey_controller.dart';
import 'package:payabo_mobile/features/setup_journey/application/setup_processing_controller.dart';
import 'package:payabo_mobile/features/setup_journey/domain/setup_enums.dart';
import 'package:payabo_mobile/features/setup_journey/domain/setup_models.dart';
import 'package:payabo_mobile/mock/repositories/mock_setup_journey_repository.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  group('buildProcessingSteps', () {
    test('includes all unconditional steps for empty profile', () {
      const profile = PayaboSetupProfile();
      final steps = buildProcessingSteps(profile);

      expect(steps.length, 4);
      expect(steps.any((s) => s.id == 'support'), isFalse,
          reason: 'support step should be excluded when supportTypes is empty');
      expect(steps.first.id, 'intro');
      expect(steps.last.id, 'ready');
    });

    test('includes support step when user supports parents', () {
      const profile = PayaboSetupProfile(
          supportTypes: <SupportType>[SupportType.parents]);
      final steps = buildProcessingSteps(profile);

      expect(steps.any((s) => s.id == 'support'), isTrue);
      expect(steps.length, 5);
    });

    test('includes support step when user supports siblings', () {
      const profile = PayaboSetupProfile(
          supportTypes: <SupportType>[SupportType.siblings]);
      final steps = buildProcessingSteps(profile);

      expect(steps.any((s) => s.id == 'support'), isTrue);
    });

    test('excludes support step when user supports noOne', () {
      const profile = PayaboSetupProfile(
          supportTypes: <SupportType>[SupportType.noOne]);
      final steps = buildProcessingSteps(profile);

      expect(steps.any((s) => s.id == 'support'), isFalse);
    });
  });

  group('SetupProcessingState', () {
    test('initial factory sets correct defaults', () {
      final steps = buildProcessingSteps(const PayaboSetupProfile());
      final state = SetupProcessingState.initial(steps);

      expect(state.currentStepIndex, 0);
      expect(state.isComplete, isFalse);
      expect(state.totalSteps, steps.length);
      expect(state.currentStep.id, 'intro');
      expect(state.isLastStep, isFalse);
    });

    test('progress reports correct fraction', () {
      final steps = buildProcessingSteps(const PayaboSetupProfile());
      final state = SetupProcessingState.initial(steps);

      // First step: (0+1)/4 = 0.25
      expect(state.progress, closeTo(1 / steps.length, 0.001));
    });

    test('isLastStep is true at final index', () {
      final steps = buildProcessingSteps(const PayaboSetupProfile());
      final state = SetupProcessingState(
        steps: steps,
        currentStepIndex: steps.length - 1,
        isComplete: false,
      );

      expect(state.isLastStep, isTrue);
    });

    test('copyWith creates new state with updated fields', () {
      final steps = buildProcessingSteps(const PayaboSetupProfile());
      final state = SetupProcessingState.initial(steps);

      final updated = state.copyWith(currentStepIndex: 3, isComplete: true);

      expect(updated.currentStepIndex, 3);
      expect(updated.isComplete, isTrue);
      expect(updated.steps, same(state.steps));
    });
  });

  group('SetupProcessingController', () {
    late ProviderContainer container;

    setUp(() {
      SharedPreferences.setMockInitialValues(<String, Object>{});
      container = ProviderContainer(
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
    });

    tearDown(() {
      container.dispose();
    });

    test('initial state is at step 0 and not complete', () {
      final state = container.read(setupProcessingControllerProvider);

      expect(state.currentStepIndex, 0);
      expect(state.isComplete, isFalse);
      expect(state.steps.isNotEmpty, isTrue);
    });

    test('startProcessing advances steps over time', () async {
      final controller =
          container.read(setupProcessingControllerProvider.notifier);
      controller.startProcessing();

      // Wait for at least one step advancement (1200ms per step + buffer)
      await Future<void>.delayed(const Duration(milliseconds: 1500));

      final state = container.read(setupProcessingControllerProvider);
      expect(state.currentStepIndex, greaterThan(0));
    });

    test('steps provider reflects profile support type', () {
      // Default profile has no support type set
      final steps = container.read(setupProcessingStepsProvider);
      expect(steps.any((s) => s.id == 'support'), isFalse);

      // Set support type to parents
      container
          .read(setupJourneyControllerProvider.notifier)
          .toggleSupportType(SupportType.parents);

      final updatedSteps = container.read(setupProcessingStepsProvider);
      expect(updatedSteps.any((s) => s.id == 'support'), isTrue);
    });
  });
}
