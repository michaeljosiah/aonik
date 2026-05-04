import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { ArrowLeft, Save, AlertCircle, X } from 'lucide-react';
import { tenantService } from '@/services/tenantService';
import { catalogService } from '@/services/catalogService';
import { tenantCountryOptions } from '@/lib/tenantCountryOptions';
import type { CreateTenantRequest, TenantEnvironment } from '@/types';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

const environments: { value: TenantEnvironment; label: string }[] = [
  { value: 'Dev', label: 'Development' },
  { value: 'Test', label: 'Test' },
  { value: 'Staging', label: 'Staging' },
  { value: 'Prod', label: 'Production' },
];

const currencies = [] as { code: string; name: string }[];

export function CreateTenantPage() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [currencyOptions, setCurrencyOptions] = useState<{ code: string; name: string }[]>(currencies);

  const [formData, setFormData] = useState<CreateTenantRequest>({
    name: '',
    environment: 'Dev',
    defaultCurrency: 'USD',
    supportedCountries: ['US'],
    supportedCurrencies: ['USD'],
    ownerEmail: '',
    ownerDisplayName: '',
  });

  const [errors, setErrors] = useState<Partial<Record<keyof CreateTenantRequest, string>>>({});

  // Lightweight email check that mirrors the server-side validator.
  // Full RFC 5322 lives at the API; this is just to catch obvious
  // typos before the user clicks Submit.
  const isEmailLike = (value: string): boolean => {
    const trimmed = value.trim();
    if (!trimmed.includes('@')) return false;
    const at = trimmed.indexOf('@');
    return at > 0 && at < trimmed.length - 1;
  };

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

  const validateForm = (): boolean => {
    const newErrors: Partial<Record<keyof CreateTenantRequest, string>> = {};

    if (!formData.name.trim()) {
      newErrors.name = 'Tenant name is required';
    } else if (formData.name.length < 3) {
      newErrors.name = 'Tenant name must be at least 3 characters';
    }

    if (!formData.environment) {
      newErrors.environment = 'Environment is required';
    }

    if (!formData.defaultCurrency) {
      newErrors.defaultCurrency = 'Default currency is required';
    }

    if (formData.supportedCountries.length === 0) {
      newErrors.supportedCountries = 'At least one country must be selected';
    }

    // Owner email is the new mandatory field — without it the backend
    // refuses to create the tenant, so we surface the error inline.
    const ownerEmail = formData.ownerEmail.trim();
    if (!ownerEmail) {
      newErrors.ownerEmail = 'Owner email is required';
    } else if (!isEmailLike(ownerEmail)) {
      newErrors.ownerEmail = 'Enter a valid email address';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!validateForm()) return;

    setLoading(true);
    setError(null);

    try {
      // Trim the owner fields so the backend's strict validation
      // never sees stray whitespace, and drop an empty display name
      // so the placeholder Party falls back to the email cleanly.
      const trimmedDisplayName = (formData.ownerDisplayName ?? '').trim();
      const payload: CreateTenantRequest = {
        ...formData,
        ownerEmail: formData.ownerEmail.trim(),
        ownerDisplayName: trimmedDisplayName.length > 0 ? trimmedDisplayName : undefined,
      };
      const tenant = await tenantService.create(payload);
      navigate(`/tenants/${tenant.tenantId}`);
    } catch (err: unknown) {
      console.error('Failed to create tenant:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to create tenant. Please try again.');
    } finally {
      setLoading(false);
    }
  };


  const toggleCountry = (code: string) => {
    setFormData(prev => ({
      ...prev,
      supportedCountries: prev.supportedCountries.includes(code)
        ? prev.supportedCountries.filter(c => c !== code)
        : [...prev.supportedCountries, code],
    }));
  };

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

        <div className="mb-6">
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Create Tenant</h1>
          <p className="text-[var(--color-text-secondary)]">
            Set up a new tenant with its environment and regional settings.
          </p>
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

        <form onSubmit={handleSubmit}>
          {/* Basic Information */}
          <Card className="mb-6">
            <CardHeader>
              <CardTitle>Basic Information</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {/* Tenant Name */}
              <div>
                <label className="block text-sm font-medium text-[var(--color-text-primary)] mb-1">
                  Tenant Name *
                </label>
                <input
                  type="text"
                  value={formData.name}
                  onChange={(e) => setFormData(prev => ({ ...prev, name: e.target.value }))}
                  placeholder="e.g., Acme Corporation"
                  className={`w-full px-4 py-2 border rounded-md text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent ${
                    errors.name ? 'border-red-300' : 'border-[var(--color-border)]'
                  }`}
                />
                {errors.name && (
                  <p className="mt-1 text-sm text-[var(--color-error)]">{errors.name}</p>
                )}
              </div>

              {/* Environment */}
              <div>
                <label className="block text-sm font-medium text-[var(--color-text-primary)] mb-1">
                  Environment *
                </label>
                <Select
                  value={formData.environment}
                  onValueChange={(value) => setFormData(prev => ({ ...prev, environment: value as TenantEnvironment }))}
                >
                  <SelectTrigger
                    aria-label="Environment"
                    className={`w-full px-4 py-2 border rounded-md text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent ${
                      errors.environment ? 'border-red-300' : 'border-[var(--color-border)]'
                    }`}
                  >
                    <SelectValue placeholder="Select environment" />
                  </SelectTrigger>
                  <SelectContent>
                    {environments.map(env => (
                      <SelectItem key={env.value} value={env.value}>{env.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {errors.environment && (
                  <p className="mt-1 text-sm text-[var(--color-error)]">{errors.environment}</p>
                )}
                <p className="mt-1 text-xs text-[var(--color-text-tertiary)]">
                  Select the environment type for this tenant.
                </p>
              </div>
            </CardContent>
          </Card>

          {/* Initial Owner */}
          <Card className="mb-6">
            <CardHeader>
              <CardTitle>Initial Owner</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <p className="text-sm text-[var(--color-text-secondary)]">
                Every tenant starts with one Tenant Administrator. We
                pre-create a pending user record for this email and
                grant <code className="text-xs">TenantAdmin</code>; the
                first sign-in matching this email links to that record.
                Random authenticated users can no longer join a tenant
                by selecting it from the login picker — additional
                users must be invited from the Users page.
              </p>

              {/* Owner Email */}
              <div>
                <label className="block text-sm font-medium text-[var(--color-text-primary)] mb-1">
                  Owner Email *
                </label>
                <input
                  type="email"
                  value={formData.ownerEmail}
                  onChange={(e) => setFormData(prev => ({ ...prev, ownerEmail: e.target.value }))}
                  placeholder="e.g., owner@acme.com"
                  className={`w-full px-4 py-2 border rounded-md text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent ${
                    errors.ownerEmail ? 'border-red-300' : 'border-[var(--color-border)]'
                  }`}
                />
                {errors.ownerEmail && (
                  <p className="mt-1 text-sm text-[var(--color-error)]">{errors.ownerEmail}</p>
                )}
              </div>

              {/* Owner Display Name (optional) */}
              <div>
                <label className="block text-sm font-medium text-[var(--color-text-primary)] mb-1">
                  Owner Display Name
                </label>
                <input
                  type="text"
                  value={formData.ownerDisplayName ?? ''}
                  onChange={(e) => setFormData(prev => ({ ...prev, ownerDisplayName: e.target.value }))}
                  placeholder="Optional — falls back to the email"
                  className="w-full px-4 py-2 border rounded-md text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent border-[var(--color-border)]"
                />
                <p className="mt-1 text-xs text-[var(--color-text-tertiary)]">
                  Used as the placeholder party's display name.
                </p>
              </div>
            </CardContent>
          </Card>

          {/* Regional Settings */}
          <Card className="mb-6">
            <CardHeader>
              <CardTitle>Regional Settings</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {/* Default Currency */}
              <div>
                <label className="block text-sm font-medium text-[var(--color-text-primary)] mb-1">
                  Default Currency *
                </label>
                <Select
                  value={formData.defaultCurrency}
                  onValueChange={(value) => setFormData(prev => ({ ...prev, defaultCurrency: value, supportedCurrencies: [value] }))}
                >
                  <SelectTrigger
                    aria-label="Default currency"
                    className={`w-full px-4 py-2 border rounded-md text-sm bg-[var(--color-surface-inset)] text-[var(--color-text-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent ${
                      errors.defaultCurrency ? 'border-red-300' : 'border-[var(--color-border)]'
                    }`}
                  >
                    <SelectValue placeholder="Select currency" />
                  </SelectTrigger>
                  <SelectContent>
                    {currencyOptions.map(currency => (
                      <SelectItem key={currency.code} value={currency.code}>
                        {currency.code} - {currency.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {errors.defaultCurrency && (
                  <p className="mt-1 text-sm text-[var(--color-error)]">{errors.defaultCurrency}</p>
                )}
              </div>

              {/* Supported Countries */}
              <div>
                <label className="block text-sm font-medium text-[var(--color-text-primary)] mb-2">
                  Supported Countries *
                </label>
                <div className={`border rounded-md p-3 bg-[var(--color-surface-inset)] ${
                  errors.supportedCountries ? 'border-red-300' : 'border-[var(--color-border)]'
                }`}>
                  <div className="flex flex-wrap gap-2">
                    {tenantCountryOptions.map(country => (
                      <button
                        key={country.code}
                        type="button"
                        onClick={() => toggleCountry(country.code)}
                        className={`px-3 py-1.5 rounded-md text-sm font-medium transition-colors ${
                          formData.supportedCountries.includes(country.code)
                            ? 'bg-[var(--color-brand-primary)] text-white'
                            : 'bg-[var(--color-background)] text-[var(--color-text-secondary)] hover:bg-[var(--color-border-light)]'
                        }`}
                      >
                        {country.code} - {country.name}
                      </button>
                    ))}
                  </div>
                </div>
                {errors.supportedCountries && (
                  <p className="mt-1 text-sm text-[var(--color-error)]">{errors.supportedCountries}</p>
                )}
                <p className="mt-1 text-xs text-[var(--color-text-tertiary)]">
                  Select the countries where this tenant will operate.
                </p>
              </div>
            </CardContent>
          </Card>

          {/* Actions */}
          <div className="flex items-center justify-end gap-3">
            <Button
              type="button"
              variant="outline"
              onClick={() => navigate('/tenants')}
              disabled={loading}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={loading}>
              {loading ? (
                <>
                  <span className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin mr-2" />
                  Creating...
                </>
              ) : (
                <>
                  <Save className="w-4 h-4 mr-2" />
                  Create Tenant
                </>
              )}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
