import { useEffect, useMemo, useState } from 'react';
import { Loader2, Plug, RefreshCw, Save } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { SheetBody, SheetFooter, SheetHeader } from '@/components/ui/sheet';
import { Textarea } from '@/components/ui/textarea';
import { speechProviderLibraryService } from '@/services/speechProviderLibraryService';
import { textToSpeechSettingsService } from '@/services/textToSpeechSettingsService';
import type {
  SpeechProvider,
  SpeechProviderConfig,
  SpeechProviderType,
  SpeechVendorDescriptor,
  SpeechVendorFormField,
  SpeechVendorFormSchema,
} from '@/types/speechLibrary';
import type { TextToSpeechVoiceOptionResponse } from '@/types';

import { ProviderTestSection } from './ProviderTestSection';

interface ProviderEditPanelProps {
  /** When set, editing an existing tenant-owned provider. Null = "Add provider". */
  initial: SpeechProvider | null;
  /** When `initial` is null, this is a fresh "Add provider" form — needs a starting type. */
  defaultType: SpeechProviderType;
  vendors: SpeechVendorDescriptor[];
  onSaved: (provider: SpeechProvider) => void;
  onCancel: () => void;
}

/**
 * Schema-driven edit panel. Renders the per-vendor form using the
 * `/speech-vendors` catalog so adding a new vendor on the backend automatically
 * surfaces the right fields without a UI rebuild.
 *
 * The earlier "clone a built-in archetype" path was retired with the catalog. Every
 * provider in the library is now a tenant-owned row that goes through Create / Update.
 *
 * Fields whose <c>widget === 'remote-select'</c> get a <see cref="RemoteSelectField"/>
 * which calls the live provider API (via the existing host/tenant credential chain on
 * <c>/tenant/settings/text-to-speech/voices</c>) so admins pick from real voices instead
 * of a hardcoded shortlist.
 */
export function ProviderEditPanel({
  initial,
  defaultType,
  vendors,
  onSaved,
  onCancel,
}: ProviderEditPanelProps) {
  const isEditing = initial !== null;

  const [type, setType] = useState<SpeechProviderType>(initial?.type ?? defaultType);
  const [vendor, setVendor] = useState<string>(initial?.vendor ?? defaultVendorFor(type, vendors));
  const [displayName, setDisplayName] = useState<string>(initial?.displayName ?? '');
  const [fieldValues, setFieldValues] = useState<Record<string, string>>(() =>
    initial ? extractFieldValues(initial.config) : {},
  );
  // Phase D: API key lives directly on the provider row. The wire DTO is tri-state
  // (null = leave alone, "" = clear, non-empty = encrypt + replace). UI tracks two
  // pieces of state: the typed key and a flag for "user explicitly cleared the field"
  // so we can distinguish "didn't touch it" from "wants to clear it".
  const [apiKeyInput, setApiKeyInput] = useState<string>('');
  const [clearStoredApiKey, setClearStoredApiKey] = useState(false);
  const [saving, setSaving] = useState(false);

  // Whenever (type, vendor) changes, reset the form schema. If the chosen vendor doesn't support
  // the current type, fall back to its first supported type.
  const schema = useMemo<SpeechVendorFormSchema | null>(() => {
    const v = vendors.find((vd) => vd.vendor === vendor);
    if (!v) return null;
    return v.forms.find((f) => f.type === type) ?? v.forms[0] ?? null;
  }, [type, vendor, vendors]);

  // Seed default values when the schema first appears or changes (only if user hasn't typed yet
  // for that field).
  useEffect(() => {
    if (!schema) return;
    setFieldValues((prev) => {
      const next = { ...prev };
      for (const f of schema.fields) {
        if (next[f.name] === undefined && f.default != null) {
          next[f.name] = f.default;
        }
      }
      return next;
    });
  }, [schema]);

  if (vendors.length === 0) {
    return (
      <>
        <SheetHeader
          icon={<Plug className="h-4 w-4" />}
          title="Loading vendor catalog…"
        />
        <SheetBody>
          <p className="text-sm text-[var(--color-text-secondary)]">Just a moment.</p>
        </SheetBody>
      </>
    );
  }

  const supportedVendorsForType = vendors.filter((vd) => vd.supportedTypes.includes(type));

  const handleSave = async () => {
    if (!schema) return;
    if (!displayName.trim()) {
      toast.error('Display name is required.');
      return;
    }

    const config = buildConfig(schema, fieldValues);
    // Compute the API key payload. Tri-state semantics for update; create just sends
    // whatever was typed (or null if blank, so the row starts keyless).
    const trimmedKey = apiKeyInput.trim();
    const apiKeyForCreate = trimmedKey.length > 0 ? trimmedKey : null;
    let apiKeyForUpdate: string | null | undefined;
    if (clearStoredApiKey) apiKeyForUpdate = '';
    else if (trimmedKey.length > 0) apiKeyForUpdate = trimmedKey;
    else apiKeyForUpdate = undefined; // leave existing key alone

    setSaving(true);
    try {
      let saved: SpeechProvider;
      if (isEditing) {
        saved = await speechProviderLibraryService.update(initial!.id, {
          displayName: displayName.trim(),
          config,
          ...(apiKeyForUpdate !== undefined ? { apiKey: apiKeyForUpdate } : {}),
        });
      } else {
        saved = await speechProviderLibraryService.create({
          displayName: displayName.trim(),
          type,
          vendor,
          config,
          apiKey: apiKeyForCreate,
        });
      }
      toast.success(`Provider "${saved.displayName}" saved.`);
      onSaved(saved);
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        (err as { message?: string })?.message ??
        'Failed to save provider.';
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  const headerTitle = isEditing ? 'Edit provider' : 'Add provider';
  const headerSubtitle = isEditing
    ? 'Update the configuration. Saving bumps the version and snapshots the previous one in history.'
    : 'Configure a vendor instance. Many providers can coexist per vendor.';

  return (
    <>
      <SheetHeader icon={<Plug className="h-4 w-4" />} title={headerTitle} subtitle={headerSubtitle} />
      <SheetBody className="gap-5">
        <div className="grid gap-3 md:grid-cols-2">
          <div className="space-y-2">
            <Label>Type</Label>
            <Select
              value={type}
              onValueChange={(v) => {
                const next = v as SpeechProviderType;
                setType(next);
                setVendor(defaultVendorFor(next, vendors));
                setFieldValues({});
              }}
              disabled={isEditing}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Stt">Speech-to-text</SelectItem>
                <SelectItem value="Tts">Text-to-speech</SelectItem>
                <SelectItem value="Composite">Composite (realtime)</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Vendor</Label>
            <Select
              value={vendor}
              onValueChange={(v) => {
                setVendor(v);
                setFieldValues({});
              }}
              disabled={isEditing}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {supportedVendorsForType.map((v) => (
                  <SelectItem key={v.vendor} value={v.vendor}>
                    {v.displayName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="provider-display-name">Display name</Label>
            <Input
              id="provider-display-name"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              placeholder="e.g. Production OpenAI · alloy"
              maxLength={200}
            />
          </div>
        </div>

        {/* Phase D: API key directly on the provider row. The unified credential resolver
            reads it as the tenant override; host default + configuration fallback still
            apply if this is left blank. */}
        <div className="space-y-2 rounded-md border border-[var(--color-border-light)] p-4">
          <div className="flex items-center justify-between">
            <Label htmlFor="provider-api-key">
              API key{' '}
              <span className="font-normal text-[var(--color-text-tertiary)]">
                (encrypted at rest)
              </span>
            </Label>
            {isEditing && initial!.hasApiKey && !clearStoredApiKey && apiKeyInput.length === 0 && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setClearStoredApiKey(true)}
                disabled={saving}
              >
                Clear stored key
              </Button>
            )}
            {clearStoredApiKey && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setClearStoredApiKey(false)}
                disabled={saving}
              >
                Undo clear
              </Button>
            )}
          </div>
          <Input
            id="provider-api-key"
            type="password"
            autoComplete="new-password"
            value={apiKeyInput}
            onChange={(e) => {
              setApiKeyInput(e.target.value);
              if (e.target.value.length > 0) setClearStoredApiKey(false);
            }}
            placeholder={
              isEditing && initial!.hasApiKey
                ? clearStoredApiKey
                  ? '[will be cleared on save]'
                  : '••••••••• (leave blank to keep stored key)'
                : 'Paste your vendor API key'
            }
            disabled={saving}
          />
          <p className="text-[11px] text-[var(--color-text-tertiary)]">
            {isEditing
              ? clearStoredApiKey
                ? 'Saving with the field blank will remove the stored credential. Cancel above to keep it.'
                : 'Leave blank to keep the existing key. Type a new one to replace it.'
              : 'Optional. Falls back to the host default if blank — fine for shared org keys, required for tenant-specific.'}
          </p>
        </div>

        {schema && (
          <div className="space-y-3 rounded-md border border-[var(--color-border-light)] p-4">
            <div className="text-xs font-medium uppercase tracking-wider text-[var(--color-text-tertiary)]">
              {schema.configKind} configuration
            </div>
            {schema.fields.map((field) => (
              <FieldRenderer
                key={field.name}
                field={field}
                value={fieldValues[field.name] ?? ''}
                onChange={(v) => setFieldValues((prev) => ({ ...prev, [field.name]: v }))}
              />
            ))}
          </div>
        )}

        {isEditing && initial!.type !== 'Composite' && (
          <ProviderTestSection providerId={initial!.id} type={initial!.type} />
        )}
      </SheetBody>
      <SheetFooter className="justify-end">
        <Button variant="outline" size="sm" onClick={onCancel} disabled={saving}>
          Cancel
        </Button>
        <Button size="sm" onClick={() => void handleSave()} disabled={saving}>
          {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          {isEditing ? 'Save changes' : 'Create'}
        </Button>
      </SheetFooter>
    </>
  );
}

// ── Field renderers ────────────────────────────────────────────────────────

function FieldRenderer({
  field,
  value,
  onChange,
}: {
  field: SpeechVendorFormField;
  value: string;
  onChange: (v: string) => void;
}) {
  const id = `provider-field-${field.name}`;

  if (field.widget === 'remote-select') {
    return <RemoteSelectField field={field} value={value} onChange={onChange} id={id} />;
  }

  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>
        {field.label}
        {field.required && <span className="text-destructive"> *</span>}
      </Label>

      {field.widget === 'select' && field.options ? (
        <Select value={value} onValueChange={onChange}>
          <SelectTrigger id={id}>
            <SelectValue placeholder={field.placeholder ?? 'Select…'} />
          </SelectTrigger>
          <SelectContent>
            {field.options.map((opt) => (
              <SelectItem key={opt.value} value={opt.value}>
                {opt.label}
                {opt.description ? ` — ${opt.description}` : ''}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      ) : field.widget === 'textarea' ? (
        <Textarea
          id={id}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={field.placeholder ?? ''}
          rows={3}
        />
      ) : (
        <Input
          id={id}
          type={field.widget === 'password' ? 'password' : field.widget === 'number' ? 'number' : 'text'}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={field.placeholder ?? ''}
          min={field.min ?? undefined}
          max={field.max ?? undefined}
        />
      )}

      {field.description && (
        <p className="text-xs text-[var(--color-text-tertiary)]">{field.description}</p>
      )}
    </div>
  );
}

/**
 * Live-fetched dropdown: calls the provider's voice-list API on mount and on Refresh.
 * If the call fails (no credentials, network error, vendor not supported), falls back
 * to a free-form text input so the admin can still paste a voiceId manually.
 */
function RemoteSelectField({
  field,
  value,
  onChange,
  id,
}: {
  field: SpeechVendorFormField;
  value: string;
  onChange: (v: string) => void;
  id: string;
}) {
  const provider = remoteOptionsKeyToProvider(field.remoteOptionsKey);
  const [options, setOptions] = useState<TextToSpeechVoiceOptionResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    if (!provider) return;
    setLoading(true);
    setError(null);
    try {
      const list = await textToSpeechSettingsService.listVoices(provider);
      setOptions(list);
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        (err as { message?: string })?.message ??
        `Failed to load ${provider} voices.`;
      setError(message);
      setOptions([]);
    } finally {
      setLoading(false);
    }
  };

  // Load on mount + whenever the loader key changes.
  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [provider]);

  // If the saved voiceId isn't in the loaded list, surface it explicitly so the user knows
  // the current row may be referencing an unavailable voice.
  const savedNotInList =
    value.length > 0 && !loading && !options.some((opt) => opt.voiceId === value);

  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between gap-2">
        <Label htmlFor={id}>
          {field.label}
          {field.required && <span className="text-destructive"> *</span>}
        </Label>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="h-6 px-2 text-[11px]"
          onClick={() => void load()}
          disabled={loading || !provider}
        >
          <RefreshCw className={`h-3 w-3 ${loading ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      {provider == null ? (
        // No mapping for this loader key — fall back to a text input so the admin isn't blocked.
        <Input
          id={id}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={field.placeholder ?? ''}
        />
      ) : (
        <Select value={value || undefined} onValueChange={onChange}>
          <SelectTrigger id={id}>
            <SelectValue
              placeholder={
                loading
                  ? `Loading ${provider} voices…`
                  : options.length === 0
                    ? 'No voices available'
                    : (field.placeholder ?? 'Select…')
              }
            />
          </SelectTrigger>
          <SelectContent>
            {savedNotInList && value && (
              <SelectItem value={value} disabled>
                {value} (not in loaded list)
              </SelectItem>
            )}
            {options.map((opt) => (
              <SelectItem key={opt.voiceId} value={opt.voiceId}>
                {opt.name}
                {opt.labels?.gender ? ` · ${opt.labels.gender}` : ''}
                {opt.labels?.accent ? ` · ${opt.labels.accent}` : ''}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      )}

      {field.description && (
        <p className="text-xs text-[var(--color-text-tertiary)]">{field.description}</p>
      )}

      {error && (
        <p className="text-xs text-[var(--color-error)]">
          {error}{' '}
          {provider && (
            <span className="text-[var(--color-text-tertiary)]">
              Set the {provider} API key on this provider above.
            </span>
          )}
        </p>
      )}

      {!error && !loading && options.length === 0 && provider && (
        <p className="text-xs text-[var(--color-text-tertiary)]">
          No voices loaded yet. Save this provider with a {provider} API key, then click Refresh.
        </p>
      )}
    </div>
  );
}

// ── Helpers ─────────────────────────────────────────────────────────────────

function defaultVendorFor(type: SpeechProviderType, vendors: SpeechVendorDescriptor[]): string {
  return vendors.find((v) => v.supportedTypes.includes(type))?.vendor ?? '';
}

/**
 * Map a backend `remoteOptionsKey` to the vendor name expected by
 * `textToSpeechSettingsService.listVoices`. Returning null causes the field to render as
 * a free-form text input (defensive: lets future keys ship before the front-end knows them).
 */
function remoteOptionsKeyToProvider(key?: string | null): string | null {
  switch (key) {
    case 'elevenlabs-voices':
      return 'ElevenLabs';
    case 'mistral-voices':
      return 'Mistral';
    default:
      return null;
  }
}

/** Construct a `SpeechProviderConfig` payload from the schema + form values. */
function buildConfig(
  schema: SpeechVendorFormSchema,
  values: Record<string, string>,
): SpeechProviderConfig {
  const config: Record<string, unknown> = { kind: schema.configKind };
  for (const f of schema.fields) {
    const raw = values[f.name];
    if (raw === undefined || raw === '') {
      // Required fields rely on backend validation to surface a 422; for optional fields we set
      // null so the C# polymorphic deserializer keeps the explicit absence.
      config[f.name] = f.required ? raw ?? '' : null;
      continue;
    }
    config[f.name] = f.widget === 'number' ? Number.parseFloat(raw) : raw;
  }
  return config as unknown as SpeechProviderConfig;
}

/** Inverse of `buildConfig` — pull the values out of a saved config to seed the form. */
function extractFieldValues(config: SpeechProviderConfig): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [key, value] of Object.entries(config)) {
    if (key === 'kind') continue;
    if (value === null || value === undefined) continue;
    out[key] = String(value);
  }
  return out;
}
