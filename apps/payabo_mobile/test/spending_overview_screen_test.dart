import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/features/spending/presentation/spending_overview_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('spending overview renders snapshot charts and preview',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingOverviewScreen()));
    await tester.pumpAndSettle();

    final Finder primaryList = find.byType(ListView).first;

    expect(find.text('Spend'), findsOneWidget);
    expect(find.text('Overview'), findsOneWidget);

    await tester.drag(primaryList, const Offset(0, -260));
    await tester.pumpAndSettle();
    expect(find.text('Total balance'), findsOneWidget);

    await tester.drag(primaryList, const Offset(0, -360));
    await tester.pumpAndSettle();
    expect(find.byType(PieChart), findsOneWidget);

    await tester.drag(primaryList, const Offset(0, -460));
    await tester.pumpAndSettle();
    expect(find.byType(LineChart), findsOneWidget);
    expect(find.text('Spend is tracking 6% lower than last month.'),
        findsOneWidget);

    await tester.drag(primaryList, const Offset(0, -520));
    await tester.pumpAndSettle();
    expect(find.text('Your food spending is 12% higher than usual this week.'),
        findsOneWidget);

    await tester.drag(primaryList, const Offset(0, -420));
    await tester.pumpAndSettle();
    expect(find.text('View all transactions'), findsOneWidget);
    expect(find.text('Uber'), findsOneWidget);
  });
}
