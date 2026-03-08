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

  NotificationPreferences _notificationPrefs = const NotificationPreferences(
    email: 'kwame.mensah@payabo.app',
    newBillsPush: true,
    billUpdatesPush: true,
    billAssistPush: false,
    mbaMessagesPush: true,
    orgMessagesPush: true,
    friendsMessagesPush: false,
    newBillsEmail: true,
    billUpdatesEmail: true,
    billAssistEmail: false,
    mbaMessagesEmail: true,
    orgMessagesEmail: true,
  );

  MarketingPreferences _marketingPrefs = const MarketingPreferences(
    email: 'kwame.mensah@payabo.app',
    news: true,
    offers: true,
    surveys: false,
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
      photoUrl: _profile.photoUrl,
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

  @override
  Future<String> uploadPhoto(String filePath) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('profile.uploadPhoto');

    const mockUrl = 'https://mock.payabo.app/photos/profile-kwame.jpg';
    _profile = UserProfile(
      firstName: _profile.firstName,
      lastName: _profile.lastName,
      email: _profile.email,
      phone: _profile.phone,
      countryCode: _profile.countryCode,
      photoUrl: mockUrl,
    );

    return mockUrl;
  }

  @override
  Future<void> deletePhoto() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('profile.deletePhoto');

    _profile = UserProfile(
      firstName: _profile.firstName,
      lastName: _profile.lastName,
      email: _profile.email,
      phone: _profile.phone,
      countryCode: _profile.countryCode,
    );
  }

  @override
  Future<NotificationPreferences> getNotificationPreferences() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('profile.getNotificationPreferences');
    return _notificationPrefs;
  }

  @override
  Future<NotificationPreferences> updateNotificationPreferences(
    NotificationPreferences preferences,
  ) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('profile.updateNotificationPreferences');
    _notificationPrefs = preferences;
    return _notificationPrefs;
  }

  @override
  Future<MarketingPreferences> getMarketingPreferences() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('profile.getMarketingPreferences');
    return _marketingPrefs;
  }

  @override
  Future<MarketingPreferences> updateMarketingPreferences(
    MarketingPreferences preferences,
  ) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('profile.updateMarketingPreferences');
    _marketingPrefs = preferences;
    return _marketingPrefs;
  }
}
