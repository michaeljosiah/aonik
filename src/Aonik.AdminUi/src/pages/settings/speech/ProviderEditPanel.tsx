import { useEffect, useMemo, useState } from 'react';
import { Loader2, Plug, Save } from 'lucide-react';
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
import type {
  SpeechProvider,
  SpeechProviderConfig,
  SpeechProviderType,
  SpeechVendorDescriptor,
  SpeechVendorFormField,
  SpeechVendorFormSchema,
} from '@/types/speechLibrary';

import { ProviderTestSection } from './ProviderTestSection';

interface ProviderEditPanelProps {
  /** When set, editing an existing tenant-owned provider OR cloning a built-in. */
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
 * Built-in archetypes route through the Clone endpoint; tenant-owned rows go
 * through Create / Update directly.
 */
export function ProviderEditPanel({
  initial,
  defaultType,
  vendors,
  onSaved,
  onCancel,
}: ProviderEditPanelProps) {
  const isEditingTenantRow = initial !== null && !initial.isBuiltIn;
  const isCloningBuiltIn = initial !== null && initial.isBuiltIn;

  const [type, setType] = useState<SpeechProviderType>(initial?.type ?? defaultType);
  const [vendor, setVendor] = useState<string>(initial?.vendor ?? defaultVendorFor(type, vendors));
  const [displayName, setDisplayName] = useState<string>(
    initial ? (isCloningBuiltIn ? `${initial.displayName} (copy)` : initial.displayName) : '',
  );
  const [fieldValues, setFieldValues] = useState<Record<string, string>>(() =>
    initial ? extractFieldValues(initial.config) : {},
  );
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
    setSaving(true);
    try {
      let saved: SpeechProvider;
      if (isEditingTenantRow) {
        saved = await speechProviderLibraryService.update(initial!.id, {
          displayName: displayName.trim(),
          config,
        });
      } else if (isCloningBuiltIn) {
        // Clone first to materialise the tenant row, then immediately apply the form values
        // (the user may have changed them from the built-in defaults).
        const cloned = await speechProviderLibraryService.cloneBuiltIn(initial!.id, {
          newDisplayName: displayName.trim(),
        });
        saved = await speechProviderLibraryService.update(cloned.id, {
          displayName: displayName.trim(),
          config,
        });
      } else {
        saved = await speechProviderLibraryService.create({
          displayName: displayName.trim(),
          type,
          vendor,
          config,
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

  const headerTitle = isEditingTenantRow
    ? 'Edit provider'
    : isCloningBuiltIn
      ? `Clone "${initial!.displayName}"`
      : 'Add provider';
  const headerSubtitle = isCloningBuiltIn
    ? 'Built-in archetypes are immutable — this creates an editable tenant copy.'
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
              disabled={isEditingTenantRow || isCloningBuiltIn}
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
              disabled={isEditingTenantRow || isCloningBuiltIn}
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

        {isEditingTenantRow && initial!.type !== 'Composite' && (
          <ProviderTestSection providerId={initial!.id} type={initial!.type} />
        )}
      </SheetBody>
      <SheetFooter className="justify-end">
        <Button variant="outline" size="sm" onClick={onCancel} disabled={saving}>
          Cancel
        </Button>
        <Button size="sm" onClick={() => void handleSave()} disabled={saving}>
          {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          {isEditingTenantRow ? 'Save changes' : isCloningBuiltIn ? 'Clone & save' : 'Create'}
        </Button>
      </SheetFooter>
    </>
  );
}

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
        <p className="text-xs text-muted-foreground">{field.description}</p>
      )}
    </div>
  );
}

// ── Helpers ─────────────────────────────────────────────────────────────────

function defaultVendorFor(type: SpeechProviderType, vendors: SpeechVendorDescriptor[]): string {
  return vendors.find((v) => v.supportedTypes.includes(type))?.vendor ?? '';
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
