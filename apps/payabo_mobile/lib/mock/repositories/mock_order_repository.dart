import '../../data/repositories/order_repository.dart';

class MockOrderRepository implements OrderRepository {
  final Map<String, DraftOrder> _ordersById = <String, DraftOrder>{};

  @override
  Future<DraftOrder> createDraftOrder({
    required String billerName,
    required String serviceName,
    required String countryCode,
    required double amount,
    required String currency,
  }) async {
    await Future<void>.delayed(const Duration(milliseconds: 300));

    final String orderId = 'ord_${DateTime.now().millisecondsSinceEpoch}';
    final DraftOrder order = DraftOrder(
      orderId: orderId,
      billerName: billerName,
      serviceName: serviceName,
      countryCode: countryCode,
      amount: amount,
      currency: currency,
    );

    _ordersById[orderId] = order;
    return order;
  }

  @override
  Future<DraftOrder?> getDraftOrder(String orderId) async {
    await Future<void>.delayed(const Duration(milliseconds: 180));
    return _ordersById[orderId];
  }
}
