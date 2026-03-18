class DraftOrder {
  const DraftOrder({
    required this.orderId,
    required this.billerName,
    required this.serviceName,
    required this.countryCode,
    required this.amount,
    required this.currency,
  });

  final String orderId;
  final String billerName;
  final String serviceName;
  final String countryCode;
  final double amount;
  final String currency;
}

/// A single line in a pricing breakdown table (e.g. "Fees  GBP 1.99").
class PricingLine {
  const PricingLine({
    required this.label,
    required this.value,
    this.bold = false,
    this.subtle = false,
    this.accent = false,
    this.isDivider = false,
  });

  final String label;
  final String value;
  final bool bold;
  final bool subtle;
  final bool accent;

  /// When true, the UI should render a divider before this line.
  final bool isDivider;
}

/// Full pricing breakdown for an order checkout screen.
class PricingBreakdown {
  const PricingBreakdown({
    required this.lines,
  });

  final List<PricingLine> lines;
}

/// Points summary shown on the thank-you / confirmation screen.
class OrderPointsSummary {
  const OrderPointsSummary({
    required this.pointsEarned,
    required this.totalPoints,
    required this.pointsLabel,
  });

  final int pointsEarned;
  final int totalPoints;
  final String pointsLabel;
}

abstract class OrderRepository {
  Future<DraftOrder> createDraftOrder({
    required String billerName,
    required String serviceName,
    required String countryCode,
    required double amount,
    required String currency,
  });

  Future<DraftOrder?> getDraftOrder(String orderId);

  /// Returns the pricing breakdown for the current order checkout.
  Future<PricingBreakdown> getPricingBreakdown(String orderId);

  /// Returns the points summary shown after a successful payment.
  Future<OrderPointsSummary> getPointsSummary(String orderId);
}
