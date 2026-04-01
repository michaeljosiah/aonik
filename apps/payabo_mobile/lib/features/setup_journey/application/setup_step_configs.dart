import 'package:flutter/material.dart';

import '../domain/setup_enums.dart';
import '../domain/setup_models.dart';

/// All 6 steps of the Payabo post-registration setup journey.
///
/// Each step has exact copy from the product spec, a typed interaction
/// pattern, and structured options that map to domain enums.
final List<SetupStepConfig> setupSteps = <SetupStepConfig>[
  // ── Step 1: Welcome ──────────────────────────────────────
  const SetupStepConfig(
    id: 'welcome',
    message: 'Hi, I\u2019m Simi \u{1F44B}\n\n'
        'I\u2019m so excited to help you take control of your money! '
        'Together we\u2019ll organise your finances, spot what matters most, '
        'and build real momentum toward your goals.\n\n'
        'Let\u2019s get started \u2014 this only takes a minute.',
    type: SetupStepType.singleAction,
    options: <SetupOption>[
      SetupOption(
        id: 'start',
        label: 'Start setup',
        icon: Icons.arrow_forward_rounded,
      ),
    ],
  ),

  // ── Step 2: What matters most? ───────────────────────────
  SetupStepConfig(
    id: 'use_cases',
    message: 'What matters most to you right now?\n\n'
        'Pick as many as you like \u2014 this helps me focus on what will '
        'make the biggest difference for you.',
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

  // ── Step 3: Connect your first account ───────────────────
  SetupStepConfig(
    id: 'connect_account',
    message: 'Want to connect an account? It\u2019s the fastest way to unlock '
        'personalised insights and start seeing your full financial picture.\n\n'
        'You can always add more accounts later \u2014 no pressure!',
    helperText: 'Your data stays private. You\u2019re always in control.',
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
        label: 'I\u2019ll do this later',
        icon: Icons.schedule_outlined,
      ),
    ],
  ),

  // ── Step 4: Family and community support (multi-select) ──
  SetupStepConfig(
    id: 'family_support',
    message: 'Do you support anyone financially?\n\n'
        'There\u2019s real strength in looking after the people who matter. '
        'I\u2019ll help you plan around those commitments so they never '
        'catch you off guard.',
    type: SetupStepType.multiSelect,
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

  // ── Step 5: Financial goals ──────────────────────────────
  SetupStepConfig(
    id: 'financial_goals',
    message: 'What are you working toward?\n\n'
        'Every big win starts with a clear goal. Pick the ones that fire '
        'you up \u2014 I\u2019ll help you stay on track.',
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

  // ── Step 6: Summary ──────────────────────────────────────
  const SetupStepConfig(
    id: 'summary',
    message: 'You\u2019re all set! \u{1F389}\n\n'
        'I\u2019ve got everything I need to build your personal financial '
        'command centre. From here, I\u2019ll keep an eye on your money, '
        'highlight opportunities, and nudge you toward your goals.\n\n'
        'Let\u2019s make great things happen.',
    helperText: 'Payabo gives you clarity and control. Recommendations '
        'always come before any action.',
    type: SetupStepType.summary,
    options: <SetupOption>[],
  ),
];
