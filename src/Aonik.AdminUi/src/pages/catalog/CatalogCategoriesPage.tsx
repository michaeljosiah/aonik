import { useCallback, useEffect, useMemo, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { RefreshCw, AlertCircle, ChevronDown, Layers, Search } from 'lucide-react';
import { catalogService } from '@/services/catalogService';
import type { CatalogBillerCategoryItem, CatalogCountryItem } from '@/types';

export function CatalogCategoriesPage() {
  const [categories, setCategories] = useState<CatalogBillerCategoryItem[]>([]);
  const [countries, setCountries] = useState<CatalogCountryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [countryFilter, setCountryFilter] = useState('');
  const [search, setSearch] = useState('');

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [countriesResponse, categoriesResponse] = await Promise.all([
        catalogService.getCountries(false),
        catalogService.getCategories(countryFilter || undefined),
      ]);
      setCountries(countriesResponse.countries);
      setCategories(categoriesResponse.categories);
    } catch (err: unknown) {
      console.error('Failed to load categories:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load catalog categories.');
    } finally {
      setLoading(false);
    }
  }, [countryFilter]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const filteredCategories = useMemo(() => {
    if (!search.trim()) {
      return categories;
    }

    const lowered = search.trim().toLowerCase();
    return categories.filter((category) =>
      category.name.toLowerCase().includes(lowered) ||
      category.countryCode.toLowerCase().includes(lowered)
    );
  }, [categories, search]);

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={[{ label: 'Catalog', href: '/catalog' }, { label: 'Categories', icon: <Layers className="w-3.5 h-3.5" /> }]} className="mb-4" />

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Catalog Categories</h1>
          <p className="text-[var(--color-text-secondary)]">
            Curate category groupings for billers. Filter by market and keep the catalog consistent.
          </p>
        </div>
        <Button variant="outline" onClick={loadData} disabled={loading} className="rounded-sm">
          <RefreshCw className={`w-4 h-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span className="flex-1">{error}</span>
            <Button variant="outline" size="sm" onClick={loadData}>
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
                  placeholder="Search for categories"
                  className="w-full pl-10 pr-4 py-2 text-sm rounded-sm border border-[var(--color-border)] bg-transparent text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)] focus:border-[var(--color-brand-primary)]"
                />
              </div>

              <div className="relative inline-flex items-center">
                <select
                  value={countryFilter}
                  onChange={(event) => setCountryFilter(event.target.value)}
                  className="appearance-none h-9 pl-3 pr-9 text-sm rounded-sm border border-[var(--color-border-light)] bg-[var(--color-surface)] text-[var(--color-text-primary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)] focus:border-[var(--color-brand-primary)] cursor-pointer"
                  aria-label="Filter by country"
                >
                  <option value="" className="bg-[var(--color-surface)] text-[var(--color-text-primary)]">
                    Filter by country
                  </option>
                  {countries.map((country) => (
                    <option
                      key={country.countryCode}
                      value={country.countryCode}
                      className="bg-[var(--color-surface)] text-[var(--color-text-primary)]"
                    >
                      {country.name} ({country.countryCode})
                    </option>
                  ))}
                </select>
                <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)] pointer-events-none" />
              </div>
            </div>

            <Badge variant="secondary">{filteredCategories.length} categories</Badge>
          </div>

          <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
            {loading ? (
              <div className="p-12 text-center">
                <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                <p className="text-sm text-[var(--color-text-secondary)]">Loading categories...</p>
              </div>
            ) : filteredCategories.length === 0 ? (
              <div className="p-12 text-center">
                <div className="mb-3 flex justify-center text-[var(--color-text-tertiary)]">
                  <Layers className="w-12 h-12" />
                </div>
                <p className="text-[var(--color-text-primary)] font-medium mb-1">No categories found</p>
                <p className="text-sm text-[var(--color-text-secondary)]">Try adjusting your filters.</p>
              </div>
            ) : (
              <div className="grid gap-4 p-6 md:grid-cols-2 xl:grid-cols-3">
                {filteredCategories.map((category) => (
                  <div
                    key={category.categoryId}
                    className="border border-[var(--color-border-light)] rounded-md p-4 bg-[var(--color-surface)] shadow-sm"
                  >
                    <div className="flex items-center justify-between mb-3">
                      <Badge variant="secondary" className="font-mono">
                        {category.countryCode}
                      </Badge>
                      <Badge variant="outline">{category.categoryId.slice(0, 8)}</Badge>
                    </div>
                    <h3 className="text-lg font-semibold text-[var(--color-text-primary)]">{category.name}</h3>
                    <p className="text-sm text-[var(--color-text-secondary)] mb-3">
                      {category.description || 'No description provided.'}
                    </p>
                    <div className="text-xs text-[var(--color-text-tertiary)]">
                      Icon: {category.iconUrl || 'Not set'}
                    </div>
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
