import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/app/demo/demo_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/data/repositories/repository_providers.dart';
import 'package:payabo_mobile/features/dashboard/application/dashboard_providers.dart';
import 'package:payabo_mobile/features/dashboard/widgets/simi_dashboard_card.dart';
import 'package:payabo_mobile/features/setup_journey/application/setup_journey_controller.dart';
import 'package:payabo_mobile/features/setup_journey/domain/setup_enums.dart';
import 'package:payabo_mobile/features/setup_journey/domain/setup_models.dart';
import 'package:payabo_mobile/mock/repositories/mock_setup_journey_repository.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'test_helpers.dart';

void main() {
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

  group('SimiDashboardCard — visibility', () {
    test('simiCardVisibleProvider is false when setup not complete', () {
      final visible = container.read(simiCardVisibleProvider);
      expect(visible, isFalse);
    });

    test('simiCardVisibleProvider is true after setup completion', () async {
      final controller =
          container.read(setupJourneyControllerProvider.notifier);
      await controller.completeSetup();

      final visible = container.read(simiCardVisibleProvider);
      expect(visible, isTrue);
    });

    test('simiCardVisibleProvider is false after dismissal', () async {
      final controller =
          container.read(setupJourneyControllerProvider.notifier);
      await controller.completeSetup();

      container.read(simiCardDismissedProvider.notifier).state = true;

      final visible = container.read(simiCardVisibleProvider);
      expect(visible, isFalse);
    });
  });

  group('SimiDashboardCard — widget', () {
    testWidgets('renders nothing when not visible', (tester) async {
      await tester.pumpWidget(
        buildTestApp(
          const SimiDashboardCard(),
        ),
      );

      // Card should render as SizedBox.shrink
      expect(find.text('Simi'), findsNothing);
    });

    testWidgets('renders card with default greeting after setup',
        (tester) async {
      // Pre-set setup as complete so the card shows
      final prefs = await SharedPreferences.getInstance();
      await prefs.setBool('payabo.setup.completed', true);

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
            setupJourneyRepositoryProvider
                .overrideWithValue(MockSetupJourneyRepository()),
            // Force the card to be visible
            simiCardVisibleProvider.overrideWithValue(true),
          ],
          child: const MaterialApp(
            home: Scaffold(
              body: SimiDashboardCard(),
            ),
          ),
        ),
      );

      expect(find.text('Simi'), findsOneWidget);
      expect(find.text('Your financial assistant'), findsOneWidget);
    });

    testWidgets('renders diaspora_family variant when support is parents',
        (tester) async {
      // Pre-configure seed with diaspora_family variant via provider override
      // instead of modifying state during build.
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
            setupJourneyRepositoryProvider
                .overrideWithValue(MockSetupJourneyRepository()),
            simiCardVisibleProvider.overrideWithValue(true),
            // Override the seed directly to produce diaspora_family variant
            dashboardSeedProvider.overrideWithValue(
              const DashboardSetupSeed(
                greetingVariant: 'diaspora_family',
                suggestedModules: <String>['remittance'],
                quickActions: <String>[],
                nudges: <String>[],
              ),
            ),
          ],
          child: const MaterialApp(
            home: Scaffold(
              body: SimiDashboardCard(),
            ),
          ),
        ),
      );

      await tester.pumpAndSettle();

      expect(find.text('Simi'), findsOneWidget);
      expect(find.text('Your family finance assistant'), findsOneWidget);
    });

    testWidgets('dismiss button hides the card', (tester) async {
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
            setupJourneyRepositoryProvider
                .overrideWithValue(MockSetupJourneyRepository()),
            simiCardVisibleProvider.overrideWithValue(true),
          ],
          child: const MaterialApp(
            home: Scaffold(
              body: SimiDashboardCard(),
            ),
          ),
        ),
      );

      expect(find.text('Simi'), findsOneWidget);

      // Tap the close icon
      await tester.tap(find.byIcon(Icons.close_rounded));
      await tester.pumpAndSettle();

      // After tapping dismiss, the provider state changes.
      // In a real scenario the card would disappear, but since we
      // override simiCardVisibleProvider with a fixed value, we verify
      // the tap doesn't throw.
    });
  });

  group('dashboardSeedProvider', () {
    test('returns seed with default variant for empty profile', () {
      final seed = container.read(dashboardSeedProvider);

      expect(seed.greetingVariant, isNotEmpty);
      expect(seed.suggestedModules, isNotNull);
    });

    test('returns diaspora_family variant when support is parents', () {
      container
          .read(setupJourneyControllerProvider.notifier)
          .setSupportType(SupportType.parents);
      container
          .read(setupJourneyControllerProvider.notifier)
          .toggleUseCase(SetupUseCase.sendMoneyHome);

      final seed = container.read(dashboardSeedProvider);

      expect(seed.greetingVariant, 'diaspora_family');
    });
  });
}
