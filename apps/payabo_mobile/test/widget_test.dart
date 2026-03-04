import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/app.dart';

void main() {
  testWidgets('app boots on index splash', (WidgetTester tester) async {
    await tester.pumpWidget(
      const ProviderScope(
        child: PayaboApp(),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Tap logo to continue'), findsOneWidget);
  });
}
