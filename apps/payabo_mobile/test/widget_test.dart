import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/app.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';

void main() {
  testWidgets('app boots on index splash', (WidgetTester tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          appEnvironmentProvider.overrideWithValue(
            const AppEnvironment(
              flavor: AppFlavor.dev,
              useMocks: true,
              apiBaseUrl: 'https://localhost:5001',
            ),
          ),
        ],
        child: const PayaboApp(),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Tap logo to continue'), findsOneWidget);
  });
}
