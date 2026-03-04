import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';

import 'package:payabo_mobile/app/app.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('end-to-end mocked friend help flow',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          appEnvironmentProvider.overrideWithValue(
            const AppEnvironment(
              flavor: AppFlavor.dev,
              useMocks: true,
              apiBaseUrl: 'https://api.dev.payabo.local',
            ),
          ),
        ],
        child: const PayaboApp(),
      ),
    );

    await tester.pumpAndSettle();
    await tester.tap(find.text('Tap logo to continue'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ElevatedButton, 'LOGIN'));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).first, 'jane@mail.com');
    await tester.enterText(find.byType(TextField).last, 'Pass1234');
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ElevatedButton, 'LOGIN'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Bills'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Ghana').first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('ECG Power').first);
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).at(0), '123456789');
    await tester.enterText(find.byType(TextField).at(2), '350');
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ElevatedButton, 'PAY NOW'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Request help with payment'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Dany Keys').first);
    await tester.pumpAndSettle();

    await tester.enterText(
      find.byType(TextField).first,
      'Please help me with this month bill.',
    );
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ElevatedButton, 'SEND MESSAGE'));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(ElevatedButton, 'CONFIRM PAYMENT'));
    await tester.pumpAndSettle();

    expect(find.text('Thank you for your order'), findsOneWidget);
  });
}
