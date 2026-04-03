class PayaboPasswordValidation {
  const PayaboPasswordValidation({
    required this.hasMinLength,
    required this.hasLowercase,
    required this.hasUppercase,
    required this.hasDigit,
    required this.hasSpecialCharacter,
  });

  final bool hasMinLength;
  final bool hasLowercase;
  final bool hasUppercase;
  final bool hasDigit;
  final bool hasSpecialCharacter;

  bool get isValid {
    return hasMinLength &&
        hasLowercase &&
        hasUppercase &&
        hasDigit &&
        hasSpecialCharacter;
  }
}

PayaboPasswordValidation validatePayaboPassword(String password) {
  return PayaboPasswordValidation(
    hasMinLength: password.length >= 8,
    hasLowercase: RegExp(r'[a-z]').hasMatch(password),
    hasUppercase: RegExp(r'[A-Z]').hasMatch(password),
    hasDigit: RegExp(r'[0-9]').hasMatch(password),
    hasSpecialCharacter: RegExp(r'[!@#$%^&*(),.?":{}|<>\-_+=\[\]\\;/~`]').hasMatch(password),
  );
}

bool isValidPayaboEmailAddress(String value) {
  final String email = value.trim();
  if (email.isEmpty) {
    return false;
  }

  return RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(email);
}
