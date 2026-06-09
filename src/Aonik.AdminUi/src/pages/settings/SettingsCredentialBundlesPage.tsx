import { useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { AlertCircle, KeyRound, Plus, RefreshCw, RotateCw, Save, Upload, X } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { credentialBundleService } from '@/services/credentialBundleService';
import { cn } from '@/lib/utils';
import type {
  ConnectorKindSchema,
  CredentialBundleListItem,
  CredentialFieldState,
} from '@/types/credentials';

function resolveUserMessage(error: unknown, fallback: string): string {
  const message =
    error && typeof error === 'object' && 'userMessage' in error
      ? String((error as { userMessage?: string }).userMessage ?? '')
      : '';
  return message || fallback;
}

function SecretBadge({ field, pending }: { field: CredentialFieldState; pending: boolean }) {
  if (pending) {
    return <Badge variant="warning">Pending save</Badge>;
  }
  if (field.isSet) {
    return (
      <Badge variant="success" className="gap-1">
        Configured{field.version > 1 ? ` · v${field.version}` : ''}
      </Badge>
    );
  }
  return <Badge variant={field.required ? 'error' : 'outline'}>{field.required ? 'Required' : 'Not set'}</Badge>;
}

function SectionCard({ title, description, action, children }: { title: string; description?: string; action?: ReactNode; children: ReactNode }) {
  return (
    <section className="mb-4 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)]">
      <div className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] px-5 py-4">
        <div className="min-w-0 flex-1">
          <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">{title}</h2>
          {description ? <p className="mt-1 max-w-3xl text-xs leading-5 text-[var(--color-text-secondary)]">{description}</p> : null}
        </div>
        {action ? <div className="flex-none">{action}</div> : null}
      </div>
      <div className="space-y-4 p-5">{children}</div>
    </section>
  );
}

interface CreateState {
  kind: string;
  ref: string;
  name: string;
  secrets: Record<string, string>;
}

export function SettingsCredentialBundlesPage() {
  const [bundles, setBundles] = useState<CredentialBundleListItem[]>([]);
  const [kinds, setKinds] = useState<ConnectorKindSchema[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [lifting, setLifting] = useState(false);
  const [create, setCreate] = useState<CreateState | null>(null);
  const [rotateFor, setRotateFor] = useState<{ ref: string; field: string; value: string } | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [bundleList, kindList] = await Promise.all([
        credentialBundleService.list(),
        credentialBundleService.getConnectorKinds(),
      ]);
      setBundles(bundleList);
      setKinds(kindList);
    } catch (err: unknown) {
      setError(resolveUserMessage(err, 'Failed to load credential bundles.'));
    } finally {
      setLoading(false);
      setInitialLoad(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const selectedKind = useMemo(
    () => kinds.find((k) => k.kind === create?.kind) ?? null,
    [kinds, create?.kind],
  );

  const beginCreate = () => {
    const firstKind = kinds[0];
    setCreate({ kind: firstKind?.kind ?? '', ref: '', name: '', secrets: {} });
  };

  const handleCreate = async () => {
    if (!create || !selectedKind) return;
    setSaving(true);
    setError(null);
    try {
      const secrets = Object.fromEntries(
        Object.entries(create.secrets).filter(([, value]) => value.trim().length > 0),
      );
      await credentialBundleService.create({
        ref: create.ref.trim(),
        name: create.name.trim() || create.ref.trim(),
        connectorKind: create.kind,
        secrets,
      });
      toast.success('Credential bundle created.');
      setCreate(null);
      await load();
    } catch (err: unknown) {
      const message = resolveUserMessage(err, 'Failed to create credential bundle.');
      setError(message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  const handleRotate = async () => {
    if (!rotateFor || rotateFor.value.trim().length === 0) return;
    setSaving(true);
    try {
      await credentialBundleService.rotate(rotateFor.ref, {
        field: rotateFor.field,
        newValue: rotateFor.value.trim(),
      });
      toast.success('Secret rotated. The previous value verifies for the grace window.');
      setRotateFor(null);
      await load();
    } catch (err: unknown) {
      toast.error(resolveUserMessage(err, 'Failed to rotate secret.'));
    } finally {
      setSaving(false);
    }
  };

  const handleLift = async () => {
    setLifting(true);
    try {
      const result = await credentialBundleService.liftLegacyFlutterwave();
      toast.success(
        `Lifted ${result.bundleRefs.length} bundle(s); backfilled ${result.payoutsBackfilled} payout(s) and ${result.transmissionsBackfilled} transmission(s).`,
      );
      await load();
    } catch (err: unknown) {
      toast.error(resolveUserMessage(err, 'Failed to lift legacy Flutterwave configuration.'));
    } finally {
      setLifting(false);
    }
  };

  if (initialLoad && loading) {
    return <PageLoadingScreen message="Loading credential bundles" />;
  }

  return (
    <div className="h-full min-h-0 overflow-auto px-8 py-6">
      <div className="mx-auto max-w-5xl">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">
              Settings · Credential bundles
            </p>
            <h2 className="text-2xl font-bold text-[var(--color-text-primary)]">Credential bundles</h2>
            <p className="text-[var(--color-text-secondary)]">
              Partner-owned connector credentials, encrypted at rest. A connector binds a bundle by its reference; secret values are never displayed.
            </p>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" className="gap-1.5" onClick={() => void load()} disabled={loading}>
              <RefreshCw className={cn('h-3 w-3', loading && 'animate-spin')} />Refresh
            </Button>
            <Button variant="outline" size="sm" className="gap-1.5" onClick={() => void handleLift()} disabled={lifting}>
              <Upload className="h-3 w-3" />{lifting ? 'Lifting...' : 'Lift legacy Flutterwave'}
            </Button>
            <Button size="sm" className="gap-1.5" onClick={beginCreate} disabled={kinds.length === 0 || create !== null}>
              <Plus className="h-3 w-3" />New bundle
            </Button>
          </div>
        </div>

        {error ? (
          <div className="mt-4 flex items-start gap-2 rounded-xl border border-[var(--color-danger)]/30 bg-[var(--color-danger)]/5 p-3 text-sm text-[var(--color-danger)]">
            <AlertCircle className="mt-0.5 h-4 w-4" />
            <span>{error}</span>
          </div>
        ) : null}

        <div className="mt-4">
          {create ? (
            <SectionCard
              title="New credential bundle"
              description="Choose a connector kind to generate its credential fields. Secrets are write-only and stored encrypted."
              action={
                <Button variant="ghost" size="sm" className="gap-1" onClick={() => setCreate(null)}>
                  <X className="h-3 w-3" />Cancel
                </Button>
              }
            >
              <div className="grid gap-3 lg:grid-cols-2">
                <div>
                  <Label htmlFor="bundle-kind">Connector kind</Label>
                  <select
                    id="bundle-kind"
                    className="mt-1 flex h-10 w-full rounded-[2px] border border-[var(--color-form-field-border)] bg-[var(--color-form-field-bg)] px-3 text-sm text-[var(--color-form-field-text)]"
                    value={create.kind}
                    onChange={(event) => setCreate({ ...create, kind: event.target.value, secrets: {} })}
                  >
                    {kinds.map((kind) => (
                      <option key={kind.kind} value={kind.kind}>{kind.displayName}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <Label htmlFor="bundle-ref">Reference (immutable)</Label>
                  <Input
                    id="bundle-ref"
                    value={create.ref}
                    placeholder="e.g. fw-uk-oauth"
                    onChange={(event) => setCreate({ ...create, ref: event.target.value })}
                  />
                </div>
                <div className="lg:col-span-2">
                  <Label htmlFor="bundle-name">Display name</Label>
                  <Input
                    id="bundle-name"
                    value={create.name}
                    placeholder="Flutterwave UK payout"
                    onChange={(event) => setCreate({ ...create, name: event.target.value })}
                  />
                </div>
              </div>

              {selectedKind ? (
                <div className="space-y-3 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-4">
                  <p className="text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">Credential fields</p>
                  {selectedKind.credentialFields.map((field) => (
                    <div key={field.name} className="grid gap-2 lg:grid-cols-[220px_minmax(0,1fr)] lg:items-center">
                      <Label htmlFor={`secret-${field.name}`} className="text-[13px]">
                        {field.label}{field.required ? <span className="text-[var(--color-danger)]"> *</span> : null}
                      </Label>
                      <Input
                        id={`secret-${field.name}`}
                        type="password"
                        value={create.secrets[field.name] ?? ''}
                        placeholder={field.required ? 'Required' : 'Optional'}
                        onChange={(event) =>
                          setCreate({ ...create, secrets: { ...create.secrets, [field.name]: event.target.value } })
                        }
                      />
                    </div>
                  ))}
                </div>
              ) : null}

              <div className="flex justify-end">
                <Button
                  size="sm"
                  className="gap-1.5"
                  onClick={() => void handleCreate()}
                  disabled={saving || create.ref.trim().length === 0 || !selectedKind}
                >
                  <Save className="h-3 w-3" />{saving ? 'Saving...' : 'Create bundle'}
                </Button>
              </div>
            </SectionCard>
          ) : null}

          {bundles.length === 0 && !create ? (
            <div className="rounded-xl border border-dashed border-[var(--color-border-light)] p-10 text-center">
              <KeyRound className="mx-auto h-6 w-6 text-[var(--color-text-tertiary)]" />
              <p className="mt-2 text-sm font-medium text-[var(--color-text-primary)]">No credential bundles yet</p>
              <p className="mt-1 text-xs text-[var(--color-text-secondary)]">
                Create a bundle, or lift existing Flutterwave settings into one.
              </p>
            </div>
          ) : null}

          {bundles.map((bundle) => (
            <SectionCard
              key={bundle.ref}
              title={bundle.name}
              description={`${bundle.connectorKind} · ${bundle.boundConnectorIds.length} connector(s) bound`}
              action={
                <Badge variant="secondary" className="font-mono text-[10.5px]">{bundle.ref}</Badge>
              }
            >
              <div className="flex flex-wrap gap-2">
                {bundle.fields.map((field) => (
                  <div key={field.name} className="flex items-center gap-2 rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-3 py-1.5">
                    <span className="text-[12px] text-[var(--color-text-primary)]">{field.label}</span>
                    <SecretBadge field={field} pending={false} />
                    {field.isSet ? (
                      <button
                        type="button"
                        className="text-[var(--color-text-tertiary)] hover:text-[var(--color-brand-primary)]"
                        title="Rotate this secret"
                        onClick={() => setRotateFor({ ref: bundle.ref, field: field.name, value: '' })}
                      >
                        <RotateCw className="h-3 w-3" />
                      </button>
                    ) : null}
                  </div>
                ))}
              </div>

              {rotateFor?.ref === bundle.ref ? (
                <div className="grid gap-2 rounded-lg border border-[var(--color-warning)]/40 bg-[var(--color-warning-light)]/40 p-3 lg:grid-cols-[1fr_auto_auto] lg:items-center">
                  <Input
                    type="password"
                    value={rotateFor.value}
                    placeholder={`New value for ${rotateFor.field}`}
                    onChange={(event) => setRotateFor({ ...rotateFor, value: event.target.value })}
                  />
                  <Button size="sm" className="gap-1.5" onClick={() => void handleRotate()} disabled={saving || rotateFor.value.trim().length === 0}>
                    <RotateCw className="h-3 w-3" />Rotate
                  </Button>
                  <Button variant="ghost" size="sm" onClick={() => setRotateFor(null)}>Cancel</Button>
                </div>
              ) : null}
            </SectionCard>
          ))}
        </div>
      </div>
    </div>
  );
}
