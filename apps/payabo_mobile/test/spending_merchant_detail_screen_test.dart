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

    expect(find.text('Current Account'), findsOneWidget);
    expect(find.text("That's all your transactions."), findsOneWidget);
  });
}
