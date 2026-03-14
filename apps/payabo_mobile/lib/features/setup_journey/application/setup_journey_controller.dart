import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../data/repositories/repository_providers.dart';
import '../domain/setup_enums.dart';
import '../domain/setup_models.dart';
import 'dashboard_seed_builder.dart';
import 'setup_step_configs.dart';

// ── State ───────────────────────────────────────────────────

class SetupJourneyState {
  const SetupJourneyState({
    required this.currentStepIndex,
    required this.profile,
    required this.isReviewing,
  });

  final int currentStepIndex;
  final PayaboSetupProfile profile;
  final bool isReviewing;

  factory SetupJourneyState.initial() {
    return const SetupJourneyState(
      currentStepIndex: 0,
      profile: PayaboSetupProfile(),
      isReviewing: false,
    );
  }

  SetupStepConfig get currentStep => setupSteps[currentStepIndex];

  int get totalSteps => setupSteps.length;

  double get progress => (currentStepIndex + 1) / setupSteps.length;

  bool get isFirstStep => currentStepIndex == 0;

  bool get isLastStep => currentStepIndex >= setupSteps.length - 1;

  SetupJourneyState copyWith({
    int? currentStepIndex,
    PayaboSetupProfile? profile,
    bool? isReviewing,
  }) {
    return SetupJourneyState(
      currentStepIndex: currentStepIndex ?? this.currentStepIndex,
      profile: profile ?? this.profile,
      isReviewing: isReviewing ?? this.isReviewing,
    );
  }
}

// ── Controller ──────────────────────────────────────────────

class SetupJourneyController extends StateNotifier<SetupJourneyState> {
  SetupJourneyController(this._ref) : super(SetupJourneyState.initial());

  final Ref _ref;

  // ── Multi-select toggles ────────────────────────────────

  void toggleUseCase(SetupUseCase useCase) {
    final current = List<SetupUseCase>.from(state.profile.selectedUseCases);
    if (current.contains(useCase)) {
      current.remove(useCase);
    } else {
      current.add(useCase);
    }
    state = state.copyWith(
      profile: state.profile.copyWith(selectedUseCases: current),
    );
  }

  void toggleAccountSource(AccountSourceType source) {
    final current =
        List<AccountSourceType>.from(state.profile.accountSourceTypes);
    if (current.contains(source)) {
      current.remove(source);
    } else {
      current.add(source);
    }
    state = state.copyWith(
      profile: state.profile.copyWith(accountSourceTypes: current),
    );
  }

  void toggleResponsibility(ResponsibilityType responsibility) {
    final current =
        List<ResponsibilityType>.from(state.profile.responsibilities);
    if (current.contains(responsibility)) {
      current.remove(responsibility);
    } else {
      current.add(responsibility);
    }
    state = state.copyWith(
      profile: state.profile.copyWith(responsibilities: current),
    );
  }

  void toggleFinancialGoal(FinancialGoalType goal) {
    final current =
        List<FinancialGoalType>.from(state.profile.financialGoals);
    if (current.contains(goal)) {
      current.remove(goal);
    } else {
      current.add(goal);
    }
    state = state.copyWith(
      profile: state.profile.copyWith(financialGoals: current),
    );
  }

  // ── Single-select setters ───────────────────────────────

  void setConnectChoice(SetupConnectChoice choice) {
    state = state.copyWith(
      profile: state.profile.copyWith(connectChoice: choice),
    );
  }

  void setSupportType(SupportType type) {
    state = state.copyWith(
      profile: state.profile.copyWith(supportType: type),
    );
  }

  // ── Navigation ──────────────────────────────────────────

  void nextStep() {
    if (state.currentStepIndex < setupSteps.length - 1) {
      state = state.copyWith(
        currentStepIndex: state.currentStepIndex + 1,
        isReviewing: false,
      );
    }
  }

  void previousStep() {
    if (state.currentStepIndex > 0) {
      state = state.copyWith(
        currentStepIndex: state.currentStepIndex - 1,
        isReviewing: false,
      );
    }
  }

  void goToStep(int index) {
    if (index >= 0 && index < setupSteps.length) {
      state = state.copyWith(
        currentStepIndex: index,
        isReviewing: false,
      );
    }
  }

  void enterReview() {
    state = state.copyWith(isReviewing: true);
  }

  void exitReview() {
    state = state.copyWith(isReviewing: false);
  }

  // ── Completion ──────────────────────────────────────────

  Future<void> completeSetup() async {
    final completedProfile = state.profile.copyWith(completed: true);

    state = state.copyWith(profile: completedProfile);

    // Persist completion flag locally
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_setupCompletedKey, true);

    // Save to repository (placeholder / mock for now)
    try {
      final repository = _ref.read(setupJourneyRepositoryProvider);
      await repository.saveSetupProfile(completedProfile);
    } catch (_) {
      // Graceful degradation — setup is saved locally even if
      // the backend call fails (relevant for Nigeria connectivity).
    }
  }

  void reset() {
    state = SetupJourneyState.initial();
  }

  static const String _setupCompletedKey = 'payabo.setup.completed';
}

// ── Providers ───────────────────────────────────────────────

final StateNotifierProvider<SetupJourneyController, SetupJourneyState>
    setupJourneyControllerProvider =
    StateNotifierProvider<SetupJourneyController, SetupJourneyState>(
  SetupJourneyController.new,
);

/// Whether the dashboard seed is ready after setup.
final Provider<DashboardSetupSeed> setupDashboardSeedProvider =
    Provider<DashboardSetupSeed>((Ref ref) {
  final profile = ref.watch(setupJourneyControllerProvider).profile;
  return DashboardSeedBuilder.build(profile);
});

/// Whether setup has been completed. Read from SharedPreferences.
///
/// When demo data mode is [DemoDataMode.fresh], this always returns
/// `false` so that the user is guided through setup again — matching
/// the "clean slate" experience the fresh mode represents.
///
/// This provider is async; the router should treat a loading state
/// as "not yet known" and avoid redirecting prematurely.
final FutureProvider<bool> setupCompletedProvider =
    FutureProvider<bool>((Ref ref) async {
  final demoDataMode = ref.watch(demoDataModeProvider);
  if (demoDataMode == DemoDataMode.fresh) {
    return false;
  }

  final prefs = await SharedPreferences.getInstance();
  return prefs.getBool(SetupJourneyController._setupCompletedKey) ?? false;
});

/// Clears the setup-completed flag and resets the controller state
/// so the user can re-enter the setup journey from the profile screen.
Future<void> clearSetupCompleted(Ref ref) async {
  final prefs = await SharedPreferences.getInstance();
  await prefs.remove(SetupJourneyController._setupCompletedKey);

  ref.read(setupJourneyControllerProvider.notifier).reset();
  ref.invalidate(setupCompletedProvider);
}
