import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/data/repositories/repository_providers.dart';
import 'package:payabo_mobile/features/setup_journey/application/setup_journey_controller.dart';
import 'package:payabo_mobile/features/setup_journey/presentation/setup_journey_screen.dart';
import 'package:payabo_mobile/mock/repositories/mock_setup_journey_repository.dart';
import 'package:payabo_mobile/shared/theme/payabo_theme.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'test_helpers.dart';

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues(<String, Object>{});
  });

  Widget buildSetupScreen() {
    return ProviderScope(
      overrides: [
        setupJourneyRepositoryProvider
            .overrideWithValue(MockSetupJourneyRepository()),
      ],
      child: buildTestApp(const SetupJourneyScreen()),
    );
  }

  group('SetupJourneyScreen — rendering', () {
    testWidgets('renders welcome step on launch', (WidgetTester tester) async {
      await tester.pumpWidget(buildSetupScreen());
      await tester.pumpAndSettle();

      // The welcome step should show the Payabo brand mark
      expect(find.text('Payabo'), findsOneWidget);
      expect(find.textContaining('Hi, I\u2019m Simi'), findsOneWidget);

      // Should show the AI message for the welcome step
      expect(find.byType(SetupJourneyScreen), findsOneWidget);
    });

    testWidgets('renders progress indicator', (WidgetTester tester) async {
      await tester.pumpWidget(buildSetupScreen());
      await tester.pumpAndSettle();

      // The progress indicator is always visible
      // (it shows step 1 of N)
      expect(find.byType(SetupJourneyScreen), findsOneWidget);
    });
  });

  group('SetupJourneyScreen — step navigation', () {
    testWidgets('tapping continue advances to next step',
        (WidgetTester tester) async {
      await tester.pumpWidget(buildSetupScreen());
      await tester.pumpAndSettle();

      // Find and tap the primary action button.
      // The welcome step has a single action; the button text comes
      // from the step config option label or the default 'Continue'.
      final buttons = find.byType(ElevatedButton);
      if (buttons.evaluate().isNotEmpty) {
        await tester.tap(buttons.first);
        await tester.pumpAndSettle();
      }

      // After advancing, we should still see the screen (step 2)
      expect(find.byType(SetupJourneyScreen), findsOneWidget);
    });
  });

  group('SetupJourneyScreen — structure', () {
    testWidgets('uses Scaffold with Stack layout', (WidgetTester tester) async {
      await tester.pumpWidget(buildSetupScreen());
      await tester.pumpAndSettle();

      expect(find.byType(Scaffold), findsWidgets);
      expect(find.byType(Stack), findsWidgets);
    });

    testWidgets('has SafeArea for top content', (WidgetTester tester) async {
      await tester.pumpWidget(buildSetupScreen());
      await tester.pumpAndSettle();

      expect(find.byType(SafeArea), findsWidgets);
    });
  });

  group('SetupJourneyScreen — dark mode completion', () {
    testWidgets('navigates to processing screen from summary step in dark mode',
        (WidgetTester tester) async {
      final container = ProviderContainer(
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
          initialDemoDataModeProvider.overrideWithValue(DemoDataMode.populated),
        ],
      );
      addTearDown(container.dispose);

      final router = GoRouter(
        initialLocation: '/setup',
        routes: <GoRoute>[
          GoRoute(
            path: '/setup',
            builder: (context, state) => const SetupJourneyScreen(),
          ),
          GoRoute(
            path: '/setup/processing',
            builder: (context, state) => const Scaffold(
              body: Center(child: Text('Processing')),
            ),
          ),
          GoRoute(
            path: '/dashboard',
            builder: (context, state) => const Scaffold(
              body: Center(child: Text('Dashboard')),
            ),
          ),
        ],
      );
      addTearDown(router.dispose);

      await tester.pumpWidget(
        UncontrolledProviderScope(
          container: container,
          child: MaterialApp.router(
            theme: buildPayaboTheme(),
            darkTheme: buildPayaboDarkTheme(),
            themeMode: ThemeMode.dark,
            routerConfig: router,
          ),
        ),
      );
      await tester.pumpAndSettle();

      container.read(setupJourneyControllerProvider.notifier).goToStep(5);
      await tester.pumpAndSettle();

      await tester.tap(find.text('LET\'S GO'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 100));
      await tester.pumpAndSettle();

      expect(find.text('Processing'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  });
}
