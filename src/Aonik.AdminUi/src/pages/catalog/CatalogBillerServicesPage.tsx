import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  RefreshCw,
  AlertCircle,
  Wrench,
  ArrowUpRight,
  Search,
  } from 'lucide-react';
import { catalogService } from '@/services/catalogService';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import type { CatalogBillerServiceItem } from '@/types';

export function CatalogBillerServicesPage() {
  const navigate = useNavigate();
  const { billerId } = useParams<{ billerId: string }>();
  const [services, setServices] = useState<CatalogBillerServiceItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  const loadServices = useCallback(async () => {
    if (!billerId) return;
    setLoading(true);
    setError(null);
    try {
      const response = await catalogService.getBillerServices(billerId);
      setServices(response.services);
    } catch (err: unknown) {
      console.error('Failed to load services:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load biller services.');
    } finally {
      setLoading(false);
      setInitialLoad(false);
    }
  }, [billerId]);

  useEffect(() => {
    loadServices();
  }, [loadServices]);

  const filteredServices = useMemo(() => {
    if (!search.trim()) {
      return services;
    }

    const lowered = search.trim().toLowerCase();
    return services.filter((service) => service.name.toLowerCase().includes(lowered));
  }, [services, search]);

  if (initialLoad) {
    return <PageLoadingScreen message="Loading services" />;
  }

  return (
    <div className="h-full overflow-auto p-6">

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Biller Services</h1>
          <p className="text-[var(--color-text-secondary)]">Review available services and their limits.</p>
        </div>
        <Button variant="outline" onClick={loadServices} disabled={loading} className="rounded-sm">
          <RefreshCw className={`w-4 h-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span className="flex-1">{error}</span>
            <Button variant="outline" size="sm" onClick={loadServices}>
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-4">
          <div className="flex items-center justify-between gap-4">
            <div className="relative w-96 max-w-full">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-tertiary)]" />
              <input
                type="text"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search for services"
                className="w-full pl-10 pr-4 py-2 text-sm rounded-sm border border-[var(--color-border)] bg-transparent text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)] focus:border-[var(--color-brand-primary)]"
              />
            </div>

            <Badge variant="secondary">{filteredServices.length} services</Badge>
          </div>

          <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]/50">
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">Service</th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">Type</th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">Currency</th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">Flags</th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">Limits</th>
                    <th className="text-right px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr>
                      <td colSpan={6} className="px-4 py-12 text-center">
                        <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                        <p className="text-sm text-[var(--color-text-secondary)]">Loading services...</p>
                      </td>
                    </tr>
                  ) : filteredServices.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="px-4 py-12 text-center">
                        <div className="mb-3 flex justify-center text-[var(--color-text-tertiary)]">
                          <Wrench className="w-12 h-12" />
                        </div>
                        <p className="text-[var(--color-text-primary)] font-medium mb-1">No services found</p>
                        <p className="text-sm text-[var(--color-text-secondary)]">
                          {search ? 'Try adjusting your search.' : 'No services are configured for this biller.'}
                        </p>
                      </td>
                    </tr>
                  ) : (
                    filteredServices.map((service) => (
                      <tr
                        key={service.serviceId}
                        className="border-b border-[var(--color-border-light)] hover:bg-[var(--color-surface-inset)] transition-colors cursor-pointer"
                        onClick={() => navigate(`/catalog/billers/${billerId}/services/${service.serviceId}`)}
                      >
                        <td className="px-4 py-3">
                          <p className="font-medium text-[var(--color-text-primary)]">{service.name}</p>
                          <p className="text-xs text-[var(--color-text-tertiary)] font-mono">{service.serviceId.slice(0, 8)}...</p>
                        </td>
                        <td className="px-4 py-3">
                          <span className="text-sm text-[var(--color-text-secondary)]">{service.type}</span>
                        </td>
                        <td className="px-4 py-3">
                          <span className="text-sm text-[var(--color-text-secondary)]">{service.currency}</span>
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex flex-wrap gap-2">
                            {!service.isActive && (
                              <Badge variant="outline" className="text-[var(--color-text-tertiary)]">
                                Inactive
                              </Badge>
                            )}
                            {service.requiresValidation && (
                              <Badge className="bg-[var(--color-pending-light)] text-[var(--color-pending)]">
                                Validation
                              </Badge>
                            )}
                            {service.supportsPartialPayment && (
                              <Badge className="bg-[var(--color-info-light)] text-[var(--color-info)]">
                                Partial
                              </Badge>
                            )}
                          </div>
                        </td>
                        <td className="px-4 py-3">
                          <span className="text-sm text-[var(--color-text-secondary)]">
                            {service.minAmount ?? '—'} to {service.maxAmount ?? '—'}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right">
                          <Button
                            variant="outline"
                            className="rounded-sm"
                            onClick={(e) => {
                              e.stopPropagation();
                              navigate(`/catalog/billers/${billerId}/services/${service.serviceId}`);
                            }}
                          >
                            View
                            <ArrowUpRight className="w-4 h-4 ml-2" />
                          </Button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
