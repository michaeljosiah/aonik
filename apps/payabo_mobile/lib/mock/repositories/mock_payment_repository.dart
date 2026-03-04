import '../../data/repositories/payment_repository.dart';
import '../mock_behavior.dart';

class MockPaymentRepository implements PaymentRepository {
  static int _counter = 1;
  final Map<String, PaymentResult> _intentStatus = <String, PaymentResult>{};
  final Map<String, int> _statusChecks = <String, int>{};

  @override
  Future<PaymentIntent> createPaymentIntent({required String orderId}) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('payments.createIntent');

    final String paymentIntentId = 'pi_${_counter.toString().padLeft(6, '0')}';
    _counter += 1;
    final String providerReference = 'mock_ref_${paymentIntentId.substring(3)}';

    _intentStatus[paymentIntentId] = PaymentResult.pending;
    _statusChecks[paymentIntentId] = 0;

    return PaymentIntent(
      paymentIntentId: paymentIntentId,
      orderId: orderId,
      providerReference: providerReference,
      status: PaymentResult.pending,
    );
  }

  @override
  Future<PaymentResult> getPaymentStatus(
      {required String paymentIntentId}) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('payments.getStatus');

    final PaymentResult current =
        _intentStatus[paymentIntentId] ?? PaymentResult.pending;
    final int checks = (_statusChecks[paymentIntentId] ?? 0) + 1;
    _statusChecks[paymentIntentId] = checks;

    if (current == PaymentResult.pending && checks == 1) {
      return PaymentResult.pending;
    }

    if (current == PaymentResult.pending && checks >= 2) {
      final int trailingNumber =
          int.tryParse(paymentIntentId.replaceAll(RegExp(r'[^0-9]'), '')) ?? 0;
      final PaymentResult resolved = trailingNumber % 4 == 0
          ? PaymentResult.failed
          : PaymentResult.success;
      _intentStatus[paymentIntentId] = resolved;
      return resolved;
    }

    return current;
  }
}
