import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../../data/repositories/catalog_repository.dart';
import '../../../data/repositories/order_repository.dart';
import '../../../data/repositories/payment_repository.dart';
import '../../../data/repositories/repository_providers.dart';

enum PaymentMethodType {
  card,
  friend,
}

class SavedCard {
  const SavedCard({
    required this.id,
    required this.brand,
    required this.last4,
    required this.expiryLabel,
  });

  final String id;
  final String brand;
  final String last4;
  final String expiryLabel;
}

class PaymentFriend {
  const PaymentFriend({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.relationship,
    required this.isFavorite,
  });

  final String id;
  final String firstName;
  final String lastName;
  final String email;
  final String relationship;
  final bool isFavorite;

  String get displayName => '$firstName $lastName';
}

class PaymentFlowState {
  const PaymentFlowState({
    required this.countryCode,
    required this.providerId,
    required this.providerName,
    required this.category,
    required this.serviceType,
    required this.smartCardId,
    required this.contactReference,
    required this.amount,
    required this.recurringBill,
    required this.recurringFrequency,
    required this.recurringStartsOn,
    required this.recurringEndsOn,
    required this.useSamePaymentMethodForRecurring,
    required this.paymentMethod,
    required this.selectedCardId,
    required this.saveCard,
    required this.selectedFriendId,
    required this.friendMessage,
    required this.orderId,
    required this.paymentIntentId,
    required this.providerReference,
    required this.paymentResult,
    required this.statusChecks,
    required this.savedCards,
    required this.friends,
  });

  final String countryCode;
  final String providerId;
  final String providerName;
  final String category;
  final String serviceType;
  final String smartCardId;
  final String contactReference;
  final String amount;
  final bool recurringBill;
  final String recurringFrequency;
  final DateTime? recurringStartsOn;
  final DateTime? recurringEndsOn;
  final bool useSamePaymentMethodForRecurring;
  final PaymentMethodType paymentMethod;
  final String selectedCardId;
  final bool saveCard;
  final String selectedFriendId;
  final String friendMessage;
  final String orderId;
  final String paymentIntentId;
  final String providerReference;
  final PaymentResult? paymentResult;
  final int statusChecks;
  final List<SavedCard> savedCards;
  final List<PaymentFriend> friends;

  SavedCard? get selectedCard {
    for (final card in savedCards) {
      if (card.id == selectedCardId) {
        return card;
      }
    }

    return null;
  }

  PaymentFriend? get selectedFriend {
    for (final friend in friends) {
      if (friend.id == selectedFriendId) {
        return friend;
      }
    }

    return null;
  }

  PaymentFlowState copyWith({
    String? countryCode,
    String? providerId,
    String? providerName,
    String? category,
    String? serviceType,
    String? smartCardId,
    String? contactReference,
    String? amount,
    bool? recurringBill,
    String? recurringFrequency,
    Object? recurringStartsOn = _copySentinel,
    Object? recurringEndsOn = _copySentinel,
    bool? useSamePaymentMethodForRecurring,
    PaymentMethodType? paymentMethod,
    String? selectedCardId,
    bool? saveCard,
    String? selectedFriendId,
    String? friendMessage,
    String? orderId,
    String? paymentIntentId,
    String? providerReference,
    Object? paymentResult = _copySentinel,
    int? statusChecks,
    List<SavedCard>? savedCards,
    List<PaymentFriend>? friends,
  }) {
    return PaymentFlowState(
      countryCode: countryCode ?? this.countryCode,
      providerId: providerId ?? this.providerId,
      providerName: providerName ?? this.providerName,
      category: category ?? this.category,
      serviceType: serviceType ?? this.serviceType,
      smartCardId: smartCardId ?? this.smartCardId,
      contactReference: contactReference ?? this.contactReference,
      amount: amount ?? this.amount,
      recurringBill: recurringBill ?? this.recurringBill,
      recurringFrequency: recurringFrequency ?? this.recurringFrequency,
      recurringStartsOn: recurringStartsOn == _copySentinel
          ? this.recurringStartsOn
          : recurringStartsOn as DateTime?,
      recurringEndsOn: recurringEndsOn == _copySentinel
          ? this.recurringEndsOn
          : recurringEndsOn as DateTime?,
      useSamePaymentMethodForRecurring: useSamePaymentMethodForRecurring ??
          this.useSamePaymentMethodForRecurring,
      paymentMethod: paymentMethod ?? this.paymentMethod,
      selectedCardId: selectedCardId ?? this.selectedCardId,
      saveCard: saveCard ?? this.saveCard,
      selectedFriendId: selectedFriendId ?? this.selectedFriendId,
      friendMessage: friendMessage ?? this.friendMessage,
      orderId: orderId ?? this.orderId,
      paymentIntentId: paymentIntentId ?? this.paymentIntentId,
      providerReference: providerReference ?? this.providerReference,
      paymentResult: paymentResult == _copySentinel
          ? this.paymentResult
          : paymentResult as PaymentResult?,
      statusChecks: statusChecks ?? this.statusChecks,
      savedCards: savedCards ?? this.savedCards,
      friends: friends ?? this.friends,
    );
  }

  factory PaymentFlowState.initial() {
    return const PaymentFlowState(
      countryCode: 'GH',
      providerId: '',
      providerName: '',
      category: 'All',
      serviceType: 'Montage Cable TV',
      smartCardId: '',
      contactReference: '',
      amount: '',
      recurringBill: false,
      recurringFrequency: 'Monthly',
      recurringStartsOn: null,
      recurringEndsOn: null,
      useSamePaymentMethodForRecurring: true,
      paymentMethod: PaymentMethodType.card,
      selectedCardId: 'card_visa_4567',
      saveCard: true,
      selectedFriendId: '',
      friendMessage: '',
      orderId: '',
      paymentIntentId: '',
      providerReference: '',
      paymentResult: null,
      statusChecks: 0,
      savedCards: <SavedCard>[
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
      ],
      friends: <PaymentFriend>[
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
      ],
    );
  }
}

class PaymentFlowController extends StateNotifier<PaymentFlowState> {
  PaymentFlowController(Ref _) : super(PaymentFlowState.initial()) {
    _restoreState();
  }

  static const String _storageKey = 'payabo.payment_flow_state.v1';

  void setCountryCode(String countryCode) {
    _commit(
      state.copyWith(
        countryCode: countryCode.toUpperCase(),
        providerId: '',
        providerName: '',
        orderId: '',
        paymentIntentId: '',
        providerReference: '',
        paymentResult: null,
        statusChecks: 0,
      ),
    );
  }

  void setCategory(String category) {
    _commit(state.copyWith(category: category));
  }

  void setProvider({required String providerId, required String providerName}) {
    _commit(
      state.copyWith(
        providerId: providerId,
        providerName: providerName,
        orderId: '',
        paymentIntentId: '',
        providerReference: '',
        paymentResult: null,
        statusChecks: 0,
      ),
    );
  }

  void setServiceType(String serviceType) {
    _commit(state.copyWith(serviceType: serviceType));
  }

  void setSmartCardId(String smartCardId) {
    _commit(state.copyWith(smartCardId: smartCardId));
  }

  void setContactReference(String value) {
    _commit(state.copyWith(contactReference: value));
  }

  void setAmount(String amount) {
    _commit(state.copyWith(amount: amount));
  }

  void setRecurringBill(bool recurringBill) {
    _commit(state.copyWith(recurringBill: recurringBill));
  }

  void setRecurringFrequency(String frequency) {
    _commit(state.copyWith(recurringFrequency: frequency));
  }

  void setRecurringStartsOn(DateTime? date) {
    _commit(state.copyWith(recurringStartsOn: date));
  }

  void setRecurringEndsOn(DateTime? date) {
    _commit(state.copyWith(recurringEndsOn: date));
  }

  void setUseSamePaymentMethodForRecurring(bool value) {
    _commit(state.copyWith(useSamePaymentMethodForRecurring: value));
  }

  void setPaymentMethod(PaymentMethodType type) {
    _commit(state.copyWith(paymentMethod: type));
  }

  void selectCard(String cardId) {
    _commit(state.copyWith(selectedCardId: cardId));
  }

  void setSaveCard(bool saveCard) {
    _commit(state.copyWith(saveCard: saveCard));
  }

  void selectFriend(String friendId) {
    _commit(state.copyWith(selectedFriendId: friendId));
  }

  void setFriendMessage(String message) {
    _commit(state.copyWith(friendMessage: message));
  }

  void addFriend({
    required String firstName,
    required String lastName,
    required String email,
    required String relationship,
    required bool saveAsFavorite,
  }) {
    final friend = PaymentFriend(
      id: 'friend_${DateTime.now().millisecondsSinceEpoch}',
      firstName: firstName.trim(),
      lastName: lastName.trim(),
      email: email.trim(),
      relationship: relationship.trim().isEmpty
          ? 'Other relationship'
          : relationship.trim(),
      isFavorite: saveAsFavorite,
    );

    _commit(
      state.copyWith(
        friends: <PaymentFriend>[friend, ...state.friends],
        selectedFriendId: friend.id,
      ),
    );
  }

  Future<void> createDraftOrder(OrderRepository repository) async {
    final amountValue = _parseAmount(state.amount);
    final draft = await repository.createDraftOrder(
      billerName: state.providerName,
      serviceName: state.serviceType,
      countryCode: state.countryCode,
      amount: amountValue,
      currency: _resolveCurrency(state.countryCode),
    );

    _commit(
      state.copyWith(
        orderId: draft.orderId,
        paymentIntentId: '',
        providerReference: '',
        paymentResult: null,
        statusChecks: 0,
      ),
    );
  }

  Future<void> createPaymentIntent(PaymentRepository repository) async {
    if (state.orderId.isEmpty) {
      return;
    }

    final intent = await repository.createPaymentIntent(orderId: state.orderId);
    _commit(
      state.copyWith(
        paymentIntentId: intent.paymentIntentId,
        providerReference: intent.providerReference,
        paymentResult: intent.status,
        statusChecks: 0,
      ),
    );
  }

  Future<PaymentResult?> refreshPaymentStatus(
      PaymentRepository repository) async {
    if (state.paymentIntentId.isEmpty) {
      return state.paymentResult;
    }

    final result = await repository.getPaymentStatus(
      paymentIntentId: state.paymentIntentId,
    );

    _commit(
      state.copyWith(
        paymentResult: result,
        statusChecks: state.statusChecks + 1,
      ),
    );
    return result;
  }

  void resetForNewCheckout() {
    _commit(
      state.copyWith(
        orderId: '',
        paymentIntentId: '',
        providerReference: '',
        paymentResult: null,
        statusChecks: 0,
        selectedFriendId: '',
        friendMessage: '',
      ),
    );
  }

  Future<void> _restoreState() async {
    final prefs = await SharedPreferences.getInstance();
    final raw = prefs.getString(_storageKey);
    if (raw == null || raw.isEmpty) {
      return;
    }

    final decoded = jsonDecode(raw);
    if (decoded is! Map<String, dynamic>) {
      return;
    }

    _commit(
      state.copyWith(
        countryCode: decoded['countryCode'] as String? ?? state.countryCode,
        providerId: decoded['providerId'] as String? ?? state.providerId,
        providerName: decoded['providerName'] as String? ?? state.providerName,
        category: decoded['category'] as String? ?? state.category,
        serviceType: decoded['serviceType'] as String? ?? state.serviceType,
        smartCardId: decoded['smartCardId'] as String? ?? state.smartCardId,
        contactReference:
            decoded['contactReference'] as String? ?? state.contactReference,
        amount: decoded['amount'] as String? ?? state.amount,
        recurringBill: decoded['recurringBill'] as bool? ?? state.recurringBill,
        recurringFrequency: decoded['recurringFrequency'] as String? ??
            state.recurringFrequency,
        recurringStartsOn: decoded['recurringStartsOn'] == null
            ? null
            : DateTime.tryParse(decoded['recurringStartsOn'] as String),
        recurringEndsOn: decoded['recurringEndsOn'] == null
            ? null
            : DateTime.tryParse(decoded['recurringEndsOn'] as String),
        useSamePaymentMethodForRecurring:
            decoded['useSamePaymentMethodForRecurring'] as bool? ??
                state.useSamePaymentMethodForRecurring,
        paymentMethod: PaymentMethodType.values[
            (decoded['paymentMethod'] as int? ?? state.paymentMethod.index)
                .clamp(0, PaymentMethodType.values.length - 1)],
        selectedCardId:
            decoded['selectedCardId'] as String? ?? state.selectedCardId,
        saveCard: decoded['saveCard'] as bool? ?? state.saveCard,
        selectedFriendId:
            decoded['selectedFriendId'] as String? ?? state.selectedFriendId,
        friendMessage:
            decoded['friendMessage'] as String? ?? state.friendMessage,
        orderId: decoded['orderId'] as String? ?? state.orderId,
        paymentIntentId:
            decoded['paymentIntentId'] as String? ?? state.paymentIntentId,
        providerReference:
            decoded['providerReference'] as String? ?? state.providerReference,
        paymentResult: decoded['paymentResult'] == null
            ? null
            : PaymentResult.values[(decoded['paymentResult'] as int)
                .clamp(0, PaymentResult.values.length - 1)],
        statusChecks: decoded['statusChecks'] as int? ?? state.statusChecks,
      ),
      persist: false,
    );
  }

  Future<void> _persistState() async {
    final prefs = await SharedPreferences.getInstance();
    final payload = <String, dynamic>{
      'countryCode': state.countryCode,
      'providerId': state.providerId,
      'providerName': state.providerName,
      'category': state.category,
      'serviceType': state.serviceType,
      'smartCardId': state.smartCardId,
      'contactReference': state.contactReference,
      'amount': state.amount,
      'recurringBill': state.recurringBill,
      'recurringFrequency': state.recurringFrequency,
      'recurringStartsOn': state.recurringStartsOn?.toIso8601String(),
      'recurringEndsOn': state.recurringEndsOn?.toIso8601String(),
      'useSamePaymentMethodForRecurring':
          state.useSamePaymentMethodForRecurring,
      'paymentMethod': state.paymentMethod.index,
      'selectedCardId': state.selectedCardId,
      'saveCard': state.saveCard,
      'selectedFriendId': state.selectedFriendId,
      'friendMessage': state.friendMessage,
      'orderId': state.orderId,
      'paymentIntentId': state.paymentIntentId,
      'providerReference': state.providerReference,
      'paymentResult': state.paymentResult?.index,
      'statusChecks': state.statusChecks,
    };

    await prefs.setString(_storageKey, jsonEncode(payload));
  }

  void _commit(PaymentFlowState nextState, {bool persist = true}) {
    state = nextState;
    if (persist) {
      _persistState();
    }
  }

  static double _parseAmount(String input) {
    final normalized = input.replaceAll(RegExp(r'[^0-9.]'), '');
    return double.tryParse(normalized) ?? 0;
  }

  static String _resolveCurrency(String countryCode) {
    switch (countryCode.toUpperCase()) {
      case 'GH':
        return 'GHS';
      case 'NG':
        return 'NGN';
      case 'KE':
        return 'KES';
      case 'GB':
        return 'GBP';
      case 'BW':
        return 'BWP';
      case 'ZM':
        return 'ZMW';
      case 'ZW':
        return 'USD';
      default:
        return 'GBP';
    }
  }
}

const _copySentinel = Object();

final StateNotifierProvider<PaymentFlowController, PaymentFlowState>
    paymentFlowControllerProvider =
    StateNotifierProvider<PaymentFlowController, PaymentFlowState>(
  PaymentFlowController.new,
);

final FutureProvider<List<CatalogCountry>> paymentCountriesProvider =
    FutureProvider<List<CatalogCountry>>(
  (Ref ref) async {
    final repository = ref.watch(catalogRepositoryProvider);
    return repository.getCountries();
  },
);

final FutureProvider<List<CatalogProvider>> paymentProvidersProvider =
    FutureProvider<List<CatalogProvider>>(
  (Ref ref) async {
    final countryCode = ref.watch(
      paymentFlowControllerProvider.select((state) => state.countryCode),
    );
    final repository = ref.watch(catalogRepositoryProvider);
    return repository.getProviders(countryCode: countryCode);
  },
);
