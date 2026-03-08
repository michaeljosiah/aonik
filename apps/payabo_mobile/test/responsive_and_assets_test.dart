import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/data/repositories/profile_repository.dart';
import 'package:payabo_mobile/data/repositories/repository_providers.dart';
import 'package:payabo_mobile/features/app/presentation/splash_screen.dart';
import 'package:payabo_mobile/features/auth/presentation/contact_details_screen.dart';
import 'package:payabo_mobile/features/chat/presentation/chat_screen.dart';
import 'package:payabo_mobile/features/dashboard/presentation/dashboard_screen.dart';
import 'package:payabo_mobile/features/payments/presentation/payment_country_screen.dart';
import 'package:payabo_mobile/features/profile/presentation/personal_details_screen.dart';
import 'package:payabo_mobile/features/profile/presentation/profile_screen.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_category_detail_screen.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_overview_screen.dart';
import 'package:payabo_mobile/mock/repositories/mock_profile_repository.dart';
import 'package:payabo_mobile/shared/widgets/payabo_app_header.dart';
import 'package:payabo_mobile/shared/widgets/payabo_bottom_nav.dart';
import 'package:payabo_mobile/shared/widgets/payabo_profile_avatar.dart';

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

  testWidgets('dashboard uses the logged in user profile image',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const DashboardScreen(),
        overrides: [
          profileRepositoryProvider.overrideWithValue(
            _ProfileRepositoryWithPhoto(),
          ),
        ],
      ),
    );

    await tester.pumpAndSettle();

    expect(find.byType(PayaboProfileAvatar), findsOneWidget);
    expect(
      find.descendant(
        of: find.byType(PayaboProfileAvatar),
        matching: find.byType(Image),
      ),
      findsOneWidget,
    );
  });

  testWidgets('profile screens keep the bottom menu visible',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const ProfileScreen()));
    await tester.pumpAndSettle();

    expect(find.byType(PayaboBottomNav), findsOneWidget);

    await tester.pumpWidget(buildTestApp(const ProfilePersonalDetailsScreen()));
    await tester.pumpAndSettle();

    expect(find.byType(PayaboBottomNav), findsOneWidget);
  });

  testWidgets('shared profile and bell header appears on app pages',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingOverviewScreen()));
    await tester.pumpAndSettle();
    expect(find.byType(PayaboAppHeader), findsOneWidget);

    await tester.pumpWidget(
      buildTestApp(const SpendingCategoryDetailScreen(categoryId: 'finances')),
    );
    await tester.pumpAndSettle();
    expect(find.byType(PayaboAppHeader), findsOneWidget);

    await tester.pumpWidget(buildTestApp(const ChatScreen()));
    await tester.pumpAndSettle();
    expect(find.byType(PayaboAppHeader), findsOneWidget);

    await tester.pumpWidget(buildTestApp(const PaymentCountryScreen()));
    await tester.pumpAndSettle();
    expect(find.byType(PayaboAppHeader), findsOneWidget);

    await tester.pumpWidget(buildTestApp(const ProfilePersonalDetailsScreen()));
    await tester.pumpAndSettle();
    expect(find.byType(PayaboAppHeader), findsOneWidget);
  });
}

class _ProfileRepositoryWithPhoto extends MockProfileRepository {
  @override
  Future<UserProfile> getProfile() async {
    return const UserProfile(
      firstName: 'Kwame',
      lastName: 'Mensah',
      email: 'kwame.mensah@payabo.app',
      phone: '+233241000000',
      countryCode: 'GH',
      photoUrl: 'https://mock.payabo.app/photos/profile-kwame.jpg',
    );
  }
}
