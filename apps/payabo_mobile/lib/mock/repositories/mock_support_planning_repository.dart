import '../../app/demo/demo_data_mode.dart';
import '../../features/support_planning/domain/support_enums.dart';
import '../../features/support_planning/domain/support_models.dart';
import '../../features/support_planning/domain/support_planning_repository.dart';
import '../mock_behavior.dart';

class MockSupportPlanningRepository implements SupportPlanningRepository {
  MockSupportPlanningRepository({
    this.demoDataMode = DemoDataMode.populated,
  });

  final DemoDataMode demoDataMode;

  // In-memory store so created items persist within a session.
  final List<SupportBeneficiary> _beneficiaries = [];
  final List<SupportPlan> _plans = [];
  bool _seeded = false;

  void _seedIfNeeded() {
    if (_seeded || demoDataMode == DemoDataMode.fresh) return;
    _seeded = true;

    _beneficiaries.addAll(const <SupportBeneficiary>[
      SupportBeneficiary(
        id: 'ben_001',
        name: 'Mama Grace',
        relationship: 'Mother',
        location: 'Lagos',
        phoneNumber: '+234 801 234 5678',
      ),
      SupportBeneficiary(
        id: 'ben_002',
        name: 'Uncle Kofi',
        relationship: 'Uncle',
        location: 'Kumasi',
        phoneNumber: '+233 24 567 8901',
      ),
    ]);

    final now = DateTime.now();
    _plans.addAll(<SupportPlan>[
      SupportPlan(
        id: 'sp_001',
        beneficiaryId: 'ben_001',
        beneficiaryName: 'Mama Grace',
        category: SupportCategory.livingExpenses,
        amount: 200.00,
        currency: 'GHS',
        frequency: SupportFrequency.monthly,
        status: SupportPlanStatus.active,
        nextDueDate: DateTime(now.year, now.month + 1, 1),
        note: 'Monthly upkeep for Mama',
      ),
      SupportPlan(
        id: 'sp_002',
        beneficiaryId: 'ben_002',
        beneficiaryName: 'Uncle Kofi',
        category: SupportCategory.medical,
        amount: 350.00,
        currency: 'GHS',
        frequency: SupportFrequency.quarterly,
        status: SupportPlanStatus.active,
        nextDueDate: DateTime(now.year, now.month, 28),
        note: 'Quarterly medical check-up contribution',
      ),
    ]);
  }

  @override
  Future<List<SupportBeneficiary>> getBeneficiaries() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('supportPlanning.getBeneficiaries');
    _seedIfNeeded();
    return List<SupportBeneficiary>.unmodifiable(_beneficiaries);
  }

  @override
  Future<SupportBeneficiary> createBeneficiary(
      CreateBeneficiaryRequest request) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('supportPlanning.createBeneficiary');
    _seedIfNeeded();

    final beneficiary = SupportBeneficiary(
      id: 'ben_${DateTime.now().millisecondsSinceEpoch}',
      name: request.name,
      relationship: request.relationship,
      location: request.location,
      phoneNumber: request.phoneNumber,
    );
    _beneficiaries.add(beneficiary);
    return beneficiary;
  }

  @override
  Future<List<SupportPlan>> getSupportPlans({String? beneficiaryId}) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('supportPlanning.getSupportPlans');
    _seedIfNeeded();

    if (beneficiaryId != null) {
      return _plans
          .where((SupportPlan p) => p.beneficiaryId == beneficiaryId)
          .toList();
    }
    return List<SupportPlan>.unmodifiable(_plans);
  }

  @override
  Future<SupportPlan> createSupportPlan(
      CreateSupportPlanRequest request) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('supportPlanning.createSupportPlan');
    _seedIfNeeded();

    final beneficiary = _beneficiaries.firstWhere(
      (SupportBeneficiary b) => b.id == request.beneficiaryId,
      orElse: () => throw StateError(
          'Beneficiary ${request.beneficiaryId} not found'),
    );

    final now = DateTime.now();
    final plan = SupportPlan(
      id: 'sp_${now.millisecondsSinceEpoch}',
      beneficiaryId: request.beneficiaryId,
      beneficiaryName: beneficiary.name,
      category: request.category,
      amount: request.amount,
      currency: request.currency,
      frequency: request.frequency,
      status: SupportPlanStatus.active,
      nextDueDate: _calculateNextDueDate(now, request.frequency),
      note: request.note,
    );
    _plans.add(plan);
    return plan;
  }

  @override
  Future<List<SupportPlan>> getUpcomingObligations({int days = 30}) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('supportPlanning.getUpcomingObligations');
    _seedIfNeeded();

    final now = DateTime.now();
    final cutoff = now.add(Duration(days: days));
    return _plans
        .where((SupportPlan p) =>
            p.status == SupportPlanStatus.active &&
            p.nextDueDate != null &&
            p.nextDueDate!.isBefore(cutoff))
        .toList()
      ..sort((SupportPlan a, SupportPlan b) =>
          (a.nextDueDate ?? now).compareTo(b.nextDueDate ?? now));
  }

  static DateTime _calculateNextDueDate(
      DateTime from, SupportFrequency frequency) {
    switch (frequency) {
      case SupportFrequency.weekly:
        return from.add(const Duration(days: 7));
      case SupportFrequency.biWeekly:
        return from.add(const Duration(days: 14));
      case SupportFrequency.monthly:
        return DateTime(from.year, from.month + 1, from.day);
      case SupportFrequency.quarterly:
        return DateTime(from.year, from.month + 3, from.day);
      case SupportFrequency.oneOff:
        return from.add(const Duration(days: 7));
    }
  }
}
