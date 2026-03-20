// ─────────────────────────────────────────────────────────
//  PayActivityRepository — interface + DTOs
//
//  Surfaces recent pay activity (transfers, bills) and
//  individual transaction detail for the Pay dashboard,
//  activity list, and transaction details screens.
// ─────────────────────────────────────────────────────────

// ─────────────────────────────────────────────────────────
//  DTOs — activity items & transaction detail
// ─────────────────────────────────────────────────────────

/// The type of payment activity.
enum PayActivityTransactionType {
  transfer,
  bill,
}

/// A single activity row shown in the Pay dashboard and activity list.
class PayActivityTransaction {
  const PayActivityTransaction({
    required this.id,
    required this.title,
    required this.subtitle,
    required this.amountLabel,
    required this.status,
    required this.type,
    required this.dateGroupLabel,
  });

  /// Unique transaction identifier.
  final String id;

  /// Display title (e.g. "Transfer to Ama Serwaa").
  final String title;

  /// Contextual subtitle (e.g. "Yesterday, 07:18 PM").
  final String subtitle;

  /// Formatted amount (e.g. "GHS 500.00").
  final String amountLabel;

  /// Status label (e.g. "Completed", "Processing", "Failed").
  final String status;

  /// Transfer or bill.
  final PayActivityTransactionType type;

  /// Date group heading for the activity list (e.g. "TODAY", "YESTERDAY",
  /// "MAY 3"). Transactions with the same value are grouped together.
  final String dateGroupLabel;
}

/// Recipient details shown on the transaction details screen.
class PayTransactionRecipient {
  const PayTransactionRecipient({
    required this.name,
    required this.initials,
    required this.bankName,
    required this.maskedAccountNumber,
    required this.country,
  });

  final String name;

  /// Two-letter initials for the avatar (e.g. "AS").
  final String initials;

  final String bankName;

  /// Masked account number (e.g. "**** **** 4521").
  final String maskedAccountNumber;

  final String country;
}

/// Full transaction detail for the details screen.
class PayTransactionDetail {
  const PayTransactionDetail({
    required this.id,
    required this.status,
    required this.statusDescription,
    required this.amountLabel,
    required this.feeLabel,
    required this.totalLabel,
    required this.recipient,
    required this.orderId,
    required this.paymentIntentId,
    required this.providerReference,
    required this.reference,
  });

  final String id;

  /// Status label (e.g. "Completed").
  final String status;

  /// Human-readable description (e.g. "This transfer was successful.").
  final String statusDescription;

  /// Formatted principal amount (e.g. "GHS 500.00").
  final String amountLabel;

  /// Formatted fee (e.g. "GHS 2.50").
  final String feeLabel;

  /// Formatted total (e.g. "GHS 502.50").
  final String totalLabel;

  final PayTransactionRecipient recipient;

  /// Platform order ID (e.g. "ORD-2026-0519-A7C3").
  final String orderId;

  /// Payment intent ID (e.g. "PI-8F42-D1E9-B6A0").
  final String paymentIntentId;

  /// Provider reference (e.g. "GCB-TXN-993812").
  final String providerReference;

  /// User-supplied reference (e.g. "Family support - May").
  final String reference;
}

// ─────────────────────────────────────────────────────────
//  Aggregate — recent activity summary
// ─────────────────────────────────────────────────────────

/// The data returned for the recent activity section of the Pay dashboard
/// and the full activity list.
class PayActivitySummary {
  const PayActivitySummary({
    this.transactions = const <PayActivityTransaction>[],
  });

  final List<PayActivityTransaction> transactions;
}

// ─────────────────────────────────────────────────────────
//  Repository interface
// ─────────────────────────────────────────────────────────

abstract class PayActivityRepository {
  /// Returns the recent pay activity for the dashboard and activity list.
  Future<PayActivitySummary> getRecentActivity();

  /// Returns the full detail for a single transaction.
  Future<PayTransactionDetail?> getTransactionDetail(String transactionId);
}
