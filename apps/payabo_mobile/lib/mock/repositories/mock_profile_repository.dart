import '../../data/repositories/profile_repository.dart';

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
    await Future<void>.delayed(const Duration(milliseconds: 220));
    return _profile;
  }

  @override
  Future<UserProfile> updateProfile(UserProfile profile) async {
    await Future<void>.delayed(const Duration(milliseconds: 220));
    _profile = profile;
    return _profile;
  }
}
