import { useEffect, useState } from 'react';
import { Cog, RotateCcw, Save, ShieldCheck, SlidersHorizontal } from 'lucide-react';
import { toast } from 'sonner';

import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { formatTenantCountryLabel, tenantCountryOptions } from '@/lib/tenantCountryOptions';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { getSelectedTenant } from '@/lib/tenantContext';
import { tenantService } from '@/services/tenantService';
import type { UpdateTenantRequest } from '@/types';

const generalSettingsStorageKey = 'aonik:settings:general';

const timeZoneOptions = [
  'UTC',
  'Africa/Lagos',
  'Africa/Nairobi',
  'Africa/Johannesburg',
  'Europe/London',
  'America/New_York',
];

const localeOptions = [
  { value: 'en-US', label: 'English (US)' },
  { value: 'en-GB', label: 'English (UK)' },
  { value: 'fr-FR', label: 'French' },
];

interface GeneralSettingsState {
  workspaceName: string;
  supportEmail: string;
  timeZone: string;
  locale: string;
  dateFormat: string;
  stepUpAuthForRisk: boolean;
  requireManualApproval: boolean;
  maintenanceNotifications: boolean;
}

interface TenantMarketSettingsState {
  supportedCountries: string[];
  allowedOriginCountries: string[];
  allowedDestinationCountries: string[];
}

function getDefaultGeneralSettings(tenantName?: string): GeneralSettingsState {
  return {
    workspaceName: tenantName?.trim() || 'Aonik Workspace',
    supportEmail: 'ops@aonik.ai',
    timeZone: 'UTC',
    locale: 'en-GB',
    dateFormat: 'DD/MM/YYYY',
    stepUpAuthForRisk: true,
    requireManualApproval: true,
    maintenanceNotifications: true,
  };
}

function getInitialGeneralSettings(tenantName?: string): GeneralSettingsState {
  const defaults = getDefaultGeneralSettings(tenantName);

  try {
    const raw = localStorage.getItem(generalSettingsStorageKey);
    if (!raw) return defaults;

    const parsed = JSON.parse(raw) as Partial<GeneralSettingsState>;
    return {
      ...defaults,
      ...parsed,
    };
  } catch {
    return defaults;
  }
}

function ToggleRow({
  title,
  description,
  checked,
  onCheckedChange,
}: {
  title: string;
  description: string;
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
}) {
  return (
    <div className="flex items-start justify-between gap-4 rounded-md border border-[var(--color-border-light)] px-4 py-3">
      <div>
        <p className="text-sm font-medium text-[var(--color-text-primary)]">{title}</p>
        <p className="text-xs text-[var(--color-text-tertiary)]">{description}</p>
      </div>
      <Switch checked={checked} onCheckedChange={onCheckedChange} />
    </div>
  );
}

export function SettingsGeneralPage() {
  const selectedTenant = getSelectedTenant();
  const [settings, setSettings] = useState<GeneralSettingsState>(() => getInitialGeneralSettings(selectedTenant?.name));
  const [tenantMarketSettings, setTenantMarketSettings] = useState<TenantMarketSettingsState>({
    supportedCountries: [],
    allowedOriginCountries: [],
    allowedDestinationCountries: [],
  });
  const [tenantSettingsLoading, setTenantSettingsLoading] = useState(true);
  const [tenantSettingsSaving, setTenantSettingsSaving] = useState(false);
  const [tenantSettingsError, setTenantSettingsError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    const loadTenantSettings = async () => {
      setTenantSettingsLoading(true);
      setTenantSettingsError(null);

      try {
        const tenant = await tenantService.getSettings();
        if (!active) {
          return;
        }

        setTenantMarketSettings({
          supportedCountries: [...tenant.supportedCountries],
          allowedOriginCountries: [...tenant.allowedOriginCountries],
          allowedDestinationCountries: [...tenant.allowedDestinationCountries],
        });
      } catch (error) {
        if (!active) {
          return;
        }

        const message = error && typeof error === 'object' && 'userMessage' in error
          ? String((error as { userMessage?: string }).userMessage ?? '')
          : '';
        setTenantSettingsError(message || 'Unable to load tenant country settings right now.');
      } finally {
        if (active) {
          setTenantSettingsLoading(false);
        }
      }
    };

    loadTenantSettings();

    return () => {
      active = false;
    };
  }, []);

  const updateSetting = <K extends keyof GeneralSettingsState>(key: K, value: GeneralSettingsState[K]) => {
    setSettings((prev) => ({ ...prev, [key]: value }));
  };

  const toggleSupportedCountry = (countryCode: string) => {
    setTenantMarketSettings((prev) => {
      const isSelected = prev.supportedCountries.includes(countryCode);
      return {
        supportedCountries: isSelected
          ? prev.supportedCountries.filter((code) => code !== countryCode)
          : [...prev.supportedCountries, countryCode],
        allowedOriginCountries: isSelected
          ? prev.allowedOriginCountries.filter((code) => code !== countryCode)
          : [...prev.allowedOriginCountries],
        allowedDestinationCountries: isSelected
          ? prev.allowedDestinationCountries.filter((code) => code !== countryCode)
          : [...prev.allowedDestinationCountries],
      };
    });
  };

  const toggleScopedCountry = (
    field: 'allowedOriginCountries' | 'allowedDestinationCountries',
    countryCode: string,
  ) => {
    setTenantMarketSettings((prev) => {
      if (!prev.supportedCountries.includes(countryCode)) {
        return prev;
      }

      const current = prev[field];
      return {
        ...prev,
        [field]: current.includes(countryCode)
          ? current.filter((code) => code !== countryCode)
          : [...current, countryCode],
      };
    });
  };

  const handleResetDefaults = () => {
    setSettings(getDefaultGeneralSettings(selectedTenant?.name));
    toast.success('Defaults restored. Save to apply changes.');
  };

  const handleSave = () => {
    localStorage.setItem(generalSettingsStorageKey, JSON.stringify(settings));
    toast.success('General settings saved.');
  };

  const handleSaveTenantMarketSettings = async () => {
    const request: UpdateTenantRequest = {
      supportedCountries: tenantMarketSettings.supportedCountries,
      allowedOriginCountries: tenantMarketSettings.allowedOriginCountries,
      allowedDestinationCountries: tenantMarketSettings.allowedDestinationCountries,
    };

    setTenantSettingsSaving(true);
    setTenantSettingsError(null);

    try {
      const tenant = await tenantService.updateSettings(request);
      setTenantMarketSettings({
        supportedCountries: [...tenant.supportedCountries],
        allowedOriginCountries: [...tenant.allowedOriginCountries],
        allowedDestinationCountries: [...tenant.allowedDestinationCountries],
      });
      toast.success('Tenant country settings saved.');
    } catch (error) {
      const message = error && typeof error === 'object' && 'userMessage' in error
        ? String((error as { userMessage?: string }).userMessage ?? '')
        : '';
      setTenantSettingsError(message || 'Unable to save tenant country settings right now.');
      toast.error(message || 'Unable to save tenant country settings right now.');
    } finally {
      setTenantSettingsSaving(false);
    }
  };

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb
        items={[
          { label: 'Settings', href: '/settings', icon: <Cog className="h-3.5 w-3.5" /> },
          { label: 'General', icon: <SlidersHorizontal className="h-3.5 w-3.5" /> },
        ]}
        className="mb-4"
      />

      <div className="mb-6">
        <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">General Settings</h1>
        <p className="text-[var(--color-text-secondary)]">
          Configure workspace identity, localization defaults, and operational controls.
        </p>
      </div>

      <div className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Workspace Profile</CardTitle>
            <CardDescription>Identity values shown to operators and external integrations.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="workspace-name">Workspace name</Label>
              <Input
                id="workspace-name"
                value={settings.workspaceName}
                onChange={(event) => updateSetting('workspaceName', event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="support-email">Support email</Label>
              <Input
                id="support-email"
                type="email"
                value={settings.supportEmail}
                onChange={(event) => updateSetting('supportEmail', event.target.value)}
              />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Country Access</CardTitle>
            <CardDescription>Define the tenant market envelope, sender countries, and destination countries.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {tenantSettingsLoading ? (
              <p className="text-sm text-[var(--color-text-secondary)]">Loading tenant country settings...</p>
            ) : (
              <>
                {tenantSettingsError && (
                  <div className="rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-4 py-3 text-sm text-[var(--color-error)]">
                    {tenantSettingsError}
                  </div>
                )}

                <div className="space-y-2">
                  <Label>Supported countries</Label>
                  <div className="flex flex-wrap gap-2 rounded-md border border-[var(--color-border-light)] p-3">
                    {tenantCountryOptions.map((country) => (
                      <button
                        key={country.code}
                        type="button"
                        onClick={() => toggleSupportedCountry(country.code)}
                        className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                          tenantMarketSettings.supportedCountries.includes(country.code)
                            ? 'bg-[var(--color-brand-primary)] text-white'
                            : 'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)] hover:bg-[var(--color-border-light)]'
                        }`}
                      >
                        {formatTenantCountryLabel(country.code)}
                      </button>
                    ))}
                  </div>
                </div>

                <div className="space-y-2">
                  <Label>Countries customers can send from</Label>
                  <div className="flex flex-wrap gap-2 rounded-md border border-[var(--color-border-light)] p-3">
                    {tenantMarketSettings.supportedCountries.map((countryCode) => (
                      <button
                        key={`origin-${countryCode}`}
                        type="button"
                        onClick={() => toggleScopedCountry('allowedOriginCountries', countryCode)}
                        className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                          tenantMarketSettings.allowedOriginCountries.includes(countryCode)
                            ? 'bg-[var(--color-brand-primary)] text-white'
                            : 'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)] hover:bg-[var(--color-border-light)]'
                        }`}
                      >
                        {formatTenantCountryLabel(countryCode)}
                      </button>
                    ))}
                  </div>
                </div>

                <div className="space-y-2">
                  <Label>Countries customers can send to</Label>
                  <div className="flex flex-wrap gap-2 rounded-md border border-[var(--color-border-light)] p-3">
                    {tenantMarketSettings.supportedCountries.map((countryCode) => (
                      <button
                        key={`destination-${countryCode}`}
                        type="button"
                        onClick={() => toggleScopedCountry('allowedDestinationCountries', countryCode)}
                        className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                          tenantMarketSettings.allowedDestinationCountries.includes(countryCode)
                            ? 'bg-[var(--color-brand-primary)] text-white'
                            : 'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)] hover:bg-[var(--color-border-light)]'
                        }`}
                      >
                        {formatTenantCountryLabel(countryCode)}
                      </button>
                    ))}
                  </div>
                </div>

                <div className="flex justify-end">
                  <Button onClick={handleSaveTenantMarketSettings} disabled={tenantSettingsSaving || tenantMarketSettings.supportedCountries.length === 0}>
                    <Save className="mr-2 h-4 w-4" />
                    {tenantSettingsSaving ? 'Saving...' : 'Save country settings'}
                  </Button>
                </div>
              </>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Regional Defaults</CardTitle>
            <CardDescription>Control formatting and timezone used by the Admin UI.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <Label>Time zone</Label>
              <Select value={settings.timeZone} onValueChange={(value) => updateSetting('timeZone', value)}>
                <SelectTrigger>
                  <SelectValue placeholder="Select timezone" />
                </SelectTrigger>
                <SelectContent>
                  {timeZoneOptions.map((timeZone) => (
                    <SelectItem key={timeZone} value={timeZone}>
                      {timeZone}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Locale</Label>
              <Select value={settings.locale} onValueChange={(value) => updateSetting('locale', value)}>
                <SelectTrigger>
                  <SelectValue placeholder="Select locale" />
                </SelectTrigger>
                <SelectContent>
                  {localeOptions.map((locale) => (
                    <SelectItem key={locale.value} value={locale.value}>
                      {locale.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Date format</Label>
              <Select value={settings.dateFormat} onValueChange={(value) => updateSetting('dateFormat', value)}>
                <SelectTrigger>
                  <SelectValue placeholder="Select date format" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="DD/MM/YYYY">DD/MM/YYYY</SelectItem>
                  <SelectItem value="MM/DD/YYYY">MM/DD/YYYY</SelectItem>
                  <SelectItem value="YYYY-MM-DD">YYYY-MM-DD</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <ShieldCheck className="h-4 w-4 text-[var(--color-brand-primary)]" />
              Approval Controls
            </CardTitle>
            <CardDescription>Guardrails for risky operations and production-impacting changes.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <ToggleRow
              title="Require step-up authentication for high-risk actions"
              description="Prompt for an additional authentication check before execution."
              checked={settings.stepUpAuthForRisk}
              onCheckedChange={(checked) => updateSetting('stepUpAuthForRisk', checked)}
            />
            <ToggleRow
              title="Require manual approval for financially material changes"
              description="Keep policy and routing updates in a review queue before apply."
              checked={settings.requireManualApproval}
              onCheckedChange={(checked) => updateSetting('requireManualApproval', checked)}
            />
            <ToggleRow
              title="Send maintenance and incident notifications"
              description="Notify workspace operators about runtime maintenance events."
              checked={settings.maintenanceNotifications}
              onCheckedChange={(checked) => updateSetting('maintenanceNotifications', checked)}
            />
          </CardContent>
        </Card>

        <div className="flex flex-wrap items-center justify-end gap-2">
          <Button variant="outline" onClick={handleResetDefaults}>
            <RotateCcw className="mr-2 h-4 w-4" />
            Reset defaults
          </Button>
          <Button onClick={handleSave}>
            <Save className="mr-2 h-4 w-4" />
            Save changes
          </Button>
        </div>
      </div>
    </div>
  );
}
