class UserProfile {
  const UserProfile({
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.phone,
    required this.countryCode,
  });

  final String firstName;
  final String lastName;
  final String email;
  final String phone;
  final String countryCode;
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
}
