import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/app/demo/demo_mode.dart';
import 'package:payabo_mobile/app/environment/app_environment.dart';
import 'package:payabo_mobile/app/environment/environment_provider.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_budget_detail_screen.dart';
import 'package:payabo_mobile/features/spending/presentation/spending_budget_screen.dart';
import 'package:payabo_mobile/shared/theme/payabo_theme.dart';

import 'test_helpers.dart';

void main() {
  testWidgets('budget screen renders summary and category cards',
      (WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp(const SpendingBudgetScreen()));
    await tester.pumpAndSettle();

    // Hero banner surfaces the summary figures and the month label.
    expect(find.text('April 2026'), findsOneWidget);
    expect(find.text('On track'), findsOneWidget);

    // Section pill for the budgets tab is selected.
    expect(find.text('Budgets'), findsOneWidget);

    // The category list renders with a heading + per-category cards keyed by id.
    expect(find.text('Category budgets'), findsOneWidget);
    expect(find.text('Housing'), findsOneWidget);
    expect(find.byKey(const Key('budget-card-housing')), findsOneWidget);
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

    expect(find.text('Create your first budget'), findsOneWidget);
    expect(find.byKey(const Key('budget-empty-create')), findsOneWidget);
    expect(find.text('Housing'), findsNothing);
  });

  testWidgets('empty state create action shows picker then opens detail',
      (WidgetTester tester) async {
    // Use a taller surface to avoid overflow from illustration + text + button.
    await tester.binding.setSurfaceSize(const Size(800, 900));
    addTearDown(() => tester.binding.setSurfaceSize(const Size(800, 600)));
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
            budgetId: state.pathParameters['budgetId'] ?? 'starter-budget-1',
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
          isDemoProvider.overrideWith((Ref ref) => true),
          initialDemoDataModeProvider.overrideWithValue(DemoDataMode.fresh),
        ],
        child: MaterialApp.router(
          theme: buildPayaboTheme(),
          routerConfig: router,
        ),
      ),
    );
    await tester.pumpAndSettle();

    // Verify the empty state is shown.
    expect(find.text('Create your first budget'), findsOneWidget);

    // Scroll the create button into view (it may be below the fold).
    await tester.scrollUntilVisible(
      find.byKey(const Key('budget-empty-create')),
      120,
      scrollable: find.byType(Scrollable).first,
    );

    // Tap the create button — should open the category picker sheet.
    await tester.tap(find.byKey(const Key('budget-empty-create')));
    await tester.pumpAndSettle();

    // The picker sheet should be visible with the predefined templates.
    expect(find.text('Choose a budget'), findsOneWidget);
    expect(find.text('Housing'), findsOneWidget);
    expect(find.text('Custom budget'), findsOneWidget);

    // Select the Housing template.
    await tester.tap(find.text('Housing'));
    await tester.pumpAndSettle();

    // Should navigate to the budget detail screen with the template name.
    expect(find.text('Monthly budget'), findsOneWidget);
    expect(find.text('Housing'), findsOneWidget);
  });
}
