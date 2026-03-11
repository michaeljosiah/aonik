import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/features/spending/presentation/spending_overview_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('spending overview shows the monthly overview card',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingOverviewScreen()));
    await tester.pumpAndSettle();

    final Finder primaryList = find.byType(ListView);

    for (int index = 0;
        index < 6 && find.text('Income').evaluate().isEmpty;
        index++) {
      await tester.drag(primaryList, const Offset(0, -420));
      await tester.pumpAndSettle();
    }

    expect(find.text('Income'), findsOneWidget);
    expect(find.text('Expenses'), findsOneWidget);
    expect(find.text('Investments'), findsOneWidget);
    expect(find.text('£4,232.24'), findsOneWidget);
    expect(find.text('£2,660.12'), findsOneWidget);
    expect(find.text('£1,754.64'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
