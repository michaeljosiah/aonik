import 'support_enums.dart';

/// A person the user supports financially.
///
/// Maps to the backend `PartyRelationship` concept — a person
/// linked to the user with a financial support obligation.
class SupportBeneficiary {
  const SupportBeneficiary({
    required this.id,
    required this.name,
    required this.relationship,
    this.location,
    this.phoneNumber,
  });

  final String id;
  final String name;

  /// Free-text relationship label (e.g. "Mother", "Brother", "Pastor").
  final String relationship;

  /// Optional location/city of the beneficiary.
  final String? location;

  /// Optional phone number for mobile money payments.
  final String? phoneNumber;

  SupportBeneficiary copyWith({
    String? name,
    String? relationship,
    String? location,
    bool clearLocation = false,
    String? phoneNumber,
    bool clearPhoneNumber = false,
  }) {
    return SupportBeneficiary(
      id: id,
      name: name ?? this.name,
      relationship: relationship ?? this.relationship,
      location: clearLocation ? null : location ?? this.location,
      phoneNumber: clearPhoneNumber ? null : phoneNumber ?? this.phoneNumber,
    );
  }
}

/// A recurring (or one-off) financial support commitment.
///
/// This will map to a backend `Bill` or `Order` with a
/// `support_plan` category when the live API is available.
class SupportPlan {
  const SupportPlan({
    required this.id,
    required this.beneficiaryId,
    required this.beneficiaryName,
    required this.category,
    required this.amount,
    required this.currency,
    required this.frequency,
    required this.status,
    this.nextDueDate,
    this.note,
  });

  final String id;
  final String beneficiaryId;
  final String beneficiaryName;
  final SupportCategory category;
  final double amount;
  final String currency;
  final SupportFrequency frequency;
  final SupportPlanStatus status;
  final DateTime? nextDueDate;
  final String? note;

  String get amountLabel => '$currency ${amount.toStringAsFixed(2)}';

  String get frequencyLabel {
    switch (frequency) {
      case SupportFrequency.weekly:
        return 'Weekly';
      case SupportFrequency.biWeekly:
        return 'Every 2 weeks';
      case SupportFrequency.monthly:
        return 'Monthly';
      case SupportFrequency.quarterly:
        return 'Quarterly';
      case SupportFrequency.oneOff:
        return 'One-off';
    }
  }

  SupportPlan copyWith({
    SupportCategory? category,
    double? amount,
    String? currency,
    SupportFrequency? frequency,
    SupportPlanStatus? status,
    DateTime? nextDueDate,
    bool clearNextDueDate = false,
    String? note,
    bool clearNote = false,
  }) {
    return SupportPlan(
      id: id,
      beneficiaryId: beneficiaryId,
      beneficiaryName: beneficiaryName,
      category: category ?? this.category,
      amount: amount ?? this.amount,
      currency: currency ?? this.currency,
      frequency: frequency ?? this.frequency,
      status: status ?? this.status,
      nextDueDate: clearNextDueDate ? null : nextDueDate ?? this.nextDueDate,
      note: clearNote ? null : note ?? this.note,
    );
  }
}

/// Request object for creating a new beneficiary.
class CreateBeneficiaryRequest {
  const CreateBeneficiaryRequest({
    required this.name,
    required this.relationship,
    this.location,
    this.phoneNumber,
  });

  final String name;
  final String relationship;
  final String? location;
  final String? phoneNumber;
}

/// Request object for creating a new support plan.
class CreateSupportPlanRequest {
  const CreateSupportPlanRequest({
    required this.beneficiaryId,
    required this.category,
    required this.amount,
    required this.currency,
    required this.frequency,
    this.note,
  });

  final String beneficiaryId;
  final SupportCategory category;
  final double amount;
  final String currency;
  final SupportFrequency frequency;
  final String? note;
}
