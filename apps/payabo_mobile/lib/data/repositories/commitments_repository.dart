// ─────────────────────────────────────────────────────────
//  CommitmentsRepository — interface + DTOs
//
//  Surfaces the unified recurring-commitment list from
//  GET /personal-finance/commitments, covering bills,
//  subscriptions, and debt repayments in one read model.
// ─────────────────────────────────────────────────────────

enum CommitmentType { bill, subscription, debtRepayment }

enum CommitmentVerificationStatus {
  detected,
  confirmed,
  manual,
  imported,
  rejected,
  archived,
}

enum CommitmentFilter { all, bills, subscriptions, loans, dueSoon, detected }

extension CommitmentFilterLabel on CommitmentFilter {
  String get label {
    switch (this) {
      case CommitmentFilter.all:
        return 'All';
      case CommitmentFilter.bills:
        return 'Bills';
      case CommitmentFilter.subscriptions:
        return 'Subscriptions';
      case CommitmentFilter.loans:
        return 'Loans';
      case CommitmentFilter.dueSoon:
        return 'Due soon';
      case CommitmentFilter.detected:
        return 'Detected';
    }
  }
}

class CommitmentItem {
  const CommitmentItem({
    required this.id,
    required this.type,
    required this.verificationStatus,
    required this.displayName,
    this.amountLabel,
    this.amount,
    this.currency,
    this.dueDate,
    this.dueDateLabel,
    this.frequency,
    this.autopay = false,
    this.category,
    this.confidenceScore,
  });

  final String id;
  final CommitmentType type;
  final CommitmentVerificationStatus verificationStatus;
  final String displayName;
  final String? amountLabel;
  final double? amount;
  final String? currency;
  final DateTime? dueDate;
  final String? dueDateLabel;
  final String? frequency;
  final bool autopay;
  final String? category;
  final double? confidenceScore;

  bool get isDetected =>
      verificationStatus == CommitmentVerificationStatus.detected;

  bool get isDueSoon =>
      dueDate != null &&
      dueDate!.difference(DateTime.now()).inDays <= 7 &&
      dueDate!.isAfter(DateTime.now());
}

class CommitmentTotals {
  const CommitmentTotals({
    required this.totalUpcomingAmountLabel,
    required this.dueSoonCount,
    required this.detectedCount,
    required this.billsCount,
    required this.subscriptionsCount,
    required this.debtRepaymentsCount,
  });

  final String totalUpcomingAmountLabel;
  final int dueSoonCount;
  final int detectedCount;
  final int billsCount;
  final int subscriptionsCount;
  final int debtRepaymentsCount;

  int get totalCount => billsCount + subscriptionsCount + debtRepaymentsCount;
}

class CommitmentListPage {
  const CommitmentListPage({
    required this.items,
    required this.totals,
  });

  final List<CommitmentItem> items;
  final CommitmentTotals totals;

  List<CommitmentItem> filtered(CommitmentFilter filter) {
    switch (filter) {
      case CommitmentFilter.all:
        return items;
      case CommitmentFilter.bills:
        return items.where((i) => i.type == CommitmentType.bill).toList();
      case CommitmentFilter.subscriptions:
        return items
            .where((i) => i.type == CommitmentType.subscription)
            .toList();
      case CommitmentFilter.loans:
        return items
            .where((i) => i.type == CommitmentType.debtRepayment)
            .toList();
      case CommitmentFilter.dueSoon:
        return items.where((i) => i.isDueSoon).toList();
      case CommitmentFilter.detected:
        return items.where((i) => i.isDetected).toList();
    }
  }
}

abstract class CommitmentsRepository {
  Future<CommitmentListPage> listCommitments();
  Future<void> confirmCommitment(String id);
  Future<void> rejectCommitment(String id);
}
