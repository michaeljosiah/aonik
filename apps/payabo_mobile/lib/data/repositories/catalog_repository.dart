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
}
