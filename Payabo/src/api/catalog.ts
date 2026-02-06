import { apiGet } from "./client";

type CatalogCountryResponse = {
  countries: Array<{ countryCode: string; name: string }>;
};

export type CatalogCountry = {
  code: string;
  name: string;
};

export type PublicCatalogCountryQuery = {
  onlyServiceCountries?: boolean;
  capabilityType?: string;
};

export const getPublicCatalogCountries = async (
  options: PublicCatalogCountryQuery = {}
): Promise<CatalogCountry[]> => {
  const params = new URLSearchParams();

  if (options.onlyServiceCountries !== undefined) {
    params.set("onlyServiceCountries", String(options.onlyServiceCountries));
  }

  if (options.capabilityType) {
    params.set("capabilityType", options.capabilityType);
  }

  const query = params.toString();
  const path = query ? `/public/catalog/countries?${query}` : "/public/catalog/countries";
  const response = await apiGet<CatalogCountryResponse>(path);

  return response.countries.map((country) => ({
    code: country.countryCode,
    name: country.name
  }));
};
