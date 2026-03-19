import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/dashboard_repository.dart';
import '../mock_behavior.dart';

class MockDashboardRepository implements DashboardRepository {
  MockDashboardRepository({
    this.demoDataMode = DemoDataMode.populated,
  });

  final DemoDataMode demoDataMode;

  static const DashboardTodayInsight _freshInsight = DashboardTodayInsight(
    message: 'Add a bill to unlock daily insights and spending guidance.',
    actionLabel: 'Add first bill',
    timestampLabel: 'Set up now',
  );

  static const DashboardTodayInsight _populatedInsight = DashboardTodayInsight(
    message: 'Dining spend is running 18% above your usual daily pace.',
    actionLabel: 'Review dining',
    timestampLabel: 'Today',
  );

  static const DashboardMetrics _freshMetrics = DashboardMetrics(
    spendableLabel: '£0.00',
    spendableSubtitle:
        'Add bills and budgets to unlock your spendable balance.',
    spendableProgress: 0,
    spendableProgressLabel: '0% free',
    netWorthLabel: '£0.00',
    netWorthChangeLabel: '£0',
    netWorthTrendLabel: 'add data',
    assetsLabel: '£0.00',
    billsLabel: '£0.00',
  );

  static const DashboardMetrics _populatedMetrics = DashboardMetrics(
    spendableLabel: '£1,285.00',
    spendableSubtitle:
        'After bills, savings, and your weekly safety buffer.',
    spendableProgress: 0.78,
    spendableProgressLabel: '78% free',
    netWorthLabel: '£22,180.64',
    netWorthChangeLabel: '+£920',
    netWorthTrendLabel: 'up 4.2%',
    assetsLabel: '£24.6k',
    billsLabel: '£1.9k',
  );

  static const List<DashboardRecentOrder> _populatedRecentOrders =
      <DashboardRecentOrder>[
    DashboardRecentOrder(
      id: 'ord_001',
      beneficiaryName: 'ECG Power',
      amountLabel: 'GHS 150.00',
      orderType: 'Bill Payment',
      dateLabel: '15 Mar',
      status: 'Completed',
    ),
    DashboardRecentOrder(
      id: 'ord_002',
      beneficiaryName: 'Ama Boafo',
      amountLabel: 'GHS 300.00',
      orderType: 'Transfer',
      dateLabel: '14 Mar',
      status: 'Completed',
    ),
    DashboardRecentOrder(
      id: 'ord_003',
      beneficiaryName: 'DSTV',
      amountLabel: 'GHS 220.00',
      orderType: 'Bill Payment',
      dateLabel: '13 Mar',
      status: 'Completed',
    ),
    DashboardRecentOrder(
      id: 'ord_004',
      beneficiaryName: 'Mama Grace',
      amountLabel: 'GHS 500.00',
      orderType: 'Transfer',
      dateLabel: '12 Mar',
      status: 'Pending',
    ),
    DashboardRecentOrder(
      id: 'ord_005',
      beneficiaryName: 'Eko Electricity',
      amountLabel: 'NGN 12,000',
      orderType: 'Bill Payment',
      dateLabel: '10 Mar',
      status: 'Failed',
    ),
  ];

  static const List<DashboardOverviewSlice> _populatedOverviewSlices =
      <DashboardOverviewSlice>[
    DashboardOverviewSlice(
      label: 'Income',
      amountLabel: '£4,232.24',
      value: 4232.24,
      colorKey: 'success',
    ),
    DashboardOverviewSlice(
      label: 'Expenses',
      amountLabel: '£2,660.12',
      value: 2660.12,
      colorKey: 'primary',
    ),
    DashboardOverviewSlice(
      label: 'Investments',
      amountLabel: '£1,754.64',
      value: 1754.64,
      colorKey: 'info',
    ),
  ];

  @override
  Future<DashboardSummary> getSummary() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('dashboard.getSummary');

    if (demoDataMode == DemoDataMode.fresh) {
      return const DashboardSummary(
        upcomingBills: <DashboardUpcomingBill>[],
        recentTransactions: <DashboardTransaction>[],
        supportObligations: <DashboardSupportObligation>[],
        recentOrders: <DashboardRecentOrder>[],
        overviewSlices: <DashboardOverviewSlice>[],
        overviewMonthLabel: 'March',
        overviewMonthShortLabel: 'Mar',
        overviewYearLabel: '2026',
        todayInsight: _freshInsight,
        metrics: _freshMetrics,
      );
    }

    return const DashboardSummary(
      upcomingBills: <DashboardUpcomingBill>[
        DashboardUpcomingBill(
          id: 'bill_001',
          biller: 'ECG Power',
          amountLabel: 'GHS 150.00',
          dueDateLabel: '2026-03-18',
        ),
        DashboardUpcomingBill(
          id: 'bill_002',
          biller: 'Ghana Water',
          amountLabel: 'GHS 90.00',
          dueDateLabel: '2026-03-19',
        ),
        DashboardUpcomingBill(
          id: 'bill_003',
          biller: 'DSTV',
          amountLabel: 'GHS 220.00',
          dueDateLabel: '2026-03-20',
        ),
        DashboardUpcomingBill(
          id: 'bill_004',
          biller: 'MTN Fibre',
          amountLabel: 'GHS 180.00',
          dueDateLabel: '2026-03-21',
        ),
        DashboardUpcomingBill(
          id: 'bill_005',
          biller: 'GOtv',
          amountLabel: 'GHS 75.00',
          dueDateLabel: '2026-03-22',
        ),
        DashboardUpcomingBill(
          id: 'bill_006',
          biller: 'AirtelTigo',
          amountLabel: 'GHS 55.00',
          dueDateLabel: '2026-03-23',
        ),
        DashboardUpcomingBill(
          id: 'bill_007',
          biller: 'Vodafone Cash',
          amountLabel: 'GHS 125.00',
          dueDateLabel: '2026-03-24',
        ),
        DashboardUpcomingBill(
          id: 'bill_008',
          biller: 'School Fees',
          amountLabel: 'GHS 450.00',
          dueDateLabel: '2026-03-25',
        ),
        DashboardUpcomingBill(
          id: 'bill_009',
          biller: 'NHIS Renewal',
          amountLabel: 'GHS 65.00',
          dueDateLabel: '2026-03-27',
        ),
        DashboardUpcomingBill(
          id: 'bill_010',
          biller: 'Netflix',
          amountLabel: 'GHS 58.00',
          dueDateLabel: '2026-03-28',
        ),
        DashboardUpcomingBill(
          id: 'bill_011',
          biller: 'Internet Bundle',
          amountLabel: 'GHS 35.00',
          dueDateLabel: '2026-03-30',
        ),
        DashboardUpcomingBill(
          id: 'bill_012',
          biller: 'Eko Electricity (NG)',
          amountLabel: 'NGN 12,000',
          dueDateLabel: '2026-03-20',
        ),
        DashboardUpcomingBill(
          id: 'bill_013',
          biller: 'British Gas (UK)',
          amountLabel: 'GBP 86.00',
          dueDateLabel: '2026-03-25',
        ),
      ],
      recentTransactions: <DashboardTransaction>[
        DashboardTransaction(
          id: 'txn_001',
          title: 'ECG Prepaid Electricity',
          amountLabel: 'GHS 150.00',
          status: 'Completed',
        ),
        DashboardTransaction(
          id: 'txn_002',
          title: 'Ghana Water Postpaid',
          amountLabel: 'GHS 90.00',
          status: 'Completed',
        ),
        DashboardTransaction(
          id: 'txn_003',
          title: 'MTN Mobile Money to Ama Boafo',
          amountLabel: 'GHS 300.00',
          status: 'Completed',
        ),
        DashboardTransaction(
          id: 'txn_004',
          title: 'Shoprite Lekki (NG)',
          amountLabel: 'NGN 18,500',
          status: 'Completed',
        ),
        DashboardTransaction(
          id: 'txn_005',
          title: 'Eko Electricity (NG)',
          amountLabel: 'NGN 12,000',
          status: 'Pending',
        ),
        DashboardTransaction(
          id: 'txn_006',
          title: 'Tesco (UK)',
          amountLabel: 'GBP 54.12',
          status: 'Completed',
        ),
        DashboardTransaction(
          id: 'txn_007',
          title: 'Netflix Subscription',
          amountLabel: 'GHS 58.00',
          status: 'Completed',
        ),
        DashboardTransaction(
          id: 'txn_008',
          title: 'Support: Mama Grace',
          amountLabel: 'GHS 500.00',
          status: 'Scheduled',
        ),
      ],
      supportObligations: <DashboardSupportObligation>[
        DashboardSupportObligation(
          id: 'sup_001',
          beneficiaryName: 'Mama Grace',
          category: 'Living Expenses',
          amountLabel: 'GHS 500.00',
          dueDateLabel: '2026-03-20',
          frequencyLabel: 'Monthly',
        ),
        DashboardSupportObligation(
          id: 'sup_002',
          beneficiaryName: 'Uncle Kofi',
          category: 'Medical',
          amountLabel: 'GHS 350.00',
          dueDateLabel: '2026-03-28',
          frequencyLabel: 'Quarterly',
        ),
        DashboardSupportObligation(
          id: 'sup_003',
          beneficiaryName: 'Auntie Esi',
          category: 'Education',
          amountLabel: 'GHS 200.00',
          dueDateLabel: '2026-04-01',
          frequencyLabel: 'Monthly',
        ),
      ],
      recentOrders: _populatedRecentOrders,
      overviewSlices: _populatedOverviewSlices,
      overviewMonthLabel: 'March',
      overviewMonthShortLabel: 'Mar',
      overviewYearLabel: '2026',
      todayInsight: _populatedInsight,
      metrics: _populatedMetrics,
    );
  }
}
