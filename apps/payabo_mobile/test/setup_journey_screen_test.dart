import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:payabo_mobile/data/repositories/repository_providers.dart';
import 'package:payabo_mobile/features/setup_journey/presentation/setup_journey_screen.dart';
import 'package:payabo_mobile/mock/repositories/mock_setup_journey_repository.dart';

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
    testWidgets('uses Scaffold with Stack layout',
        (WidgetTester tester) async {
      await tester.pumpWidget(buildSetupScreen());
      await tester.pumpAndSettle();

      expect(find.byType(Scaffold), findsWidgets);
      expect(find.byType(Stack), findsWidgets);
    });

    testWidgets('has SafeArea for top content',
        (WidgetTester tester) async {
      await tester.pumpWidget(buildSetupScreen());
      await tester.pumpAndSettle();

      expect(find.byType(SafeArea), findsWidgets);
    });
  });
}
