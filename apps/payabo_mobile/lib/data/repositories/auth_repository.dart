class AuthTokenResult {
  const AuthTokenResult({
    required this.accessToken,
    required this.tokenType,
    required this.expiresIn,
    this.refreshToken,
    this.idToken,
  });

  final String accessToken;
  final String tokenType;
  final int expiresIn;
  final String? refreshToken;
  final String? idToken;
}

class AuthUserInfo {
  const AuthUserInfo({
    required this.userId,
    required this.email,
    this.firstName,
    this.lastName,
  });

  final String userId;
  final String email;
  final String? firstName;
  final String? lastName;
}

class AuthOnboardingGate {
  const AuthOnboardingGate({
    required this.gate,
    required this.isSatisfied,
    required this.isRequired,
    required this.requiredActions,
  });

  final String gate;
  final bool isSatisfied;
  final bool isRequired;
  final List<String> requiredActions;
}

class AuthOnboardingSnapshot {
  const AuthOnboardingSnapshot({
    required this.userId,
    required this.partyId,
    required this.gates,
    required this.nextActions,
  });

  final String userId;
  final String? partyId;
  final List<AuthOnboardingGate> gates;
  final List<String> nextActions;

  bool get hasPendingRequiredActions {
    if (nextActions.isNotEmpty) {
      return true;
    }

    return gates.any((AuthOnboardingGate gate) {
      return gate.isRequired && !gate.isSatisfied;
    });
  }
}

class RegisterIndividualRequest {
  const RegisterIndividualRequest({
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.password,
    this.registrationCountry,
    this.phone,
    this.title,
  });

  final String firstName;
  final String lastName;
  final String email;
  final String password;
  final String? registrationCountry;
  final String? phone;
  final String? title;
}

abstract class AuthRepository {
  Future<AuthTokenResult> signInWithPassword({
    required String email,
    required String password,
  });

  Future<AuthTokenResult> refreshAccessToken({
    required String refreshToken,
  });

  Future<AuthOnboardingSnapshot?> registerIndividual(
    RegisterIndividualRequest request,
  );

  Future<void> sendPasswordResetEmail(String email);

  Future<AuthUserInfo> getUserInfo();

  Future<AuthOnboardingSnapshot?> getOnboardingSnapshot();
}
