import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/features/spending/presentation/spending_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('spending screen renders mocked sections',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Updated today, 06:43'), findsOneWidget);
    expect(find.text('February spend'), findsOneWidget);
    expect(find.byType(LineChart), findsOneWidget);

    final Finder primaryScrollable = find.byType(Scrollable).first;

    await tester.scrollUntilVisible(
      find.text('Categories'),
      300,
      scrollable: primaryScrollable,
    );
    expect(find.text('Categories'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('Finances'),
      220,
      scrollable: primaryScrollable,
    );
    expect(find.text('Finances'), findsOneWidget);
  });
}
