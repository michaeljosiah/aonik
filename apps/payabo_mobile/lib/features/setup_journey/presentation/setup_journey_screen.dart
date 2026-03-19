import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/environment/environment_provider.dart';
import '../../../shared/theme/payabo_color_resolver.dart';
import '../../../shared/theme/payabo_spacing.dart';
import '../../../shared/theme/payabo_theme.dart';
import '../../spending/presentation/account_link_connect_sheet.dart';
import '../application/setup_journey_controller.dart';
import '../domain/setup_enums.dart';
import 'widgets/setup_action_card.dart';
import 'widgets/setup_ai_message_panel.dart';
import 'widgets/setup_hero_background.dart';
import 'widgets/setup_progress_indicator.dart';

/// Main screen for the Payabo post-registration AI-guided setup journey.
///
/// This is a single-route screen (`/setup`) that manages all 8 steps
/// internally through [SetupJourneyController]. It does NOT replace
/// or modify the existing auth registration flow.
///
/// ## Where the flow starts
/// After successful registration in [LoginDetailsScreen], the router
/// redirect logic detects that setup has not been completed and sends
/// the user to `/setup` instead of `/dashboard`.
///
/// ## Account linking (Step 4)
/// When the user selects a bank connection option, the existing
/// [showAccountLinkConnectSheet] from the Spending section is
/// presented. It shows country selection and then launches Plaid
/// Link. Nigerian bank connectivity is a future integration and
/// still shows a placeholder notice.
///
/// ## How dashboard personalisation consumes the setup result
/// On completion, [SetupJourneyController.completeSetup] persists
/// the [PayaboSetupProfile]. The [setupDashboardSeedProvider] builds
/// a [DashboardSetupSeed] that the dashboard can read to personalise
/// greetings, modules, quick actions, and nudges.
class SetupJourneyScreen extends ConsumerWidget {
  const SetupJourneyScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(setupJourneyControllerProvider);
    final controller = ref.read(setupJourneyControllerProvider.notifier);

    // Force dark theme on the entire setup flow subtree so that
    // context.colors (which reads Theme.of(context).brightness)
    // always resolves to dark-mode tokens — regardless of system
    // brightness. This keeps the cinematic setup look in both modes.
    return Theme(
      data: buildPayaboDarkTheme(),
      child: Builder(
        builder: (BuildContext context) {
    final c = context.colors;

    return PopScope(
      canPop: state.isFirstStep,
      onPopInvokedWithResult: (bool didPop, _) {
        if (!didPop) {
          controller.previousStep();
        }
      },
      child: Scaffold(
        body: Stack(
          children: <Widget>[
            // Full-screen background
            const Positioned.fill(
              child: SetupHeroBackground(),
            ),

            // Top content — logo + AI message, sits above the card.
            // Uses Positioned to fill the top area, leaving room for
            // the bottom-pinned action card.
            Positioned.fill(
              child: SafeArea(
                bottom: false,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    _buildTopBar(context, c),

                    Expanded(
                      child: Padding(
                        padding: const EdgeInsets.only(
                          top: PayaboSpacing.lg,
                          bottom: PayaboSpacing.sm,
                        ),
                        child: Align(
                          alignment: Alignment.topLeft,
                          child: SingleChildScrollView(
                            child: SetupAiMessagePanel(
                              message: state.currentStep.message,
                              helperText: state.currentStep.helperText,
                              stepKey: state.currentStep.id,
                            ),
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),

            // Bottom-pinned: progress indicator + action card.
            Positioned(
              left: 0,
              right: 0,
              bottom: 0,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  SetupProgressIndicator(
                    currentStep: state.currentStepIndex,
                    totalSteps: state.totalSteps,
                  ),
                  SetupActionCard(
                    stepConfig: state.currentStep,
                    selectedIds: _getSelectedIds(state),
                    onOptionTap: (String optionId) =>
                        _handleOptionTap(controller, state, optionId),
                    onNext: () =>
                        _handleNext(context, ref, controller, state),
                    onBack: state.isFirstStep
                        ? null
                        : controller.previousStep,
                    isFirstStep: state.isFirstStep,
                    nextLabel: _getNextLabel(state),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
        },
      ),
    );
  }

  Widget _buildTopBar(BuildContext context, PayaboColorResolver c) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        PayaboSpacing.xl,
        PayaboSpacing.md,
        PayaboSpacing.xl,
        0,
      ),
      child: Row(
        children: <Widget>[
          // Small brand mark
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: c.surfaceBase.withValues(alpha: 0.8),
              borderRadius: BorderRadius.circular(10),
              border: Border.all(
                color: c.borderWarm.withValues(alpha: 0.5),
              ),
            ),
            child: Icon(
              Icons.auto_awesome_rounded,
              size: 18,
              color: c.primary,
            ),
          ),
          const SizedBox(width: PayaboSpacing.sm),
          Text(
            'Payabo',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  color: c.headerTitle,
                  fontWeight: FontWeight.w700,
                ),
          ),
        ],
      ),
    );
  }

  // ── Selection state mapping ─────────────────────────────

  Set<String> _getSelectedIds(SetupJourneyState state) {
    final step = state.currentStep;

    switch (step.id) {
      case 'use_cases':
        return state.profile.selectedUseCases
            .map((SetupUseCase e) => e.name)
            .toSet();
      case 'account_sources':
        return state.profile.accountSourceTypes
            .map((AccountSourceType e) => e.name)
            .toSet();
      case 'connect_account':
        final choice = state.profile.connectChoice;
        return choice != null ? <String>{choice.name} : <String>{};
      case 'responsibilities':
        return state.profile.responsibilities
            .map((ResponsibilityType e) => e.name)
            .toSet();
      case 'family_support':
        final support = state.profile.supportType;
        return support != null ? <String>{support.name} : <String>{};
      case 'financial_goals':
        return state.profile.financialGoals
            .map((FinancialGoalType e) => e.name)
            .toSet();
      default:
        return <String>{};
    }
  }

  // ── Option tap handling ─────────────────────────────────

  void _handleOptionTap(
    SetupJourneyController controller,
    SetupJourneyState state,
    String optionId,
  ) {
    final step = state.currentStep;

    switch (step.id) {
      case 'welcome':
        // Single action — handled by onNext
        break;

      case 'use_cases':
        final useCase = SetupUseCase.values.firstWhere(
          (SetupUseCase e) => e.name == optionId,
        );
        controller.toggleUseCase(useCase);

      case 'account_sources':
        final source = AccountSourceType.values.firstWhere(
          (AccountSourceType e) => e.name == optionId,
        );
        controller.toggleAccountSource(source);

      case 'connect_account':
        final choice = SetupConnectChoice.values.firstWhere(
          (SetupConnectChoice e) => e.name == optionId,
        );
        controller.setConnectChoice(choice);

      case 'responsibilities':
        final responsibility = ResponsibilityType.values.firstWhere(
          (ResponsibilityType e) => e.name == optionId,
        );
        controller.toggleResponsibility(responsibility);

      case 'family_support':
        final support = SupportType.values.firstWhere(
          (SupportType e) => e.name == optionId,
        );
        controller.setSupportType(support);

      case 'financial_goals':
        final goal = FinancialGoalType.values.firstWhere(
          (FinancialGoalType e) => e.name == optionId,
        );
        controller.toggleFinancialGoal(goal);
    }
  }

  // ── Next step handling ──────────────────────────────────

  void _handleNext(
    BuildContext context,
    WidgetRef ref,
    SetupJourneyController controller,
    SetupJourneyState state,
  ) {
    final step = state.currentStep;

    // Handle account connection hooks (Step 4).
    // When a bank connection option is selected, open the connect sheet
    // and advance to the next step once the modal is dismissed (whether
    // the user completed the flow or cancelled).
    if (step.id == 'connect_account') {
      final choice = state.profile.connectChoice;
      if (choice == SetupConnectChoice.connectUkBank) {
        _onConnectBank(context, ref).then((_) => controller.nextStep());
        return;
      } else if (choice == SetupConnectChoice.connectNigerianBank) {
        _onConnectNigerianBank(context);
      }
    }

    // Handle summary step — complete setup and go to dashboard
    if (step.type == SetupStepType.summary) {
      _onCompleteSetup(context, controller, state);
      return;
    }

    controller.nextStep();
  }

  // ── Completion ──────────────────────────────────────────

  void _onCompleteSetup(
    BuildContext context,
    SetupJourneyController controller,
    SetupJourneyState _,
  ) {
    controller.completeSetupLocally();
    context.go('/setup/processing');
  }

  // ── Account connection hooks ────────────────────────────

  /// Opens the existing Plaid account-link connect sheet which shows
  /// country selection followed by the secure Plaid Link flow.
  /// Reuses the same [showAccountLinkConnectSheet] used in Spending.
  Future<void> _onConnectBank(BuildContext context, WidgetRef ref) async {
    final environment = ref.read(appEnvironmentProvider);

    await showAccountLinkConnectSheet(
      context,
      ref,
      provider: environment.resolvedAccountLinkProvider,
      mode: 'connect',
      title: 'Connect bank account',
    );
  }

  /// Placeholder — wire to future Nigeria bank aggregation provider.
  /// Nigeria connectivity may not always be reliable; this hook must
  /// handle failure gracefully.
  void _onConnectNigerianBank(BuildContext context) {
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text(
          'Nigerian bank connection will be available soon. '
          'You can connect your account from Settings later.',
        ),
      ),
    );
  }

  // ── Label helpers ───────────────────────────────────────

  String? _getNextLabel(SetupJourneyState state) {
    if (state.currentStep.type == SetupStepType.singleAction) {
      return null; // Button label comes from the single option
    }
    if (state.currentStep.type == SetupStepType.summary) {
      return 'Let\'s go';
    }
    return 'Continue';
  }
}
