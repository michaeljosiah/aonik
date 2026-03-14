import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/features/dashboard/presentation/dashboard_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('dashboard shows overview card above upcoming bills',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const DashboardScreen()));
    await tester.pumpAndSettle();

    final Finder primaryList = find.byType(ListView).first;

    for (int index = 0;
        index < 6 && find.text('Upcoming bills').evaluate().isEmpty;
        index++) {
      await tester.drag(primaryList, const Offset(0, -280));
      await tester.pumpAndSettle();
    }

    expect(find.text('Overview'), findsOneWidget);
    expect(find.text('Income'), findsOneWidget);
    expect(find.text('Expenses'), findsOneWidget);
    expect(find.text('Investments'), findsOneWidget);
    expect(find.text('₵4,232.24'), findsOneWidget);
    expect(find.text('₵2,660.12'), findsOneWidget);
    expect(find.text('₵1,754.64'), findsOneWidget);

    final double overviewTop = tester.getTopLeft(find.text('Overview')).dy;
    final double upcomingBillsTop =
        tester.getTopLeft(find.text('Upcoming bills')).dy;

    expect(overviewTop, lessThan(upcomingBillsTop));
    expect(tester.takeException(), isNull);
  });
}
