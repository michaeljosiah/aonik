import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/features/app/presentation/splash_screen.dart';
import 'package:payabo_mobile/features/auth/presentation/contact_details_screen.dart';
import 'package:payabo_mobile/features/dashboard/presentation/dashboard_screen.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('assets render for image and svg widgets',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SplashScreen()));
    await tester.pumpAndSettle();
    expect(find.byType(Image), findsWidgets);

    await tester.pumpWidget(buildTestApp(const ContactDetailsScreen()));
    await tester.pumpAndSettle();
    expect(find.byType(SvgPicture), findsWidgets);
  });

  testWidgets('dashboard renders on small and large mobile sizes',
      (WidgetTester tester) async {
    for (final Size size in <Size>[
      const Size(360, 740),
      const Size(430, 932)
    ]) {
      await tester.binding.setSurfaceSize(size);
      await tester.pumpWidget(buildTestApp(const DashboardScreen()));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
    }
  });
}
