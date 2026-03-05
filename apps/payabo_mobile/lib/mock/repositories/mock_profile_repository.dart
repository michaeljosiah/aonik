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

  @override
  Future<UserProfile> updateEmail({
    required String currentEmail,
    required String newEmail,
    required String password,
  }) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('profile.updateEmail');

    _profile = UserProfile(
      firstName: _profile.firstName,
      lastName: _profile.lastName,
      email: newEmail.trim(),
      phone: _profile.phone,
      countryCode: _profile.countryCode,
    );

    return _profile;
  }

  @override
  Future<void> updatePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('profile.updatePassword');
  }
}
