import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_accounts_screen.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_budget_screen.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_screen.dart';
import 'package:payabo_mobile/shared/theme/payabo_theme.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('spending screen renders mocked sections',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Spend'), findsOneWidget);
    expect(find.text('Transactions'), findsOneWidget);
    expect(find.text('Overview'), findsNothing);
    expect(find.text('Your spending'), findsNothing);
    expect(find.text('Your budget'), findsNothing);
    expect(find.text('February spend'), findsOneWidget);

    final Finder primaryList = find.byType(ListView).first;

    await tester.drag(primaryList, const Offset(0, -520));
    await tester.pumpAndSettle();
    expect(find.text('Categories'), findsOneWidget);

    await tester.drag(primaryList, const Offset(0, -240));
    await tester.pumpAndSettle();
    expect(find.text('Finances'), findsOneWidget);
  });

  testWidgets('spending screen shows empty state in fresh demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const SpendingScreen(),
        demoDataMode: DemoDataMode.fresh,
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Fresh spending state'), findsOneWidget);
    expect(find.text('No spending activity yet'), findsOneWidget);
    expect(find.text('Finances'), findsNothing);
  });

  testWidgets('budgets pill opens the budget screen',
      (WidgetTester tester) async {
    final GoRouter router = GoRouter(
      initialLocation: '/spending',
      routes: <GoRoute>[
        GoRoute(
          path: '/spending',
          builder: (BuildContext context, GoRouterState state) =>
              const SpendingScreen(),
        ),
        GoRoute(
          path: '/spending/budgets',
          builder: (BuildContext context, GoRouterState state) =>
              const SpendingBudgetScreen(),
        ),
      ],
    );

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
          initialDemoDataModeProvider.overrideWithValue(DemoDataMode.populated),
        ],
        child: MaterialApp.router(
          theme: buildPayaboTheme(),
          routerConfig: router,
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Budgets'));
    await tester.pumpAndSettle();

    expect(find.text('Monthly budget'), findsOneWidget);

    final Finder primaryList = find.byType(ListView).first;
    await tester.drag(primaryList, const Offset(0, -280));
    await tester.pumpAndSettle();

    expect(find.text('Category budgets'), findsOneWidget);
    expect(find.text('Housing'), findsOneWidget);
  });

  testWidgets('accounts pill opens the accounts screen',
      (WidgetTester tester) async {
    final GoRouter router = GoRouter(
      initialLocation: '/spending',
      routes: <GoRoute>[
        GoRoute(
          path: '/spending',
          builder: (BuildContext context, GoRouterState state) =>
              const SpendingScreen(),
        ),
        GoRoute(
          path: '/spending/accounts',
          builder: (BuildContext context, GoRouterState state) =>
              const SpendingAccountsScreen(),
        ),
      ],
    );

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
          initialDemoDataModeProvider.overrideWithValue(DemoDataMode.populated),
        ],
        child: MaterialApp.router(
          theme: buildPayaboTheme(),
          routerConfig: router,
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Accounts'));
    await tester.pumpAndSettle();

    expect(find.text('Connected accounts'), findsOneWidget);
    final Finder primaryScrollable = find.byType(Scrollable).last;

    await tester.scrollUntilVisible(
      find.text('Everyday current'),
      260,
      scrollable: primaryScrollable,
    );
    expect(find.text('Everyday current'), findsOneWidget);
  });
}
