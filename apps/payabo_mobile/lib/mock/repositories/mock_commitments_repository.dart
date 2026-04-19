import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/commitments_repository.dart';
import '../mock_behavior.dart';

class MockCommitmentsRepository implements CommitmentsRepository {
  MockCommitmentsRepository({
    this.demoDataMode = DemoDataMode.populated,
  }) : _items = demoDataMode == DemoDataMode.fresh
            ? <CommitmentItem>[]
            : List<CommitmentItem>.from(_seed);

  final DemoDataMode demoDataMode;
  List<CommitmentItem> _items;

  @override
  Future<CommitmentListPage> listCommitments() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('commitments.listCommitments');
    final active = _items
        .where((i) =>
            i.verificationStatus != CommitmentVerificationStatus.rejected &&
            i.verificationStatus != CommitmentVerificationStatus.archived)
        .toList(growable: false);
    return CommitmentListPage(items: active, totals: _computeTotals(active));
  }

  @override
  Future<void> confirmCommitment(String id) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('commitments.confirmCommitment');
    _items = _items.map((i) {
      if (i.id != id) return i;
      return CommitmentItem(
        id: i.id,
        type: i.type,
        verificationStatus: CommitmentVerificationStatus.confirmed,
        displayName: i.displayName,
        amountLabel: i.amountLabel,
        amount: i.amount,
        currency: i.currency,
        dueDate: i.dueDate,
        dueDateLabel: i.dueDateLabel,
        frequency: i.frequency,
        autopay: i.autopay,
        category: i.category,
        confidenceScore: i.confidenceScore,
      );
    }).toList();
  }

  @override
  Future<void> rejectCommitment(String id) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('commitments.rejectCommitment');
    _items = _items.map((i) {
      if (i.id != id) return i;
      return CommitmentItem(
        id: i.id,
        type: i.type,
        verificationStatus: CommitmentVerificationStatus.rejected,
        displayName: i.displayName,
        amountLabel: i.amountLabel,
        amount: i.amount,
        currency: i.currency,
        dueDate: i.dueDate,
        dueDateLabel: i.dueDateLabel,
        frequency: i.frequency,
        autopay: i.autopay,
        category: i.category,
        confidenceScore: i.confidenceScore,
      );
    }).toList();
  }

  // ═══════════════════════════════════════════════════════
  // Totals
  // ═══════════════════════════════════════════════════════

  static CommitmentTotals _computeTotals(List<CommitmentItem> items) {
    final total = items.fold<double>(0, (s, i) => s + (i.amount ?? 0));
    final now = DateTime.now();
    return CommitmentTotals(
      totalUpcomingAmountLabel: '£${total.toStringAsFixed(2)}',
      dueSoonCount: items
          .where((i) =>
              i.dueDate != null &&
              i.dueDate!.isAfter(now) &&
              i.dueDate!.difference(now).inDays <= 7)
          .length,
      detectedCount: items
          .where((i) =>
              i.verificationStatus == CommitmentVerificationStatus.detected)
          .length,
      billsCount:
          items.where((i) => i.type == CommitmentType.bill).length,
      subscriptionsCount:
          items.where((i) => i.type == CommitmentType.subscription).length,
      debtRepaymentsCount:
          items.where((i) => i.type == CommitmentType.debtRepayment).length,
    );
  }

  // ═══════════════════════════════════════════════════════
  // Seed data
  // ═══════════════════════════════════════════════════════

  static final _now = DateTime.now();

  static final List<CommitmentItem> _seed = <CommitmentItem>[
    CommitmentItem(
      id: 'bill-electricity',
      type: CommitmentType.bill,
      verificationStatus: CommitmentVerificationStatus.confirmed,
      displayName: 'Electricity',
      amount: 87.50,
      amountLabel: '£87.50',
      currency: 'GBP',
      dueDate: _now.add(const Duration(days: 4)),
      dueDateLabel: 'in 4 days',
      frequency: 'Monthly',
      autopay: true,
      category: 'Utilities',
    ),
    CommitmentItem(
      id: 'bill-council-tax',
      type: CommitmentType.bill,
      verificationStatus: CommitmentVerificationStatus.confirmed,
      displayName: 'Council Tax',
      amount: 142.00,
      amountLabel: '£142.00',
      currency: 'GBP',
      dueDate: _now.add(const Duration(days: 12)),
      dueDateLabel: 'in 12 days',
      frequency: 'Monthly',
      autopay: false,
      category: 'Housing',
    ),
    CommitmentItem(
      id: 'bill-broadband',
      type: CommitmentType.bill,
      verificationStatus: CommitmentVerificationStatus.detected,
      displayName: 'Virgin Media Broadband',
      amount: 35.00,
      amountLabel: '£35.00',
      currency: 'GBP',
      dueDate: _now.add(const Duration(days: 20)),
      dueDateLabel: 'in 20 days',
      frequency: 'Monthly',
      autopay: false,
      category: 'Utilities',
      confidenceScore: 0.88,
    ),
    CommitmentItem(
      id: 'sub-netflix',
      type: CommitmentType.subscription,
      verificationStatus: CommitmentVerificationStatus.confirmed,
      displayName: 'Netflix',
      amount: 17.99,
      amountLabel: '£17.99',
      currency: 'GBP',
      dueDate: _now.add(const Duration(days: 9)),
      dueDateLabel: 'in 9 days',
      frequency: 'Monthly',
      autopay: true,
      category: 'Entertainment',
    ),
    CommitmentItem(
      id: 'sub-spotify',
      type: CommitmentType.subscription,
      verificationStatus: CommitmentVerificationStatus.detected,
      displayName: 'Spotify',
      amount: 10.99,
      amountLabel: '£10.99',
      currency: 'GBP',
      dueDate: _now.add(const Duration(days: 16)),
      dueDateLabel: 'in 16 days',
      frequency: 'Monthly',
      autopay: true,
      category: 'Entertainment',
      confidenceScore: 0.91,
    ),
    CommitmentItem(
      id: 'debt-mortgage',
      type: CommitmentType.debtRepayment,
      verificationStatus: CommitmentVerificationStatus.confirmed,
      displayName: 'Halifax Mortgage',
      amount: 1250.00,
      amountLabel: '£1,250.00',
      currency: 'GBP',
      dueDate: _now.add(const Duration(days: 15)),
      dueDateLabel: 'in 15 days',
      frequency: 'Monthly',
      autopay: true,
      category: 'Housing',
    ),
  ];
}
