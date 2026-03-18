class CatalogCountry {
  const CatalogCountry({
    required this.code,
    required this.name,
    required this.currency,
  });

  final String code;
  final String name;
  final String currency;
}

class CatalogProvider {
  const CatalogProvider({
    required this.id,
    required this.name,
    required this.countryCode,
  });

  final String id;
  final String name;
  final String countryCode;
}

abstract class CatalogRepository {
  Future<List<CatalogCountry>> getCountries();

  Future<List<CatalogProvider>> getProviders({
    required String countryCode,
  });

  /// Returns the list of service type labels for the service-details form.
  Future<List<String>> getServiceTypes();

  /// Returns the list of recurring frequency labels for the service-details form.
  Future<List<String>> getRecurringFrequencies();

  /// Returns the list of provider category labels for the provider-list filter chips.
  Future<List<String>> getProviderCategories();
}
