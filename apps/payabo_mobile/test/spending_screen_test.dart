import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/app/demo/demo_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_accounts_screen.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_budget_screen.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_screen.dart';
import 'package:payabo_mobile/shared/theme/payabo_theme.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('spending screen shows account cards and transactions in demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingScreen()));
    await tester.pumpAndSettle();

    // Header + section pills
    expect(find.text('Your spending'), findsOneWidget);
    expect(find.text('Transactions'), findsOneWidget);

    // Account card content — both cards may be partially visible in PageView
    expect(find.text('UK Current'), findsAtLeast(1));
    expect(find.text('Balance'), findsAtLeast(1));

    // The "Recent transactions" heading and transactions are inside the
    // DraggableScrollableSheet's ListView and may be below the fold.
    // Drag the sheet up to reveal them.
    await tester.drag(find.text('Transactions'), const Offset(0, -300));
    await tester.pumpAndSettle();

    expect(find.text('Recent transactions'), findsOneWidget);

    // First batch of demo transactions (current account).
    // Category labels include subcategory: "Housing · Rent", "Shopping · Online Shopping".
    expect(find.text('Open Rent'), findsAtLeast(1));
    expect(find.textContaining('Housing'), findsAtLeast(1));

    // Drag further if needed to reveal Amazon row
    await tester.drag(find.text('Recent transactions'), const Offset(0, -200));
    await tester.pumpAndSettle();

    expect(find.text('Amazon'), findsOneWidget);
    expect(find.textContaining('Shopping'), findsOneWidget);
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

    // Simi top bar label present (empty state shows "Simi" instead of "Your spending").
    // Section tabs are hidden in the empty state.
    expect(find.text('Simi'), findsOneWidget);
    expect(find.text('Transactions'), findsNothing);
    expect(find.text('Budgets'), findsNothing);

    // Simi AI typewriter message should be fully revealed after pumpAndSettle
    // (the empty state always uses the live message about linking a bank account).
    expect(find.textContaining('link a bank account'), findsOneWidget);

    // Simi attribution helper text
    expect(find.text('Simi, your AI assistant'), findsOneWidget);

    // Fixed bottom panel — no scrolling needed, panel is static.
    expect(find.text('Get started'), findsOneWidget);
    expect(find.text('Link an account'), findsOneWidget);
    expect(find.text('Add account manually'), findsOneWidget);

    // No account cards or transactions should appear
    expect(find.text('Current'), findsNothing);
    expect(find.text('Open Rent'), findsNothing);
  });

  testWidgets('spending screen shows live empty state when not in demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const SpendingScreen(),
        isDemo: false,
        environment: const AppEnvironment(
          flavor: AppFlavor.dev,
          useMocks: false,
          apiBaseUrl: 'https://api.dev.payabo.local',
        ),
      ),
    );
    await tester.pumpAndSettle();

    // Simi top bar label present (empty state shows "Simi" instead of "Spend").
    // Section tabs are hidden in the empty state.
    expect(find.text('Simi'), findsOneWidget);
    expect(find.text('Transactions'), findsNothing);

    // Simi AI typewriter message should be fully revealed after pumpAndSettle
    // (the live-empty message mentions "link a bank account").
    expect(find.textContaining('link a bank account'), findsOneWidget);

    // Simi attribution helper text
    expect(find.text('Simi, your AI assistant'), findsOneWidget);

    // Fixed bottom panel — all actions visible without scrolling.
    expect(find.text('Get started'), findsOneWidget);
    expect(find.text('Link an account'), findsOneWidget);
    expect(find.text('Add account manually'), findsOneWidget);

    // No fresh-demo link in live mode
    expect(find.text('Open profile settings'), findsNothing);

    // No demo data should leak through
    expect(find.text('Open Rent'), findsNothing);
    expect(find.text('Amazon'), findsNothing);
    expect(find.text('Current'), findsNothing);
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
          isDemoProvider.overrideWith((_) => true),
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
          isDemoProvider.overrideWith((_) => true),
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
      find.text('UK Current'),
      260,
      scrollable: primaryScrollable,
    );
    expect(find.text('UK Current'), findsOneWidget);
  });
}
