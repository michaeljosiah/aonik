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

abstract class OrderRepository {
  Future<DraftOrder> createDraftOrder({
    required String billerName,
    required String serviceName,
    required String countryCode,
    required double amount,
    required String currency,
  });

  Future<DraftOrder?> getDraftOrder(String orderId);
}
