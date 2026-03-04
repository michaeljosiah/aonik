import '../../data/repositories/order_repository.dart';
import '../mock_behavior.dart';

class MockOrderRepository implements OrderRepository {
  static int _counter = 1;
  final Map<String, DraftOrder> _ordersById = <String, DraftOrder>{};

  @override
  Future<DraftOrder> createDraftOrder({
    required String billerName,
    required String serviceName,
    required String countryCode,
    required double amount,
    required String currency,
  }) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('orders.createDraft');

    final String orderId = 'ord_${_counter.toString().padLeft(6, '0')}';
    _counter += 1;
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
    await MockBehavior.delay();
    return _ordersById[orderId];
  }
}
