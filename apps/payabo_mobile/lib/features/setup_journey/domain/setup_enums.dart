// Enums for the Payabo post-registration AI-guided setup journey.
//
// Each enum maps to an AONIK domain concept:
// - SetupUseCase       -> PersonalProfile preferences / dashboard module priority
// - AccountSourceType  -> ExternalAccount.type
// - SetupConnectChoice -> FinancialConnectionSession trigger
// - ResponsibilityType -> Bill / Subscription category
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

/// Where the user keeps money (Step 3).
enum AccountSourceType {
  ukBank,
  nigerianBank,
  mobileWallet,
  cashManual,
}

/// Account connection choice (Step 4).
enum SetupConnectChoice {
  connectUkBank,
  connectNigerianBank,
  skipForNow,
}

/// Regular financial responsibilities (Step 5).
enum ResponsibilityType {
  rentOrMortgage,
  electricity,
  internet,
  subscriptions,
  familySupport,
}

/// Family and community support signal (Step 6).
enum SupportType {
  parents,
  siblings,
  children,
  communityChurch,
  noOne,
}

/// Financial goals the user is working toward (Step 7).
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
