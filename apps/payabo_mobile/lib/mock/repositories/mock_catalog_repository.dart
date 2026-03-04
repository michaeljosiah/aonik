import '../../data/repositories/catalog_repository.dart';
import '../mock_behavior.dart';

class MockCatalogRepository implements CatalogRepository {
  static const List<CatalogCountry> _countries = <CatalogCountry>[
    CatalogCountry(code: 'GH', name: 'Ghana', currency: 'GHS'),
    CatalogCountry(code: 'NG', name: 'Nigeria', currency: 'NGN'),
    CatalogCountry(code: 'KE', name: 'Kenya', currency: 'KES'),
  ];

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
    return _countries;
  }

  @override
  Future<List<CatalogProvider>> getProviders(
      {required String countryCode}) async {
    await MockBehavior.delay();
    MockBehavior.throwIfEnabled('catalog.getProviders');
    return _providers
        .where((CatalogProvider provider) =>
            provider.countryCode == countryCode.toUpperCase())
        .toList(growable: false);
  }
}
