import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/budget_repository.dart';
import '../../features/spending/presentation/spending_budget_data.dart';
import '../mock_behavior.dart';

class MockBudgetRepository implements BudgetRepository {
  MockBudgetRepository({
    this.demoDataMode = DemoDataMode.populated,
  }) : _budgets = demoDataMode == DemoDataMode.fresh
            ? <SpendingBudgetCategory>[]
            : cloneSpendingBudgetCategories(spendingBudgetCategories).toList();

  final DemoDataMode demoDataMode;

  List<SpendingBudgetCategory> _budgets;

  @override
  Future<List<SpendingBudgetCategory>> getBudgets() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('budget.getBudgets');
    return cloneSpendingBudgetCategories(_budgets);
  }

  @override
  Future<List<SpendingBudgetCategory>> saveBudgetAmount({
    required String budgetId,
    required double totalAllocated,
  }) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('budget.saveBudgetAmount');

    _budgets = _budgets.map((SpendingBudgetCategory category) {
      if (category.id != budgetId) {
        return category;
      }

      return _scaleCategoryBudget(category, totalAllocated);
    }).toList(growable: false);

    return cloneSpendingBudgetCategories(_budgets);
  }

  @override
  Future<List<SpendingBudgetCategory>> deleteBudget(String budgetId) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('budget.deleteBudget');

    _budgets = _budgets
        .where((SpendingBudgetCategory category) => category.id != budgetId)
        .toList(growable: false);

    return cloneSpendingBudgetCategories(_budgets);
  }

  SpendingBudgetCategory _scaleCategoryBudget(
    SpendingBudgetCategory category,
    double totalAllocated,
  ) {
    if (category.lineItems.isEmpty) {
      return category;
    }

    final double safeTotalAllocated = totalAllocated < 0 ? 0 : totalAllocated;
    final double currentAllocated = category.allocated;

    if (currentAllocated <= 0) {
      final List<SpendingBudgetLineItem> items = <SpendingBudgetLineItem>[];
      double remaining = safeTotalAllocated;

      for (int index = 0; index < category.lineItems.length; index++) {
        final SpendingBudgetLineItem item = category.lineItems[index];

        if (index == category.lineItems.length - 1) {
          items.add(item.copyWith(allocated: _roundCurrency(remaining)));
          continue;
        }

        final double splitAmount =
            _roundCurrency(safeTotalAllocated / category.lineItems.length);
        items.add(item.copyWith(allocated: splitAmount));
        remaining -= splitAmount;
      }

      return category.copyWith(lineItems: items);
    }

    final double ratio = safeTotalAllocated / currentAllocated;
    final List<SpendingBudgetLineItem> scaledItems = <SpendingBudgetLineItem>[];
    double allocatedSoFar = 0;

    for (int index = 0; index < category.lineItems.length; index++) {
      final SpendingBudgetLineItem item = category.lineItems[index];

      if (index == category.lineItems.length - 1) {
        scaledItems.add(
          item.copyWith(
            allocated: _roundCurrency(safeTotalAllocated - allocatedSoFar),
          ),
        );
        continue;
      }

      final double scaledAmount = _roundCurrency(item.allocated * ratio);
      allocatedSoFar += scaledAmount;
      scaledItems.add(item.copyWith(allocated: scaledAmount));
    }

    return category.copyWith(lineItems: scaledItems);
  }

  double _roundCurrency(double amount) {
    return double.parse(amount.toStringAsFixed(2));
  }
}
