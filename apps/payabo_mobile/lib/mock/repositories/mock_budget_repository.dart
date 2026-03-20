import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/account_links_repository.dart';
import '../../data/repositories/budget_repository.dart';
import '../../features/spending/presentation/spending_budget_data.dart';
import '../mock_behavior.dart';

class MockBudgetRepository implements BudgetRepository {
  MockBudgetRepository({
    this.demoDataMode = DemoDataMode.populated,
    Set<String> Function()? activeConnectionIdsGetter,
    List<AccountLinkItem> Function()? runtimeAccountsGetter,
  }) : _activeConnectionIdsGetter = activeConnectionIdsGetter,
       _runtimeAccountsGetter = runtimeAccountsGetter,
       _budgets = demoDataMode == DemoDataMode.fresh
            ? <SpendingBudgetCategory>[]
            : cloneSpendingBudgetCategories(spendingBudgetCategories).toList();

  final DemoDataMode demoDataMode;

  /// When non-null, called at query time to resolve the current set of active
  /// connection IDs. Budget line-item "spent" amounts are aggregate and not
  /// connection-specific, but this callback enables the repository to
  /// participate in cross-repository coordination (e.g. provider invalidation).
  // ignore: unused_field
  final Set<String> Function()? _activeConnectionIdsGetter;

  /// When non-null, called at query time to retrieve runtime-created accounts.
  /// Reserved for future use — enables the repository to participate in
  /// cross-repository coordination (e.g. provider invalidation).
  // ignore: unused_field
  final List<AccountLinkItem> Function()? _runtimeAccountsGetter;

  List<SpendingBudgetCategory> _budgets;

  @override
  Future<List<SpendingBudgetCategory>> getBudgets() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('budget.getBudgets');
    return cloneSpendingBudgetCategories(_budgets);
  }

  @override
  Future<SpendingBudgetCategory> createBudget({String? categoryId}) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('budget.createBudget');

    final SpendingBudgetCategory budget;

    if (categoryId != null) {
      // Create a budget from the chosen category (name, icon, color, etc.).
      // Line items start empty — the user adds their own.
      budget = cloneSpendingBudgetCategory(
        getSpendingBudgetCategoryById(categoryId),
      );
    } else {
      budget = createStarterSpendingBudgetCategory(
        index: _nextBudgetIndex(),
      );
    }

    _budgets = <SpendingBudgetCategory>[..._budgets, budget];
    return cloneSpendingBudgetCategory(budget);
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
    final double safeTotalAllocated = totalAllocated < 0 ? 0 : totalAllocated;

    // When the budget has no line items yet, create a single "Budget" line
    // to hold the total. This keeps the `allocated` getter (which sums line
    // items) consistent with what the user set.
    if (category.lineItems.isEmpty) {
      return category.copyWith(
        lineItems: <SpendingBudgetLineItem>[
          SpendingBudgetLineItem(
            id: 'budget',
            name: 'Budget',
            allocated: _roundCurrency(safeTotalAllocated),
            spent: 0,
          ),
        ],
      );
    }

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

  int _nextBudgetIndex() {
    int highestStarterIndex = 0;

    for (final SpendingBudgetCategory budget in _budgets) {
      final Match? match =
          RegExp(r'^starter-budget-(\d+)$').firstMatch(budget.id);
      if (match == null) {
        continue;
      }

      final int? index = int.tryParse(match.group(1) ?? '');
      if (index != null && index > highestStarterIndex) {
        highestStarterIndex = index;
      }
    }

    return highestStarterIndex + 1;
  }
}
