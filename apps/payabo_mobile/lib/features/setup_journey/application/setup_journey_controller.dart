import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../../app/demo/demo_data_mode.dart';
import '../../../app/demo/demo_mode.dart';
import '../../../app/environment/environment_provider.dart';
import '../../../app/errors/api_error_notifier.dart';
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
    final current = List<FinancialGoalType>.from(state.profile.financialGoals);
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
    final completedProfile = _markSetupCompleted();
    await _persistSetupCompletedFlag();
    unawaited(_persistSetupProfile(completedProfile));
  }

  void completeSetupLocally() {
    final completedProfile = _markSetupCompleted();
    unawaited(_persistCompletedProfile(completedProfile));
  }

  PayaboSetupProfile _markSetupCompleted() {
    final completedProfile = state.profile.copyWith(completed: true);

    state = state.copyWith(profile: completedProfile);
    _ref.invalidate(setupCompletedProvider);

    return completedProfile;
  }

  Future<void> _persistCompletedProfile(PayaboSetupProfile profile) async {
    await _persistSetupCompletedFlag();
    await _persistSetupProfile(profile);
  }

  Future<void> _persistSetupCompletedFlag() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_setupCompletedKey, true);
  }

  Future<void> _persistSetupProfile(PayaboSetupProfile profile) async {
    try {
      final repository = _ref.read(setupJourneyRepositoryProvider);
      await repository.saveSetupProfile(profile);
    } catch (error) {
      // Preserve local completion state if the backend call fails, but surface
      // the error so it is visible in the UI.
      if (mounted) {
        _ref.read(apiErrorNotifierProvider.notifier).report(error);
      }
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
/// When the current demo session uses [DemoDataMode.fresh], this always
/// returns `false` so the user is guided through setup again.
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
  if (_useLocalSetupPersistence(ref)) {
    return prefs.getBool(SetupJourneyController._setupCompletedKey) ?? false;
  }

  try {
    final repository = ref.watch(setupJourneyRepositoryProvider);
    final profile = await repository.loadSetupProfile();
    final completed = profile?.completed ?? false;

    if (completed) {
      await prefs.setBool(SetupJourneyController._setupCompletedKey, true);
    } else {
      await prefs.remove(SetupJourneyController._setupCompletedKey);
    }

    return completed;
  } catch (_) {
    return prefs.getBool(SetupJourneyController._setupCompletedKey) ?? false;
  }
});

bool _useLocalSetupPersistence(Ref ref) {
  return ref.watch(appEnvironmentProvider).useMocks ||
      ref.watch(isDemoProvider);
}

/// Clears the setup-completed flag and resets the controller state
/// so the user can re-enter the setup journey from the profile screen.
Future<void> clearSetupCompleted(WidgetRef ref) async {
  final prefs = await SharedPreferences.getInstance();
  try {
    final repository = ref.read(setupJourneyRepositoryProvider);
    await repository.clearSetupProfile();
  } catch (_) {
    // Best-effort clear. Local state is still reset so the user can
    // restart setup immediately on this device.
  }

  await prefs.remove(SetupJourneyController._setupCompletedKey);

  ref.read(setupJourneyControllerProvider.notifier).reset();
  ref.invalidate(setupCompletedProvider);
}
