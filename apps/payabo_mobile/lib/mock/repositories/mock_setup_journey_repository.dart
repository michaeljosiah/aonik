import '../../features/setup_journey/domain/setup_journey_repository.dart';
import '../../features/setup_journey/domain/setup_models.dart';
import '../mock_behavior.dart';

/// Mock implementation of [SetupJourneyRepository].
///
/// All methods simulate network latency via [MockBehavior.delay] and
/// store data in memory. Replace with a live implementation that calls
/// the AONIK backend when available.
class MockSetupJourneyRepository implements SetupJourneyRepository {
  PayaboSetupProfile? _savedProfile;

  @override
  Future<void> saveSetupProfile(PayaboSetupProfile profile) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('setupJourney.saveSetupProfile');

    _savedProfile = profile;
  }

  @override
  Future<PayaboSetupProfile?> loadSetupProfile() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('setupJourney.loadSetupProfile');

    return _savedProfile;
  }

  @override
  Future<void> clearSetupProfile() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('setupJourney.clearSetupProfile');

    _savedProfile = null;
  }

  @override
  Future<void> triggerUkAccountLink() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('setupJourney.triggerUkAccountLink');

    // Placeholder — no-op in mock.
  }

  @override
  Future<void> triggerNigeriaAccountLink() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('setupJourney.triggerNigeriaAccountLink');

    // Placeholder — no-op in mock.
  }
}
