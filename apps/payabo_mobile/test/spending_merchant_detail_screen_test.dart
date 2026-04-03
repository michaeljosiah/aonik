import 'package:fl_chart/fl_chart.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
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

    expect(find.text('Uber'), findsOneWidget);
    expect(find.text('Transactions'), findsOneWidget);
    expect(find.text('Average spend'), findsOneWidget);
    expect(find.text('Total spent'), findsOneWidget);
    expect(find.text('\u00A312.80'), findsOneWidget);
    expect(find.text('\u00A3857.60'), findsOneWidget);
    expect(find.text('67'), findsOneWidget);
    expect(find.byType(LineChart), findsNothing);
  });

  testWidgets('spending merchant detail shows zeroed stats in fresh demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const SpendingMerchantDetailScreen(merchantId: 'uber'),
        demoDataMode: DemoDataMode.fresh,
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Uber'), findsOneWidget);
    expect(find.text('Transactions'), findsOneWidget);
    expect(find.text('Average spend'), findsOneWidget);
    expect(find.text('Total spent'), findsOneWidget);
    expect(find.text('0'), findsOneWidget);
    expect(find.text('\u00A30.00'), findsNWidgets(2));
    expect(find.byType(LineChart), findsNothing);
  });
}
