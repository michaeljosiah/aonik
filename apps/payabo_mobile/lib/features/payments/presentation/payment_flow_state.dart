import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../data/repositories/catalog_repository.dart';
import '../../../data/repositories/order_repository.dart';
import '../../../data/repositories/payment_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../mock/mock_payment_seed_data.dart';
import 'payment_country_reference_service.dart';
import 'payment_flow_persistence.dart';

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

  factory PaymentFlowState.initial({
    List<SavedCard> savedCards = const <SavedCard>[],
    List<PaymentFriend> friends = const <PaymentFriend>[],
  }) {
    return PaymentFlowState(
      countryCode: '',
      providerId: '',
      providerName: '',
      category: 'All',
      serviceType: '',
      smartCardId: '',
      contactReference: '',
      amount: '',
      recurringBill: false,
      recurringFrequency: 'Monthly',
      recurringStartsOn: null,
      recurringEndsOn: null,
      useSamePaymentMethodForRecurring: true,
      paymentMethod: PaymentMethodType.card,
      selectedCardId: savedCards.isEmpty ? '' : savedCards.first.id,
      saveCard: true,
      selectedFriendId: '',
      friendMessage: '',
      orderId: '',
      paymentIntentId: '',
      providerReference: '',
      paymentResult: null,
      statusChecks: 0,
      savedCards: savedCards,
      friends: friends,
    );
  }
}

class PaymentFlowController extends StateNotifier<PaymentFlowState> {
  PaymentFlowController(
    this._ref,
    this._demoDataMode, {
    List<SavedCard> initialSavedCards = const <SavedCard>[],
    List<PaymentFriend> initialFriends = const <PaymentFriend>[],
  }) : super(PaymentFlowState.initial(
          savedCards: initialSavedCards,
          friends: initialFriends,
        )) {
    _restoreState();
  }

  final Ref _ref;
  final DemoDataMode _demoDataMode;

  PaymentFlowPersistence get _persistence =>
      _ref.read(paymentFlowPersistenceProvider);

  PaymentCountryReferenceService get _countryReferences =>
      _ref.read(paymentCountryReferenceServiceProvider);

  void setCountryCode(String countryCode) {
    state = state.copyWith(
      countryCode: countryCode.toUpperCase(),
      providerId: '',
      providerName: '',
      orderId: '',
      paymentIntentId: '',
      providerReference: '',
      paymentResult: null,
      statusChecks: 0,
    );
    _persistState();
  }

  void setCategory(String category) {
    state = state.copyWith(category: category);
  }

  void setProvider({required String providerId, required String providerName}) {
    state = state.copyWith(
      providerId: providerId,
      providerName: providerName,
      orderId: '',
      paymentIntentId: '',
      providerReference: '',
      paymentResult: null,
      statusChecks: 0,
    );
    _persistState();
  }

  void setServiceType(String serviceType) {
    state = state.copyWith(serviceType: serviceType);
  }

  void setSmartCardId(String smartCardId) {
    state = state.copyWith(smartCardId: smartCardId);
  }

  void setContactReference(String value) {
    state = state.copyWith(contactReference: value);
  }

  void setAmount(String amount) {
    state = state.copyWith(amount: amount);
  }

  void setRecurringBill(bool recurringBill) {
    state = state.copyWith(
      recurringBill: recurringBill,
      recurringStartsOn: recurringBill ? state.recurringStartsOn : null,
      recurringEndsOn: recurringBill ? state.recurringEndsOn : null,
    );
  }

  void setRecurringFrequency(String frequency) {
    state = state.copyWith(recurringFrequency: frequency);
  }

  void setRecurringStartsOn(DateTime? date) {
    state = state.copyWith(recurringStartsOn: date);
  }

  void setRecurringEndsOn(DateTime? date) {
    state = state.copyWith(recurringEndsOn: date);
  }

  void setUseSamePaymentMethodForRecurring(bool value) {
    state = state.copyWith(useSamePaymentMethodForRecurring: value);
  }

  void setPaymentMethod(PaymentMethodType type) {
    state = state.copyWith(
      paymentMethod: type,
      selectedFriendId:
          type == PaymentMethodType.card ? '' : state.selectedFriendId,
      friendMessage:
          type == PaymentMethodType.card ? '' : state.friendMessage,
    );
    _persistState();
  }

  void selectCard(String cardId) {
    state = state.copyWith(selectedCardId: cardId);
    _persistState();
  }

  void setSaveCard(bool saveCard) {
    state = state.copyWith(saveCard: saveCard);
    _persistState();
  }

  void selectFriend(String friendId) {
    state = state.copyWith(selectedFriendId: friendId);
    _persistState();
  }

  void setFriendMessage(String message, {bool persist = false}) {
    state = state.copyWith(friendMessage: message);
    if (persist) {
      _persistState();
    }
  }

  void addFriend({
    required String firstName,
    required String lastName,
    required String email,
    required String relationship,
    required bool saveAsFavorite,
  }) {
    final friend = PaymentFriend(
      id: 'friend_${DateTime.now().microsecondsSinceEpoch}_${Object().hashCode}',
      firstName: firstName.trim(),
      lastName: lastName.trim(),
      email: email.trim(),
      relationship: relationship.trim().isEmpty
          ? 'Other relationship'
          : relationship.trim(),
      isFavorite: saveAsFavorite,
    );

    state = state.copyWith(
      friends: <PaymentFriend>[friend, ...state.friends],
      selectedFriendId: friend.id,
    );
    _persistState();
  }

  Future<void> createDraftOrder(OrderRepository repository) async {
    final amountValue = _parseAmount(state.amount);
    final draft = await repository.createDraftOrder(
      billerName: state.providerName,
      serviceName: state.serviceType,
      countryCode: state.countryCode,
      amount: amountValue,
      currency: _countryReferences.resolveCurrency(state.countryCode),
    );

    state = state.copyWith(
      orderId: draft.orderId,
      paymentIntentId: '',
      providerReference: '',
      paymentResult: null,
      statusChecks: 0,
    );
    _persistState();
  }

  Future<void> createPaymentIntent(PaymentRepository repository) async {
    if (state.orderId.isEmpty) {
      return;
    }

    final intent = await repository.createPaymentIntent(orderId: state.orderId);
    state = state.copyWith(
      paymentIntentId: intent.paymentIntentId,
      providerReference: intent.providerReference,
      paymentResult: intent.status,
      statusChecks: 0,
    );
    _persistState();
  }

  Future<PaymentResult?> refreshPaymentStatus(
      PaymentRepository repository) async {
    if (state.paymentIntentId.isEmpty) {
      return state.paymentResult;
    }

    final result = await repository.getPaymentStatus(
      paymentIntentId: state.paymentIntentId,
    );

    state = state.copyWith(
      paymentResult: result,
      statusChecks: state.statusChecks + 1,
    );
    _persistState();
    return result;
  }

  void resetForNewCheckout() {
    state = state.copyWith(
      orderId: '',
      paymentIntentId: '',
      providerReference: '',
      paymentResult: null,
      statusChecks: 0,
      selectedFriendId: '',
      friendMessage: '',
    );
    _persistState();
  }

  Future<void> _restoreState() async {
    final PersistedPaymentFlowSnapshot? snapshot = await _persistence.read();
    if (snapshot == null) {
      return;
    }

    if (snapshot.demoDataModeName != _demoDataMode.name) {
      await _persistence.clear();
      return;
    }

    state = state.copyWith(
      countryCode: snapshot.countryCode,
      providerId: snapshot.providerId,
      providerName: snapshot.providerName,
      category: snapshot.category,
      serviceType: snapshot.serviceType,
      smartCardId: snapshot.smartCardId,
      contactReference: snapshot.contactReference,
      amount: snapshot.amount,
      recurringBill: snapshot.recurringBill,
      recurringFrequency: snapshot.recurringFrequency,
      recurringStartsOn: snapshot.recurringStartsOn,
      recurringEndsOn: snapshot.recurringEndsOn,
      useSamePaymentMethodForRecurring:
          snapshot.useSamePaymentMethodForRecurring,
      paymentMethod: PaymentMethodType.values[snapshot.paymentMethodIndex
          .clamp(0, PaymentMethodType.values.length - 1)],
      selectedCardId: snapshot.selectedCardId,
      saveCard: snapshot.saveCard,
      selectedFriendId: snapshot.selectedFriendId,
      friendMessage: snapshot.friendMessage,
      orderId: snapshot.orderId,
      paymentIntentId: snapshot.paymentIntentId,
      providerReference: snapshot.providerReference,
      paymentResult: snapshot.paymentResultIndex == null
          ? null
          : PaymentResult.values[snapshot.paymentResultIndex!
              .clamp(0, PaymentResult.values.length - 1)],
      statusChecks: snapshot.statusChecks,
    );
  }

  Future<void> _persistState() async {
    await _persistence.write(
      PersistedPaymentFlowSnapshot(
        demoDataModeName: _demoDataMode.name,
        countryCode: state.countryCode,
        providerId: state.providerId,
        providerName: state.providerName,
        category: state.category,
        serviceType: state.serviceType,
        smartCardId: state.smartCardId,
        contactReference: state.contactReference,
        amount: state.amount,
        recurringBill: state.recurringBill,
        recurringFrequency: state.recurringFrequency,
        recurringStartsOn: state.recurringStartsOn,
        recurringEndsOn: state.recurringEndsOn,
        useSamePaymentMethodForRecurring:
            state.useSamePaymentMethodForRecurring,
        paymentMethodIndex: state.paymentMethod.index,
        selectedCardId: state.selectedCardId,
        saveCard: state.saveCard,
        selectedFriendId: state.selectedFriendId,
        friendMessage: state.friendMessage,
        orderId: state.orderId,
        paymentIntentId: state.paymentIntentId,
        providerReference: state.providerReference,
        paymentResultIndex: state.paymentResult?.index,
        statusChecks: state.statusChecks,
      ),
    );
  }

  static double _parseAmount(String input) {
    final normalized = input.replaceAll(RegExp(r'[^0-9.]'), '');
    return double.tryParse(normalized) ?? 0;
  }
}

const _copySentinel = Object();

class PaymentOrderSummary {
  const PaymentOrderSummary({
    required this.countryCode,
    required this.providerName,
    required this.serviceType,
    required this.smartCardId,
    required this.amount,
  });

  final String countryCode;
  final String providerName;
  final String serviceType;
  final String smartCardId;
  final String amount;

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        other is PaymentOrderSummary &&
            other.countryCode == countryCode &&
            other.providerName == providerName &&
            other.serviceType == serviceType &&
            other.smartCardId == smartCardId &&
            other.amount == amount;
  }

  @override
  int get hashCode {
    return Object.hash(
      countryCode,
      providerName,
      serviceType,
      smartCardId,
      amount,
    );
  }
}

class PaymentStatusSummary {
  const PaymentStatusSummary({
    required this.orderId,
    required this.paymentIntentId,
    required this.paymentResult,
    required this.statusChecks,
  });

  final String orderId;
  final String paymentIntentId;
  final PaymentResult? paymentResult;
  final int statusChecks;

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        other is PaymentStatusSummary &&
            other.orderId == orderId &&
            other.paymentIntentId == paymentIntentId &&
            other.paymentResult == paymentResult &&
            other.statusChecks == statusChecks;
  }

  @override
  int get hashCode {
    return Object.hash(orderId, paymentIntentId, paymentResult, statusChecks);
  }
}

final StateNotifierProvider<PaymentFlowController, PaymentFlowState>
    paymentFlowControllerProvider =
    StateNotifierProvider<PaymentFlowController, PaymentFlowState>(
  (Ref ref) {
    final demoDataMode = ref.watch(demoDataModeProvider);
    return PaymentFlowController(
      ref,
      demoDataMode,
      initialSavedCards: MockPaymentSeedData.savedCards(demoDataMode),
      initialFriends: MockPaymentSeedData.friends(demoDataMode),
    );
  },
);

final Provider<String> paymentOrderIdProvider = Provider<String>(
  (Ref ref) {
    return ref.watch(
      paymentFlowControllerProvider.select((PaymentFlowState state) {
        return state.orderId;
      }),
    );
  },
);

final Provider<String> paymentCategoryProvider = Provider<String>(
  (Ref ref) {
    return ref.watch(
      paymentFlowControllerProvider.select((PaymentFlowState state) {
        return state.category;
      }),
    );
  },
);

final Provider<List<SavedCard>> paymentSavedCardsProvider =
    Provider<List<SavedCard>>(
  (Ref ref) {
    return ref.watch(
      paymentFlowControllerProvider.select((PaymentFlowState state) {
        return state.savedCards;
      }),
    );
  },
);

final Provider<List<PaymentFriend>> paymentFriendsProvider =
    Provider<List<PaymentFriend>>(
  (Ref ref) {
    return ref.watch(
      paymentFlowControllerProvider.select((PaymentFlowState state) {
        return state.friends;
      }),
    );
  },
);

final Provider<SavedCard?> selectedPaymentCardProvider = Provider<SavedCard?>(
  (Ref ref) {
    return ref.watch(
      paymentFlowControllerProvider.select((PaymentFlowState state) {
        return state.selectedCard;
      }),
    );
  },
);

final Provider<PaymentFriend?> selectedPaymentFriendProvider =
    Provider<PaymentFriend?>(
  (Ref ref) {
    return ref.watch(
      paymentFlowControllerProvider.select((PaymentFlowState state) {
        return state.selectedFriend;
      }),
    );
  },
);

final Provider<bool> paymentSaveCardProvider = Provider<bool>(
  (Ref ref) {
    return ref.watch(
      paymentFlowControllerProvider.select((PaymentFlowState state) {
        return state.saveCard;
      }),
    );
  },
);

final Provider<String> paymentFriendMessageProvider = Provider<String>(
  (Ref ref) {
    return ref.watch(
      paymentFlowControllerProvider.select((PaymentFlowState state) {
        return state.friendMessage;
      }),
    );
  },
);

final Provider<PaymentOrderSummary> paymentOrderSummaryProvider =
    Provider<PaymentOrderSummary>(
  (Ref ref) {
    return ref.watch(
      paymentFlowControllerProvider.select((PaymentFlowState state) {
        return PaymentOrderSummary(
          countryCode: state.countryCode,
          providerName: state.providerName,
          serviceType: state.serviceType,
          smartCardId: state.smartCardId,
          amount: state.amount,
        );
      }),
    );
  },
);

final Provider<PaymentStatusSummary> paymentStatusSummaryProvider =
    Provider<PaymentStatusSummary>(
  (Ref ref) {
    return ref.watch(
      paymentFlowControllerProvider.select((PaymentFlowState state) {
        return PaymentStatusSummary(
          orderId: state.orderId,
          paymentIntentId: state.paymentIntentId,
          paymentResult: state.paymentResult,
          statusChecks: state.statusChecks,
        );
      }),
    );
  },
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

final FutureProvider<List<String>> paymentServiceTypesProvider =
    FutureProvider<List<String>>(
  (Ref ref) async {
    final repository = ref.watch(catalogRepositoryProvider);
    return repository.getServiceTypes();
  },
);

final FutureProvider<List<String>> paymentRecurringFrequenciesProvider =
    FutureProvider<List<String>>(
  (Ref ref) async {
    final repository = ref.watch(catalogRepositoryProvider);
    return repository.getRecurringFrequencies();
  },
);

final FutureProvider<List<String>> paymentProviderCategoriesProvider =
    FutureProvider<List<String>>(
  (Ref ref) async {
    final repository = ref.watch(catalogRepositoryProvider);
    return repository.getProviderCategories();
  },
);

final FutureProvider<PricingBreakdown> paymentPricingBreakdownProvider =
    FutureProvider<PricingBreakdown>(
  (Ref ref) async {
    final orderId = ref.watch(paymentOrderIdProvider);
    if (orderId.isEmpty) {
      return const PricingBreakdown(lines: <PricingLine>[]);
    }
    final repository = ref.watch(orderRepositoryProvider);
    return repository.getPricingBreakdown(orderId);
  },
);

final FutureProvider<OrderPointsSummary> paymentPointsSummaryProvider =
    FutureProvider<OrderPointsSummary>(
  (Ref ref) async {
    final orderId = ref.watch(paymentOrderIdProvider);
    if (orderId.isEmpty) {
      return const OrderPointsSummary(
        pointsEarned: 0,
        totalPoints: 0,
        pointsLabel: '',
      );
    }
    final repository = ref.watch(orderRepositoryProvider);
    return repository.getPointsSummary(orderId);
  },
);
