import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('spending screen renders mocked sections',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Spend'), findsOneWidget);
    expect(find.text('Transactions'), findsOneWidget);
    expect(find.text('Overview'), findsNothing);
    expect(find.text('Your spending'), findsNothing);
    expect(find.text('Your budget'), findsNothing);
    expect(find.text('February spend'), findsOneWidget);

    final Finder primaryList = find.byType(ListView).first;

    await tester.drag(primaryList, const Offset(0, -520));
    await tester.pumpAndSettle();
    expect(find.text('Categories'), findsOneWidget);

    await tester.drag(primaryList, const Offset(0, -240));
    await tester.pumpAndSettle();
    expect(find.text('Finances'), findsOneWidget);
  });

  testWidgets('spending screen shows empty state in fresh demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const SpendingScreen(),
        demoDataMode: DemoDataMode.fresh,
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Fresh spending state'), findsOneWidget);
    expect(find.text('No spending activity yet'), findsOneWidget);
    expect(find.text('Finances'), findsNothing);
  });
}
