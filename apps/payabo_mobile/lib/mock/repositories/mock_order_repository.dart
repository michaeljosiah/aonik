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

  @override
  Future<PricingBreakdown> getPricingBreakdown(String orderId) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('orders.getPricingBreakdown');

    return const PricingBreakdown(
      lines: <PricingLine>[
        PricingLine(label: 'Rate NGN:GBP', value: '303.5770', subtle: true),
        PricingLine(label: 'Sub-total', value: 'GBP 5.11', bold: true),
        PricingLine(label: 'Fees', value: 'GBP 1.99'),
        PricingLine(label: 'VAT', value: 'GBP 0.30'),
        PricingLine(label: 'Total', value: 'GBP 7.40', bold: true, isDivider: true),
        PricingLine(label: 'You will earn', value: '74 MBA POINTS', accent: true),
      ],
    );
  }

  @override
  Future<OrderPointsSummary> getPointsSummary(String orderId) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('orders.getPointsSummary');

    return const OrderPointsSummary(
      pointsEarned: 78,
      totalPoints: 5800,
      pointsLabel: 'MBA',
    );
  }
}
