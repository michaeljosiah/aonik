import '../../features/spending/presentation/spending_budget_data.dart';

abstract class BudgetRepository {
  Future<List<SpendingBudgetCategory>> getBudgets();

  /// Creates a new budget.
  ///
  /// When [categoryId] matches a predefined budget-category template the new
  /// budget is seeded from that template (name, icon, line-items, etc.).
  /// When [categoryId] is `null` a generic starter budget is created instead.
  Future<SpendingBudgetCategory> createBudget({String? categoryId});

  Future<List<SpendingBudgetCategory>> saveBudgetAmount({
    required String budgetId,
    required double totalAllocated,
  });

  Future<List<SpendingBudgetCategory>> deleteBudget(String budgetId);
}
