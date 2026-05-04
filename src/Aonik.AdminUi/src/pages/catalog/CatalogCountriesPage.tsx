import { useCallback, useEffect, useMemo, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { DataTableHeader, DataTablePagination, type ViewMode } from '@/components/ui/data-table';
import { RefreshCw, AlertCircle, Globe2, ToggleLeft, ToggleRight } from 'lucide-react';
import { catalogService } from '@/services/catalogService';
import type { CatalogCountryItem } from '@/types';

// FlatIcon circular country flags mapping
// Using FlatIcon's circular flag collection from the 'circle-flags' pack
const getFlagUrl = (countryCode: string) => {
  if (countryCode.length !== 2) {
    return 'https://cdn-icons-png.flaticon.com/512/330/330557.png'; // Globe icon as fallback
  }
  
  // FlatIcon circular flags - Using circle-flags CDN which provides SVG circular flags
  // This is a more reliable source with all country codes supported
  return `https://hatscripts.github.io/circle-flags/flags/${countryCode.toLowerCase()}.svg`;
};

export function CatalogCountriesPage() {
  const [countries, setCountries] = useState<CatalogCountryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [onlyServiceCountries, setOnlyServiceCountries] = useState(false);
  const [search, setSearch] = useState('');
  const [viewMode, setViewMode] = useState<ViewMode>('list');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(12);

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

  useEffect(() => {
    setPageNumber(1);
  }, [search, onlyServiceCountries]);

  const filteredCountries = useMemo(() => {
    if (!search.trim()) {
      return countries;
    }

    const lowered = search.trim().toLowerCase();
    return countries.filter((country) =>
      country.name.toLowerCase().includes(lowered) || country.countryCode.toLowerCase().includes(lowered)
    );
  }, [countries, search]);

  const pagedCountries = useMemo(() => {
    const start = (pageNumber - 1) * pageSize;
    return filteredCountries.slice(start, start + pageSize);
  }, [filteredCountries, pageNumber, pageSize]);

  const handlePageSizeChange = (newPageSize: number) => {
    setPageSize(newPageSize);
    setPageNumber(1);
  };

  return (
    <div className="h-full overflow-auto p-6">

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
          <DataTableHeader
            searchValue={search}
            onSearchChange={setSearch}
            searchPlaceholder="Search for countries"
            viewMode={viewMode}
            onViewModeChange={setViewMode}
            showViewToggle
            className="px-0 border-b-0"
            actions={(
              <>
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
                <Badge variant="secondary">{filteredCountries.length} countries</Badge>
              </>
            )}
          />

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
              viewMode === 'list' ? (
                <div className="overflow-x-auto">
                  <table className="w-full">
                    <thead>
                      <tr className="border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]/50">
                        <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)] w-16">
                          Flag
                        </th>
                        <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                          Country
                        </th>
                        <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                          Code
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {pagedCountries.map((country) => (
                        <tr key={country.countryCode} className="border-b border-[var(--color-border-light)]">
                          <td className="px-4 py-3">
                            <img 
                              src={getFlagUrl(country.countryCode)} 
                              alt={`${country.name} flag`}
                              className="w-8 h-8 rounded-full object-cover"
                            />
                          </td>
                          <td className="px-4 py-3">
                            <div>
                              <p className="font-medium text-[var(--color-text-primary)]">{country.name}</p>
                              <p className="text-xs text-[var(--color-text-tertiary)]">Catalog availability reference</p>
                            </div>
                          </td>
                          <td className="px-4 py-3">
                            <Badge variant="secondary" className="font-mono">
                              {country.countryCode}
                            </Badge>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <div className="grid gap-3 p-6 md:grid-cols-2 xl:grid-cols-3">
                  {pagedCountries.map((country) => (
                    <div
                      key={country.countryCode}
                      className="border border-[var(--color-border-light)] rounded-md p-4 bg-[var(--color-surface)] shadow-sm"
                    >
                      <div className="flex items-center justify-between mb-3">
                        <Badge variant="secondary" className="font-mono">
                          {country.countryCode}
                        </Badge>
                        <img 
                          src={getFlagUrl(country.countryCode)} 
                          alt={`${country.name} flag`}
                          className="w-10 h-10 rounded-full object-cover"
                        />
                      </div>
                      <h3 className="text-lg font-semibold text-[var(--color-text-primary)]">{country.name}</h3>
                      <p className="text-sm text-[var(--color-text-secondary)]">Catalog availability reference</p>
                    </div>
                  ))}
                </div>
              )
            )}
          </div>

          <div className="pt-4">
            <DataTablePagination
              pageNumber={pageNumber}
              pageSize={pageSize}
              totalCount={filteredCountries.length}
              onPageChange={setPageNumber}
              onPageSizeChange={handlePageSizeChange}
              className="px-0 border-t-0"
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
