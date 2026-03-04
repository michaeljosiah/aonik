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

class DashboardSummary {
  const DashboardSummary({
    required this.upcomingBills,
    required this.recentTransactions,
  });

  final List<DashboardUpcomingBill> upcomingBills;
  final List<DashboardTransaction> recentTransactions;
}

abstract class DashboardRepository {
  Future<DashboardSummary> getSummary();
}
