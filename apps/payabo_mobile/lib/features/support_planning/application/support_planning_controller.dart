import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';

import '../../../data/repositories/repository_providers.dart';
import '../domain/support_enums.dart';
import '../domain/support_models.dart';
import '../domain/support_planning_repository.dart';

// ── State ───────────────────────────────────────────────────

class SupportPlanningState {
  const SupportPlanningState({
    this.beneficiaries = const <SupportBeneficiary>[],
    this.plans = const <SupportPlan>[],
    this.upcomingObligations = const <SupportPlan>[],
    this.isLoading = false,
    this.error,
  });

  final List<SupportBeneficiary> beneficiaries;
  final List<SupportPlan> plans;
  final List<SupportPlan> upcomingObligations;
  final bool isLoading;
  final String? error;

  SupportPlanningState copyWith({
    List<SupportBeneficiary>? beneficiaries,
    List<SupportPlan>? plans,
    List<SupportPlan>? upcomingObligations,
    bool? isLoading,
    String? error,
    bool clearError = false,
  }) {
    return SupportPlanningState(
      beneficiaries: beneficiaries ?? this.beneficiaries,
      plans: plans ?? this.plans,
      upcomingObligations: upcomingObligations ?? this.upcomingObligations,
      isLoading: isLoading ?? this.isLoading,
      error: clearError ? null : error ?? this.error,
    );
  }
}

// ── Controller ──────────────────────────────────────────────

class SupportPlanningController extends StateNotifier<SupportPlanningState> {
  SupportPlanningController(this._repository)
      : super(const SupportPlanningState());

  final SupportPlanningRepository _repository;

  Future<void> loadAll() async {
    state = state.copyWith(isLoading: true, clearError: true);
    try {
      final results = await Future.wait(<Future<Object?>>[
        _repository.getBeneficiaries(),
        _repository.getSupportPlans(),
        _repository.getUpcomingObligations(),
      ]);
      state = state.copyWith(
        beneficiaries: results[0] as List<SupportBeneficiary>,
        plans: results[1] as List<SupportPlan>,
        upcomingObligations: results[2] as List<SupportPlan>,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(isLoading: false, error: e.toString());
    }
  }

  Future<SupportBeneficiary?> addBeneficiary({
    required String name,
    required String relationship,
    String? location,
    String? phoneNumber,
  }) async {
    state = state.copyWith(isLoading: true, clearError: true);
    try {
      final beneficiary = await _repository.createBeneficiary(
        CreateBeneficiaryRequest(
          name: name,
          relationship: relationship,
          location: location,
          phoneNumber: phoneNumber,
        ),
      );
      state = state.copyWith(
        beneficiaries: [...state.beneficiaries, beneficiary],
        isLoading: false,
      );
      return beneficiary;
    } catch (e) {
      state = state.copyWith(isLoading: false, error: e.toString());
      return null;
    }
  }

  Future<SupportPlan?> addSupportPlan({
    required String beneficiaryId,
    required SupportCategory category,
    required double amount,
    required String currency,
    required SupportFrequency frequency,
    String? note,
  }) async {
    state = state.copyWith(isLoading: true, clearError: true);
    try {
      final plan = await _repository.createSupportPlan(
        CreateSupportPlanRequest(
          beneficiaryId: beneficiaryId,
          category: category,
          amount: amount,
          currency: currency,
          frequency: frequency,
          note: note,
        ),
      );
      state = state.copyWith(
        plans: [...state.plans, plan],
        upcomingObligations: [...state.upcomingObligations, plan],
        isLoading: false,
      );
      return plan;
    } catch (e) {
      state = state.copyWith(isLoading: false, error: e.toString());
      return null;
    }
  }
}

// ── Providers ───────────────────────────────────────────────

final StateNotifierProvider<SupportPlanningController, SupportPlanningState>
    supportPlanningControllerProvider = StateNotifierProvider<
        SupportPlanningController, SupportPlanningState>(
  (Ref ref) {
    final repository = ref.watch(supportPlanningRepositoryProvider);
    return SupportPlanningController(repository);
  },
);
