import '../../app/demo/demo_data_mode.dart';
import '../../data/repositories/profile_repository.dart';
import '../mock_behavior.dart';

class MockProfileRepository implements ProfileRepository {
  MockProfileRepository({
    this.demoDataMode = DemoDataMode.populated,
  })  : _profile = _buildProfile(demoDataMode),
        _notificationPrefs = _buildNotificationPreferences(demoDataMode),
        _marketingPrefs = _buildMarketingPreferences(demoDataMode);

  final DemoDataMode demoDataMode;

  UserProfile _profile;
  NotificationPreferences _notificationPrefs;
  MarketingPreferences _marketingPrefs;

  static UserProfile _buildProfile(DemoDataMode demoDataMode) {
    switch (demoDataMode) {
      case DemoDataMode.fresh:
        return const UserProfile(
          firstName: 'Kwame',
          lastName: 'Mensah',
          email: 'kwame.mensah@payabo.app',
          phone: '+233241000000',
          countryCode: 'GH',
        );
      case DemoDataMode.populated:
        return const UserProfile(
          firstName: 'Kwame',
          lastName: 'Mensah',
          email: 'kwame.mensah@payabo.app',
          phone: '+233241000000',
          countryCode: 'GH',
          photoUrl: 'assets/images/demo_profile.jpg',
        );
    }
  }

  static NotificationPreferences _buildNotificationPreferences(
    DemoDataMode demoDataMode,
  ) {
    switch (demoDataMode) {
      case DemoDataMode.fresh:
        return const NotificationPreferences(
          email: 'kwame.mensah@payabo.app',
          newBillsPush: false,
          billUpdatesPush: false,
          billAssistPush: false,
          mbaMessagesPush: false,
          orgMessagesPush: false,
          friendsMessagesPush: false,
          newBillsEmail: false,
          billUpdatesEmail: false,
          billAssistEmail: false,
          mbaMessagesEmail: false,
          orgMessagesEmail: false,
        );
      case DemoDataMode.populated:
        return const NotificationPreferences(
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
    }
  }

  static MarketingPreferences _buildMarketingPreferences(
    DemoDataMode demoDataMode,
  ) {
    switch (demoDataMode) {
      case DemoDataMode.fresh:
        return const MarketingPreferences(
          email: 'kwame.mensah@payabo.app',
          news: false,
          offers: false,
          surveys: false,
        );
      case DemoDataMode.populated:
        return const MarketingPreferences(
          email: 'kwame.mensah@payabo.app',
          news: true,
          offers: true,
          surveys: false,
        );
    }
  }

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
  Future<String> uploadPhoto(
    String filePath, {
    String? fileName,
    String? contentType,
  }) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('profile.uploadPhoto');

    _profile = UserProfile(
      firstName: _profile.firstName,
      lastName: _profile.lastName,
      email: _profile.email,
      phone: _profile.phone,
      countryCode: _profile.countryCode,
      photoUrl: filePath,
    );

    return filePath;
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
