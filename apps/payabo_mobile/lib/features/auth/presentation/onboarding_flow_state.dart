import 'package:flutter_riverpod/legacy.dart';

import '../../../shared/reference/payabo_country_reference.dart';
import '../../../shared/validation/payabo_input_validators.dart';

typedef OnboardingCountry = PayaboCountryReference;

const List<OnboardingCountry> onboardingCountries = payaboOnboardingCountries;

OnboardingCountry resolveOnboardingCountry(String countryCode) {
  final String normalized = countryCode.trim().toUpperCase();

  return onboardingCountries.firstWhere(
    (OnboardingCountry country) => country.code == normalized,
    orElse: () => payaboCountryUnitedKingdom,
  );
}

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
  });

  final String registrationCountryCode;
  final String phoneCountryCode;
  final String firstName;
  final String lastName;
  final String mobileNumber;
  final String email;
  final String password;

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
    );
  }

  factory OnboardingState.initial() {
    return const OnboardingState(
      registrationCountryCode: 'GB',
      phoneCountryCode: 'GB',
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

  void reset() {
    state = OnboardingState.initial();
  }
}

final StateNotifierProvider<OnboardingController, OnboardingState>
    onboardingControllerProvider =
    StateNotifierProvider<OnboardingController, OnboardingState>(
  (ref) => OnboardingController(),
);
