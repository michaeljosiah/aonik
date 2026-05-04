import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, Building, Loader2, Save, X } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Sheet,
  SheetBody,
  SheetContent,
  SheetFooter,
  SheetHeader,
} from '@/components/ui/sheet';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { tenantService } from '@/services/tenantService';
import { catalogService } from '@/services/catalogService';
import { tenantCountryOptions } from '@/lib/tenantCountryOptions';
import type { CreateTenantRequest, TenantEnvironment } from '@/types';

/**
 * "Create tenant" form rendered as a right-anchored slide-out panel.
 * Visual port of the slide-out pattern in the starter template
 * (`Templates/aonik-admin-starterkit/screens/forms.jsx` → `SlideOutPanel`):
 * sticky header with brand-tinted icon badge, scrolling body with the
 * fields, sticky footer with Cancel + primary action.
 *
 * The route stays `/tenants/new` so deep links keep working; closing
 * the sheet (Cancel, X, or Escape) navigates back to `/tenants`.
 */
const environments: { value: TenantEnvironment; label: string }[] = [
  { value: 'Dev', label: 'Development' },
  { value: 'Test', label: 'Test' },
  { value: 'Staging', label: 'Staging' },
  { value: 'Prod', label: 'Production' },
];

const initialCurrencies: { code: string; name: string }[] = [];

export function CreateTenantPage() {
  const navigate = useNavigate();
  const [open, setOpen] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [currencyOptions, setCurrencyOptions] = useState<{ code: string; name: string }[]>(initialCurrencies);

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

  // Mirror the sheet's open/closed state to the route. Closing the
  // panel (X, overlay click, Escape, or Cancel) sends the user back
  // to the tenants list — the slide-out is meant to feel layered on
  // top of that list, even though it's reachable as its own route.
  const handleOpenChange = (next: boolean) => {
    if (loading) return;
    setOpen(next);
    if (!next) {
      // Defer the navigation a tick so Radix can play the close
      // animation before the route unmounts the component.
      window.setTimeout(() => navigate('/tenants'), 150);
    }
  };

  const validateForm = (): boolean => {
    const newErrors: Partial<Record<keyof CreateTenantRequest, string>> = {};

    if (!formData.name.trim()) {
      newErrors.name = 'Tenant name is required';
    } else if (formData.name.length < 3) {
      newErrors.name = 'Tenant name must be at least 3 characters';
    }

    if (!formData.environment) newErrors.environment = 'Environment is required';
    if (!formData.defaultCurrency) newErrors.defaultCurrency = 'Default currency is required';
    if (formData.supportedCountries.length === 0) {
      newErrors.supportedCountries = 'At least one country must be selected';
    }

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
      const trimmedDisplayName = (formData.ownerDisplayName ?? '').trim();
      const payload: CreateTenantRequest = {
        ...formData,
        ownerEmail: formData.ownerEmail.trim(),
        ownerDisplayName: trimmedDisplayName.length > 0 ? trimmedDisplayName : undefined,
      };
      const tenant = await tenantService.create(payload);
      // Skip the route-restore animation here — we're heading to a
      // different route entirely, so closing the sheet would just
      // bounce through the tenants list.
      setOpen(false);
      window.setTimeout(() => navigate(`/tenants/${tenant.tenantId}`), 100);
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
    <Sheet open={open} onOpenChange={handleOpenChange}>
      <SheetContent size="md">
        <SheetHeader
          icon={<Building className="h-4 w-4" />}
          title="Create tenant"
          subtitle="Set up a new tenant with its initial owner and regional defaults."
        />

        <form
          onSubmit={handleSubmit}
          className="flex flex-1 flex-col overflow-hidden"
        >
          <SheetBody>
            {/* Inline error alert — surfaces server errors at the top
                of the body so the user sees them next to the fields,
                not buried under the footer. */}
            {error ? (
              <div className="flex items-start gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2.5 text-sm text-red-700">
                <AlertCircle className="mt-0.5 h-4 w-4 flex-none" />
                <span className="flex-1">{error}</span>
                <button
                  type="button"
                  onClick={() => setError(null)}
                  aria-label="Dismiss"
                  className="text-red-400 hover:text-red-600"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>
            ) : null}

            {/* ── Basic information ──────────────────────────────── */}
            <Field
              label="Tenant name"
              required
              error={errors.name}
            >
              <input
                type="text"
                value={formData.name}
                onChange={(e) => setFormData(prev => ({ ...prev, name: e.target.value }))}
                placeholder="e.g., Acme Corporation"
                className={fieldInputClass(!!errors.name)}
              />
            </Field>

            <Field label="Environment" required error={errors.environment}>
              <Select
                value={formData.environment}
                onValueChange={(value) => setFormData(prev => ({ ...prev, environment: value as TenantEnvironment }))}
              >
                <SelectTrigger
                  aria-label="Environment"
                  className={fieldInputClass(!!errors.environment)}
                >
                  <SelectValue placeholder="Select environment" />
                </SelectTrigger>
                <SelectContent>
                  {environments.map(env => (
                    <SelectItem key={env.value} value={env.value}>{env.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>

            {/* ── Initial owner ──────────────────────────────────── */}
            <SectionDivider label="Initial owner" />
            <p className="text-[11.5px] leading-relaxed text-[var(--color-text-secondary)]">
              We pre-create a pending user for this email and grant
              <code className="mx-1 rounded bg-[var(--color-surface-inset)] px-1 text-[10.5px]">TenantAdmin</code>;
              the first sign-in matching this email links to that record.
              Additional users must be invited from the Users page.
            </p>

            <Field label="Owner email" required error={errors.ownerEmail}>
              <input
                type="email"
                value={formData.ownerEmail}
                onChange={(e) => setFormData(prev => ({ ...prev, ownerEmail: e.target.value }))}
                placeholder="owner@acme.com"
                className={fieldInputClass(!!errors.ownerEmail)}
              />
            </Field>

            <Field
              label="Owner display name"
              hint="Optional"
              helper="Falls back to the email when omitted."
            >
              <input
                type="text"
                value={formData.ownerDisplayName ?? ''}
                onChange={(e) => setFormData(prev => ({ ...prev, ownerDisplayName: e.target.value }))}
                placeholder="e.g., Jane Doe"
                className={fieldInputClass(false)}
              />
            </Field>

            {/* ── Regional settings ──────────────────────────────── */}
            <SectionDivider label="Regional settings" />

            <Field label="Default currency" required error={errors.defaultCurrency}>
              <Select
                value={formData.defaultCurrency}
                onValueChange={(value) => setFormData(prev => ({ ...prev, defaultCurrency: value, supportedCurrencies: [value] }))}
              >
                <SelectTrigger
                  aria-label="Default currency"
                  className={fieldInputClass(!!errors.defaultCurrency)}
                >
                  <SelectValue placeholder="Select currency" />
                </SelectTrigger>
                <SelectContent>
                  {currencyOptions.map(currency => (
                    <SelectItem key={currency.code} value={currency.code}>
                      {currency.code} — {currency.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>

            <Field
              label="Supported countries"
              required
              error={errors.supportedCountries}
              helper="Tenant operates in these regions."
            >
              <div
                className={`rounded-md border bg-[var(--color-surface-inset)] p-2 ${
                  errors.supportedCountries ? 'border-red-300' : 'border-[var(--color-border)]'
                }`}
              >
                <div className="flex flex-wrap gap-1.5">
                  {tenantCountryOptions.map(country => {
                    const active = formData.supportedCountries.includes(country.code);
                    return (
                      <button
                        key={country.code}
                        type="button"
                        onClick={() => toggleCountry(country.code)}
                        className={`rounded-md px-2.5 py-1 text-[11.5px] font-medium transition-colors ${
                          active
                            ? 'bg-[var(--color-brand-primary)] text-white'
                            : 'bg-[var(--color-background)] text-[var(--color-text-secondary)] hover:bg-[var(--color-border-light)]'
                        }`}
                      >
                        {country.code} — {country.name}
                      </button>
                    );
                  })}
                </div>
              </div>
            </Field>
          </SheetBody>

          <SheetFooter>
            {/* Hint on the left so the primary action stays anchored
                to the right edge — matches the starter template's
                "Save as draft / Back / Continue" rhythm. */}
            <span className="text-[11px] text-[var(--color-text-tertiary)]">
              Owner receives a pending invitation on first sign-in.
            </span>
            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => handleOpenChange(false)}
                disabled={loading}
              >
                Cancel
              </Button>
              <Button type="submit" size="sm" disabled={loading}>
                {loading ? (
                  <>
                    <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                    Creating…
                  </>
                ) : (
                  <>
                    <Save className="mr-1.5 h-3.5 w-3.5" />
                    Create tenant
                  </>
                )}
              </Button>
            </div>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  );
}

// ── Field primitives ──────────────────────────────────────────────────
//
// Local label/input wrapper that mirrors the starter template's field
// rhythm (label · hint on the right · input · helper · error). Kept
// inside this file so the slide-out reads as a single self-contained
// surface; if a second slide-out shows up we'll lift these into a
// shared module.

function fieldInputClass(hasError: boolean): string {
  return [
    'h-9 w-full rounded-md border bg-[var(--color-surface-inset)] px-3 text-[13px] text-[var(--color-text-primary)]',
    'focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent',
    hasError ? 'border-red-300' : 'border-[var(--color-border)]',
  ].join(' ');
}

interface FieldProps {
  label: string;
  required?: boolean;
  hint?: string;
  helper?: string;
  error?: string;
  children: React.ReactNode;
}

function Field({ label, required, hint, helper, error, children }: FieldProps) {
  return (
    <div>
      <div className="mb-1 flex items-center gap-1.5">
        <span className="text-[11.5px] font-medium tracking-[0.01em] text-[var(--color-text-secondary)]">
          {label}
          {required ? <span className="ml-0.5 text-[var(--color-brand-primary)]">*</span> : null}
        </span>
        {hint ? (
          <span className="ml-auto text-[10.5px] text-[var(--color-text-tertiary)]">{hint}</span>
        ) : null}
      </div>
      {children}
      {error ? (
        <p className="mt-1 text-[11px] text-[var(--color-error)]">{error}</p>
      ) : helper ? (
        <p className="mt-1 text-[10.5px] text-[var(--color-text-tertiary)]">{helper}</p>
      ) : null}
    </div>
  );
}

function SectionDivider({ label }: { label: string }) {
  return (
    <div className="mt-2 flex items-center gap-2">
      <span className="text-[10.5px] font-semibold uppercase tracking-[0.08em] text-[var(--color-text-tertiary)]">
        {label}
      </span>
      <span className="h-px flex-1 bg-[var(--color-border-light)]" />
    </div>
  );
}
