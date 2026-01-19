import { useCallback, useEffect, useMemo, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { RefreshCw, AlertCircle, Globe2, ToggleLeft, ToggleRight, Search } from 'lucide-react';
import { catalogService } from '@/services/catalogService';
import type { CatalogCountryItem } from '@/types';

export function CatalogCountriesPage() {
  const [countries, setCountries] = useState<CatalogCountryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [onlyServiceCountries, setOnlyServiceCountries] = useState(false);
  const [search, setSearch] = useState('');

  const loadCountries = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await catalogService.getCountries(onlyServiceCountries);
      setCountries(response.countries);
    } catch (err: unknown) {
      console.error('Failed to load countries:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load catalog countries.');
    } finally {
      setLoading(false);
    }
  }, [onlyServiceCountries]);

  useEffect(() => {
    loadCountries();
  }, [loadCountries]);

  const filteredCountries = useMemo(() => {
    if (!search.trim()) {
      return countries;
    }

    const lowered = search.trim().toLowerCase();
    return countries.filter((country) =>
      country.name.toLowerCase().includes(lowered) || country.countryCode.toLowerCase().includes(lowered)
    );
  }, [countries, search]);

  return (
    <div className="flex-1 overflow-auto">
      <div className="p-6">
        <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4 mb-6">
          <div>
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Catalog Countries</h1>
            <p className="text-[var(--color-text-secondary)]">
              Reference markets available for bill pay catalogs. Filter to show only countries with active services.
            </p>
          </div>
          <div className="flex items-center gap-3">
            <Button variant="outline" onClick={loadCountries} disabled={loading}>
              <RefreshCw className={`w-4 h-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
            <Button
              variant="ghost"
              onClick={() => setOnlyServiceCountries((prev) => !prev)}
            >
              {onlyServiceCountries ? (
                <ToggleRight className="w-4 h-4 mr-2 text-[var(--color-brand-primary)]" />
              ) : (
                <ToggleLeft className="w-4 h-4 mr-2 text-[var(--color-text-tertiary)]" />
              )}
              Only service countries
            </Button>
          </div>
        </div>

        <Card className="mb-6">
          <CardContent className="p-4">
            <div className="flex items-center gap-3">
              <div className="relative flex-1">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)]" />
                <input
                  type="text"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Search by name or code"
                  className="w-full pl-10 pr-4 py-2 border border-[var(--color-border)] rounded-lg text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                />
              </div>
              <Badge variant="secondary">{filteredCountries.length} countries</Badge>
            </div>
          </CardContent>
        </Card>

        {error && (
          <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
            <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
              <AlertCircle className="w-5 h-5" />
              <span className="flex-1">{error}</span>
              <Button variant="outline" size="sm" onClick={loadCountries}>
                Retry
              </Button>
            </CardContent>
          </Card>
        )}

        <Card>
          <CardContent className="p-0">
            {loading ? (
              <div className="p-12 text-center">
                <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                <p className="text-sm text-[var(--color-text-secondary)]">Loading countries...</p>
              </div>
            ) : filteredCountries.length === 0 ? (
              <div className="p-12 text-center">
                <Globe2 className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
                <p className="text-[var(--color-text-primary)] font-medium mb-1">No countries found</p>
                <p className="text-sm text-[var(--color-text-secondary)]">Try adjusting your filters.</p>
              </div>
            ) : (
              <div className="grid gap-3 p-6 md:grid-cols-2 xl:grid-cols-3">
                {filteredCountries.map((country) => (
                  <div
                    key={country.countryCode}
                    className="border border-[var(--color-border-light)] rounded-xl p-4 bg-[var(--color-surface)] shadow-sm"
                  >
                    <div className="flex items-center justify-between mb-2">
                      <Badge variant="secondary" className="font-mono">
                        {country.countryCode}
                      </Badge>
                    </div>
                    <h3 className="text-lg font-semibold text-[var(--color-text-primary)]">{country.name}</h3>
                    <p className="text-sm text-[var(--color-text-secondary)]">Catalog availability reference</p>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
