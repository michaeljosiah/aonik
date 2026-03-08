class UserProfile {
  const UserProfile({
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.phone,
    required this.countryCode,
    this.photoUrl,
  });

  final String firstName;
  final String lastName;
  final String email;
  final String phone;
  final String countryCode;
  final String? photoUrl;
}

class NotificationPreferences {
  const NotificationPreferences({
    required this.email,
    required this.newBillsPush,
    required this.billUpdatesPush,
    required this.billAssistPush,
    required this.mbaMessagesPush,
    required this.orgMessagesPush,
    required this.friendsMessagesPush,
    required this.newBillsEmail,
    required this.billUpdatesEmail,
    required this.billAssistEmail,
    required this.mbaMessagesEmail,
    required this.orgMessagesEmail,
  });

  final String email;
  final bool newBillsPush;
  final bool billUpdatesPush;
  final bool billAssistPush;
  final bool mbaMessagesPush;
  final bool orgMessagesPush;
  final bool friendsMessagesPush;
  final bool newBillsEmail;
  final bool billUpdatesEmail;
  final bool billAssistEmail;
  final bool mbaMessagesEmail;
  final bool orgMessagesEmail;
}

class MarketingPreferences {
  const MarketingPreferences({
    required this.email,
    required this.news,
    required this.offers,
    required this.surveys,
  });

  final String email;
  final bool news;
  final bool offers;
  final bool surveys;
}

abstract class ProfileRepository {
  Future<UserProfile> getProfile();

  Future<UserProfile> updateProfile(UserProfile profile);

  Future<UserProfile> updateEmail({
    required String currentEmail,
    required String newEmail,
    required String password,
  });

  Future<void> updatePassword({
    required String currentPassword,
    required String newPassword,
  });

  Future<String> uploadPhoto(String filePath);

  Future<void> deletePhoto();

  Future<NotificationPreferences> getNotificationPreferences();

  Future<NotificationPreferences> updateNotificationPreferences(
    NotificationPreferences preferences,
  );

  Future<MarketingPreferences> getMarketingPreferences();

  Future<MarketingPreferences> updateMarketingPreferences(
    MarketingPreferences preferences,
  );
}
