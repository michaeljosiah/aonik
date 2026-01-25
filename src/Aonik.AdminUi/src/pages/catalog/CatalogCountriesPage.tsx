import { useCallback, useEffect, useMemo, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
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
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={[{ label: 'Catalog', href: '/catalog' }, { label: 'Countries', icon: <Globe2 className="w-3.5 h-3.5" /> }]} className="mb-4" />

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Catalog Countries</h1>
          <p className="text-[var(--color-text-secondary)]">
            Reference markets available for bill pay catalogs. Filter to show only countries with active services.
          </p>
        </div>
        <Button variant="outline" onClick={loadCountries} disabled={loading} className="rounded-sm">
          <RefreshCw className={`w-4 h-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

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
        <CardContent className="p-4">
          <div className="flex items-center justify-between gap-4">
            <div className="flex items-center gap-4 flex-1">
              <div className="relative w-96 max-w-full">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)]" />
                <input
                  type="text"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Search for countries"
                  className="w-full pl-10 pr-4 py-2 text-sm rounded-sm border border-[var(--color-border)] bg-transparent text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)] focus:border-[var(--color-brand-primary)]"
                />
              </div>

              <Button
                type="button"
                variant="outline"
                className="rounded-sm"
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

            <Badge variant="secondary">{filteredCountries.length} countries</Badge>
          </div>

          <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
            {loading ? (
              <div className="p-12 text-center">
                <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                <p className="text-sm text-[var(--color-text-secondary)]">Loading countries...</p>
              </div>
            ) : filteredCountries.length === 0 ? (
              <div className="p-12 text-center">
                <div className="mb-3 flex justify-center text-[var(--color-text-tertiary)]">
                  <Globe2 className="w-12 h-12" />
                </div>
                <p className="text-[var(--color-text-primary)] font-medium mb-1">No countries found</p>
                <p className="text-sm text-[var(--color-text-secondary)]">Try adjusting your filters.</p>
              </div>
            ) : (
              <div className="grid gap-3 p-6 md:grid-cols-2 xl:grid-cols-3">
                {filteredCountries.map((country) => (
                  <div
                    key={country.countryCode}
                    className="border border-[var(--color-border-light)] rounded-md p-4 bg-[var(--color-surface)] shadow-sm"
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
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
