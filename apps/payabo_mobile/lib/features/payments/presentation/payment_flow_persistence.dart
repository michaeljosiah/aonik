import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

class PersistedPaymentFlowSnapshot {
  const PersistedPaymentFlowSnapshot({
    required this.demoDataModeName,
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
    required this.paymentMethodIndex,
    required this.selectedCardId,
    required this.saveCard,
    required this.selectedFriendId,
    required this.friendMessage,
    required this.orderId,
    required this.paymentIntentId,
    required this.providerReference,
    required this.paymentResultIndex,
    required this.statusChecks,
  });

  final String demoDataModeName;
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
  final int paymentMethodIndex;
  final String selectedCardId;
  final bool saveCard;
  final String selectedFriendId;
  final String friendMessage;
  final String orderId;
  final String paymentIntentId;
  final String providerReference;
  final int? paymentResultIndex;
  final int statusChecks;
}

abstract class PaymentFlowPersistence {
  Future<PersistedPaymentFlowSnapshot?> read();

  Future<void> write(PersistedPaymentFlowSnapshot snapshot);

  Future<void> clear();
}

class SharedPreferencesPaymentFlowPersistence
    implements PaymentFlowPersistence {
  static const String _storageKey = 'payabo.payment_flow_state.v1';

  @override
  Future<PersistedPaymentFlowSnapshot?> read() async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    final String? raw = prefs.getString(_storageKey);
    if (raw == null || raw.isEmpty) {
      return null;
    }

    final Object? decoded = jsonDecode(raw);
    if (decoded is! Map<String, dynamic>) {
      return null;
    }

    return PersistedPaymentFlowSnapshot(
      demoDataModeName: decoded['demoDataMode'] as String? ?? '',
      countryCode: decoded['countryCode'] as String? ?? 'GH',
      providerId: decoded['providerId'] as String? ?? '',
      providerName: decoded['providerName'] as String? ?? '',
      category: decoded['category'] as String? ?? 'All',
      serviceType: decoded['serviceType'] as String? ?? 'Montage Cable TV',
      smartCardId: decoded['smartCardId'] as String? ?? '',
      contactReference: decoded['contactReference'] as String? ?? '',
      amount: decoded['amount'] as String? ?? '',
      recurringBill: decoded['recurringBill'] as bool? ?? false,
      recurringFrequency: decoded['recurringFrequency'] as String? ?? 'Monthly',
      recurringStartsOn: decoded['recurringStartsOn'] == null
          ? null
          : DateTime.tryParse(decoded['recurringStartsOn'] as String),
      recurringEndsOn: decoded['recurringEndsOn'] == null
          ? null
          : DateTime.tryParse(decoded['recurringEndsOn'] as String),
      useSamePaymentMethodForRecurring:
          decoded['useSamePaymentMethodForRecurring'] as bool? ?? true,
      paymentMethodIndex: decoded['paymentMethod'] as int? ?? 0,
      selectedCardId: decoded['selectedCardId'] as String? ?? 'card_visa_4567',
      saveCard: decoded['saveCard'] as bool? ?? true,
      selectedFriendId: decoded['selectedFriendId'] as String? ?? '',
      friendMessage: decoded['friendMessage'] as String? ?? '',
      orderId: decoded['orderId'] as String? ?? '',
      paymentIntentId: decoded['paymentIntentId'] as String? ?? '',
      providerReference: decoded['providerReference'] as String? ?? '',
      paymentResultIndex: decoded['paymentResult'] as int?,
      statusChecks: decoded['statusChecks'] as int? ?? 0,
    );
  }

  @override
  Future<void> write(PersistedPaymentFlowSnapshot snapshot) async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    final Map<String, dynamic> payload = <String, dynamic>{
      'demoDataMode': snapshot.demoDataModeName,
      'countryCode': snapshot.countryCode,
      'providerId': snapshot.providerId,
      'providerName': snapshot.providerName,
      'category': snapshot.category,
      'serviceType': snapshot.serviceType,
      'smartCardId': snapshot.smartCardId,
      'contactReference': snapshot.contactReference,
      'amount': snapshot.amount,
      'recurringBill': snapshot.recurringBill,
      'recurringFrequency': snapshot.recurringFrequency,
      'recurringStartsOn': snapshot.recurringStartsOn?.toIso8601String(),
      'recurringEndsOn': snapshot.recurringEndsOn?.toIso8601String(),
      'useSamePaymentMethodForRecurring':
          snapshot.useSamePaymentMethodForRecurring,
      'paymentMethod': snapshot.paymentMethodIndex,
      'selectedCardId': snapshot.selectedCardId,
      'saveCard': snapshot.saveCard,
      'selectedFriendId': snapshot.selectedFriendId,
      'friendMessage': snapshot.friendMessage,
      'orderId': snapshot.orderId,
      'paymentIntentId': snapshot.paymentIntentId,
      'providerReference': snapshot.providerReference,
      'paymentResult': snapshot.paymentResultIndex,
      'statusChecks': snapshot.statusChecks,
    };

    await prefs.setString(_storageKey, jsonEncode(payload));
  }

  @override
  Future<void> clear() async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.remove(_storageKey);
  }
}

final Provider<PaymentFlowPersistence> paymentFlowPersistenceProvider =
    Provider<PaymentFlowPersistence>(
  (Ref ref) => SharedPreferencesPaymentFlowPersistence(),
);
