import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_budget_detail_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('budget detail screen renders and adjusts the draft amount',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(const SpendingBudgetDetailScreen(budgetId: 'transport')),
    );
    await tester.pumpAndSettle();

    expect(find.text('Monthly budget'), findsOneWidget);
    expect(find.text('Transport'), findsOneWidget);

    Text amountText =
        tester.widget<Text>(find.byKey(const Key('budget-amount-value')));
    expect(amountText.data, '£420');

    await tester.tap(find.bySemanticsLabel('Increase budget'));
    await tester.pumpAndSettle();

    amountText =
        tester.widget<Text>(find.byKey(const Key('budget-amount-value')));
    expect(amountText.data, '£445');

    await tester.scrollUntilVisible(
      find.text('View transactions'),
      220,
      scrollable: find.byType(Scrollable).first,
    );
    expect(find.text('View transactions'), findsOneWidget);
  });

  testWidgets('budget detail screen switches categories from the picker',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(const SpendingBudgetDetailScreen(budgetId: 'housing')),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('budget-category-selector')));
    await tester.pumpAndSettle();

    expect(find.text('Switch budget'), findsOneWidget);
    await tester.tap(find.text('Food & Groceries'));
    await tester.pumpAndSettle();

    expect(find.text('Food & Groceries'), findsOneWidget);

    final Text amountText =
        tester.widget<Text>(find.byKey(const Key('budget-amount-value')));
    expect(amountText.data, '£600');
  });

  testWidgets('budget detail screen shows empty state in fresh demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const SpendingBudgetDetailScreen(budgetId: 'housing'),
        demoDataMode: DemoDataMode.fresh,
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Monthly budget'), findsOneWidget);
    expect(find.text('No budgets available yet'), findsOneWidget);
    expect(find.text('View transactions'), findsNothing);
  });
}
