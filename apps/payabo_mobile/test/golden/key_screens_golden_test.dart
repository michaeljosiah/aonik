import 'dart:ui' as ui;

import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/features/dashboard/presentation/dashboard_screen.dart';
import 'package:payabo_mobile/features/payments/presentation/payment_country_screen.dart';
import 'package:payabo_mobile/features/profile/presentation/profile_screen.dart';

import '../test_helpers.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('dashboard populated golden', (WidgetTester tester) async {
    await tester.binding.setSurfaceSize(const ui.Size(390, 844));
    await tester.pumpWidget(buildTestApp(const DashboardScreen()));
    await tester.pumpAndSettle();
    await expectLater(
      find.byType(DashboardScreen),
      matchesGoldenFile('goldens/dashboard_populated.png'),
    );
  }, skip: true);

  testWidgets('payment country golden', (WidgetTester tester) async {
    await tester.binding.setSurfaceSize(const ui.Size(390, 844));
    await tester.pumpWidget(buildTestApp(const PaymentCountryScreen()));
    await tester.pumpAndSettle();
    await expectLater(
      find.byType(PaymentCountryScreen),
      matchesGoldenFile('goldens/payment_country.png'),
    );
  }, skip: true);

  testWidgets('profile home golden', (WidgetTester tester) async {
    await tester.binding.setSurfaceSize(const ui.Size(390, 844));
    await tester.pumpWidget(buildTestApp(const ProfileScreen()));
    await tester.pumpAndSettle();
    await expectLater(
      find.byType(ProfileScreen),
      matchesGoldenFile('goldens/profile_home.png'),
    );
  }, skip: true);
}
