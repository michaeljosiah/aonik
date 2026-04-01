export interface TenantCountryOption {
  code: string;
  name: string;
}

export const tenantCountryOptions: TenantCountryOption[] = [
  { code: 'BW', name: 'Botswana' },
  { code: 'CA', name: 'Canada' },
  { code: 'DE', name: 'Germany' },
  { code: 'FR', name: 'France' },
  { code: 'GB', name: 'United Kingdom' },
  { code: 'GH', name: 'Ghana' },
  { code: 'IN', name: 'India' },
  { code: 'JP', name: 'Japan' },
  { code: 'KE', name: 'Kenya' },
  { code: 'MX', name: 'Mexico' },
  { code: 'NG', name: 'Nigeria' },
  { code: 'NZ', name: 'New Zealand' },
  { code: 'SG', name: 'Singapore' },
  { code: 'US', name: 'United States' },
  { code: 'ZA', name: 'South Africa' },
  { code: 'ZM', name: 'Zambia' },
  { code: 'ZW', name: 'Zimbabwe' },
];

export function formatTenantCountryLabel(countryCode: string): string {
  const normalized = countryCode.trim().toUpperCase();
  const match = tenantCountryOptions.find((country) => country.code === normalized);
  return match ? `${match.code} - ${match.name}` : normalized;
}
