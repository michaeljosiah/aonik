import 'support_models.dart';

/// Repository contract for support planning operations.
///
/// Manages beneficiaries (people the user supports) and their
/// associated support plans (recurring financial commitments).
abstract class SupportPlanningRepository {
  /// Lists all beneficiaries for the current user.
  Future<List<SupportBeneficiary>> getBeneficiaries();

  /// Creates a new beneficiary and returns it with a generated ID.
  Future<SupportBeneficiary> createBeneficiary(
      CreateBeneficiaryRequest request);

  /// Lists all support plans, optionally filtered by beneficiary.
  Future<List<SupportPlan>> getSupportPlans({String? beneficiaryId});

  /// Creates a new support plan and returns it with a generated ID.
  Future<SupportPlan> createSupportPlan(CreateSupportPlanRequest request);

  /// Returns upcoming support obligations within the next [days] days.
  Future<List<SupportPlan>> getUpcomingObligations({int days = 30});
}
