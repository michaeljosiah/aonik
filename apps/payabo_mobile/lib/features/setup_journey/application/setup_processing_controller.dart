import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/legacy.dart';

import '../domain/setup_enums.dart';
import '../domain/setup_models.dart';
import 'setup_journey_controller.dart';

// ── Processing steps ────────────────────────────────────────

/// The ordered steps Simi walks through after setup completes.
///
/// Steps with a non-null [SetupProcessingStep.showWhen] are only
/// included when the predicate returns true for the user's profile.
final List<SetupProcessingStep> _allProcessingSteps = <SetupProcessingStep>[
  const SetupProcessingStep(
    id: 'intro',
    message: 'Hi, I\u2019m Simi. Let me prepare your financial assistant.',
  ),
  const SetupProcessingStep(
    id: 'priorities',
    message: 'Understanding your financial priorities\u2026',
  ),
  const SetupProcessingStep(
    id: 'accounts',
    message: 'Mapping your accounts and money sources\u2026',
  ),
  const SetupProcessingStep(
    id: 'bills',
    message: 'Organising your bills and responsibilities\u2026',
  ),
  SetupProcessingStep(
    id: 'support',
    message: 'Learning who you support financially\u2026',
    showWhen: (PayaboSetupProfile p) =>
        p.supportType != null && p.supportType != SupportType.noOne,
  ),
  const SetupProcessingStep(
    id: 'dashboard',
    message: 'Preparing your financial dashboard\u2026',
  ),
  const SetupProcessingStep(
    id: 'ready',
    message: 'Your financial assistant is ready.',
  ),
];

/// Returns only the steps applicable to the given [profile].
List<SetupProcessingStep> buildProcessingSteps(PayaboSetupProfile profile) {
  return _allProcessingSteps
      .where((SetupProcessingStep s) => s.showWhen == null || s.showWhen!(profile))
      .toList(growable: false);
}

// ── State ───────────────────────────────────────────────────

class SetupProcessingState {
  const SetupProcessingState({
    required this.steps,
    required this.currentStepIndex,
    required this.isComplete,
  });

  final List<SetupProcessingStep> steps;
  final int currentStepIndex;
  final bool isComplete;

  factory SetupProcessingState.initial(List<SetupProcessingStep> steps) {
    return SetupProcessingState(
      steps: steps,
      currentStepIndex: 0,
      isComplete: false,
    );
  }

  SetupProcessingStep get currentStep => steps[currentStepIndex];

  int get totalSteps => steps.length;

  double get progress => totalSteps > 0 ? (currentStepIndex + 1) / totalSteps : 0;

  bool get isLastStep => currentStepIndex >= steps.length - 1;

  SetupProcessingState copyWith({
    int? currentStepIndex,
    bool? isComplete,
  }) {
    return SetupProcessingState(
      steps: steps,
      currentStepIndex: currentStepIndex ?? this.currentStepIndex,
      isComplete: isComplete ?? this.isComplete,
    );
  }
}

// ── Controller ──────────────────────────────────────────────

class SetupProcessingController extends StateNotifier<SetupProcessingState> {
  SetupProcessingController(List<SetupProcessingStep> steps)
      : super(SetupProcessingState.initial(steps));

  Timer? _timer;

  /// Duration per step in milliseconds.
  static const int _msPerStep = 1200;

  /// Extra hold at the final step before marking complete.
  static const int _finalHoldMs = 800;

  /// Starts the timed processing sequence.
  void startProcessing() {
    if (state.isComplete) return;

    _timer?.cancel();
    _timer = Timer.periodic(
      const Duration(milliseconds: _msPerStep),
      (_) => _advanceStep(),
    );
  }

  void _advanceStep() {
    if (!mounted) {
      _timer?.cancel();
      return;
    }

    if (state.isLastStep) {
      _timer?.cancel();
      // Hold briefly on the final message before signalling completion.
      Future<void>.delayed(
        const Duration(milliseconds: _finalHoldMs),
        () {
          if (mounted) {
            state = state.copyWith(isComplete: true);
          }
        },
      );
      return;
    }

    state = state.copyWith(
      currentStepIndex: state.currentStepIndex + 1,
    );
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }
}

// ── Providers ───────────────────────────────────────────────

/// Builds the processing step list from the completed setup profile.
final Provider<List<SetupProcessingStep>> setupProcessingStepsProvider =
    Provider<List<SetupProcessingStep>>((Ref ref) {
  final profile = ref.watch(setupJourneyControllerProvider).profile;
  return buildProcessingSteps(profile);
});

/// The processing controller for the post-setup AI animation.
final StateNotifierProvider<SetupProcessingController, SetupProcessingState>
    setupProcessingControllerProvider =
    StateNotifierProvider<SetupProcessingController, SetupProcessingState>(
  (Ref ref) {
    final steps = ref.watch(setupProcessingStepsProvider);
    return SetupProcessingController(steps);
  },
);
