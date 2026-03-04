import '../../data/repositories/profile_repository.dart';
import '../mock_behavior.dart';

class MockProfileRepository implements ProfileRepository {
  UserProfile _profile = const UserProfile(
    firstName: 'Kwame',
    lastName: 'Mensah',
    email: 'kwame.mensah@payabo.app',
    phone: '+233241000000',
    countryCode: 'GH',
  );

  @override
  Future<UserProfile> getProfile() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('profile.getProfile');
    return _profile;
  }

  @override
  Future<UserProfile> updateProfile(UserProfile profile) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('profile.updateProfile');
    _profile = profile;
    return _profile;
  }
}
