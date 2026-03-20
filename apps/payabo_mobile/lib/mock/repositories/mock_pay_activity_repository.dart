import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/pay_activity_repository.dart';
import '../mock_behavior.dart';

class MockPayActivityRepository implements PayActivityRepository {
  MockPayActivityRepository({
    this.demoDataMode = DemoDataMode.populated,
  });

  final DemoDataMode demoDataMode;

  // ─────────────────────────────────────────────────────────
  //  Populated demo data — transactions
  // ─────────────────────────────────────────────────────────

  static const List<PayActivityTransaction> _populatedTransactions =
      <PayActivityTransaction>[
    PayActivityTransaction(
      id: 'ptx_001',
      title: 'Transfer to Ama Serwaa',
      subtitle: 'Today, 09:42 AM',
      amountLabel: 'GHS 500.00',
      status: 'Completed',
      type: PayActivityTransactionType.transfer,
      dateGroupLabel: 'TODAY',
    ),
    PayActivityTransaction(
      id: 'ptx_002',
      title: 'DSTV subscription',
      subtitle: 'Today, 08:10 AM',
      amountLabel: 'GHS 240.00',
      status: 'Failed',
      type: PayActivityTransactionType.bill,
      dateGroupLabel: 'TODAY',
    ),
    PayActivityTransaction(
      id: 'ptx_003',
      title: 'Transfer to Mum',
      subtitle: 'Yesterday, 07:18 PM',
      amountLabel: 'GHS 200.00',
      status: 'Processing',
      type: PayActivityTransactionType.transfer,
      dateGroupLabel: 'YESTERDAY',
    ),
    PayActivityTransaction(
      id: 'ptx_004',
      title: 'ECG prepaid top-up',
      subtitle: 'Yesterday, 11:05 AM',
      amountLabel: 'GHS 120.00',
      status: 'Processing',
      type: PayActivityTransactionType.bill,
      dateGroupLabel: 'YESTERDAY',
    ),
    PayActivityTransaction(
      id: 'ptx_005',
      title: 'Water bill',
      subtitle: 'May 3, 2026, 07:18 PM',
      amountLabel: 'GHS 56.00',
      status: 'Completed',
      type: PayActivityTransactionType.bill,
      dateGroupLabel: 'MAY 3',
    ),
  ];

  // ─────────────────────────────────────────────────────────
  //  Populated demo data — transaction details
  // ─────────────────────────────────────────────────────────

  static const Map<String, PayTransactionDetail> _populatedDetails =
      <String, PayTransactionDetail>{
    'ptx_001': PayTransactionDetail(
      id: 'ptx_001',
      status: 'Completed',
      statusDescription: 'This transfer was successful.',
      amountLabel: 'GHS 500.00',
      feeLabel: 'GHS 2.50',
      totalLabel: 'GHS 502.50',
      recipient: PayTransactionRecipient(
        name: 'Ama Serwaa',
        initials: 'AS',
        bankName: 'GCB Bank',
        maskedAccountNumber: '**** **** 4521',
        country: 'Ghana',
      ),
      orderId: 'ORD-2026-0519-A7C3',
      paymentIntentId: 'PI-8F42-D1E9-B6A0',
      providerReference: 'GCB-TXN-993812',
      reference: 'Family support - May',
    ),
    'ptx_002': PayTransactionDetail(
      id: 'ptx_002',
      status: 'Failed',
      statusDescription: 'This payment could not be completed.',
      amountLabel: 'GHS 240.00',
      feeLabel: 'GHS 0.00',
      totalLabel: 'GHS 240.00',
      recipient: PayTransactionRecipient(
        name: 'DSTV',
        initials: 'DS',
        bankName: 'Multichoice Ghana',
        maskedAccountNumber: '**** **** 7890',
        country: 'Ghana',
      ),
      orderId: 'ORD-2026-0519-B2D4',
      paymentIntentId: 'PI-3A21-E5F8-C7B2',
      providerReference: 'MCG-TXN-448821',
      reference: 'DSTV subscription - May',
    ),
    'ptx_003': PayTransactionDetail(
      id: 'ptx_003',
      status: 'Processing',
      statusDescription: 'This transfer is being processed.',
      amountLabel: 'GHS 200.00',
      feeLabel: 'GHS 1.50',
      totalLabel: 'GHS 201.50',
      recipient: PayTransactionRecipient(
        name: 'Mum',
        initials: 'MU',
        bankName: 'Ecobank Ghana',
        maskedAccountNumber: '**** **** 3344',
        country: 'Ghana',
      ),
      orderId: 'ORD-2026-0518-C9E1',
      paymentIntentId: 'PI-7D65-A3B2-F1C8',
      providerReference: 'ECO-TXN-774523',
      reference: 'Monthly upkeep',
    ),
    'ptx_004': PayTransactionDetail(
      id: 'ptx_004',
      status: 'Processing',
      statusDescription: 'This payment is being processed.',
      amountLabel: 'GHS 120.00',
      feeLabel: 'GHS 1.00',
      totalLabel: 'GHS 121.00',
      recipient: PayTransactionRecipient(
        name: 'ECG Power',
        initials: 'EC',
        bankName: 'Electricity Company of Ghana',
        maskedAccountNumber: '**** **** 5567',
        country: 'Ghana',
      ),
      orderId: 'ORD-2026-0518-D4F2',
      paymentIntentId: 'PI-2C89-B4A7-E3D6',
      providerReference: 'ECG-TXN-112233',
      reference: 'Prepaid electricity top-up',
    ),
    'ptx_005': PayTransactionDetail(
      id: 'ptx_005',
      status: 'Completed',
      statusDescription: 'This payment was successful.',
      amountLabel: 'GHS 56.00',
      feeLabel: 'GHS 0.50',
      totalLabel: 'GHS 56.50',
      recipient: PayTransactionRecipient(
        name: 'Ghana Water',
        initials: 'GW',
        bankName: 'Ghana Water Company',
        maskedAccountNumber: '**** **** 8812',
        country: 'Ghana',
      ),
      orderId: 'ORD-2026-0503-E7A3',
      paymentIntentId: 'PI-9F12-C6D8-A4E5',
      providerReference: 'GWC-TXN-556677',
      reference: 'Water bill - May',
    ),
  };

  // ─────────────────────────────────────────────────────────
  //  Repository implementation
  // ─────────────────────────────────────────────────────────

  @override
  Future<PayActivitySummary> getRecentActivity() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('payActivity.getRecentActivity');

    if (demoDataMode == DemoDataMode.fresh) {
      return const PayActivitySummary();
    }

    return const PayActivitySummary(
      transactions: _populatedTransactions,
    );
  }

  @override
  Future<PayTransactionDetail?> getTransactionDetail(
    String transactionId,
  ) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('payActivity.getTransactionDetail');

    if (demoDataMode == DemoDataMode.fresh) {
      return null;
    }

    return _populatedDetails[transactionId];
  }
}
