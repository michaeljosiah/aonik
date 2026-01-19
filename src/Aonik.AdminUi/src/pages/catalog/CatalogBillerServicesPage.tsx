import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  ArrowLeft,
  RefreshCw,
  AlertCircle,
  Wrench,
  ArrowUpRight,
} from 'lucide-react';
import { catalogService } from '@/services/catalogService';
import type { CatalogBillerServiceItem } from '@/types';

export function CatalogBillerServicesPage() {
  const navigate = useNavigate();
  const { billerId } = useParams<{ billerId: string }>();
  const [services, setServices] = useState<CatalogBillerServiceItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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
    }
  }, [billerId]);

  useEffect(() => {
    loadServices();
  }, [loadServices]);

  return (
    <div className="flex-1 overflow-auto">
      <div className="p-6">
        <div className="flex items-center gap-4 mb-6">
          <Button variant="ghost" size="sm" onClick={() => navigate(`/catalog/billers/${billerId}`)}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back to Biller
          </Button>
        </div>

        <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4 mb-6">
          <div>
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Biller Services</h1>
            <p className="text-[var(--color-text-secondary)]">Review available services and their limits.</p>
          </div>
          <Button variant="outline" onClick={loadServices} disabled={loading}>
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
          <CardContent className="p-0">
            {loading ? (
              <div className="p-12 text-center">
                <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                <p className="text-sm text-[var(--color-text-secondary)]">Loading services...</p>
              </div>
            ) : services.length === 0 ? (
              <div className="p-12 text-center">
                <Wrench className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
                <p className="text-[var(--color-text-primary)] font-medium mb-1">No services found</p>
                <p className="text-sm text-[var(--color-text-secondary)]">No services are configured for this biller.</p>
              </div>
            ) : (
              <div className="divide-y divide-[var(--color-border-light)]">
                {services.map((service) => (
                  <div key={service.serviceId} className="px-6 py-5 flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
                    <div>
                      <div className="flex items-center gap-2 flex-wrap">
                        <h3 className="text-lg font-semibold text-[var(--color-text-primary)]">{service.name}</h3>
                        {!service.isActive && (
                          <Badge variant="outline" className="text-[var(--color-text-tertiary)]">
                            Inactive
                          </Badge>
                        )}
                        {service.requiresValidation && (
                          <Badge className="bg-[var(--color-pending-light)] text-[var(--color-pending)]">Validation</Badge>
                        )}
                      </div>
                      <p className="text-sm text-[var(--color-text-secondary)]">
                        {service.type} • {service.currency}
                      </p>
                      <p className="text-xs text-[var(--color-text-tertiary)] mt-1">
                        Limits: {service.minAmount ?? '—'} to {service.maxAmount ?? '—'}
                      </p>
                    </div>
                    <div className="flex items-center gap-3">
                      <Badge variant="outline" className="font-mono">
                        {service.serviceId.slice(0, 8)}
                      </Badge>
                      <Button
                        variant="outline"
                        onClick={() => navigate(`/catalog/billers/${billerId}/services/${service.serviceId}`)}
                      >
                        View
                        <ArrowUpRight className="w-4 h-4 ml-2" />
                      </Button>
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
