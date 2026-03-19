import 'package:flutter_test/flutter_test.dart';

import 'package:payabo_mobile/app/demo/demo_data_mode.dart';
import 'package:payabo_mobile/mock/repositories/mock_budget_repository.dart';

void main() {
  test('mock budget repository creates a starter budget', () async {
    final MockBudgetRepository repository = MockBudgetRepository(
      demoDataMode: DemoDataMode.fresh,
    );

    final createdBudget = await repository.createBudget();
    final budgets = await repository.getBudgets();

    expect(createdBudget.id, 'starter-budget-1');
    expect(createdBudget.name, 'My budget');
    expect(budgets, hasLength(1));
  });

  test('mock budget repository saves an updated budget amount', () async {
    final MockBudgetRepository repository = MockBudgetRepository();

    await repository.saveBudgetAmount(
      budgetId: 'transport',
      totalAllocated: 445,
    );

    final budgets = await repository.getBudgets();
    final transport = budgets.firstWhere((budget) => budget.id == 'transport');

    expect(transport.allocated, 445);
  });

  test('mock budget repository deletes a budget', () async {
    final MockBudgetRepository repository = MockBudgetRepository();

    await repository.deleteBudget('housing');

    final budgets = await repository.getBudgets();

    expect(budgets.any((budget) => budget.id == 'housing'), isFalse);
  });
}
