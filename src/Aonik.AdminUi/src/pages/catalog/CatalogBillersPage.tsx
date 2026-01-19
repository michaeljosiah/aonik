import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  RefreshCw,
  AlertCircle,
  Building2,
  Search,
  ChevronLeft,
  ChevronRight,
  ArrowUpRight,
} from 'lucide-react';
import { catalogService } from '@/services/catalogService';
import type {
  CatalogBillerSummaryItem,
  CatalogBillerCategoryItem,
  CatalogCountryItem,
} from '@/types';

const pageSize = 12;

export function CatalogBillersPage() {
  const navigate = useNavigate();
  const [billers, setBillers] = useState<CatalogBillerSummaryItem[]>([]);
  const [categories, setCategories] = useState<CatalogBillerCategoryItem[]>([]);
  const [countries, setCountries] = useState<CatalogCountryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [countryFilter, setCountryFilter] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [countriesResponse, categoriesResponse, billersResponse] = await Promise.all([
        catalogService.getCountries(false),
        catalogService.getCategories(countryFilter || undefined),
        catalogService.getBillers({
          countryCode: countryFilter || undefined,
          categoryId: categoryFilter || undefined,
          search: search || undefined,
          page,
          pageSize,
        }),
      ]);

      setCountries(countriesResponse.countries);
      setCategories(categoriesResponse.categories);
      setBillers(billersResponse.billers);
      setTotalPages(Math.max(billersResponse.pagination.totalPages || 1, 1));
    } catch (err: unknown) {
      console.error('Failed to load billers:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load catalog billers.');
    } finally {
      setLoading(false);
    }
  }, [countryFilter, categoryFilter, search, page]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  useEffect(() => {
    setPage(1);
  }, [countryFilter, categoryFilter, search]);

  const categoryMap = useMemo(() => {
    return new Map(categories.map((category) => [category.categoryId, category]));
  }, [categories]);

  const countryMap = useMemo(() => {
    return new Map(countries.map((country) => [country.countryCode, country]));
  }, [countries]);

  const activeFilters = useMemo(() => {
    return [countryFilter, categoryFilter, search].filter(Boolean).length;
  }, [countryFilter, categoryFilter, search]);

  return (
    <div className="flex-1 overflow-auto">
      <div className="p-6">
        <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4 mb-6">
          <div>
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Catalog Billers</h1>
            <p className="text-[var(--color-text-secondary)]">
              Review billers available for collections. Explore services and correspondent mapping details.
            </p>
          </div>
          <Button variant="outline" onClick={loadData} disabled={loading}>
            <RefreshCw className={`w-4 h-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>

        <Card className="mb-6">
          <CardContent className="p-4">
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
              <div>
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
              <div>
                <label className="block text-sm font-medium text-[var(--color-text-secondary)] mb-1">
                  Category
                </label>
                <select
                  value={categoryFilter}
                  onChange={(event) => setCategoryFilter(event.target.value)}
                  className="w-full px-3 py-2 border border-[var(--color-border)] rounded-lg text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                >
                  <option value="">All Categories</option>
                  {categories.map((category) => (
                    <option key={category.categoryId} value={category.categoryId}>
                      {category.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="lg:col-span-2">
                <label className="block text-sm font-medium text-[var(--color-text-secondary)] mb-1">
                  Search
                </label>
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)]" />
                  <input
                    type="text"
                    value={search}
                    onChange={(event) => setSearch(event.target.value)}
                    placeholder="Search billers"
                    className="w-full pl-10 pr-4 py-2 border border-[var(--color-border)] rounded-lg text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                  />
                </div>
              </div>
            </div>
            <div className="mt-4 flex flex-wrap items-center gap-3">
              <Badge variant="secondary">{billers.length} billers</Badge>
              {activeFilters > 0 && (
                <Badge variant="outline">{activeFilters} filters applied</Badge>
              )}
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
                <p className="text-sm text-[var(--color-text-secondary)]">Loading billers...</p>
              </div>
            ) : billers.length === 0 ? (
              <div className="p-12 text-center">
                <Building2 className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
                <p className="text-[var(--color-text-primary)] font-medium mb-1">No billers found</p>
                <p className="text-sm text-[var(--color-text-secondary)]">Try adjusting your filters.</p>
              </div>
            ) : (
              <div className="divide-y divide-[var(--color-border-light)]">
                {billers.map((biller) => {
                  const category = categoryMap.get(biller.categoryId);
                  const country = countryMap.get(biller.countryCode);
                  return (
                    <div
                      key={biller.billerId}
                      className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4 px-6 py-5 hover:bg-[var(--color-background)]"
                    >
                      <div className="flex items-start gap-4">
                        <div className="w-12 h-12 rounded-xl bg-[var(--color-brand-primary-light)] flex items-center justify-center">
                          <Building2 className="w-6 h-6 text-[var(--color-brand-primary)]" />
                        </div>
                        <div>
                          <div className="flex items-center gap-2 flex-wrap">
                            <h3 className="text-lg font-semibold text-[var(--color-text-primary)]">{biller.name}</h3>
                            {!biller.isActive && (
                              <Badge variant="outline" className="text-[var(--color-text-tertiary)]">
                                Inactive
                              </Badge>
                            )}
                            {biller.isFeatured && (
                              <Badge className="bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)]">
                                Featured
                              </Badge>
                            )}
                          </div>
                          <p className="text-sm text-[var(--color-text-secondary)]">
                            {category?.name ?? 'Uncategorized'} • {country?.name ?? biller.countryCode}
                          </p>
                          <div className="text-xs text-[var(--color-text-tertiary)] mt-1">
                            Correspondent: {biller.correspondentPartnerId ?? 'Not assigned'}
                          </div>
                        </div>
                      </div>
                      <div className="flex items-center gap-3">
                        <Badge variant="outline" className="font-mono">
                          {biller.billerId.slice(0, 8)}
                        </Badge>
                        <Button
                          variant="outline"
                          onClick={() => navigate(`/catalog/billers/${biller.billerId}`)}
                        >
                          View
                          <ArrowUpRight className="w-4 h-4 ml-2" />
                        </Button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
            <div className="flex items-center justify-between px-6 py-4 border-t border-[var(--color-border-light)]">
              <p className="text-sm text-[var(--color-text-secondary)]">
                Page {page} of {totalPages}
              </p>
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page <= 1}
                  onClick={() => setPage((prev) => Math.max(prev - 1, 1))}
                >
                  <ChevronLeft className="w-4 h-4" />
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page >= totalPages}
                  onClick={() => setPage((prev) => Math.min(prev + 1, totalPages))}
                >
                  <ChevronRight className="w-4 h-4" />
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
