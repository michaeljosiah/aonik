import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/features/spending/presentation/spending_merchant_detail_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('spending merchant detail renders merchant data',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const SpendingMerchantDetailScreen(merchantId: 'uber'),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Uber'), findsWidgets);
    expect(find.text('March spend'), findsOneWidget);
    expect(find.byType(LineChart), findsOneWidget);

    final Finder primaryScrollable = find.byType(Scrollable).first;
    await tester.scrollUntilVisible(
      find.text('Current Account'),
      260,
      scrollable: primaryScrollable,
    );

    await tester.scrollUntilVisible(
      find.text("That's all your transactions."),
      260,
      scrollable: primaryScrollable,
    );

    expect(find.text('Current Account'), findsOneWidget);
    expect(find.text("That's all your transactions."), findsOneWidget);
  });

  testWidgets(
      'spending merchant detail shows live empty state when not in demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const SpendingMerchantDetailScreen(merchantId: 'uber'),
        isDemo: false,
      ),
    );
    await tester.pumpAndSettle();

    // Header should still show the merchant title.
    expect(find.text('Uber'), findsOneWidget);

    // Should show the live-mode empty state card.
    expect(find.text('No uber data yet'), findsOneWidget);

    // Should NOT show any populated demo data.
    expect(find.text('March spend'), findsNothing);
    expect(find.byType(LineChart), findsNothing);
    expect(find.text('Current Account'), findsNothing);
  });
}
