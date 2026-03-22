import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/app/demo/demo_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/data/repositories/profile_repository.dart';
import 'package:payabo_mobile/data/repositories/repository_providers.dart';
import 'package:payabo_mobile/features/app/presentation/splash_screen.dart';
import 'package:payabo_mobile/features/auth/presentation/contact_details_screen.dart';
import 'package:payabo_mobile/features/chat/presentation/chat_screen.dart';
import 'package:payabo_mobile/features/dashboard/presentation/dashboard_screen.dart';
import 'package:payabo_mobile/features/notifications/presentation/notification_center_screen.dart';
import 'package:payabo_mobile/features/payments/presentation/pay_dashboard_screen.dart';
import 'package:payabo_mobile/features/payments/presentation/payment_country_screen.dart';
import 'package:payabo_mobile/features/profile/presentation/personal_details_screen.dart';
import 'package:payabo_mobile/features/profile/presentation/profile_screen.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_budget_screen.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_category_detail_screen.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_screen.dart';
import 'package:payabo_mobile/mock/repositories/mock_profile_repository.dart';
import 'package:payabo_mobile/shared/theme/payabo_theme.dart';
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
      ProviderScope(
        overrides: [
          appEnvironmentProvider.overrideWithValue(
            const AppEnvironment(
              flavor: AppFlavor.dev,
              useMocks: true,
              apiBaseUrl: 'https://api.dev.payabo.local',
            ),
          ),
          profileRepositoryProvider.overrideWithValue(
            _ProfileRepositoryWithPhoto(),
          ),
        ],
        child: MaterialApp(
          theme: buildPayaboTheme(),
          home: const DashboardScreen(),
        ),
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

    expect(find.text('Welcome back'), findsOneWidget);
    expect(find.text('Kwame Mensah'), findsOneWidget);
  });

  testWidgets('dashboard uses the empty experience for fresh demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          appEnvironmentProvider.overrideWithValue(
            const AppEnvironment(
              flavor: AppFlavor.dev,
              useMocks: true,
              apiBaseUrl: 'https://api.dev.payabo.local',
            ),
          ),
          isDemoProvider.overrideWith((Ref ref) => true),
          initialDemoDataModeProvider.overrideWithValue(DemoDataMode.fresh),
          profileRepositoryProvider.overrideWithValue(
            _ImmediateFreshProfileRepository(),
          ),
        ],
        child: MaterialApp(
          theme: buildPayaboTheme(),
          home: const DashboardScreen(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    final Finder primaryScroll = find.byType(ListView).first;

    while (find
        .text('No upcoming bills yet. Add a bill to start tracking due dates.')
        .evaluate()
        .isEmpty) {
      await tester.drag(primaryScroll, const Offset(0, -280));
      await tester.pumpAndSettle();
    }

    expect(
      find.text(
          'No upcoming bills yet. Add a bill to start tracking due dates.'),
      findsOneWidget,
    );
  });

  testWidgets('dashboard upcoming bills preview is limited to five items',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const DashboardScreen()));
    await tester.pumpAndSettle();

    final Finder primaryScroll = find.byType(ListView);

    while (find.text('GOtv').evaluate().isEmpty) {
      await tester.drag(primaryScroll, const Offset(0, -300));
      await tester.pumpAndSettle();
    }

    expect(find.text('GOtv'), findsOneWidget);
    expect(find.text('AirtelTigo'), findsNothing);
    expect(find.text('Netflix'), findsNothing);

    await tester.pumpAndSettle();
  });

  testWidgets('dashboard insight carousel supports swipe and auto-scroll',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const DashboardScreen()));
    await tester.pumpAndSettle();

    final Finder insightPager = find.byType(PageView);

    // First carousel page is the Simi CTA banner card.
    expect(find.text('Need help? Simi is here to guide you'), findsOneWidget);

    await tester.drag(insightPager, const Offset(-320, 0));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));

    expect(find.text('Available to spend'), findsOneWidget);

    await tester.drag(insightPager, const Offset(320, 0));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));

    expect(find.text('Need help? Simi is here to guide you'), findsOneWidget);

    // Auto-scroll fires every 5 seconds.
    await tester.pump(const Duration(seconds: 5));
    await tester.pump(const Duration(milliseconds: 500));

    expect(find.text('Available to spend'), findsOneWidget);

    await tester.pump(const Duration(seconds: 5));
    await tester.pump(const Duration(milliseconds: 500));

    expect(find.text('Net worth'), findsOneWidget);
  });

  testWidgets('profile screens keep the bottom menu visible',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const ProfileScreen(),
        isDemo: false,
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byType(PayaboBottomNav), findsOneWidget);

    await tester.pumpWidget(
      buildTestApp(
        const ProfilePersonalDetailsScreen(),
        isDemo: false,
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byType(PayaboBottomNav), findsOneWidget);
  });

  testWidgets('profile screens use the compact settings layout',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const ProfileScreen(),
        isDemo: false,
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byType(PayaboAppHeader), findsNothing);
    expect(find.text('Profile'), findsOneWidget);
    expect(find.text('Demo data preferences'), findsNothing);

    await tester.pumpWidget(
      buildTestApp(
        const ProfilePersonalDetailsScreen(),
        isDemo: false,
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byType(PayaboAppHeader), findsNothing);
  });

  testWidgets('main app pages use their expected top chrome',
      (WidgetTester tester) async {
    // Pay dashboard uses a custom pinned header (matching Home dashboard)
    // with a profile avatar and notification bell — not PayaboAppHeader.
    await tester.pumpWidget(buildTestApp(const PayDashboardScreen()));
    await tester.pumpAndSettle();
    expect(find.byType(PayaboProfileAvatar), findsOneWidget);
    expect(find.byType(PayaboBottomNav), findsOneWidget);

    await tester.pumpWidget(buildTestApp(const SpendingScreen()));
    await tester.pumpAndSettle();
    expect(find.byType(PayaboAppHeader), findsOneWidget);

    await tester.pumpWidget(buildTestApp(const SpendingBudgetScreen()));
    await tester.pumpAndSettle();
    expect(find.byType(PayaboAppHeader), findsOneWidget);

    await tester.pumpWidget(
      buildTestApp(const SpendingCategoryDetailScreen(categoryId: 'finances')),
    );
    await tester.pumpAndSettle();
    expect(find.byType(PayaboAppHeader), findsOneWidget);

    await tester.pumpWidget(buildTestApp(const ChatScreen()));
    await tester.pumpAndSettle();
    expect(find.byType(PayaboAppHeader), findsNothing);
    expect(find.byIcon(Icons.menu_rounded), findsOneWidget);

    await tester.pumpWidget(buildTestApp(const PaymentCountryScreen()));
    await tester.pumpAndSettle();
    expect(find.byType(PayaboAppHeader), findsOneWidget);
    expect(find.byType(PayaboBottomNav), findsOneWidget);
  });

  testWidgets('notification center screen renders grouped items',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const NotificationCenterScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Notifications'), findsOneWidget);
    expect(find.text('Electricity bill reminder'), findsOneWidget);
    expect(find.text('Spend alert'), findsOneWidget);
  });

  testWidgets('notification center starts empty in fresh demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const NotificationCenterScreen(),
        demoDataMode: DemoDataMode.fresh,
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('No notifications yet'), findsOneWidget);
    expect(find.text('Electricity bill reminder'), findsNothing);
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

class _ImmediateFreshProfileRepository extends MockProfileRepository {
  _ImmediateFreshProfileRepository() : super(demoDataMode: DemoDataMode.fresh);

  @override
  Future<UserProfile> getProfile() async {
    return const UserProfile(
      firstName: '',
      lastName: '',
      email: '',
      phone: '',
      countryCode: 'GH',
    );
  }

  @override
  Future<NotificationPreferences> getNotificationPreferences() async {
    return const NotificationPreferences(
      email: '',
      newBillsPush: false,
      billUpdatesPush: false,
      billAssistPush: false,
      mbaMessagesPush: false,
      orgMessagesPush: false,
      friendsMessagesPush: false,
      newBillsEmail: false,
      billUpdatesEmail: false,
      billAssistEmail: false,
      mbaMessagesEmail: false,
      orgMessagesEmail: false,
    );
  }

  @override
  Future<MarketingPreferences> getMarketingPreferences() async {
    return const MarketingPreferences(
      email: '',
      news: false,
      offers: false,
      surveys: false,
    );
  }
}
