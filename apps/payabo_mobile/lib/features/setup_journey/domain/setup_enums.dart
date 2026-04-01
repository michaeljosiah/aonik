// Enums for the Payabo post-registration AI-guided setup journey.
//
// Each enum maps to an AONIK domain concept:
// - SetupUseCase       -> PersonalProfile preferences / dashboard module priority
// - SetupConnectChoice -> FinancialConnectionSession trigger
// - SupportType        -> HouseholdMember / PartyRelationship
// - FinancialGoalType  -> Goal entity
// - SetupStepType      -> UI interaction pattern per step

/// What the user wants help with (Step 2).
enum SetupUseCase {
  trackMoney,
  manageBills,
  sendMoneyHome,
  improveSpending,
  saveForGoals,
}

/// Account connection choice (Step 3).
enum SetupConnectChoice {
  connectUkBank,
  connectNigerianBank,
  skipForNow,
}

/// Family and community support signal (Step 4, multi-select).
enum SupportType {
  parents,
  siblings,
  children,
  communityChurch,
  noOne,
}

/// Financial goals the user is working toward (Step 5).
enum FinancialGoalType {
  saveMore,
  buildEmergencyFund,
  reduceSpending,
  sendMoneySmarter,
  buyHome,
}

/// Interaction type for each setup step.
enum SetupStepType {
  singleAction,
  singleSelect,
  multiSelect,
  summary,
}
