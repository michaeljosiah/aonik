import '../domain/setup_enums.dart';
import '../domain/setup_models.dart';

/// Builds a [DashboardSetupSeed] from the completed setup profile.
///
/// This is placeholder logic. It will be replaced by AI-driven
/// personalisation from the AONIK Agent framework (AiRun / Proposal
/// pattern) when the recommendation pipeline is available.
///
/// The seed determines:
/// - greeting variant (how the dashboard addresses the user)
/// - suggested modules (which dashboard sections to surface first)
/// - quick actions (one-tap shortcuts on the dashboard)
/// - nudges (gentle prompts for next steps)
abstract final class DashboardSeedBuilder {
  static DashboardSetupSeed build(PayaboSetupProfile profile) {
    final modules = <String>[];
    final quickActions = <String>[];
    final nudges = <String>[];

    // ── Greeting variant ──────────────────────────────────
    String greeting = 'default';

    if (profile.selectedUseCases.contains(SetupUseCase.sendMoneyHome) ||
        profile.supportType == SupportType.parents ||
        profile.supportType == SupportType.siblings) {
      greeting = 'diaspora_family';
    } else if (profile.selectedUseCases.contains(SetupUseCase.saveForGoals)) {
      greeting = 'goal_focused';
    } else if (profile.selectedUseCases.contains(SetupUseCase.manageBills)) {
      greeting = 'bills_focused';
    }

    // ── Suggested modules ─────────────────────────────────
    if (profile.selectedUseCases.contains(SetupUseCase.manageBills) ||
        profile.responsibilities.isNotEmpty) {
      modules.add('bills');
    }

    if (profile.selectedUseCases.contains(SetupUseCase.trackMoney)) {
      modules.add('accounts_overview');
    }

    if (profile.selectedUseCases.contains(SetupUseCase.saveForGoals) ||
        profile.financialGoals.isNotEmpty) {
      modules.add('goals');
    }

    if (profile.selectedUseCases.contains(SetupUseCase.improveSpending)) {
      modules.add('spending_insights');
    }

    if (profile.selectedUseCases.contains(SetupUseCase.sendMoneyHome)) {
      modules.add('remittance');
    }

    // Always include recent activity
    if (!modules.contains('accounts_overview')) {
      modules.add('recent_activity');
    }

    // ── Quick actions ─────────────────────────────────────
    if (profile.accountConnectionSkipped) {
      quickActions.add('connect_account');
    }

    if (profile.selectedUseCases.contains(SetupUseCase.sendMoneyHome)) {
      quickActions.add('send_money');
    }

    if (profile.selectedUseCases.contains(SetupUseCase.manageBills)) {
      quickActions.add('pay_bill');
    }

    // ── Nudges ────────────────────────────────────────────
    if (profile.accountConnectionSkipped) {
      nudges.add(
        'Connect your first account so I can start tracking your finances '
        'automatically.',
      );
    }

    if (profile.responsibilities.contains(ResponsibilityType.familySupport) ||
        (profile.supportType != null &&
            profile.supportType != SupportType.noOne)) {
      nudges.add(
        'I\'ll help you plan around your family commitments so nothing '
        'catches you off guard.',
      );
    }

    if (profile.financialGoals.contains(FinancialGoalType.buildEmergencyFund)) {
      nudges.add(
        'Let\'s set up your emergency fund target so I can track your '
        'progress.',
      );
    }

    if (profile.financialGoals.contains(FinancialGoalType.buyHome)) {
      nudges.add(
        'Saving for a home is a big goal. I\'ll help you stay on track.',
      );
    }

    return DashboardSetupSeed(
      greetingVariant: greeting,
      suggestedModules: modules,
      quickActions: quickActions,
      nudges: nudges,
    );
  }
}
