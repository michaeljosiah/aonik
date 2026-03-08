import '../../data/repositories/dashboard_repository.dart';
import '../mock_behavior.dart';

class MockDashboardRepository implements DashboardRepository {
  @override
  Future<DashboardSummary> getSummary() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('dashboard.getSummary');

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
        DashboardUpcomingBill(
          id: 'bill_003',
          biller: 'DSTV',
          amountLabel: 'GHS 220.00',
          dueDateLabel: '2026-03-16',
        ),
        DashboardUpcomingBill(
          id: 'bill_004',
          biller: 'MTN Fibre',
          amountLabel: 'GHS 180.00',
          dueDateLabel: '2026-03-17',
        ),
        DashboardUpcomingBill(
          id: 'bill_005',
          biller: 'GOtv',
          amountLabel: 'GHS 75.00',
          dueDateLabel: '2026-03-18',
        ),
        DashboardUpcomingBill(
          id: 'bill_006',
          biller: 'AirtelTigo',
          amountLabel: 'GHS 55.00',
          dueDateLabel: '2026-03-18',
        ),
        DashboardUpcomingBill(
          id: 'bill_007',
          biller: 'Vodafone Cash',
          amountLabel: 'GHS 125.00',
          dueDateLabel: '2026-03-19',
        ),
        DashboardUpcomingBill(
          id: 'bill_008',
          biller: 'School Fees',
          amountLabel: 'GHS 450.00',
          dueDateLabel: '2026-03-20',
        ),
        DashboardUpcomingBill(
          id: 'bill_009',
          biller: 'NHIS Renewal',
          amountLabel: 'GHS 65.00',
          dueDateLabel: '2026-03-21',
        ),
        DashboardUpcomingBill(
          id: 'bill_010',
          biller: 'Netflix',
          amountLabel: 'GHS 58.00',
          dueDateLabel: '2026-03-21',
        ),
        DashboardUpcomingBill(
          id: 'bill_011',
          biller: 'Internet Bundle',
          amountLabel: 'GHS 35.00',
          dueDateLabel: '2026-03-22',
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
