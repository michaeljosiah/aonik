import 'package:flutter/material.dart';

import 'setup_enums.dart';

// ── Step configuration ──────────────────────────────────────

/// A single selectable option within a setup step.
class SetupOption {
  const SetupOption({
    required this.id,
    required this.label,
    this.icon,
  });

  final String id;
  final String label;
  final IconData? icon;
}

/// Configuration for one step of the setup journey.
class SetupStepConfig {
  const SetupStepConfig({
    required this.id,
    required this.message,
    required this.type,
    required this.options,
    this.helperText,
    this.canSkip = false,
  });

  final String id;
  final String message;
  final String? helperText;
  final SetupStepType type;
  final List<SetupOption> options;
  final bool canSkip;
}

// ── Setup profile (collected data) ──────────────────────────

/// The complete setup profile collected during the journey.
///
/// This model captures user intent and context for downstream
/// dashboard personalisation and future AI recommendations.
/// It does NOT execute any financially material action.
class PayaboSetupProfile {
  const PayaboSetupProfile({
    this.selectedUseCases = const <SetupUseCase>[],
    this.connectChoice,
    this.supportTypes = const <SupportType>[],
    this.financialGoals = const <FinancialGoalType>[],
    this.completed = false,
  });

  final List<SetupUseCase> selectedUseCases;
  final SetupConnectChoice? connectChoice;
  final List<SupportType> supportTypes;
  final List<FinancialGoalType> financialGoals;
  final bool completed;

  factory PayaboSetupProfile.empty() {
    return const PayaboSetupProfile();
  }

  bool get accountConnectionSkipped =>
      connectChoice == null || connectChoice == SetupConnectChoice.skipForNow;

  PayaboSetupProfile copyWith({
    List<SetupUseCase>? selectedUseCases,
    SetupConnectChoice? connectChoice,
    bool clearConnectChoice = false,
    List<SupportType>? supportTypes,
    List<FinancialGoalType>? financialGoals,
    bool? completed,
  }) {
    return PayaboSetupProfile(
      selectedUseCases: selectedUseCases ?? this.selectedUseCases,
      connectChoice:
          clearConnectChoice ? null : connectChoice ?? this.connectChoice,
      supportTypes: supportTypes ?? this.supportTypes,
      financialGoals: financialGoals ?? this.financialGoals,
      completed: completed ?? this.completed,
    );
  }
}

// ── Processing sequence ─────────────────────────────────────

/// A single step in the post-setup AI processing animation.
///
/// Each step represents Simi analysing a different aspect of
/// the onboarding data. Steps that are conditional on specific
/// setup signals use [showWhen] to determine visibility.
class SetupProcessingStep {
  const SetupProcessingStep({
    required this.id,
    required this.message,
    this.showWhen,
  });

  /// Unique identifier for the step.
  final String id;

  /// The message Simi displays during this step.
  final String message;

  /// Optional predicate that determines whether this step appears
  /// in the sequence. When `null`, the step always appears.
  final bool Function(PayaboSetupProfile)? showWhen;
}

// ── Dashboard handoff ───────────────────────────────────────

/// Structured seed data for personalising the dashboard immediately
/// after setup completion.
///
/// This is placeholder logic today. It will be replaced by AI-driven
/// personalisation from the AONIK Agent framework (AiRun / Proposal
/// pattern) when the recommendation pipeline is available.
class DashboardSetupSeed {
  const DashboardSetupSeed({
    required this.greetingVariant,
    required this.suggestedModules,
    required this.quickActions,
    required this.nudges,
  });

  final String greetingVariant;
  final List<String> suggestedModules;
  final List<String> quickActions;
  final List<String> nudges;

  factory DashboardSetupSeed.empty() {
    return const DashboardSetupSeed(
      greetingVariant: 'default',
      suggestedModules: <String>[],
      quickActions: <String>[],
      nudges: <String>[],
    );
  }
}
