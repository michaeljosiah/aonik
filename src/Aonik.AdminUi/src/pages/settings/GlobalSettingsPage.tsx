import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import {
  Cog, Save, RotateCcw, Loader2, Eye, EyeOff, AlertTriangle, ShieldCheck,
  Search, X, Info, CheckCircle2, CircleAlert,
} from 'lucide-react';
import { toast } from 'sonner';

import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { globalSettingsService } from '@/services/globalSettingsService';
import { formatTenantCountryLabel, tenantCountryOptions } from '@/lib/tenantCountryOptions';
import { getSelectedTenant } from '@/lib/tenantContext';
import { tenantService } from '@/services/tenantService';
import type { UpdateTenantRequest } from '@/types';

// ---------------------------------------------------------------------------
// Global setting definitions — mirrors the backend SettingDefinitions.cs
// ---------------------------------------------------------------------------

interface FieldDef {
  key: string;
  label: string;
  description?: string;
  /** Extended help text shown via an info icon toggle */
  help?: string;
  type: 'text' | 'password' | 'select' | 'toggle';
  options?: { value: string; label: string }[];
  defaultValue?: string;
  placeholder?: string;
}

interface SectionDef {
  title: string;
  description?: string;
  fields: FieldDef[];
  /** Show this section only when a specific setting has a specific value */
  visibleWhen?: { key: string; value: string };
}

interface GlobalTabDef {
  id: string;
  label: string;
  sections: SectionDef[];
}

const GLOBAL_TABS: GlobalTabDef[] = [
  {
    id: 'ai',
    label: 'AI',
    sections: [
      {
        title: 'Provider',
        description: 'Select the AI provider powering LLM features. Set to Stub for development without API keys.',
        fields: [
          {
            key: 'Ai.Provider',
            label: 'AI Provider',
            type: 'select',
            options: [
              { value: 'Stub', label: 'Stub (echo, no real LLM)' },
              { value: 'OpenAI', label: 'OpenAI' },
            ],
            defaultValue: 'Stub',
          },
        ],
      },
      {
        title: 'OpenAI Configuration',
        description: 'API key and model settings for OpenAI. Only used when AI Provider is set to OpenAI.',
        visibleWhen: { key: 'Ai.Provider', value: 'OpenAI' },
        fields: [
          {
            key: 'Ai.OpenAI.ApiKey',
            label: 'API Key',
            type: 'password',
            placeholder: 'sk-...',
            description: 'Encrypted at rest. Leave blank to keep current value.',
            help: 'Your OpenAI API key is used for all LLM and image generation calls. It is encrypted at rest in the database and never exposed to browser clients. Rotate regularly via the OpenAI dashboard.',
          },
          {
            key: 'Ai.OpenAI.Model',
            label: 'Chat Model',
            type: 'select',
            options: [
              { value: 'gpt-5-mini', label: 'GPT-5 Mini' },
              { value: 'gpt-4.1-mini', label: 'GPT-4.1 Mini' },
              { value: 'gpt-4.1-nano', label: 'GPT-4.1 Nano' },
              { value: 'gpt-4o', label: 'GPT-4o' },
              { value: 'gpt-4o-mini', label: 'GPT-4o Mini' },
            ],
            defaultValue: 'gpt-5-mini',
            help: 'The primary model used for agent conversations, tool calls, and content generation. Smaller models are cheaper but less capable. Changes take effect on the next agent invocation.',
          },
          {
            key: 'Ai.OpenAI.ImageModel',
            label: 'Image Model',
            type: 'select',
            options: [
              { value: 'dall-e-3', label: 'DALL-E 3' },
              { value: 'dall-e-2', label: 'DALL-E 2' },
              { value: 'gpt-image-1', label: 'GPT Image 1' },
            ],
            defaultValue: 'dall-e-3',
            help: 'Used for AI-generated images such as content block illustrations. DALL-E 3 produces higher quality but costs more per image.',
          },
        ],
      },
      {
        title: 'User Memory',
        description: 'Controls how agent user memory is stored and retrieved.',
        fields: [
          {
            key: 'Ai.UserMemory.Backend',
            label: 'Memory Backend',
            type: 'select',
            options: [
              { value: 'SqlServer', label: 'SQL Server' },
              { value: 'Qdrant', label: 'Qdrant (vector search)' },
            ],
            defaultValue: 'SqlServer',
            help: 'SQL Server stores memory as key-value pairs. Qdrant enables semantic vector search over memories, allowing agents to recall relevant context by meaning rather than exact key match.',
          },
        ],
      },
      {
        title: 'OpenTelemetry',
        description: 'Controls whether AI trace payloads include sensitive prompts and outputs.',
        fields: [
          {
            key: 'Ai.OpenTelemetry.EnableSensitiveData',
            label: 'Enable Sensitive Data',
            type: 'toggle',
            defaultValue: 'false',
            help: 'When enabled, AI trace observations may include prompt and response payloads in OpenTelemetry export. Keep disabled in production unless you explicitly need payload-level debugging.',
          },
        ],
      },
    ],
  },
  {
    id: 'storage',
    label: 'Storage',
    sections: [
      {
        title: 'Provider',
        description: 'Select the blob storage backend for file uploads.',
        fields: [
          {
            key: 'BlobStorage.Provider',
            label: 'Storage Provider',
            type: 'select',
            options: [
              { value: 'Local', label: 'Local filesystem' },
              { value: 'Azure', label: 'Azure Blob Storage' },
            ],
            defaultValue: 'Local',
          },
          {
            key: 'BlobStorage.Azure.AccountName',
            label: 'Azure Account Name',
            type: 'text',
            placeholder: 'mystorageaccount',
          },
        ],
      },
      {
        title: 'Public Base URLs',
        description: 'CDN or public-facing URLs for serving uploaded assets.',
        fields: [
          {
            key: 'BlobStorage.ProfilePhotos.PublicBaseUrl',
            label: 'Profile Photos',
            type: 'text',
            placeholder: 'https://cdn.example.com/photos',
          },
          {
            key: 'BlobStorage.ProductImages.PublicBaseUrl',
            label: 'Product Images',
            type: 'text',
            placeholder: 'https://cdn.example.com/products',
          },
          {
            key: 'BlobStorage.Documents.PublicBaseUrl',
            label: 'Documents',
            type: 'text',
            placeholder: 'https://cdn.example.com/docs',
          },
        ],
      },
    ],
  },
  {
    id: 'communication',
    label: 'Communication',
    sections: [
      {
        title: 'Azure Communication Services',
        description: 'Sender addresses for transactional email and SMS.',
        fields: [
          {
            key: 'Communication.Azure.Email.FromAddress',
            label: 'Email From Address',
            type: 'text',
            placeholder: 'noreply@example.com',
          },
          {
            key: 'Communication.Azure.Sms.FromPhoneNumber',
            label: 'SMS From Phone Number',
            type: 'text',
            placeholder: '+44...',
          },
        ],
      },
    ],
  },
  {
    id: 'features',
    label: 'Feature Flags',
    sections: [
      {
        title: 'Bill Payments',
        description: 'Control which billing and invoicing features are enabled.',
        fields: [
          { key: 'FeatureManagement.BillPayments.Invoicing.Create', label: 'Invoice Creation', type: 'toggle', defaultValue: 'true' },
          { key: 'FeatureManagement.BillPayments.Invoicing.Issue', label: 'Invoice Issuing', type: 'toggle', defaultValue: 'true' },
          { key: 'FeatureManagement.BillPayments.Invoicing.Payment', label: 'Invoice Payment', type: 'toggle', defaultValue: 'true' },
          { key: 'FeatureManagement.BillPayments.Invoicing.Discounts', label: 'Discounts', type: 'toggle', defaultValue: 'false' },
          { key: 'FeatureManagement.BillPayments.Invoicing.Allocations', label: 'Allocations', type: 'toggle', defaultValue: 'true' },
          { key: 'FeatureManagement.BillPayments.CustomerAccounts.Management', label: 'Customer Account Management', type: 'toggle', defaultValue: 'true' },
        ],
      },
    ],
  },
  {
    id: 'observability',
    label: 'Observability',
    sections: [
      {
        title: 'Azure Application Insights',
        description: 'Configure the Application Insights instance used for the Observability dashboard.',
        fields: [
          {
            key: 'Observability.AppInsights.AppId',
            label: 'Application ID',
            type: 'text',
            placeholder: 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx',
            help: 'The Application ID from Azure Portal > Application Insights > API Access. This is NOT the Instrumentation Key or Connection String.',
          },
          {
            key: 'Observability.AppInsights.ApiKey',
            label: 'API Key',
            type: 'password',
            placeholder: 'xxxxxxxxxxxxxxxxxxxxxxxxxxxxx',
            description: 'Encrypted at rest. Leave blank to keep current value.',
            help: 'Generate an API key from Azure Portal > Application Insights > API Access > Create API Key. Grant Read telemetry permission.',
          },
        ],
      },
    ],
  },
  {
    id: 'platform',
    label: 'Platform Admin',
    sections: [
      {
        title: 'Role Mapping',
        description: 'Configure how platform admin roles are resolved from identity tokens.',
        fields: [
          { key: 'PlatformAdmin.RoleClaimType', label: 'Role Claim Type', type: 'text', defaultValue: 'roles' },
          { key: 'PlatformAdmin.RoleValue', label: 'Role Value', type: 'text', defaultValue: 'Aonik.PlatformAdmin' },
          { key: 'PlatformAdmin.ScopeClaimType', label: 'Scope Claim Type', type: 'text', defaultValue: 'aonik_platform_admin' },
          { key: 'PlatformAdmin.AdminEmails.0', label: 'Primary Admin Email', type: 'text', placeholder: 'admin@example.com' },
        ],
      },
      {
        title: 'Bootstrap',
        fields: [
          {
            key: 'Bootstrap.Enabled',
            label: 'Bootstrap Enabled',
            description: 'When enabled, the setup wizard is accessible for initial platform configuration.',
            type: 'toggle',
            defaultValue: 'false',
            help: 'Only enable this during initial deployment or when re-configuring the platform from scratch. The wizard allows creating tenants, seeding data, and setting up identity providers. Disable after setup is complete.',
          },
        ],
      },
    ],
  },
];

const ALL_GLOBAL_KEYS = GLOBAL_TABS.flatMap((tab) =>
  tab.sections.flatMap((section) => section.fields.map((f) => f.key))
);

// ---------------------------------------------------------------------------
// General settings (workspace profile, locale, approval controls)
// ---------------------------------------------------------------------------

const generalSettingsStorageKey = 'aonik:settings:general';

const timeZoneOptions = [
  'UTC', 'Africa/Lagos', 'Africa/Nairobi', 'Africa/Johannesburg',
  'Europe/London', 'America/New_York',
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
    return { ...defaults, ...parsed };
  } catch {
    return defaults;
  }
}

function ToggleRow({ title, description, checked, onCheckedChange }: {
  title: string; description: string; checked: boolean; onCheckedChange: (checked: boolean) => void;
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

// ---------------------------------------------------------------------------
// Tab definitions (order determines tab order)
// ---------------------------------------------------------------------------

const ALL_TABS = [
  { id: 'general', label: 'General' },
  ...GLOBAL_TABS.map((t) => ({ id: t.id, label: t.label })),
];

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function GlobalSettingsPage() {
  const [activeTab, setActiveTab] = useState('general');

  // ── Global settings state ──
  const [globalValues, setGlobalValues] = useState<Record<string, string | null>>({});
  const [globalOriginal, setGlobalOriginal] = useState<Record<string, string | null>>({});
  const [globalLoading, setGlobalLoading] = useState(true);
  const [globalSaving, setGlobalSaving] = useState(false);
  const [globalError, setGlobalError] = useState<string | null>(null);
  const [revealedPasswords, setRevealedPasswords] = useState<Set<string>>(new Set());

  // ── Search state ──
  const [searchQuery, setSearchQuery] = useState('');
  const [searchOpen, setSearchOpen] = useState(false);
  const searchInputRef = useRef<HTMLInputElement>(null);

  // ── Help toggle state ──
  const [expandedHelp, setExpandedHelp] = useState<Set<string>>(new Set());

  // ── General settings state ──
  const selectedTenant = getSelectedTenant();
  const [generalSettings, setGeneralSettings] = useState<GeneralSettingsState>(
    () => getInitialGeneralSettings(selectedTenant?.name)
  );
  const [tenantMarketSettings, setTenantMarketSettings] = useState<TenantMarketSettingsState>({
    supportedCountries: [], allowedOriginCountries: [], allowedDestinationCountries: [],
  });
  const [tenantSettingsLoading, setTenantSettingsLoading] = useState(true);
  const [tenantSettingsSaving, setTenantSettingsSaving] = useState(false);
  const [tenantSettingsError, setTenantSettingsError] = useState<string | null>(null);

  // ── Dirty tracking for global settings ──
  const globalDirtyKeys = Object.keys(globalValues).filter((key) => {
    const field = GLOBAL_TABS.flatMap((t) => t.sections.flatMap((s) => s.fields)).find((f) => f.key === key);
    if (field?.type === 'password' && (globalValues[key] === '' || globalValues[key] === null)) return false;
    return globalValues[key] !== globalOriginal[key];
  });
  const isGlobalDirty = globalDirtyKeys.length > 0;

  // ── Search matching ──
  const searchLower = searchQuery.toLowerCase().trim();
  const searchMatchingTabs = useMemo(() => {
    if (!searchLower) return new Set<string>();
    const matches = new Set<string>();
    for (const tab of GLOBAL_TABS) {
      for (const section of tab.sections) {
        if (section.title.toLowerCase().includes(searchLower)) { matches.add(tab.id); continue; }
        for (const field of section.fields) {
          if (
            field.label.toLowerCase().includes(searchLower) ||
            field.key.toLowerCase().includes(searchLower) ||
            field.description?.toLowerCase().includes(searchLower) ||
            field.help?.toLowerCase().includes(searchLower) ||
            field.options?.some((o) => o.label.toLowerCase().includes(searchLower))
          ) {
            matches.add(tab.id);
          }
        }
      }
    }
    // Check general tab keywords
    const generalKeywords = ['workspace', 'profile', 'name', 'email', 'timezone', 'locale', 'date', 'country', 'approval', 'authentication', 'notification', 'maintenance', 'regional'];
    if (generalKeywords.some((k) => k.includes(searchLower) || searchLower.includes(k))) {
      matches.add('general');
    }
    return matches;
  }, [searchLower]);

  const fieldMatchesSearch = useCallback((field: FieldDef): boolean => {
    if (!searchLower) return true;
    return (
      field.label.toLowerCase().includes(searchLower) ||
      field.key.toLowerCase().includes(searchLower) ||
      (field.description?.toLowerCase().includes(searchLower) ?? false) ||
      (field.help?.toLowerCase().includes(searchLower) ?? false) ||
      (field.options?.some((o) => o.label.toLowerCase().includes(searchLower)) ?? false)
    );
  }, [searchLower]);

  const sectionMatchesSearch = useCallback((section: SectionDef): boolean => {
    if (!searchLower) return true;
    if (section.title.toLowerCase().includes(searchLower)) return true;
    return section.fields.some((f) => fieldMatchesSearch(f));
  }, [searchLower, fieldMatchesSearch]);

  // ── Unsaved changes guard ──
  useEffect(() => {
    if (!isGlobalDirty) return;
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault();
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [isGlobalDirty]);

  const handleTabChange = (tabId: string) => {
    if (isGlobalDirty && activeTab !== 'general') {
      const confirmed = window.confirm('You have unsaved changes. Switch tab and discard them?');
      if (!confirmed) return;
      handleResetGlobal();
    }
    setActiveTab(tabId);
  };

  // ── Help toggle handler ──
  const toggleHelp = (key: string) => {
    setExpandedHelp((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key); else next.add(key);
      return next;
    });
  };

  // ── Load global settings ──
  const loadGlobalSettings = useCallback(async () => {
    setGlobalLoading(true);
    setGlobalError(null);
    try {
      const response = await globalSettingsService.batchGet(ALL_GLOBAL_KEYS);
      const map: Record<string, string | null> = {};
      for (const key of ALL_GLOBAL_KEYS) {
        const setting = response.settings.find((s) => s.key === key);
        map[key] = setting?.value ?? null;
      }
      setGlobalValues({ ...map });
      setGlobalOriginal({ ...map });
    } catch (err: unknown) {
      setGlobalError((err as { userMessage?: string })?.userMessage ?? 'Failed to load settings.');
    } finally {
      setGlobalLoading(false);
    }
  }, []);

  // ── Load tenant settings ──
  useEffect(() => {
    void loadGlobalSettings();

    let active = true;
    const loadTenantSettings = async () => {
      setTenantSettingsLoading(true);
      setTenantSettingsError(null);
      try {
        const tenant = await tenantService.getSettings();
        if (!active) return;
        setTenantMarketSettings({
          supportedCountries: [...tenant.supportedCountries],
          allowedOriginCountries: [...tenant.allowedOriginCountries],
          allowedDestinationCountries: [...tenant.allowedDestinationCountries],
        });
      } catch (error) {
        if (!active) return;
        const message = error && typeof error === 'object' && 'userMessage' in error
          ? String((error as { userMessage?: string }).userMessage ?? '') : '';
        setTenantSettingsError(message || 'Unable to load tenant country settings right now.');
      } finally {
        if (active) setTenantSettingsLoading(false);
      }
    };
    loadTenantSettings();
    return () => { active = false; };
  }, [loadGlobalSettings]);

  // ── Global settings handlers ──
  const updateGlobalValue = (key: string, value: string | null) => {
    setGlobalValues((prev) => ({ ...prev, [key]: value }));
  };

  const handleSaveGlobal = async () => {
    setGlobalSaving(true);
    try {
      await Promise.all(globalDirtyKeys.map((key) => globalSettingsService.update(key, globalValues[key] ?? null)));
      setGlobalOriginal((prev) => {
        const next = { ...prev };
        for (const key of globalDirtyKeys) next[key] = globalValues[key];
        return next;
      });
      setGlobalValues((prev) => {
        const next = { ...prev };
        for (const key of globalDirtyKeys) {
          const field = GLOBAL_TABS.flatMap((t) => t.sections.flatMap((s) => s.fields)).find((f) => f.key === key);
          if (field?.type === 'password') next[key] = null;
        }
        return next;
      });
      setRevealedPasswords(new Set());
      toast.success(`Saved ${globalDirtyKeys.length} setting${globalDirtyKeys.length === 1 ? '' : 's'}.`);
    } catch (err: unknown) {
      toast.error((err as { userMessage?: string })?.userMessage ?? 'Failed to save settings.');
    } finally {
      setGlobalSaving(false);
    }
  };

  const handleResetGlobal = () => {
    setGlobalValues({ ...globalOriginal });
    setRevealedPasswords(new Set());
  };

  // ── General settings handlers ──
  const updateGeneralSetting = <K extends keyof GeneralSettingsState>(key: K, value: GeneralSettingsState[K]) => {
    setGeneralSettings((prev) => ({ ...prev, [key]: value }));
  };

  const toggleSupportedCountry = (countryCode: string) => {
    setTenantMarketSettings((prev) => {
      const isSelected = prev.supportedCountries.includes(countryCode);
      return {
        supportedCountries: isSelected
          ? prev.supportedCountries.filter((c) => c !== countryCode)
          : [...prev.supportedCountries, countryCode],
        allowedOriginCountries: isSelected
          ? prev.allowedOriginCountries.filter((c) => c !== countryCode)
          : [...prev.allowedOriginCountries],
        allowedDestinationCountries: isSelected
          ? prev.allowedDestinationCountries.filter((c) => c !== countryCode)
          : [...prev.allowedDestinationCountries],
      };
    });
  };

  const toggleScopedCountry = (field: 'allowedOriginCountries' | 'allowedDestinationCountries', countryCode: string) => {
    setTenantMarketSettings((prev) => {
      if (!prev.supportedCountries.includes(countryCode)) return prev;
      const current = prev[field];
      return {
        ...prev,
        [field]: current.includes(countryCode)
          ? current.filter((c) => c !== countryCode)
          : [...current, countryCode],
      };
    });
  };

  const handleSaveGeneral = () => {
    localStorage.setItem(generalSettingsStorageKey, JSON.stringify(generalSettings));
    toast.success('General settings saved.');
  };

  const handleResetGeneralDefaults = () => {
    setGeneralSettings(getDefaultGeneralSettings(selectedTenant?.name));
    toast.success('Defaults restored. Save to apply changes.');
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
        ? String((error as { userMessage?: string }).userMessage ?? '') : '';
      setTenantSettingsError(message || 'Unable to save tenant country settings right now.');
      toast.error(message || 'Unable to save tenant country settings right now.');
    } finally {
      setTenantSettingsSaving(false);
    }
  };

  const togglePasswordVisibility = (key: string) => {
    setRevealedPasswords((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key); else next.add(key);
      return next;
    });
  };

  // ---------------------------------------------------------------------------
  // Field helpers
  // ---------------------------------------------------------------------------

  const renderFieldLabel = (field: FieldDef) => (
    <div className="flex items-center gap-1.5">
      <Label className="text-sm font-medium text-[var(--color-text-primary)]">{field.label}</Label>
      {field.help && (
        <button
          type="button"
          onClick={() => toggleHelp(field.key)}
          className={`rounded-full p-0.5 transition-colors ${
            expandedHelp.has(field.key)
              ? 'text-[var(--color-brand-primary)]'
              : 'text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)]'
          }`}
          title="Toggle help"
        >
          <Info className="h-3.5 w-3.5" />
        </button>
      )}
    </div>
  );

  const renderFieldHelp = (field: FieldDef) => {
    if (!field.help || !expandedHelp.has(field.key)) return null;
    return (
      <div className="rounded-md border border-[var(--color-brand-primary)]/20 bg-[var(--color-brand-primary)]/5 px-3 py-2 text-xs text-[var(--color-text-secondary)] max-w-[28rem]">
        {field.help}
      </div>
    );
  };

  /** Status badge for password fields — shows whether a value is stored */
  const renderPasswordStatus = (field: FieldDef) => {
    if (field.type !== 'password') return null;
    const currentValue = globalValues[field.key];
    const originalValue = globalOriginal[field.key];
    // If user has typed a new value, show pending indicator
    if (currentValue && currentValue !== originalValue) {
      return (
        <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 px-2 py-0.5 text-[10px] font-medium text-amber-700">
          <CircleAlert className="h-3 w-3" />
          Pending save
        </span>
      );
    }
    // If a value exists server-side (original is not null/empty), show configured
    if (originalValue) {
      return (
        <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-medium text-emerald-700">
          <CheckCircle2 className="h-3 w-3" />
          Configured
        </span>
      );
    }
    // No value stored
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-red-50 px-2 py-0.5 text-[10px] font-medium text-red-600">
        <CircleAlert className="h-3 w-3" />
        Not set
      </span>
    );
  };

  // ---------------------------------------------------------------------------
  // Field renderer for global settings
  // ---------------------------------------------------------------------------

  const renderField = (field: FieldDef) => {
    const value = globalValues[field.key];
    // Dim fields that don't match search
    const dimmed = searchLower && !fieldMatchesSearch(field);
    const dimClass = dimmed ? 'opacity-30 pointer-events-none' : '';

    switch (field.type) {
      case 'text':
        return (
          <div key={field.key} className={`space-y-1.5 transition-opacity ${dimClass}`}>
            {renderFieldLabel(field)}
            {field.description && <p className="text-xs text-[var(--color-text-tertiary)]">{field.description}</p>}
            {renderFieldHelp(field)}
            <Input
              value={value ?? ''}
              onChange={(e) => updateGlobalValue(field.key, e.target.value || null)}
              placeholder={field.placeholder}
              className="max-w-[28rem]"
            />
          </div>
        );
      case 'password':
        return (
          <div key={field.key} className={`space-y-1.5 transition-opacity ${dimClass}`}>
            <div className="flex items-center gap-2">
              {renderFieldLabel(field)}
              {renderPasswordStatus(field)}
            </div>
            {field.description && <p className="text-xs text-[var(--color-text-tertiary)]">{field.description}</p>}
            {renderFieldHelp(field)}
            <div className="relative max-w-[28rem]">
              <Input
                type={revealedPasswords.has(field.key) ? 'text' : 'password'}
                value={value ?? ''}
                onChange={(e) => updateGlobalValue(field.key, e.target.value || null)}
                placeholder={field.placeholder}
                className="pr-10"
              />
              <button
                type="button"
                onClick={() => togglePasswordVisibility(field.key)}
                className="absolute right-2 top-1/2 -translate-y-1/2 text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)]"
              >
                {revealedPasswords.has(field.key) ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
              </button>
            </div>
          </div>
        );
      case 'select':
        return (
          <div key={field.key} className={`space-y-1.5 transition-opacity ${dimClass}`}>
            {renderFieldLabel(field)}
            {field.description && <p className="text-xs text-[var(--color-text-tertiary)]">{field.description}</p>}
            {renderFieldHelp(field)}
            <div className="max-w-[28rem]">
              <Select value={value ?? field.defaultValue ?? ''} onValueChange={(v) => updateGlobalValue(field.key, v)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {field.options?.map((opt) => (
                    <SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
        );
      case 'toggle':
        return (
          <div key={field.key} className={`flex items-center justify-between max-w-[28rem] transition-opacity ${dimClass}`}>
            <div className="space-y-0.5">
              {renderFieldLabel(field)}
              {field.description && <p className="text-xs text-[var(--color-text-tertiary)]">{field.description}</p>}
              {renderFieldHelp(field)}
            </div>
            <Switch
              checked={(value ?? field.defaultValue) === 'true'}
              onCheckedChange={(checked) => updateGlobalValue(field.key, checked ? 'true' : 'false')}
            />
          </div>
        );
      default:
        return null;
    }
  };

  // ---------------------------------------------------------------------------
  // General tab content
  // ---------------------------------------------------------------------------

  const renderGeneralTab = () => (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Workspace Profile</CardTitle>
          <CardDescription>Identity values shown to operators and external integrations.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="workspace-name">Workspace name</Label>
            <Input id="workspace-name" value={generalSettings.workspaceName} onChange={(e) => updateGeneralSetting('workspaceName', e.target.value)} />
          </div>
          <div className="space-y-2">
            <Label htmlFor="support-email">Support email</Label>
            <Input id="support-email" type="email" value={generalSettings.supportEmail} onChange={(e) => updateGeneralSetting('supportEmail', e.target.value)} />
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
                    <button key={country.code} type="button" onClick={() => toggleSupportedCountry(country.code)}
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
                    <button key={`origin-${countryCode}`} type="button" onClick={() => toggleScopedCountry('allowedOriginCountries', countryCode)}
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
                    <button key={`dest-${countryCode}`} type="button" onClick={() => toggleScopedCountry('allowedDestinationCountries', countryCode)}
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
            <Select value={generalSettings.timeZone} onValueChange={(v) => updateGeneralSetting('timeZone', v)}>
              <SelectTrigger><SelectValue placeholder="Select timezone" /></SelectTrigger>
              <SelectContent>
                {timeZoneOptions.map((tz) => <SelectItem key={tz} value={tz}>{tz}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Locale</Label>
            <Select value={generalSettings.locale} onValueChange={(v) => updateGeneralSetting('locale', v)}>
              <SelectTrigger><SelectValue placeholder="Select locale" /></SelectTrigger>
              <SelectContent>
                {localeOptions.map((l) => <SelectItem key={l.value} value={l.value}>{l.label}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Date format</Label>
            <Select value={generalSettings.dateFormat} onValueChange={(v) => updateGeneralSetting('dateFormat', v)}>
              <SelectTrigger><SelectValue placeholder="Select date format" /></SelectTrigger>
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
          <ToggleRow title="Require step-up authentication for high-risk actions" description="Prompt for an additional authentication check before execution." checked={generalSettings.stepUpAuthForRisk} onCheckedChange={(c) => updateGeneralSetting('stepUpAuthForRisk', c)} />
          <ToggleRow title="Require manual approval for financially material changes" description="Keep policy and routing updates in a review queue before apply." checked={generalSettings.requireManualApproval} onCheckedChange={(c) => updateGeneralSetting('requireManualApproval', c)} />
          <ToggleRow title="Send maintenance and incident notifications" description="Notify workspace operators about runtime maintenance events." checked={generalSettings.maintenanceNotifications} onCheckedChange={(c) => updateGeneralSetting('maintenanceNotifications', c)} />
        </CardContent>
      </Card>

      <div className="flex flex-wrap items-center justify-end gap-2">
        <Button variant="outline" onClick={handleResetGeneralDefaults}>
          <RotateCcw className="mr-2 h-4 w-4" />
          Reset defaults
        </Button>
        <Button onClick={handleSaveGeneral}>
          <Save className="mr-2 h-4 w-4" />
          Save changes
        </Button>
      </div>
    </div>
  );

  // ---------------------------------------------------------------------------
  // Render
  // ---------------------------------------------------------------------------

  const isGlobalTab = activeTab !== 'general';

  return (
    <div className="h-full overflow-auto">
      <div className="p-6 pb-0">
        <Breadcrumb
          items={[{ label: 'Settings', href: '/settings', icon: <Cog className="h-3.5 w-3.5" /> }]}
          className="mb-4"
        />

        <div className="mb-4 flex items-start justify-between gap-4">
          <div className="min-w-0 flex-1">
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Settings</h1>
            <p className="text-[var(--color-text-secondary)]">
              Workspace identity, AI provider, storage, communication, and feature configuration.
            </p>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            {/* Search toggle */}
            {searchOpen ? (
              <div className="relative">
                <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-[var(--color-text-tertiary)]" />
                <Input
                  ref={searchInputRef}
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  placeholder="Search settings..."
                  className="h-8 w-56 pl-8 pr-8 text-sm"
                  onKeyDown={(e) => {
                    if (e.key === 'Escape') { setSearchQuery(''); setSearchOpen(false); }
                  }}
                />
                {searchQuery && (
                  <button
                    type="button"
                    onClick={() => setSearchQuery('')}
                    className="absolute right-2 top-1/2 -translate-y-1/2 text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)]"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                )}
              </div>
            ) : (
              <Button variant="outline" size="sm" onClick={() => { setSearchOpen(true); setTimeout(() => searchInputRef.current?.focus(), 50); }}>
                <Search className="mr-1.5 h-3.5 w-3.5" />
                Search
              </Button>
            )}
            {isGlobalTab && (
              <>
                <Button variant="outline" size="sm" onClick={handleResetGlobal} disabled={!isGlobalDirty || globalSaving}>
                  <RotateCcw className="mr-1.5 h-3.5 w-3.5" />
                  Reset
                </Button>
                <Button size="sm" onClick={handleSaveGlobal} disabled={!isGlobalDirty || globalSaving}>
                  {globalSaving ? <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" /> : <Save className="mr-1.5 h-3.5 w-3.5" />}
                  Save {isGlobalDirty ? `(${globalDirtyKeys.length})` : ''}
                </Button>
              </>
            )}
          </div>
        </div>
      </div>

      {globalError && (
        <div className="px-6">
          <Card className="mb-4 border-[var(--color-error)]/30 bg-[var(--color-error)]/5">
            <CardContent className="flex items-center gap-3 p-4">
              <AlertTriangle className="h-5 w-5 text-[var(--color-error)]" />
              <p className="text-sm text-[var(--color-error)]">{globalError}</p>
            </CardContent>
          </Card>
        </div>
      )}

      <Tabs value={activeTab} onValueChange={handleTabChange}>
        <div className="flex items-center justify-between border-b border-[var(--color-border-light)] px-6">
          <TabsList className="bg-transparent p-0 h-auto flex flex-wrap gap-0">
            {ALL_TABS.map((tab) => {
              const globalTab = GLOBAL_TABS.find((t) => t.id === tab.id);
              const tabDirtyCount = globalTab
                ? globalDirtyKeys.filter((k) =>
                    globalTab.sections.flatMap((s) => s.fields.map((f) => f.key)).includes(k)
                  ).length
                : 0;
              const hasSearchMatch = searchLower && searchMatchingTabs.has(tab.id);
              const noSearchMatch = searchLower && !searchMatchingTabs.has(tab.id);
              return (
                <TabsTrigger
                  key={tab.id}
                  value={tab.id}
                  className={`px-4 py-3 text-sm rounded-none border-b-2 border-transparent transition-opacity data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)] data-[state=active]:shadow-none ${
                    noSearchMatch ? 'opacity-40' : ''
                  }`}
                >
                  {tab.label}
                  {hasSearchMatch && (
                    <span className="ml-1.5 h-1.5 w-1.5 rounded-full bg-[var(--color-brand-primary)] inline-block" />
                  )}
                  {tabDirtyCount > 0 && (
                    <span className="ml-1.5 inline-flex h-4 min-w-4 items-center justify-center rounded-full bg-[var(--color-brand-primary)] px-1 text-[10px] font-semibold text-white">
                      {tabDirtyCount}
                    </span>
                  )}
                </TabsTrigger>
              );
            })}
          </TabsList>
        </div>

        <div className="p-6">
          <TabsContent value="general" className="mt-0">
            {renderGeneralTab()}
          </TabsContent>

          {globalLoading ? (
            GLOBAL_TABS.map((tab) => (
              <TabsContent key={tab.id} value={tab.id} className="mt-0">
                <Card>
                  <CardContent className="flex items-center justify-center py-16">
                    <Loader2 className="mr-2 h-5 w-5 animate-spin text-[var(--color-text-tertiary)]" />
                    <span className="text-[var(--color-text-secondary)]">Loading settings...</span>
                  </CardContent>
                </Card>
              </TabsContent>
            ))
          ) : (
            GLOBAL_TABS.map((tab) => (
              <TabsContent key={tab.id} value={tab.id} className="mt-0">
                <div className="space-y-6">
                  {tab.sections.map((section) => {
                    // Conditional visibility
                    const hidden = section.visibleWhen && (globalValues[section.visibleWhen.key] ?? '') !== section.visibleWhen.value;
                    const sectionDimmed = searchLower && !sectionMatchesSearch(section);
                    return (
                      <Card
                        key={section.title}
                        className={`transition-all duration-300 ${
                          hidden ? 'opacity-30 pointer-events-none select-none' : ''
                        } ${sectionDimmed ? 'opacity-30' : ''}`}
                      >
                        <CardHeader>
                          <div className="flex items-center gap-2">
                            <CardTitle className="text-base">{section.title}</CardTitle>
                            {hidden && (
                              <span className="rounded-full bg-[var(--color-surface-inset)] px-2 py-0.5 text-[10px] font-medium text-[var(--color-text-tertiary)]">
                                Inactive
                              </span>
                            )}
                          </div>
                          {section.description && <CardDescription>{section.description}</CardDescription>}
                        </CardHeader>
                        <CardContent className="space-y-5">
                          {section.fields.map((field) => renderField(field))}
                        </CardContent>
                      </Card>
                    );
                  })}
                </div>
              </TabsContent>
            ))
          )}
        </div>
      </Tabs>
    </div>
  );
}
