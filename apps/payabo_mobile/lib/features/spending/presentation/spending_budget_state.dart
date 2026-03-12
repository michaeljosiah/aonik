import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../data/repositories/repository_providers.dart';
import 'spending_budget_data.dart';

final FutureProvider<List<SpendingBudgetCategory>> spendingBudgetsProvider =
    FutureProvider<List<SpendingBudgetCategory>>((Ref ref) async {
  ref.watch(demoDataModeProvider);
  final repository = ref.watch(budgetRepositoryProvider);
  return repository.getBudgets();
});
