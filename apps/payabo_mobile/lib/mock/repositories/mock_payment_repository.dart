import '../../data/repositories/payment_repository.dart';

class MockPaymentRepository implements PaymentRepository {
  final Map<String, PaymentResult> _intentStatus = <String, PaymentResult>{};
  final Map<String, int> _statusChecks = <String, int>{};

  @override
  Future<PaymentIntent> createPaymentIntent({required String orderId}) async {
    await Future<void>.delayed(const Duration(milliseconds: 420));

    final String paymentIntentId =
        'pi_${DateTime.now().millisecondsSinceEpoch}';
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
    await Future<void>.delayed(const Duration(milliseconds: 500));

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
