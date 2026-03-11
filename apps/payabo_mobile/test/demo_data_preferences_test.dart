import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/features/payments/presentation/payment_flow_state.dart';
import 'package:payabo_mobile/features/profile/presentation/demo_data_preferences_screen.dart';
import 'package:payabo_mobile/shared/theme/payabo_theme.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  testWidgets('demo data preferences updates the selected mode',
      (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues(<String, Object>{});

    final container = ProviderContainer(
      overrides: [
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

    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: MaterialApp(
          theme: buildPayaboTheme(),
          home: const DemoDataPreferencesScreen(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(container.read(demoDataModeProvider), DemoDataMode.populated);

    await tester.tap(find.text('Fresh demo state'));
    await tester.pumpAndSettle();

    expect(container.read(demoDataModeProvider), DemoDataMode.fresh);
  });

  test('fresh demo mode clears seeded checkout helpers', () {
    SharedPreferences.setMockInitialValues(<String, Object>{});

    final container = ProviderContainer(
      overrides: [
        initialDemoDataModeProvider.overrideWithValue(DemoDataMode.fresh),
      ],
    );
    addTearDown(container.dispose);

    final state = container.read(paymentFlowControllerProvider);

    expect(state.savedCards, isEmpty);
    expect(state.friends, isEmpty);
    expect(state.selectedCardId, isEmpty);
  });
}
