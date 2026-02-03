import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import {
  RefreshCw,
  AlertCircle,
  Building2,
  Search,
  ArrowUpRight,
} from 'lucide-react';
import { catalogService } from '@/services/catalogService';
import type {
  CatalogBillerSummaryItem,
  CatalogBillerCategoryItem,
  CatalogCountryItem,
} from '@/types';
import { DataTablePagination } from '@/components/ui/data-table';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { CountrySelect } from '@/components/ui/country-select';

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
  const [totalCount, setTotalCount] = useState(0);
  const [pageSize, setPageSize] = useState(12);

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
      setTotalCount(billersResponse.pagination.totalCount || 0);
    } catch (err: unknown) {
      console.error('Failed to load billers:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load catalog billers.');
    } finally {
      setLoading(false);
    }
  }, [countryFilter, categoryFilter, search, page, pageSize]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  useEffect(() => {
    setPage(1);
  }, [countryFilter, categoryFilter, search]);

  const handlePageSizeChange = (newPageSize: number) => {
    setPageSize(newPageSize);
    setPage(1);
  };

  const categoryMap = useMemo(() => {
    return new Map(categories.map((category) => [category.categoryId, category]));
  }, [categories]);

  const countryMap = useMemo(() => {
    return new Map(countries.map((country) => [country.countryCode, country]));
  }, [countries]);

  const activeFilters = useMemo(() => {
    return [countryFilter, categoryFilter, search].filter(Boolean).length;
  }, [countryFilter, categoryFilter, search]);

  const breadcrumbItems = [
    { label: 'Catalog', href: '/catalog' },
    { label: 'Billers', icon: <Building2 className="w-3.5 h-3.5" /> },
  ];

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Catalog Billers</h1>
          <p className="text-[var(--color-text-secondary)]">
            Review billers available for collections. Explore services and correspondent mapping details.
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
                  placeholder="Search for billers"
                  className="w-full pl-10 pr-4 py-2 text-sm rounded-sm border border-[var(--color-border)] bg-transparent text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)] focus:border-[var(--color-brand-primary)]"
                />
              </div>

              <div className="w-56 max-w-full">
                <CountrySelect
                  value={countryFilter}
                  onChange={setCountryFilter}
                  placeholder="Filter by country"
                  includeEmpty={true}
                  emptyLabel="All countries"
                  className="w-full"
                />
              </div>

              <Select
                value={categoryFilter || undefined}
                onValueChange={(value) => setCategoryFilter(value === '__all__' ? '' : value)}
              >
                <SelectTrigger aria-label="Filter by category" className="h-9 rounded-sm w-56">
                  <SelectValue placeholder="Filter by category" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">All categories</SelectItem>
                  {categories.map((category) => (
                    <SelectItem key={category.categoryId} value={category.categoryId}>
                      {category.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

          </div>

          <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
            {loading ? (
              <div className="p-12 text-center">
                <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                <p className="text-sm text-[var(--color-text-secondary)]">Loading billers...</p>
              </div>
            ) : billers.length === 0 ? (
              <div className="p-12 text-center">
                <div className="mb-3 flex justify-center text-[var(--color-text-tertiary)]">
                  <Building2 className="w-12 h-12" />
                </div>
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
                      className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4 px-4 py-4 hover:bg-[var(--color-surface-inset)] transition-colors"
                    >
                      <div className="flex items-start gap-4">
                        <div className="w-12 h-12 rounded-md bg-[var(--color-brand-primary-light)] flex items-center justify-center">
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
                          className="rounded-sm"
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
          </div>

          <div className="pt-4">
            <DataTablePagination
              pageNumber={page}
              pageSize={pageSize}
              totalCount={totalCount}
              onPageChange={setPage}
              onPageSizeChange={handlePageSizeChange}
              className="px-0 border-t-0"
            />
          </div>

          {activeFilters > 0 && (
            <div className="mt-3 flex flex-wrap items-center gap-3">
              <Badge variant="outline">{activeFilters} filters applied</Badge>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
