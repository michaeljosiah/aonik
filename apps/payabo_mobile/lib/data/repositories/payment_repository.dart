enum PaymentResult {
  pending,
  success,
  failed,
}

class PaymentIntent {
  const PaymentIntent({
    required this.paymentIntentId,
    required this.orderId,
    required this.providerReference,
    required this.status,
  });

  final String paymentIntentId;
  final String orderId;
  final String providerReference;
  final PaymentResult status;
}

abstract class PaymentRepository {
  Future<PaymentIntent> createPaymentIntent({
    required String orderId,
  });

  Future<PaymentResult> getPaymentStatus({
    required String paymentIntentId,
  });
}
