import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/repositories/pay_activity_repository.dart';
import '../../../data/repositories/repository_providers.dart';

/// Fetches the recent pay activity summary from the repository.
///
/// Automatically rebuilds when the underlying repository swaps between
/// mock and live implementations (e.g. when [isDemoProvider] changes).
final FutureProvider<PayActivitySummary> payActivitySummaryProvider =
    FutureProvider<PayActivitySummary>(
  (Ref ref) {
    final repo = ref.watch(payActivityRepositoryProvider);
    return repo.getRecentActivity();
  },
);

/// A family provider that fetches the detail for a single transaction by ID.
///
/// Returns `null` if the transaction is not found (e.g. fresh demo mode).
final payTransactionDetailProvider =
    FutureProvider.family<PayTransactionDetail?, String>(
  (Ref ref, String transactionId) {
    final repo = ref.watch(payActivityRepositoryProvider);
    return repo.getTransactionDetail(transactionId);
  },
);
