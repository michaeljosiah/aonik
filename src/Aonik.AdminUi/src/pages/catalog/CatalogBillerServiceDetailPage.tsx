import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import {
  ArrowLeft,
  RefreshCw,
  AlertCircle,
  Code2,
  CheckCircle,
  XCircle,
  ListTree,
  Building2,
  Wrench,
} from 'lucide-react';
import { catalogService } from '@/services/catalogService';
import type { CatalogBillerServiceDetailResponse } from '@/types';

export function CatalogBillerServiceDetailPage() {
  const navigate = useNavigate();
  const { billerId, serviceId } = useParams<{ billerId: string; serviceId: string }>();
  const [service, setService] = useState<CatalogBillerServiceDetailResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadService = useCallback(async () => {
    if (!billerId || !serviceId) return;
    setLoading(true);
    setError(null);
    try {
      const response = await catalogService.getBillerServiceDetail(billerId, serviceId);
      setService(response);
    } catch (err: unknown) {
      console.error('Failed to load service:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load service details.');
    } finally {
      setLoading(false);
    }
  }, [billerId, serviceId]);

  useEffect(() => {
    loadService();
  }, [loadService]);

  if (loading) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center">
          <RefreshCw className="w-8 h-8 animate-spin mx-auto mb-3 text-[var(--color-brand-primary)]" />
          <p className="text-[var(--color-text-secondary)]">Loading service...</p>
        </div>
      </div>
    );
  }

  if (!service) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center">
          <AlertCircle className="w-12 h-12 mx-auto mb-3 text-[var(--color-error)]" />
          <h2 className="text-xl font-semibold text-[var(--color-text-primary)] mb-2">Service Not Found</h2>
          <p className="text-[var(--color-text-secondary)] mb-4">We could not find that service definition.</p>
          <Button onClick={() => navigate(`/catalog/billers/${billerId}/services`)}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back to Services
          </Button>
        </div>
      </div>
    );
  }

  const handleBack = () => navigate(`/catalog/billers/${billerId}/services`);

  const breadcrumbItems = [
    { label: 'Catalog', href: '/catalog' },
    { label: 'Billers', href: '/catalog/billers', icon: <Building2 className="w-3.5 h-3.5" /> },
    { label: 'Services', href: `/catalog/billers/${billerId}/services`, icon: <Wrench className="w-3.5 h-3.5" /> },
    { label: service.name, icon: <Code2 className="w-3.5 h-3.5" /> },
  ];

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4 mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">{service.name}</h1>
          <p className="text-[var(--color-text-secondary)]">Service definition and validation schema.</p>
        </div>
        <div className="flex items-center gap-3">
          <Button variant="outline" onClick={handleBack} className="rounded-sm">
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <Button variant="outline" onClick={loadService} className="rounded-sm">
            <RefreshCw className="w-4 h-4 mr-2" />
            Refresh
          </Button>
        </div>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span className="flex-1">{error}</span>
            <Button variant="outline" size="sm" onClick={loadService}>
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <ListTree className="w-5 h-5" />
              Input Fields
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {service.fields.length === 0 ? (
              <p className="text-sm text-[var(--color-text-secondary)]">No input fields configured.</p>
            ) : (
              service.fields.map((field) => (
                <div
                  key={field.key}
                  className="border border-[var(--color-border-light)] rounded-md p-4 bg-[var(--color-surface)]"
                >
                  <div className="flex items-center justify-between mb-2">
                    <h3 className="text-lg font-semibold text-[var(--color-text-primary)]">{field.label}</h3>
                    <Badge variant={field.required ? 'default' : 'outline'}>
                      {field.required ? 'Required' : 'Optional'}
                    </Badge>
                  </div>
                  <div className="text-sm text-[var(--color-text-secondary)] mb-2">{field.key}</div>
                  <div className="flex flex-wrap gap-2 text-xs text-[var(--color-text-tertiary)]">
                    <Badge variant="secondary">{field.fieldType}</Badge>
                    {field.minLength && <Badge variant="outline">Min {field.minLength}</Badge>}
                    {field.maxLength && <Badge variant="outline">Max {field.maxLength}</Badge>}
                    {field.mask && <Badge variant="outline">Mask {field.mask}</Badge>}
                  </div>
                  {field.placeholder && (
                    <p className="text-xs text-[var(--color-text-tertiary)] mt-2">Placeholder: {field.placeholder}</p>
                  )}
                  {field.options && field.options.length > 0 && (
                    <div className="mt-2">
                      <p className="text-xs text-[var(--color-text-tertiary)] mb-1">Options</p>
                      <div className="flex flex-wrap gap-2">
                        {field.options.map((option) => (
                          <Badge key={option.value} variant="secondary">
                            {option.label}
                          </Badge>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              ))
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Code2 className="w-5 h-5" />
              Service Metadata
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-center justify-between">
              <span className="text-sm text-[var(--color-text-secondary)]">Type</span>
              <Badge variant="secondary">{service.type}</Badge>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm text-[var(--color-text-secondary)]">Currency</span>
              <Badge variant="outline">{service.currency}</Badge>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm text-[var(--color-text-secondary)]">Min Amount</span>
              <span className="text-sm text-[var(--color-text-primary)]">{service.minAmount ?? '—'}</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm text-[var(--color-text-secondary)]">Max Amount</span>
              <span className="text-sm text-[var(--color-text-primary)]">{service.maxAmount ?? '—'}</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm text-[var(--color-text-secondary)]">Partial Payment</span>
              {service.supportsPartialPayment ? (
                <CheckCircle className="w-5 h-5 text-[var(--color-success)]" />
              ) : (
                <XCircle className="w-5 h-5 text-[var(--color-text-tertiary)]" />
              )}
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm text-[var(--color-text-secondary)]">Validation Required</span>
              {service.requiresValidation ? (
                <CheckCircle className="w-5 h-5 text-[var(--color-success)]" />
              ) : (
                <XCircle className="w-5 h-5 text-[var(--color-text-tertiary)]" />
              )}
            </div>
            <div className="pt-2 border-t border-[var(--color-border-light)]">
              <p className="text-sm text-[var(--color-text-secondary)]">Validation Endpoint</p>
              <p className="text-xs text-[var(--color-text-tertiary)] break-all">
                {service.validation?.validationEndpoint ?? 'Not configured'}
              </p>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
