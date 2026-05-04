import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { ChevronDown, Globe } from 'lucide-react';

import { cn } from '@/lib/utils';
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
  const [open, setOpen] = useState(false);
  const [inputValue, setInputValue] = useState('');
  const containerRef = useRef<HTMLDivElement | null>(null);

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

  const selectedCountry = useMemo(
    () => countries.find((country) => country.countryCode === value),
    [countries, value]
  );

  useEffect(() => {
    if (!open) {
      setInputValue(selectedCountry?.name ?? '');
    }
  }, [open, selectedCountry]);

  useEffect(() => {
    if (!open && !value) {
      setInputValue('');
    }
  }, [open, value]);

  useEffect(() => {
    if (!open) return;

    const handleOutsideClick = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };

    document.addEventListener('mousedown', handleOutsideClick);
    return () => document.removeEventListener('mousedown', handleOutsideClick);
  }, [open]);

  const filteredCountries = useMemo(() => {
    const query = inputValue.trim().toLowerCase();
    if (!query) return countries;

    return countries.filter((country) =>
      country.name.toLowerCase().includes(query) ||
      country.countryCode.toLowerCase().includes(query)
    );
  }, [countries, inputValue]);

  const handleSelect = (countryCode: string) => {
    onChange(countryCode);
    const selected = countries.find((country) => country.countryCode === countryCode);
    setInputValue(selected?.name ?? '');
    setOpen(false);
  };

  const handleClear = () => {
    onChange('');
    setInputValue('');
    setOpen(false);
  };

  if (error) {
    return (
      <div
        className={cn(
          'flex h-10 w-full items-center rounded-none border border-[var(--color-form-field-border)] bg-[var(--color-form-field-bg)] px-3 text-sm text-[var(--color-form-field-text)]',
          className
        )}
      >
        <Globe className="w-4 h-4 mr-2 text-[var(--color-text-tertiary)]" />
        <span>Error loading countries</span>
      </div>
    );
  }

  return (
    <div ref={containerRef} className={cn('relative', className)}>
      <div
        className={cn(
          'flex h-10 w-full items-center gap-2 rounded-none border border-[var(--color-form-field-border)] bg-[var(--color-form-field-bg)] px-3 py-2 text-sm text-[var(--color-form-field-text)] focus-within:outline-none focus-within:ring-0 focus-within:border-[var(--color-form-field-border-focus)]',
          disabled && 'cursor-not-allowed opacity-50'
        )}
        onClick={() => !disabled && setOpen(true)}
        role="combobox"
        aria-expanded={open}
        aria-haspopup="listbox"
      >
        {selectedCountry ? (
          <img
            src={getFlagUrl(selectedCountry.countryCode)}
            alt={selectedCountry.name}
            className="w-5 h-5 rounded-full object-cover"
          />
        ) : (
          <Globe className="w-4 h-4 text-[var(--color-text-tertiary)]" />
        )}
        <input
          type="text"
          value={inputValue}
          onChange={(event) => {
            setInputValue(event.target.value);
            if (!open) setOpen(true);
          }}
          onFocus={() => !disabled && setOpen(true)}
          onKeyDown={(event) => {
            if (event.key === 'Escape') {
              setOpen(false);
            }
          }}
          className="flex-1 bg-transparent text-[var(--color-form-field-text)] outline-none placeholder:text-[var(--color-form-field-placeholder)]"
          placeholder={placeholder}
          disabled={disabled}
        />
        <ChevronDown className={cn('h-4 w-4 text-[var(--color-text-tertiary)] transition-transform', open && 'rotate-180')} />
      </div>

      {open && (
        <div className="absolute z-[200] mt-1 w-full rounded-none border border-[var(--color-form-field-border)] bg-[var(--color-form-field-bg)] shadow-md">
          <div className="max-h-64 overflow-auto p-1">
            {includeEmpty && (
              <button
                type="button"
                onClick={handleClear}
                className="flex w-full items-center gap-2 rounded-sm px-2 py-2 text-left text-sm text-[var(--color-text-secondary)] hover:bg-[var(--color-brand-primary-light)] hover:text-[var(--color-brand-primary)]"
              >
                <Globe className="w-4 h-4" />
                <span>{emptyLabel}</span>
              </button>
            )}

            {loading ? (
              <div className="px-2 py-2 text-sm text-[var(--color-text-tertiary)]">Loading countries...</div>
            ) : filteredCountries.length === 0 ? (
              <div className="px-2 py-2 text-sm text-[var(--color-text-tertiary)]">No countries found</div>
            ) : (
              filteredCountries.map((country) => (
                <button
                  key={country.countryCode}
                  type="button"
                  onClick={() => handleSelect(country.countryCode)}
                  className="flex w-full items-center gap-2 rounded-sm px-2 py-2 text-left text-sm hover:bg-[var(--color-brand-primary-light)] hover:text-[var(--color-brand-primary)]"
                >
                  <img
                    src={getFlagUrl(country.countryCode)}
                    alt={country.name}
                    className="w-5 h-5 rounded-full object-cover"
                  />
                  <span>{country.name}</span>
                  <span className="ml-auto text-xs text-[var(--color-text-tertiary)]">
                    {country.countryCode}
                  </span>
                </button>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}
