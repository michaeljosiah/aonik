class PayaboCountryReference {
  const PayaboCountryReference({
    required this.code,
    required this.name,
    required this.dialCode,
    required this.currencyCode,
    required this.flagEmoji,
    this.flagAsset,
  });

  final String code;
  final String name;
  final String dialCode;
  final String currencyCode;
  final String flagEmoji;
  final String? flagAsset;
}

const PayaboCountryReference payaboCountryBotswana = PayaboCountryReference(
  code: 'BW',
  name: 'Botswana',
  dialCode: '+267',
  currencyCode: 'BWP',
  flagEmoji: '\u{1F1E7}\u{1F1FC}',
  flagAsset: 'assets/images/flags/bw.svg',
);

const PayaboCountryReference payaboCountryCanada = PayaboCountryReference(
  code: 'CA',
  name: 'Canada',
  dialCode: '+1',
  currencyCode: 'CAD',
  flagEmoji: '\u{1F1E8}\u{1F1E6}',
);

const PayaboCountryReference payaboCountryGhana = PayaboCountryReference(
  code: 'GH',
  name: 'Ghana',
  dialCode: '+233',
  currencyCode: 'GHS',
  flagEmoji: '\u{1F1EC}\u{1F1ED}',
  flagAsset: 'assets/images/flags/gh.svg',
);

const PayaboCountryReference payaboCountryIndia = PayaboCountryReference(
  code: 'IN',
  name: 'India',
  dialCode: '+91',
  currencyCode: 'INR',
  flagEmoji: '\u{1F1EE}\u{1F1F3}',
);

const PayaboCountryReference payaboCountryIreland = PayaboCountryReference(
  code: 'IE',
  name: 'Ireland',
  dialCode: '+353',
  currencyCode: 'EUR',
  flagEmoji: '\u{1F1EE}\u{1F1EA}',
);

const PayaboCountryReference payaboCountryKenya = PayaboCountryReference(
  code: 'KE',
  name: 'Kenya',
  dialCode: '+254',
  currencyCode: 'KES',
  flagEmoji: '\u{1F1F0}\u{1F1EA}',
);

const PayaboCountryReference payaboCountryNigeria = PayaboCountryReference(
  code: 'NG',
  name: 'Nigeria',
  dialCode: '+234',
  currencyCode: 'NGN',
  flagEmoji: '\u{1F1F3}\u{1F1EC}',
  flagAsset: 'assets/images/flags/ng.svg',
);

const PayaboCountryReference payaboCountrySouthAfrica = PayaboCountryReference(
  code: 'ZA',
  name: 'South Africa',
  dialCode: '+27',
  currencyCode: 'ZAR',
  flagEmoji: '\u{1F1FF}\u{1F1E6}',
);

const PayaboCountryReference payaboCountryUnitedKingdom =
    PayaboCountryReference(
  code: 'GB',
  name: 'United Kingdom',
  dialCode: '+44',
  currencyCode: 'GBP',
  flagEmoji: '\u{1F1EC}\u{1F1E7}',
  flagAsset: 'assets/images/flags/gb.svg',
);

const PayaboCountryReference payaboCountryUnitedStates = PayaboCountryReference(
  code: 'US',
  name: 'United States',
  dialCode: '+1',
  currencyCode: 'USD',
  flagEmoji: '\u{1F1FA}\u{1F1F8}',
);

const PayaboCountryReference payaboCountryZambia = PayaboCountryReference(
  code: 'ZM',
  name: 'Zambia',
  dialCode: '+260',
  currencyCode: 'ZMW',
  flagEmoji: '\u{1F1FF}\u{1F1F2}',
  flagAsset: 'assets/images/flags/zm.svg',
);

const PayaboCountryReference payaboCountryZimbabwe = PayaboCountryReference(
  code: 'ZW',
  name: 'Zimbabwe',
  dialCode: '+263',
  currencyCode: 'USD',
  flagEmoji: '\u{1F1FF}\u{1F1FC}',
  flagAsset: 'assets/images/flags/zw.svg',
);

const List<PayaboCountryReference> payaboCountries = <PayaboCountryReference>[
  payaboCountryBotswana,
  payaboCountryCanada,
  payaboCountryGhana,
  payaboCountryIndia,
  payaboCountryIreland,
  payaboCountryKenya,
  payaboCountryNigeria,
  payaboCountrySouthAfrica,
  payaboCountryUnitedKingdom,
  payaboCountryUnitedStates,
  payaboCountryZambia,
  payaboCountryZimbabwe,
];

const List<PayaboCountryReference> payaboOnboardingCountries =
    <PayaboCountryReference>[
  payaboCountryBotswana,
  payaboCountryGhana,
  payaboCountryUnitedKingdom,
  payaboCountryNigeria,
  payaboCountryZambia,
  payaboCountryZimbabwe,
];

PayaboCountryReference? tryResolvePayaboCountry(String countryCode) {
  final String normalized = countryCode.trim().toUpperCase();

  for (final PayaboCountryReference country in payaboCountries) {
    if (country.code == normalized) {
      return country;
    }
  }

  return null;
}

PayaboCountryReference buildPayaboCountryFallback(String countryCode) {
  final String normalized = countryCode.trim().toUpperCase();
  return PayaboCountryReference(
    code: normalized,
    name: normalized,
    dialCode: '',
    currencyCode: '',
    flagEmoji: _buildFlagEmoji(normalized),
  );
}

PayaboCountryReference resolvePayaboCountryOrFallback(String countryCode) {
  return tryResolvePayaboCountry(countryCode) ??
      buildPayaboCountryFallback(countryCode);
}

PayaboCountryReference resolvePayaboCountry(
  String countryCode, {
  String fallbackCode = 'GB',
}) {
  final String normalizedFallback = fallbackCode.trim().toUpperCase();
  return tryResolvePayaboCountry(countryCode) ??
      tryResolvePayaboCountry(normalizedFallback) ??
      payaboCountryUnitedKingdom;
}

String resolvePayaboCurrencyCode(
  String countryCode, {
  String fallbackCode = 'GB',
}) {
  return resolvePayaboCountry(
    countryCode,
    fallbackCode: fallbackCode,
  ).currencyCode;
}

String _buildFlagEmoji(String countryCode) {
  if (countryCode.length != 2) {
    return '\u{1F310}';
  }

  final String normalized = countryCode.toUpperCase();
  final int first = normalized.codeUnitAt(0);
  final int second = normalized.codeUnitAt(1);
  if (first < 65 || first > 90 || second < 65 || second > 90) {
    return '\u{1F310}';
  }

  return String.fromCharCodes(<int>[
    0x1F1E6 + (first - 65),
    0x1F1E6 + (second - 65),
  ]);
}
