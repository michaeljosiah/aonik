import '../../data/repositories/dashboard_repository.dart';

class MockDashboardRepository implements DashboardRepository {
  @override
  Future<DashboardSummary> getSummary() async {
    await Future<void>.delayed(const Duration(milliseconds: 350));

    return const DashboardSummary(
      upcomingBills: <DashboardUpcomingBill>[
        DashboardUpcomingBill(
          id: 'bill_001',
          biller: 'ECG Power',
          amountLabel: 'GHS 150.00',
          dueDateLabel: '2026-03-10',
        ),
        DashboardUpcomingBill(
          id: 'bill_002',
          biller: 'Ghana Water',
          amountLabel: 'GHS 90.00',
          dueDateLabel: '2026-03-15',
        ),
      ],
      recentTransactions: <DashboardTransaction>[
        DashboardTransaction(
          id: 'txn_001',
          title: 'ECG Prepaid Electricity',
          amountLabel: 'GHS 120.00',
          status: 'Completed',
        ),
        DashboardTransaction(
          id: 'txn_002',
          title: 'Ghana Water Postpaid',
          amountLabel: 'GHS 78.50',
          status: 'Pending',
        ),
      ],
    );
  }
}
