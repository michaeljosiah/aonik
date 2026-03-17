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

  testWidgets(
      'spending category detail shows live empty state when not in demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const SpendingCategoryDetailScreen(categoryId: 'shopping'),
        isDemo: false,
      ),
    );
    await tester.pumpAndSettle();

    // Header should still show the category title.
    expect(find.text('Shopping'), findsOneWidget);

    // Should show the live-mode empty state card.
    expect(find.text('No shopping data yet'), findsOneWidget);
    expect(
      find.text(
        'Connect a bank account to see your spending insights for shopping here.',
      ),
      findsOneWidget,
    );

    // Should NOT show any populated demo data.
    expect(find.text('March spend'), findsNothing);
    expect(find.byType(LineChart), findsNothing);
    expect(find.text('Uber Eats'), findsNothing);
    expect(find.text('1 active spending alert'), findsNothing);
  });
}
