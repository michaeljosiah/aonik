import '../../features/spending/presentation/spending_budget_data.dart';

abstract class BudgetRepository {
  Future<List<SpendingBudgetCategory>> getBudgets();

  Future<List<SpendingBudgetCategory>> saveBudgetAmount({
    required String budgetId,
    required double totalAllocated,
  });

  Future<List<SpendingBudgetCategory>> deleteBudget(String budgetId);
}
