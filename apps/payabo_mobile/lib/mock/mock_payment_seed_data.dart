import '../app/demo/demo_data_mode.dart';
import '../features/payments/presentation/payment_flow_state.dart';

/// Demo seed data for the payment flow, kept separate from the state class
/// so that `PaymentFlowState` has no knowledge of demo/mock concerns.
class MockPaymentSeedData {
  const MockPaymentSeedData._();

  static List<SavedCard> savedCards(DemoDataMode mode) {
    if (mode == DemoDataMode.fresh) {
      return const <SavedCard>[];
    }

    return const <SavedCard>[
      SavedCard(
        id: 'card_visa_4567',
        brand: 'Card type',
        last4: '4567',
        expiryLabel: '12/24',
      ),
      SavedCard(
        id: 'card_master_9021',
        brand: 'Card type',
        last4: '9021',
        expiryLabel: '08/27',
      ),
    ];
  }

  static List<PaymentFriend> friends(DemoDataMode mode) {
    if (mode == DemoDataMode.fresh) {
      return const <PaymentFriend>[];
    }

    return const <PaymentFriend>[
      PaymentFriend(
        id: 'friend_dany',
        firstName: 'Dany',
        lastName: 'Keys',
        email: 'dany.keys@mailinator.com',
        relationship: 'Sister',
        isFavorite: true,
      ),
      PaymentFriend(
        id: 'friend_alicia',
        firstName: 'Alicia',
        lastName: 'Keys',
        email: 'alicia.keys@mailinator.com',
        relationship: 'Mother',
        isFavorite: false,
      ),
      PaymentFriend(
        id: 'friend_ken',
        firstName: 'Ken',
        lastName: 'Keys',
        email: 'ken.keys@mailinator.com',
        relationship: 'Uncle',
        isFavorite: false,
      ),
    ];
  }
}
