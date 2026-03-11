import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_budget_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('budget screen renders summary and expandable categories',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingBudgetScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Spend'), findsOneWidget);
    expect(find.text('Budgets'), findsOneWidget);
    expect(find.text('Monthly budget'), findsOneWidget);

    final Finder primaryList = find.byType(ListView).first;
    await tester.drag(primaryList, const Offset(0, -280));
    await tester.pumpAndSettle();

    expect(find.text('Category budgets'), findsOneWidget);
    expect(find.text('Housing'), findsOneWidget);
    expect(find.text('Rent'), findsOneWidget);

    await tester.tap(find.text('Housing'));
    await tester.pumpAndSettle();

    expect(find.text('Rent'), findsNothing);

    await tester.tap(find.text('Housing'));
    await tester.pumpAndSettle();

    expect(find.text('Rent'), findsOneWidget);
  });

  testWidgets('budget screen shows empty state in fresh demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const SpendingBudgetScreen(),
        demoDataMode: DemoDataMode.fresh,
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Start planning'), findsOneWidget);

    final Finder primaryList = find.byType(ListView).first;
    await tester.drag(primaryList, const Offset(0, -260));
    await tester.pumpAndSettle();

    expect(find.text('No budgets set yet'), findsOneWidget);
    expect(find.text('Housing'), findsNothing);
  });
}
