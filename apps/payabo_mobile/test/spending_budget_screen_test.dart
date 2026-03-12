import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_budget_detail_screen.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_budget_screen.dart';
import 'package:payabo_mobile/shared/theme/payabo_theme.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('budget screen renders summary and expandable categories',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingBudgetScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Spend'), findsOneWidget);
    expect(find.text('Budgets'), findsOneWidget);
    expect(find.text('Monthly budget'), findsOneWidget);

    final Finder primaryList = find.byType(ListView).first;
    await tester.drag(primaryList, const Offset(0, -280));
    await tester.pumpAndSettle();

    expect(find.text('Category budgets'), findsOneWidget);
    expect(find.text('Housing'), findsOneWidget);
    expect(find.text('Rent'), findsOneWidget);

    await tester.tap(find.byKey(const Key('budget-expand-housing')));
    await tester.pumpAndSettle();

    expect(find.text('Rent'), findsNothing);

    await tester.tap(find.byKey(const Key('budget-expand-housing')));
    await tester.pumpAndSettle();

    expect(find.text('Rent'), findsOneWidget);
  });

  testWidgets('budget card opens the budget detail screen',
      (WidgetTester tester) async {
    final GoRouter router = GoRouter(
      initialLocation: '/spending/budgets',
      routes: <GoRoute>[
        GoRoute(
          path: '/spending/budgets',
          builder: (BuildContext context, GoRouterState state) =>
              const SpendingBudgetScreen(),
        ),
        GoRoute(
          path: '/spending/budgets/:budgetId',
          builder: (BuildContext context, GoRouterState state) =>
              SpendingBudgetDetailScreen(
            budgetId: state.pathParameters['budgetId'] ?? 'housing',
          ),
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

    final Finder primaryList = find.byType(ListView).first;
    await tester.drag(primaryList, const Offset(0, -280));
    await tester.pumpAndSettle();

    await tester.scrollUntilVisible(
      find.text('Housing'),
      120,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.tap(find.text('Housing'));
    await tester.pumpAndSettle();

    expect(find.text('Monthly budget'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('View transactions'),
      220,
      scrollable: find.byType(Scrollable).first,
    );
    expect(find.text('View transactions'), findsOneWidget);
  });

  testWidgets('budget screen shows empty state in fresh demo mode',
      (WidgetTester tester) async {
    await tester.pumpWidget(
      buildTestApp(
        const SpendingBudgetScreen(),
        demoDataMode: DemoDataMode.fresh,
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Start planning'), findsOneWidget);

    final Finder primaryList = find.byType(ListView).first;
    await tester.drag(primaryList, const Offset(0, -260));
    await tester.pumpAndSettle();

    expect(find.text('No budgets set yet'), findsOneWidget);
    expect(find.text('Housing'), findsNothing);
  });
}
