import { useCallback, useEffect, useMemo, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { RefreshCw, AlertCircle, Layers, Search } from 'lucide-react';
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
    <div className="flex-1 overflow-auto">
      <div className="p-6">
        <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4 mb-6">
          <div>
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Catalog Categories</h1>
            <p className="text-[var(--color-text-secondary)]">
              Curate category groupings for billers. Filter by market and keep the catalog consistent.
            </p>
          </div>
          <Button variant="outline" onClick={loadData} disabled={loading}>
            <RefreshCw className={`w-4 h-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>

        <Card className="mb-6">
          <CardContent className="p-4">
            <div className="flex flex-col md:flex-row gap-4 md:items-center">
              <div className="flex-1">
                <label className="block text-sm font-medium text-[var(--color-text-secondary)] mb-1">
                  Country
                </label>
                <select
                  value={countryFilter}
                  onChange={(event) => setCountryFilter(event.target.value)}
                  className="w-full px-3 py-2 border border-[var(--color-border)] rounded-lg text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                >
                  <option value="">All Countries</option>
                  {countries.map((country) => (
                    <option key={country.countryCode} value={country.countryCode}>
                      {country.name} ({country.countryCode})
                    </option>
                  ))}
                </select>
              </div>
              <div className="flex-1">
                <label className="block text-sm font-medium text-[var(--color-text-secondary)] mb-1">
                  Search
                </label>
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)]" />
                  <input
                    type="text"
                    value={search}
                    onChange={(event) => setSearch(event.target.value)}
                    placeholder="Category name"
                    className="w-full pl-10 pr-4 py-2 border border-[var(--color-border)] rounded-lg text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                  />
                </div>
              </div>
              <div className="flex items-end">
                <Badge variant="secondary">{filteredCategories.length} categories</Badge>
              </div>
            </div>
          </CardContent>
        </Card>

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
          <CardContent className="p-0">
            {loading ? (
              <div className="p-12 text-center">
                <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                <p className="text-sm text-[var(--color-text-secondary)]">Loading categories...</p>
              </div>
            ) : filteredCategories.length === 0 ? (
              <div className="p-12 text-center">
                <Layers className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
                <p className="text-[var(--color-text-primary)] font-medium mb-1">No categories found</p>
                <p className="text-sm text-[var(--color-text-secondary)]">Try adjusting your filters.</p>
              </div>
            ) : (
              <div className="grid gap-4 p-6 md:grid-cols-2 xl:grid-cols-3">
                {filteredCategories.map((category) => (
                  <div
                    key={category.categoryId}
                    className="border border-[var(--color-border-light)] rounded-xl p-4 bg-[var(--color-surface)] shadow-sm"
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
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
