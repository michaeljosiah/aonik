import { useState, useEffect, useCallback, useMemo } from 'react';
import { Globe } from 'lucide-react';

import { cn } from '@/lib/utils';
import { Combobox, type ComboboxOption } from '@/components/ui/combobox';
import { catalogService } from '@/services/catalogService';
import type { CatalogCountryItem } from '@/types';

// CDN URL for circular country flags
const getFlagUrl = (countryCode: string) => {
  if (!countryCode || countryCode.length !== 2) {
    return 'https://cdn-icons-png.flaticon.com/512/330/330557.png';
  }
  return `https://hatscripts.github.io/circle-flags/flags/${countryCode.toLowerCase()}.svg`;
};

interface CountrySelectProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  className?: string;
  includeEmpty?: boolean;
  emptyLabel?: string;
}

export function CountrySelect({
  value,
  onChange,
  placeholder = 'Select a country',
  disabled,
  className,
  includeEmpty = true,
  emptyLabel = 'Clear selection',
}: CountrySelectProps) {
  const [countries, setCountries] = useState<CatalogCountryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadCountries = useCallback(async () => {
    try {
      setLoading(true);
      // Use the tenant-scoped endpoint, not /host/catalog/countries.
      // The host route requires Tenants.Read which only PlatformAdmin holds;
      // tenant operators (the people actually using forms with this picker)
      // hit 403 and the picker rendered "Error loading countries".
      const response = await catalogService.getTenantCountries();
      const sortedCountries = response.countries.sort((a, b) => a.name.localeCompare(b.name));
      setCountries(sortedCountries);
    } catch (err) {
      console.error('Failed to load countries:', err);
      setError('Failed to load countries');
    }
    finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadCountries();
  }, [loadCountries]);

  const options = useMemo<ComboboxOption[]>(
    () =>
      countries.map((country) => ({
        value: country.countryCode,
        label: country.name,
        keywords: [country.countryCode],
        icon: (
          <img
            src={getFlagUrl(country.countryCode)}
            alt=""
            className="size-5 shrink-0 rounded-full object-cover"
          />
        ),
      })),
    [countries]
  );

  if (error) {
    return (
      <div
        className={cn(
          'flex h-9 w-full items-center gap-2 rounded-md border border-input px-3 text-sm text-muted-foreground',
          className
        )}
      >
        <Globe className="size-4" />
        <span>Couldn't load countries. Reload the page to try again.</span>
      </div>
    );
  }

  return (
    <Combobox
      options={options}
      value={value}
      onValueChange={onChange}
      placeholder={placeholder}
      searchPlaceholder="Search countries"
      emptyText="No countries match."
      disabled={disabled}
      loading={loading}
      clearLabel={includeEmpty ? emptyLabel : undefined}
      className={className}
    />
  );
}
