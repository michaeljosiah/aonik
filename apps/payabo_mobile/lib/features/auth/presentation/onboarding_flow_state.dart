import 'package:flutter_riverpod/flutter_riverpod.dart'
    show FutureProvider, Ref;
import 'package:flutter_riverpod/legacy.dart';

import '../../../data/repositories/auth_repository.dart';
import '../../../data/repositories/repository_providers.dart';
import '../../../shared/reference/payabo_country_reference.dart';
import '../../../shared/validation/payabo_input_validators.dart';

typedef OnboardingCountry = PayaboCountryReference;

const Object _onboardingSentinel = Object();

const List<OnboardingCountry> phoneSelectionCountries =
    payaboOnboardingCountries;

OnboardingCountry resolveOnboardingCountry(String countryCode) {
  return resolvePayaboCountry(countryCode);
}

final FutureProvider<List<OnboardingCountry>>
    registrationCountryOptionsProvider =
    FutureProvider<List<OnboardingCountry>>((Ref ref) async {
  final AuthRepository repository = ref.watch(authRepositoryProvider);
  final List<String> countryCodes = await repository.getRegistrationCountries();

  return countryCodes
      .map(resolvePayaboCountryOrFallback)
      .toList(growable: false);
});

bool isValidEmail(String value) {
  return isValidPayaboEmailAddress(value);
}

bool meetsPasswordRequirements(String password) {
  return validatePayaboPassword(password).isValid;
}

class OnboardingState {
  const OnboardingState({
    required this.registrationCountryCode,
    required this.phoneCountryCode,
    required this.firstName,
    required this.lastName,
    required this.mobileNumber,
    required this.email,
    required this.password,
    this.phoneOtpChallengeId,
    this.phoneOtpDevCode,
  });

  final String registrationCountryCode;
  final String phoneCountryCode;
  final String firstName;
  final String lastName;
  final String mobileNumber;
  final String email;
  final String password;
  final String? phoneOtpChallengeId;

  /// The plaintext OTP code returned by the API in development environments.
  /// Null in production or when the SMS provider is properly configured.
  final String? phoneOtpDevCode;

  OnboardingCountry get registrationCountry =>
      resolveOnboardingCountry(registrationCountryCode);

  OnboardingCountry get phoneCountry =>
      resolveOnboardingCountry(phoneCountryCode);

  OnboardingState copyWith({
    String? registrationCountryCode,
    String? phoneCountryCode,
    String? firstName,
    String? lastName,
    String? mobileNumber,
    String? email,
    String? password,
    Object? phoneOtpChallengeId = _onboardingSentinel,
    Object? phoneOtpDevCode = _onboardingSentinel,
  }) {
    return OnboardingState(
      registrationCountryCode:
          registrationCountryCode ?? this.registrationCountryCode,
      phoneCountryCode: phoneCountryCode ?? this.phoneCountryCode,
      firstName: firstName ?? this.firstName,
      lastName: lastName ?? this.lastName,
      mobileNumber: mobileNumber ?? this.mobileNumber,
      email: email ?? this.email,
      password: password ?? this.password,
      phoneOtpChallengeId: phoneOtpChallengeId == _onboardingSentinel
          ? this.phoneOtpChallengeId
          : phoneOtpChallengeId as String?,
      phoneOtpDevCode: phoneOtpDevCode == _onboardingSentinel
          ? this.phoneOtpDevCode
          : phoneOtpDevCode as String?,
    );
  }

  factory OnboardingState.initial() {
    return const OnboardingState(
      registrationCountryCode: '',
      phoneCountryCode: '',
      firstName: '',
      lastName: '',
      mobileNumber: '',
      email: '',
      password: '',
    );
  }
}

class OnboardingController extends StateNotifier<OnboardingState> {
  OnboardingController() : super(OnboardingState.initial());

  void setRegistrationCountry(String countryCode) {
    state = state.copyWith(registrationCountryCode: countryCode);
  }

  void setPhoneCountry(String countryCode) {
    state = state.copyWith(phoneCountryCode: countryCode);
  }

  void setFirstName(String value) {
    state = state.copyWith(firstName: value);
  }

  void setLastName(String value) {
    state = state.copyWith(lastName: value);
  }

  void setMobileNumber(String value) {
    state = state.copyWith(mobileNumber: value);
  }

  void setEmail(String value) {
    state = state.copyWith(email: value);
  }

  void setPassword(String value) {
    state = state.copyWith(password: value);
  }

  void setPhoneOtpChallengeId(String value) {
    state = state.copyWith(phoneOtpChallengeId: value);
  }

  void setPhoneOtpDevCode(String? value) {
    state = state.copyWith(phoneOtpDevCode: value);
  }

  void reset() {
    state = OnboardingState.initial();
  }
}

final StateNotifierProvider<OnboardingController, OnboardingState>
    onboardingControllerProvider =
    StateNotifierProvider<OnboardingController, OnboardingState>(
  (ref) => OnboardingController(),
);
