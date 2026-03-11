import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../shared/reference/payabo_country_reference.dart';

class PaymentCountryReferenceService {
  const PaymentCountryReferenceService();

  String resolveCurrency(String countryCode) {
    return resolvePayaboCurrencyCode(countryCode);
  }
}

final Provider<PaymentCountryReferenceService>
    paymentCountryReferenceServiceProvider =
    Provider<PaymentCountryReferenceService>(
  (Ref ref) => const PaymentCountryReferenceService(),
);
