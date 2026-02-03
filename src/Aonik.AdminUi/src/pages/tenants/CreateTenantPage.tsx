import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { ArrowLeft, Save, AlertCircle, X } from 'lucide-react';
import { tenantService } from '@/services/tenantService';
import { catalogService } from '@/services/catalogService';
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
  });

  const [errors, setErrors] = useState<Partial<Record<keyof CreateTenantRequest, string>>>({});

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

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!validateForm()) return;

    setLoading(true);
    setError(null);

    try {
      const tenant = await tenantService.create(formData);
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
                    {countries.map(country => (
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
