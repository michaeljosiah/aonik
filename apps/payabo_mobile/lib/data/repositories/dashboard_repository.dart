class DashboardUpcomingBill {
  const DashboardUpcomingBill({
    required this.id,
    required this.biller,
    required this.amountLabel,
    required this.dueDateLabel,
  });

  final String id;
  final String biller;
  final String amountLabel;
  final String dueDateLabel;
}

class DashboardTransaction {
  const DashboardTransaction({
    required this.id,
    required this.title,
    required this.amountLabel,
    required this.status,
  });

  final String id;
  final String title;
  final String amountLabel;
  final String status;
}

/// An upcoming family/community support obligation shown on the dashboard.
///
/// Kept intentionally lightweight — mirrors [DashboardUpcomingBill] so the
/// timeline can interleave both types sorted by due date.
class DashboardSupportObligation {
  const DashboardSupportObligation({
    required this.id,
    required this.beneficiaryName,
    required this.category,
    required this.amountLabel,
    required this.dueDateLabel,
    required this.frequencyLabel,
  });

  final String id;
  final String beneficiaryName;

  /// Human-readable category (e.g. "Living Expenses", "Education").
  final String category;
  final String amountLabel;
  final String dueDateLabel;
  final String frequencyLabel;
}

class DashboardSummary {
  const DashboardSummary({
    required this.upcomingBills,
    required this.recentTransactions,
    this.supportObligations = const <DashboardSupportObligation>[],
  });

  final List<DashboardUpcomingBill> upcomingBills;
  final List<DashboardTransaction> recentTransactions;
  final List<DashboardSupportObligation> supportObligations;
}

abstract class DashboardRepository {
  Future<DashboardSummary> getSummary();
}
