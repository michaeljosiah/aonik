import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/app/demo/demo_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/data/repositories/repository_providers.dart';
import 'package:payabo_mobile/features/support_planning/application/support_planning_controller.dart';
import 'package:payabo_mobile/features/support_planning/domain/support_models.dart';
import 'package:payabo_mobile/features/support_planning/presentation/add_beneficiary_screen.dart';
import 'package:payabo_mobile/mock/repositories/mock_support_planning_repository.dart';
import 'package:shared_preferences/shared_preferences.dart';


void main() {
  group('SupportPlanningController', () {
    late ProviderContainer container;
    late MockSupportPlanningRepository repository;

    setUp(() {
      SharedPreferences.setMockInitialValues(<String, Object>{});
      repository = MockSupportPlanningRepository();
      container = ProviderContainer(
        overrides: [
          supportPlanningRepositoryProvider.overrideWithValue(repository),
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

    test('initial state has empty lists and no loading', () {
      final state = container.read(supportPlanningControllerProvider);

      expect(state.beneficiaries, isEmpty);
      expect(state.plans, isEmpty);
      expect(state.upcomingObligations, isEmpty);
      expect(state.isLoading, isFalse);
      expect(state.error, isNull);
    });

    test('loadAll populates state from repository', () async {
      final controller =
          container.read(supportPlanningControllerProvider.notifier);
      await controller.loadAll();

      final state = container.read(supportPlanningControllerProvider);

      expect(state.beneficiaries, isNotEmpty);
      expect(state.plans, isNotEmpty);
      expect(state.upcomingObligations, isNotEmpty);
      expect(state.isLoading, isFalse);
      expect(state.error, isNull);
    });

    test('addBeneficiary adds to state and returns result', () async {
      final controller =
          container.read(supportPlanningControllerProvider.notifier);

      final result = await controller.addBeneficiary(
        name: 'Test Person',
        relationship: 'Friend',
        location: 'Accra',
        phoneNumber: '+233123456789',
      );

      expect(result, isNotNull);
      expect(result!.name, 'Test Person');
      expect(result.relationship, 'Friend');
      expect(result.location, 'Accra');

      final state = container.read(supportPlanningControllerProvider);
      expect(state.beneficiaries, contains(result));
      expect(state.isLoading, isFalse);
    });

    test('addBeneficiary handles missing optional fields', () async {
      final controller =
          container.read(supportPlanningControllerProvider.notifier);

      final result = await controller.addBeneficiary(
        name: 'Minimal Person',
        relationship: 'Cousin',
      );

      expect(result, isNotNull);
      expect(result!.location, isNull);
      expect(result.phoneNumber, isNull);
    });
  });

  group('SupportPlanningState', () {
    test('copyWith preserves unset fields', () {
      const state = SupportPlanningState(
        beneficiaries: <SupportBeneficiary>[],
        isLoading: true,
        error: 'some error',
      );

      final updated = state.copyWith(isLoading: false);

      expect(updated.isLoading, isFalse);
      expect(updated.error, 'some error');
    });

    test('copyWith with clearError removes error', () {
      const state = SupportPlanningState(
        error: 'something went wrong',
      );

      final updated = state.copyWith(clearError: true);

      expect(updated.error, isNull);
    });
  });

  group('AddBeneficiaryScreen — widget', () {
    testWidgets('renders Simi context message and form', (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            appEnvironmentProvider.overrideWithValue(
              const AppEnvironment(
                flavor: AppFlavor.dev,
                useMocks: true,
                apiBaseUrl: 'https://api.dev.payabo.local',
              ),
            ),
            isDemoProvider.overrideWith((Ref ref) => true),
            initialDemoDataModeProvider
                .overrideWithValue(DemoDataMode.populated),
            supportPlanningRepositoryProvider
                .overrideWithValue(MockSupportPlanningRepository()),
          ],
          child: const MaterialApp(
            home: AddBeneficiaryScreen(),
          ),
        ),
      );

      await tester.pumpAndSettle();

      // App bar title
      expect(find.text('Add someone you support'), findsOneWidget);

      // Simi context message
      expect(
        find.textContaining('I noticed you support family members'),
        findsOneWidget,
      );

      // Form fields
      expect(find.text('Name'), findsOneWidget);
      expect(find.text('Relationship'), findsOneWidget);
    });

    testWidgets('form fields are interactive', (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            appEnvironmentProvider.overrideWithValue(
              const AppEnvironment(
                flavor: AppFlavor.dev,
                useMocks: true,
                apiBaseUrl: 'https://api.dev.payabo.local',
              ),
            ),
            isDemoProvider.overrideWith((Ref ref) => true),
            initialDemoDataModeProvider
                .overrideWithValue(DemoDataMode.populated),
            supportPlanningRepositoryProvider
                .overrideWithValue(MockSupportPlanningRepository()),
          ],
          child: const MaterialApp(
            home: AddBeneficiaryScreen(),
          ),
        ),
      );

      await tester.pumpAndSettle();

      // Find and interact with the name field (first TextFormField in the form)
      final nameField = find.byType(TextFormField).first;
      expect(nameField, findsOneWidget);

      await tester.enterText(nameField, 'Mama Akua');
      expect(find.text('Mama Akua'), findsOneWidget);
    });
  });
}
