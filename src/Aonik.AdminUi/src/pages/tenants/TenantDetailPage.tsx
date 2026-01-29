import { useState, useEffect, useCallback } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  ArrowLeft,
  Save,
  AlertCircle,
  X,
  RefreshCw,
  Building2,
  CheckCircle,
  Clock,
  XCircle,
  Play,
  Pause,
  Settings,
  Activity,
  Globe,
  DollarSign,
  Calendar,
  User,
} from 'lucide-react';
import { tenantService } from '@/services/tenantService';
import { catalogService } from '@/services/catalogService';
import type { TenantHealthResult } from '@/services/tenantService';
import type { Tenant, UpdateTenantRequest, TenantStatus, TenantEnvironment } from '@/types';

const statusConfig: Record<TenantStatus, { icon: React.ElementType; color: string; bgColor: string; label: string }> = {
  Active: { icon: CheckCircle, color: 'text-[var(--color-success)]', bgColor: 'bg-[var(--color-success-light)]', label: 'Active' },
  Provisioning: { icon: Clock, color: 'text-[var(--color-warning)]', bgColor: 'bg-[var(--color-warning-light)]', label: 'Provisioning' },
  Deactivated: { icon: XCircle, color: 'text-[var(--color-text-tertiary)]', bgColor: 'bg-[var(--color-surface-inset)]', label: 'Deactivated' },
  Suspended: { icon: AlertCircle, color: 'text-[var(--color-error)]', bgColor: 'bg-[var(--color-error-light)]', label: 'Suspended' },
};

const environmentColors: Record<string, string> = {
  Dev: 'bg-[var(--color-info-light)] text-[var(--color-info)]',
  Test: 'bg-[var(--color-brand-secondary-light)] text-[var(--color-brand-secondary)]',
  Staging: 'bg-[var(--color-pending-light)] text-[var(--color-pending)]',
  Prod: 'bg-[var(--color-success-light)] text-[var(--color-success)]',
};

const environments: { value: TenantEnvironment; label: string }[] = [
  { value: 'Dev', label: 'Development' },
  { value: 'Test', label: 'Test' },
  { value: 'Staging', label: 'Staging' },
  { value: 'Prod', label: 'Production' },
];

const currencies = [] as { code: string; name: string }[];

const countries = [
  { code: 'US', name: 'United States' },
  { code: 'GB', name: 'United Kingdom' },
  { code: 'DE', name: 'Germany' },
  { code: 'FR', name: 'France' },
  { code: 'CA', name: 'Canada' },
  { code: 'AU', name: 'Australia' },
  { code: 'JP', name: 'Japan' },
  { code: 'CN', name: 'China' },
  { code: 'IN', name: 'India' },
  { code: 'BR', name: 'Brazil' },
  { code: 'MX', name: 'Mexico' },
  { code: 'ES', name: 'Spain' },
  { code: 'IT', name: 'Italy' },
  { code: 'NL', name: 'Netherlands' },
  { code: 'SE', name: 'Sweden' },
  { code: 'CH', name: 'Switzerland' },
  { code: 'SG', name: 'Singapore' },
  { code: 'NZ', name: 'New Zealand' },
];

export function TenantDetailPage() {
  const navigate = useNavigate();
  const { id: tenantId } = useParams<{ id: string }>();
  
  const [tenant, setTenant] = useState<Tenant | null>(null);
  const [health, setHealth] = useState<TenantHealthResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  
  const [formData, setFormData] = useState<UpdateTenantRequest>({});
  const [errors, setErrors] = useState<Partial<Record<keyof UpdateTenantRequest, string>>>({});
  const [currencyOptions, setCurrencyOptions] = useState<{ code: string; name: string }[]>(currencies);

  useEffect(() => {
    let active = true;
    const loadCurrencies = async () => {
      try {
        const response = await catalogService.getCurrencies();
        if (!active) return;
        setCurrencyOptions(response.currencies ?? []);
      } catch {
        // keep defaults
      }
    };
    loadCurrencies();
    return () => {
      active = false;
    };
  }, []);

  const loadTenant = useCallback(async () => {
    if (!tenantId) return;
    
    setLoading(true);
    setError(null);
    try {
      const data = await tenantService.get(tenantId);
      setTenant(data);
      setFormData({
        name: data.name,
        environment: data.environment,
        defaultCurrency: data.defaultCurrency,
        supportedCountries: [...data.supportedCountries],
        supportedCurrencies: [...(data.supportedCurrencies ?? [data.defaultCurrency])],
      });
    } catch (err: unknown) {
      console.error('Failed to load tenant:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load tenant. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [tenantId]);

  const loadHealth = useCallback(async () => {
    if (!tenantId) return;
    
    try {
      const data = await tenantService.getHealth(tenantId);
      setHealth(data);
    } catch (err) {
      console.error('Failed to load health:', err);
      // Health check failure is not critical, don't set error
    }
  }, [tenantId]);

  useEffect(() => {
    loadTenant();
    loadHealth();
  }, [loadTenant, loadHealth]);

  const validateForm = (): boolean => {
    const newErrors: Partial<Record<keyof UpdateTenantRequest, string>> = {};

    if (formData.name !== undefined && formData.name.trim() === '') {
      newErrors.name = 'Tenant name is required';
    } else if (formData.name !== undefined && formData.name.length < 3) {
      newErrors.name = 'Tenant name must be at least 3 characters';
    }

    if (formData.supportedCountries !== undefined && formData.supportedCountries.length === 0) {
      newErrors.supportedCountries = 'At least one country must be selected';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = async () => {
    if (!tenantId || !validateForm()) return;

    setSaving(true);
    setError(null);

    try {
      const updated = await tenantService.update(tenantId, formData);
      setTenant(updated);
      setIsEditing(false);
    } catch (err: unknown) {
      console.error('Failed to update tenant:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to update tenant. Please try again.');
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = () => {
    if (tenant) {
      setFormData({
        name: tenant.name,
        environment: tenant.environment,
        defaultCurrency: tenant.defaultCurrency,
        supportedCountries: [...tenant.supportedCountries],
      });
    }
    setErrors({});
    setIsEditing(false);
  };

  const handleActivate = async () => {
    if (!tenantId) return;
    setActionLoading('activate');
    try {
      await tenantService.activate(tenantId);
      await loadTenant();
    } catch (err: unknown) {
      console.error('Failed to activate tenant:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to activate tenant. Please try again.');
    } finally {
      setActionLoading(null);
    }
  };

  const handleDeactivate = async () => {
    if (!tenantId) return;
    setActionLoading('deactivate');
    try {
      await tenantService.deactivate(tenantId);
      await loadTenant();
    } catch (err: unknown) {
      console.error('Failed to deactivate tenant:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to deactivate tenant. Please try again.');
    } finally {
      setActionLoading(null);
    }
  };

  const handleProvision = async () => {
    if (!tenantId) return;
    setActionLoading('provision');
    try {
      await tenantService.provision(tenantId);
      await loadTenant();
      await loadHealth();
    } catch (err: unknown) {
      console.error('Failed to provision tenant:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to provision tenant. Please try again.');
    } finally {
      setActionLoading(null);
    }
  };

  const toggleCountry = (code: string) => {
    setFormData(prev => ({
      ...prev,
      supportedCountries: prev.supportedCountries?.includes(code)
        ? prev.supportedCountries.filter(c => c !== code)
        : [...(prev.supportedCountries || []), code],
    }));
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return '-';
    return new Date(dateString).toLocaleString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  if (loading) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center">
          <RefreshCw className="w-8 h-8 animate-spin mx-auto mb-3 text-[var(--color-brand-primary)]" />
          <p className="text-[var(--color-text-secondary)]">Loading tenant...</p>
        </div>
      </div>
    );
  }

  if (!tenant) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center">
          <AlertCircle className="w-12 h-12 mx-auto mb-3 text-[var(--color-error)]" />
          <h2 className="text-xl font-semibold text-[var(--color-text-primary)] mb-2">Tenant Not Found</h2>
          <p className="text-[var(--color-text-secondary)] mb-4">The tenant you're looking for doesn't exist or has been deleted.</p>
          <Button onClick={() => navigate('/tenants')}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back to Tenants
          </Button>
        </div>
      </div>
    );
  }

  const StatusIcon = statusConfig[tenant.status]?.icon || AlertCircle;
  const statusColor = statusConfig[tenant.status]?.color || 'text-gray-500';
  const statusBgColor = statusConfig[tenant.status]?.bgColor || 'bg-gray-100';
  const envColor = environmentColors[tenant.environment] || 'bg-gray-100 text-gray-700';

  return (
    <div className="flex-1 overflow-auto">
      <div className="p-6">
        {/* Page Header */}
        <div className="flex items-center gap-4 mb-6">
          <Button variant="ghost" size="sm" onClick={() => navigate('/tenants')}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back to Tenants
          </Button>
        </div>

        {/* Tenant Header */}
        <div className="flex items-start justify-between mb-6">
          <div className="flex items-center gap-4">
            <div className="w-16 h-16 rounded-md bg-[var(--color-brand-primary-light)] flex items-center justify-center">
              <Building2 className="w-8 h-8 text-[var(--color-brand-primary)]" />
            </div>
            <div>
              <div className="flex items-center gap-3 mb-1">
                <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">{tenant.name}</h1>
                <Badge className={`${envColor} font-medium`}>{tenant.environment}</Badge>
                <div className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium ${statusBgColor} ${statusColor}`}>
                  <StatusIcon className="w-3.5 h-3.5" />
                  {tenant.status}
                </div>
              </div>
              <p className="text-sm text-[var(--color-text-tertiary)] font-mono">{tenant.tenantId}</p>
            </div>
          </div>
          
          {/* Actions */}
          <div className="flex items-center gap-2">
            {!isEditing && (
              <>
                {tenant.status === 'Deactivated' && (
                  <Button
                    variant="outline"
                    onClick={handleActivate}
                    disabled={actionLoading !== null}
                  >
                    {actionLoading === 'activate' ? (
                      <RefreshCw className="w-4 h-4 mr-2 animate-spin" />
                    ) : (
                      <Play className="w-4 h-4 mr-2" />
                    )}
                    Activate
                  </Button>
                )}
                {tenant.status === 'Active' && (
                  <Button
                    variant="outline"
                    onClick={handleDeactivate}
                    disabled={actionLoading !== null}
                  >
                    {actionLoading === 'deactivate' ? (
                      <RefreshCw className="w-4 h-4 mr-2 animate-spin" />
                    ) : (
                      <Pause className="w-4 h-4 mr-2" />
                    )}
                    Deactivate
                  </Button>
                )}
                {tenant.status === 'Provisioning' && (
                  <Button
                    variant="outline"
                    onClick={handleProvision}
                    disabled={actionLoading !== null}
                  >
                    {actionLoading === 'provision' ? (
                      <RefreshCw className="w-4 h-4 mr-2 animate-spin" />
                    ) : (
                      <Settings className="w-4 h-4 mr-2" />
                    )}
                    Provision
                  </Button>
                )}
                <Button onClick={() => setIsEditing(true)}>
                  <Settings className="w-4 h-4 mr-2" />
                  Edit
                </Button>
              </>
            )}
            {isEditing && (
              <>
                <Button variant="outline" onClick={handleCancel} disabled={saving}>
                  Cancel
                </Button>
                <Button onClick={handleSave} disabled={saving}>
                  {saving ? (
                    <>
                      <RefreshCw className="w-4 h-4 mr-2 animate-spin" />
                      Saving...
                    </>
                  ) : (
                    <>
                      <Save className="w-4 h-4 mr-2" />
                      Save Changes
                    </>
                  )}
                </Button>
              </>
            )}
          </div>
        </div>

        {/* Error Alert */}
        {error && (
          <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
            <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
              <AlertCircle className="w-5 h-5 flex-shrink-0" />
              <span className="flex-1">{error}</span>
              <Button variant="ghost" size="sm" onClick={() => setError(null)}>
                <X className="w-4 h-4" />
              </Button>
            </CardContent>
          </Card>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Main Content */}
          <div className="lg:col-span-2 space-y-6">
            {/* Basic Information */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Building2 className="w-5 h-5" />
                  Basic Information
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {/* Tenant Name */}
                <div>
                  <label className="block text-sm font-medium text-[var(--color-text-secondary)] mb-1">
                    Tenant Name
                  </label>
                  {isEditing ? (
                    <>
                      <input
                        type="text"
                        value={formData.name || ''}
                        onChange={(e) => setFormData(prev => ({ ...prev, name: e.target.value }))}
                        className={`w-full px-4 py-2 border rounded-md text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent ${
                          errors.name ? 'border-red-300' : 'border-[var(--color-border)]'
                        }`}
                      />
                      {errors.name && (
                        <p className="mt-1 text-sm text-[var(--color-error)]">{errors.name}</p>
                      )}
                    </>
                  ) : (
                    <p className="text-[var(--color-text-primary)]">{tenant.name}</p>
                  )}
                </div>

                {/* Environment */}
                <div>
                  <label className="block text-sm font-medium text-[var(--color-text-secondary)] mb-1">
                    Environment
                  </label>
                  {isEditing ? (
                    <select
                      value={formData.environment || ''}
                      onChange={(e) => setFormData(prev => ({ ...prev, environment: e.target.value as TenantEnvironment }))}
                      className="w-full px-4 py-2 border border-[var(--color-border)] rounded-md text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                    >
                      {environments.map(env => (
                        <option key={env.value} value={env.value}>{env.label}</option>
                      ))}
                    </select>
                  ) : (
                    <Badge className={`${envColor} font-medium`}>{tenant.environment}</Badge>
                  )}
                </div>
              </CardContent>
            </Card>

            {/* Regional Settings */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Globe className="w-5 h-5" />
                  Regional Settings
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {/* Default Currency */}
                <div>
                  <label className="block text-sm font-medium text-[var(--color-text-secondary)] mb-1">
                    Default Currency
                  </label>
                  {isEditing ? (
                    <select
                      value={formData.defaultCurrency || ''}
                      onChange={(e) => setFormData(prev => ({ ...prev, defaultCurrency: e.target.value, supportedCurrencies: [e.target.value] }))}
                      className="w-full px-4 py-2 border border-[var(--color-border)] rounded-md text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
                    >
                      {currencyOptions.map(currency => (
                        <option key={currency.code} value={currency.code}>
                          {currency.code} - {currency.name}
                        </option>
                      ))}
                    </select>
                  ) : (
                    <div className="flex items-center gap-2">
                      <DollarSign className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                      <span className="text-[var(--color-text-primary)]">{tenant.defaultCurrency}</span>
                    </div>
                  )}
                </div>

                {/* Supported Countries */}
                <div>
                  <label className="block text-sm font-medium text-[var(--color-text-secondary)] mb-2">
                    Supported Countries
                  </label>
                  {isEditing ? (
                    <>
                      <div className={`border rounded-md p-3 bg-[var(--color-surface-inset)] ${
                        errors.supportedCountries ? 'border-red-300' : 'border-[var(--color-border)]'
                      }`}>
                        <div className="flex flex-wrap gap-2">
                          {countries.map(country => (
                            <button
                              key={country.code}
                              type="button"
                              onClick={() => toggleCountry(country.code)}
                              className={`px-3 py-1.5 rounded-md text-sm font-medium transition-colors ${
                                formData.supportedCountries?.includes(country.code)
                                  ? 'bg-[var(--color-brand-primary)] text-white'
                                  : 'bg-[var(--color-background)] text-[var(--color-text-secondary)] hover:bg-[var(--color-border-light)]'
                              }`}
                            >
                              {country.code}
                            </button>
                          ))}
                        </div>
                      </div>
                      {errors.supportedCountries && (
                        <p className="mt-1 text-sm text-[var(--color-error)]">{errors.supportedCountries}</p>
                      )}
                    </>
                  ) : (
                    <div className="flex flex-wrap gap-2">
                      {tenant.supportedCountries.map((code, idx) => {
                        const country = countries.find(c => c.code === code);
                        return (
                          <Badge key={`${code}-${idx}`} variant="secondary">
                            {country ? `${code} - ${country.name}` : code}
                          </Badge>
                        );
                      })}
                    </div>
                  )}
                </div>
              </CardContent>
            </Card>
          </div>

          {/* Sidebar */}
          <div className="space-y-6">
            {/* Health Status */}
            <Card>
              <CardHeader className="pb-3">
                <div className="flex items-center justify-between">
                  <CardTitle className="flex items-center gap-2 text-base">
                    <Activity className="w-5 h-5" />
                    Health Status
                  </CardTitle>
                  <Button variant="ghost" size="sm" onClick={loadHealth}>
                    <RefreshCw className="w-4 h-4" />
                  </Button>
                </div>
              </CardHeader>
              <CardContent>
                {health ? (
                  <div className="space-y-3">
                    <div className={`flex items-center gap-2 p-3 rounded-md ${health.isHealthy ? 'bg-[var(--color-success-light)]' : 'bg-[var(--color-error-light)]'}`}>
                      {health.isHealthy ? (
                        <CheckCircle className="w-5 h-5 text-[var(--color-success)]" />
                      ) : (
                        <XCircle className="w-5 h-5 text-[var(--color-error)]" />
                      )}
                      <span className={`font-medium ${health.isHealthy ? 'text-[var(--color-success)]' : 'text-[var(--color-error)]'}`}>
                        {health.isHealthy ? 'All Systems Operational' : 'Issues Detected'}
                      </span>
                    </div>
                    {health.checks && health.checks.length > 0 ? (
                      <div className="space-y-2">
                        {health.checks.map((check, idx) => (
                          <div key={`${check.name}-${idx}`} className="flex items-center justify-between text-sm">
                            <span className="text-[var(--color-text-secondary)]">{check.name}</span>
                            {check.status === 'Passed' ? (
                              <CheckCircle className="w-4 h-4 text-[var(--color-success)]" />
                            ) : (
                              <XCircle className="w-4 h-4 text-[var(--color-error)]" />
                            )}
                          </div>
                        ))}
                      </div>
                    ) : (
                      <p className="text-sm text-[var(--color-text-tertiary)]">
                        No checks reported
                      </p>
                    )}
                  </div>
                ) : (
                  <p className="text-sm text-[var(--color-text-tertiary)]">
                    Health check not available
                  </p>
                )}
              </CardContent>
            </Card>

            {/* Metadata */}
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="flex items-center gap-2 text-base">
                  <Calendar className="w-5 h-5" />
                  Metadata
                </CardTitle>
              </CardHeader>
              <CardContent>
                <dl className="space-y-3">
                  <div>
                    <dt className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Created</dt>
                    <dd className="text-sm text-[var(--color-text-primary)]">{formatDate(tenant.createdAt)}</dd>
                  </div>
                  {tenant.createdBy && (
                    <div>
                      <dt className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Created By</dt>
                      <dd className="text-sm text-[var(--color-text-primary)] flex items-center gap-1">
                        <User className="w-3.5 h-3.5" />
                        {tenant.createdBy}
                      </dd>
                    </div>
                  )}
                  <div>
                    <dt className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Last Updated</dt>
                    <dd className="text-sm text-[var(--color-text-primary)]">{formatDate(tenant.updatedAt)}</dd>
                  </div>
                  {tenant.updatedBy && (
                    <div>
                      <dt className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Updated By</dt>
                      <dd className="text-sm text-[var(--color-text-primary)] flex items-center gap-1">
                        <User className="w-3.5 h-3.5" />
                        {tenant.updatedBy}
                      </dd>
                    </div>
                  )}
                </dl>
              </CardContent>
            </Card>
          </div>
        </div>
      </div>
    </div>
  );
}
