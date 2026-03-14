import 'setup_models.dart';

/// Repository abstraction for the setup journey.
///
/// The live implementation stores the profile through the AONIK backend:
/// - GET /personal-finance/setup-profile
/// - PUT /personal-finance/setup-profile
/// - DELETE /personal-finance/setup-profile
/// - POST /personal-finance/account-links/sessions (triggerUkAccountLink)
/// - POST /personal-finance/account-links/sessions (triggerNigeriaAccountLink)
///
/// The setup journey only captures context for future AI-driven
/// recommendations and safe, explicit proposals. It must not execute
/// any financially material action.
abstract class SetupJourneyRepository {
  /// Persist the completed setup profile for downstream personalisation.
  Future<void> saveSetupProfile(PayaboSetupProfile profile);

  /// Load a previously saved setup profile, if any.
  Future<PayaboSetupProfile?> loadSetupProfile();

  /// Clear a previously saved setup profile.
  Future<void> clearSetupProfile();

  /// Trigger UK Open Banking account linking flow.
  ///
  /// Placeholder — wire to Plaid UK integration when available.
  /// The existing [AccountLinksRepository] may be extended for this.
  Future<void> triggerUkAccountLink();

  /// Trigger Nigeria bank account linking flow.
  ///
  /// Placeholder — wire to future Nigeria provider integration.
  /// Nigeria connectivity may not always be reliable; callers must
  /// handle failure gracefully.
  Future<void> triggerNigeriaAccountLink();
}
