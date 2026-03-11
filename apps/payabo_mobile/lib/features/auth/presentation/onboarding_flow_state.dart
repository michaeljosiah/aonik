import 'package:flutter_riverpod/legacy.dart';

class OnboardingCountry {
  const OnboardingCountry({
    required this.code,
    required this.name,
    required this.dialCode,
    required this.flagAsset,
  });

  final String code;
  final String name;
  final String dialCode;
  final String flagAsset;
}

const List<OnboardingCountry> onboardingCountries = <OnboardingCountry>[
  OnboardingCountry(
    code: 'BW',
    name: 'Botswana',
    dialCode: '+267',
    flagAsset: 'assets/images/flags/bw.svg',
  ),
  OnboardingCountry(
    code: 'GH',
    name: 'Ghana',
    dialCode: '+233',
    flagAsset: 'assets/images/flags/gh.svg',
  ),
  OnboardingCountry(
    code: 'GB',
    name: 'United Kingdom',
    dialCode: '+44',
    flagAsset: 'assets/images/flags/gb.svg',
  ),
  OnboardingCountry(
    code: 'NG',
    name: 'Nigeria',
    dialCode: '+234',
    flagAsset: 'assets/images/flags/ng.svg',
  ),
  OnboardingCountry(
    code: 'ZM',
    name: 'Zambia',
    dialCode: '+260',
    flagAsset: 'assets/images/flags/zm.svg',
  ),
  OnboardingCountry(
    code: 'ZW',
    name: 'Zimbabwe',
    dialCode: '+263',
    flagAsset: 'assets/images/flags/zw.svg',
  ),
];

OnboardingCountry resolveOnboardingCountry(String countryCode) {
  final normalized = countryCode.trim().toUpperCase();

  return onboardingCountries.firstWhere(
    (country) => country.code == normalized,
    orElse: () =>
        onboardingCountries.firstWhere((country) => country.code == 'GB'),
  );
}

bool isValidEmail(String value) {
  final email = value.trim();
  if (email.isEmpty) {
    return false;
  }

  final emailPattern = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
  return emailPattern.hasMatch(email);
}

bool meetsPasswordRequirements(String password) {
  if (password.length < 8) {
    return false;
  }

  final hasLower = RegExp('[a-z]').hasMatch(password);
  final hasUpper = RegExp('[A-Z]').hasMatch(password);
  final hasDigit = RegExp('[0-9]').hasMatch(password);
  return hasLower && hasUpper && hasDigit;
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
