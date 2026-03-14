import 'package:flutter/material.dart';

import '../domain/setup_enums.dart';
import '../domain/setup_models.dart';

/// All 8 steps of the Payabo post-registration setup journey.
///
/// Each step has exact copy from the product spec, a typed interaction
/// pattern, and structured options that map to domain enums.
final List<SetupStepConfig> setupSteps = <SetupStepConfig>[
  // ── Step 1: Welcome ──────────────────────────────────────
  SetupStepConfig(
    id: 'welcome',
    message:
        'Hi, I\'m Payabo \u{1F44B}\n\n'
        'I\'ll help you organise your finances, track what matters, '
        'and make smarter money decisions across all your accounts.\n\n'
        'Let\'s get you set up.',
    type: SetupStepType.singleAction,
    options: <SetupOption>[
      SetupOption(
        id: 'start',
        label: 'Start setup',
        icon: Icons.arrow_forward_rounded,
      ),
    ],
  ),

  // ── Step 2: What do you want help with? ──────────────────
  SetupStepConfig(
    id: 'use_cases',
    message:
        'What would you like help with first?\n\n'
        'I can tailor Payabo around how you manage your money.',
    type: SetupStepType.multiSelect,
    options: <SetupOption>[
      SetupOption(
        id: SetupUseCase.trackMoney.name,
        label: 'Track all my money',
        icon: Icons.account_balance_wallet_outlined,
      ),
      SetupOption(
        id: SetupUseCase.manageBills.name,
        label: 'Manage my bills',
        icon: Icons.receipt_long_outlined,
      ),
      SetupOption(
        id: SetupUseCase.sendMoneyHome.name,
        label: 'Send money home',
        icon: Icons.send_outlined,
      ),
      SetupOption(
        id: SetupUseCase.improveSpending.name,
        label: 'Improve my spending',
        icon: Icons.trending_up_rounded,
      ),
      SetupOption(
        id: SetupUseCase.saveForGoals.name,
        label: 'Save for goals',
        icon: Icons.flag_outlined,
      ),
    ],
  ),

  // ── Step 3: Where do you keep your money? ────────────────
  SetupStepConfig(
    id: 'account_sources',
    message:
        'Where do you currently keep your money?\n\n'
        'You can connect multiple account types so I can give you '
        'a complete financial picture.',
    type: SetupStepType.multiSelect,
    options: <SetupOption>[
      SetupOption(
        id: AccountSourceType.ukBank.name,
        label: 'UK bank accounts',
        icon: Icons.account_balance_outlined,
      ),
      SetupOption(
        id: AccountSourceType.nigerianBank.name,
        label: 'Nigerian bank accounts',
        icon: Icons.account_balance_outlined,
      ),
      SetupOption(
        id: AccountSourceType.mobileWallet.name,
        label: 'Mobile wallets',
        icon: Icons.phone_android_outlined,
      ),
      SetupOption(
        id: AccountSourceType.cashManual.name,
        label: 'Cash / manual tracking',
        icon: Icons.payments_outlined,
      ),
    ],
  ),

  // ── Step 4: Connect your first account ───────────────────
  SetupStepConfig(
    id: 'connect_account',
    message:
        'Connecting at least one account helps me automatically track '
        'your finances and give you useful recommendations straight away.\n\n'
        'You can always add more later.',
    helperText: 'You remain in control. Accounts can be connected later.',
    type: SetupStepType.singleSelect,
    canSkip: true,
    options: <SetupOption>[
      SetupOption(
        id: SetupConnectChoice.connectUkBank.name,
        label: 'Connect UK bank',
        icon: Icons.link_rounded,
      ),
      SetupOption(
        id: SetupConnectChoice.connectNigerianBank.name,
        label: 'Connect Nigerian bank',
        icon: Icons.link_rounded,
      ),
      SetupOption(
        id: SetupConnectChoice.skipForNow.name,
        label: 'I\'ll do this later',
        icon: Icons.schedule_outlined,
      ),
    ],
  ),

  // ── Step 5: Bills and responsibilities ───────────────────
  SetupStepConfig(
    id: 'responsibilities',
    message:
        'What regular things do you usually pay for?\n\n'
        'I can help you keep track of these and remind you before '
        'they become urgent.',
    type: SetupStepType.multiSelect,
    options: <SetupOption>[
      SetupOption(
        id: ResponsibilityType.rentOrMortgage.name,
        label: 'Rent or mortgage',
        icon: Icons.home_outlined,
      ),
      SetupOption(
        id: ResponsibilityType.electricity.name,
        label: 'Electricity',
        icon: Icons.bolt_outlined,
      ),
      SetupOption(
        id: ResponsibilityType.internet.name,
        label: 'Internet',
        icon: Icons.wifi_outlined,
      ),
      SetupOption(
        id: ResponsibilityType.subscriptions.name,
        label: 'Subscriptions',
        icon: Icons.subscriptions_outlined,
      ),
      SetupOption(
        id: ResponsibilityType.familySupport.name,
        label: 'Family support',
        icon: Icons.family_restroom_outlined,
      ),
    ],
  ),

  // ── Step 6: Family and community support ─────────────────
  SetupStepConfig(
    id: 'family_support',
    message:
        'Do you regularly support anyone financially?\n\n'
        'This helps me plan around the commitments that matter in real '
        'life, not just personal spending.',
    type: SetupStepType.singleSelect,
    options: <SetupOption>[
      SetupOption(
        id: SupportType.parents.name,
        label: 'Parents',
        icon: Icons.person_outlined,
      ),
      SetupOption(
        id: SupportType.siblings.name,
        label: 'Siblings',
        icon: Icons.people_outlined,
      ),
      SetupOption(
        id: SupportType.children.name,
        label: 'Children',
        icon: Icons.child_care_outlined,
      ),
      SetupOption(
        id: SupportType.communityChurch.name,
        label: 'Community / church',
        icon: Icons.groups_outlined,
      ),
      SetupOption(
        id: SupportType.noOne.name,
        label: 'No one',
        icon: Icons.remove_circle_outline,
      ),
    ],
  ),

  // ── Step 7: Financial goals ──────────────────────────────
  SetupStepConfig(
    id: 'financial_goals',
    message:
        'What are you working toward right now?\n\n'
        'Choose the goals that matter most to you.',
    type: SetupStepType.multiSelect,
    options: <SetupOption>[
      SetupOption(
        id: FinancialGoalType.saveMore.name,
        label: 'Save more money',
        icon: Icons.savings_outlined,
      ),
      SetupOption(
        id: FinancialGoalType.buildEmergencyFund.name,
        label: 'Build emergency fund',
        icon: Icons.shield_outlined,
      ),
      SetupOption(
        id: FinancialGoalType.reduceSpending.name,
        label: 'Reduce spending',
        icon: Icons.trending_down_rounded,
      ),
      SetupOption(
        id: FinancialGoalType.sendMoneySmarter.name,
        label: 'Send money home smarter',
        icon: Icons.compare_arrows_rounded,
      ),
      SetupOption(
        id: FinancialGoalType.buyHome.name,
        label: 'Buy a home',
        icon: Icons.house_outlined,
      ),
    ],
  ),

  // ── Step 8: Summary ──────────────────────────────────────
  SetupStepConfig(
    id: 'summary',
    message:
        'Great, I have enough to start helping you.\n\n'
        'I\'ll use this setup to organise your dashboard, track what '
        'matters, and suggest the right next actions for your finances.',
    helperText:
        'Payabo helps organise your finances. You stay in control, '
        'and recommendations always come before any action.',
    type: SetupStepType.summary,
    options: <SetupOption>[],
  ),
];
