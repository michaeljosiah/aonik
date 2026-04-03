import '../../data/repositories/catalog_repository.dart';
import '../../shared/reference/payabo_country_reference.dart';
import '../mock_behavior.dart';

class MockCatalogRepository implements CatalogRepository {
  static const List<CatalogProvider> _providers = <CatalogProvider>[
    CatalogProvider(id: 'prov_ecg', name: 'ECG Power', countryCode: 'GH'),
    CatalogProvider(id: 'prov_gwcl', name: 'Ghana Water', countryCode: 'GH'),
    CatalogProvider(id: 'prov_eko', name: 'Eko Electricity', countryCode: 'NG'),
    CatalogProvider(id: 'prov_safaricom', name: 'Safaricom', countryCode: 'KE'),
  ];

  @override
  Future<List<CatalogCountry>> getCountries() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('catalog.getCountries');
    return payaboCountries
        .map(
          (country) => CatalogCountry(
            code: country.code,
            name: country.name,
            currency: country.currencyCode,
          ),
        )
        .toList(growable: false);
  }

  @override
  Future<List<CatalogProvider>> getProviders(
      {required String countryCode}) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('catalog.getProviders');
    final normalizedCountryCode = countryCode.trim().toUpperCase();
    if (normalizedCountryCode.isEmpty) {
      return _providers;
    }

    return _providers
        .where((CatalogProvider provider) =>
            provider.countryCode == normalizedCountryCode)
        .toList(growable: false);
  }

  @override
  Future<List<String>> getServiceTypes() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('catalog.getServiceTypes');
    return const <String>[
      'Montage Cable TV',
      'Internet Data Bundle',
      'Electricity Prepaid',
    ];
  }

  @override
  Future<List<String>> getRecurringFrequencies() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('catalog.getRecurringFrequencies');
    return const <String>[
      'Daily',
      'Weekly',
      'Monthly',
      'Quarterly',
    ];
  }

  @override
  Future<List<String>> getProviderCategories() async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('catalog.getProviderCategories');
    return const <String>[
      'All',
      'Education',
      'Hospital',
      'TV providers',
      'Electricity',
      'Others',
    ];
  }
}
