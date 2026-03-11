import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_category_detail_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('spending category detail renders structure',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const SpendingCategoryDetailScreen(categoryId: 'shopping'),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Shopping'), findsOneWidget);
    expect(find.text('March spend'), findsOneWidget);
    expect(find.byType(LineChart), findsOneWidget);

    final Finder primaryScrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('1 active spending alert'),
      220,
      scrollable: primaryScrollable,
    );
    expect(find.text('1 active spending alert'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('1 Transaction'),
      260,
      scrollable: primaryScrollable,
    );
    await tester.scrollUntilVisible(
      find.text('Uber Eats'),
      220,
      scrollable: primaryScrollable,
    );
    expect(find.text('Uber Eats'), findsOneWidget);
    expect(find.text('Current Account'), findsOneWidget);
  });

  testWidgets('spending category detail shows empty state in fresh demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const SpendingCategoryDetailScreen(categoryId: 'shopping'),
        demoDataMode: DemoDataMode.fresh,
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Shopping'), findsOneWidget);
    expect(find.text('No shopping transactions yet'), findsOneWidget);
    expect(find.byType(LineChart), findsNothing);
  });
}
