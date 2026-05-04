import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  ArrowLeft,
  RefreshCw,
  AlertCircle,
  Building2,
  Phone,
  Mail,
  Globe2,
  Layers,
  Link2,
  Wrench,
} from 'lucide-react';
import { catalogService } from '@/services/catalogService';
import type {
  CatalogBillerDetailResponse,
  CatalogCountryItem,
  CatalogBillerCategoryItem,
} from '@/types';

export function CatalogBillerDetailPage() {
  const navigate = useNavigate();
  const { billerId } = useParams<{ billerId: string }>();
  const [biller, setBiller] = useState<CatalogBillerDetailResponse | null>(null);
  const [countries, setCountries] = useState<CatalogCountryItem[]>([]);
  const [categories, setCategories] = useState<CatalogBillerCategoryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    if (!billerId) return;
    setLoading(true);
    setError(null);
    try {
      const [billerResponse, countriesResponse, categoriesResponse] = await Promise.all([
        catalogService.getBillerDetail(billerId),
        catalogService.getCountries(false),
        catalogService.getCategories(),
      ]);
      setBiller(billerResponse);
      setCountries(countriesResponse.countries);
      setCategories(categoriesResponse.categories);
    } catch (err: unknown) {
      console.error('Failed to load biller:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load biller details.');
    } finally {
      setLoading(false);
    }
  }, [billerId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const category = useMemo(
    () => categories.find((item) => item.categoryId === biller?.categoryId),
    [categories, biller?.categoryId]
  );

  const country = useMemo(
    () => countries.find((item) => item.countryCode === biller?.countryCode),
    [countries, biller?.countryCode]
  );

  if (loading) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center">
          <RefreshCw className="w-8 h-8 animate-spin mx-auto mb-3 text-[var(--color-brand-primary)]" />
          <p className="text-[var(--color-text-secondary)]">Loading biller...</p>
        </div>
      </div>
    );
  }

  if (!biller) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center">
          <AlertCircle className="w-12 h-12 mx-auto mb-3 text-[var(--color-error)]" />
          <h2 className="text-xl font-semibold text-[var(--color-text-primary)] mb-2">Biller Not Found</h2>
          <p className="text-[var(--color-text-secondary)] mb-4">We could not find that biller in the catalog.</p>
          <Button onClick={() => navigate('/catalog/billers')}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back to Billers
          </Button>
        </div>
      </div>
    );
  }
  return (
    <div className="h-full overflow-auto p-6">

      <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6 mb-6">
        <div className="flex items-center gap-4">
          <div className="w-12 h-12 rounded-md bg-[var(--color-brand-primary-light)] flex items-center justify-center">
            <Building2 className="w-6 h-6 text-[var(--color-brand-primary)]" />
          </div>
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">{biller.name}</h1>
              {!biller.isActive && (
                <Badge variant="outline" className="text-[var(--color-text-tertiary)]">
                  Inactive
                </Badge>
              )}
            </div>
            <p className="text-[var(--color-text-secondary)]">
              {category?.name ?? 'Uncategorized'} • {country?.name ?? biller.countryCode}
            </p>
            <p className="text-xs text-[var(--color-text-tertiary)] font-mono mt-1">{biller.billerId}</p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <Button variant="outline" onClick={loadData} className="rounded-sm">
            <RefreshCw className="w-4 h-4 mr-2" />
            Refresh
          </Button>
          <Button
            className="rounded-sm"
            onClick={() => navigate(`/catalog/billers/${biller.billerId}/services`)}
          >
            <Wrench className="w-4 h-4 mr-2" />
            View Services
          </Button>
        </div>
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

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Overview</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <p className="text-sm text-[var(--color-text-secondary)]">Description</p>
              <p className="text-[var(--color-text-primary)]">{biller.description || 'No description provided.'}</p>
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <div className="p-4 rounded-md bg-[var(--color-surface-inset)] border border-[var(--color-border-light)]">
                <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                  <Globe2 className="w-4 h-4" />
                  Country
                </div>
                <p className="text-lg font-semibold text-[var(--color-text-primary)]">
                  {country?.name ?? biller.countryCode}
                </p>
              </div>
              <div className="p-4 rounded-md bg-[var(--color-surface-inset)] border border-[var(--color-border-light)]">
                <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                  <Layers className="w-4 h-4" />
                  Category
                </div>
                <p className="text-lg font-semibold text-[var(--color-text-primary)]">
                  {category?.name ?? 'Uncategorized'}
                </p>
              </div>
              <div className="p-4 rounded-md bg-[var(--color-surface-inset)] border border-[var(--color-border-light)]">
                <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                  <Link2 className="w-4 h-4" />
                  Correspondent
                </div>
                <p className="text-lg font-semibold text-[var(--color-text-primary)]">
                  {biller.correspondentPartnerId ?? 'Not assigned'}
                </p>
              </div>
              <div className="p-4 rounded-md bg-[var(--color-surface-inset)] border border-[var(--color-border-light)]">
                <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                  <Building2 className="w-4 h-4" />
                  Services
                </div>
                <p className="text-lg font-semibold text-[var(--color-text-primary)]">{biller.serviceCount}</p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Support</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
              <Phone className="w-4 h-4" />
              Phone
            </div>
            <p className="text-[var(--color-text-primary)]">{biller.supportPhone || 'Not available'}</p>
            <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
              <Mail className="w-4 h-4" />
              Email
            </div>
            <p className="text-[var(--color-text-primary)]">{biller.supportEmail || 'Not available'}</p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
