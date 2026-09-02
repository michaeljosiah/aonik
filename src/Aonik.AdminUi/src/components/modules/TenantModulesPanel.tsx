import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, Lock, RefreshCw, Save } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { formatDateTime } from '@/lib/format';
import { invalidateModuleManifest } from '@/modules/manifestCache';
import { tenantModuleService } from '@/services/tenantModuleService';
import type {
  ModuleDependencyErrorBody,
  TenantModuleItemResponse,
  TenantModuleSource,
  TenantModuleToggleRequest,
} from '@/types';

export interface TenantModulesPanelProps {
  tenantId: string;
  /** Tenant admins read; only host admins may change module state. */
  readOnly: boolean;
}

interface PendingCascade {
  conflict: ModuleDependencyErrorBody;
  /** The toggles that were rejected, resubmitted together with the cascade. */
  toggles: TenantModuleToggleRequest[];
}

const sourceLabels: Record<TenantModuleSource, string> = {
  core: 'Core module',
  default: 'Platform default',
  pack: 'Set by configuration pack',
  explicit: 'Set by host administrator',
};

function readUserMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'userMessage' in err) {
    const message = (err as { userMessage?: unknown }).userMessage;
    if (typeof message === 'string' && message.trim().length > 0) return message;
  }
  return fallback;
}

function readDependencyConflict(err: unknown): ModuleDependencyErrorBody | null {
  const response = (err as { response?: { status?: number; data?: unknown } } | null)?.response;
  if (!response || response.status !== 409) return null;

  const data = response.data as Partial<ModuleDependencyErrorBody> | null | undefined;
  if (!data || typeof data !== 'object') return null;
  if (data.code !== 'module.dependency_missing' && data.code !== 'module.dependents_enabled') return null;
  if (typeof data.moduleId !== 'string') return null;

  return {
    error: typeof data.error === 'string' ? data.error : '',
    code: data.code,
    moduleId: data.moduleId,
    relatedModuleIds: Array.isArray(data.relatedModuleIds)
      ? data.relatedModuleIds.filter((id): id is string => typeof id === 'string')
      : [],
  };
}

function joinNames(names: string[]): string {
  if (names.length === 0) return '';
  if (names.length === 1) return names[0];
  return `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`;
}

export function TenantModulesPanel({ tenantId, readOnly }: TenantModulesPanelProps) {
  const [modules, setModules] = useState<TenantModuleItemResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [draft, setDraft] = useState<Record<string, boolean>>({});
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [cascade, setCascade] = useState<PendingCascade | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await tenantModuleService.get(tenantId);
      setModules(response.modules);
      setDraft({});
      setCascade(null);
    } catch (err) {
      setError(readUserMessage(err, 'Could not load modules for this organisation.'));
    } finally {
      setLoading(false);
    }
  }, [tenantId]);

  useEffect(() => {
    void load();
  }, [load]);

  const nameOf = useCallback(
    (moduleId: string) => modules.find((m) => m.moduleId === moduleId)?.name ?? moduleId,
    [modules],
  );

  const effectiveState = useCallback(
    (item: TenantModuleItemResponse) => (item.isCore ? true : draft[item.moduleId] ?? item.isEnabled),
    [draft],
  );

  const pendingToggles = useMemo<TenantModuleToggleRequest[]>(() => {
    const trimmedReason = reason.trim();
    return modules
      .filter((m) => !m.isCore && draft[m.moduleId] !== undefined && draft[m.moduleId] !== m.isEnabled)
      .map((m) => ({
        moduleId: m.moduleId,
        isEnabled: draft[m.moduleId],
        reason: trimmedReason.length > 0 ? trimmedReason : null,
      }));
  }, [draft, modules, reason]);

  const enabledCount = modules.filter((m) => effectiveState(m)).length;

  const submit = useCallback(
    async (toggles: TenantModuleToggleRequest[]) => {
      if (toggles.length === 0) return;
      setSaving(true);
      setError(null);
      try {
        const response = await tenantModuleService.update(tenantId, { modules: toggles });
        setModules(response.modules);
        setDraft({});
        setReason('');
        setCascade(null);
        // The sidebar, router and settings tiles all read the manifest.
        invalidateModuleManifest();
        toast.success('Modules updated');
      } catch (err) {
        const conflict = readDependencyConflict(err);
        if (conflict) {
          setCascade({ conflict, toggles });
        } else {
          setError(readUserMessage(err, 'Could not update modules.'));
        }
      } finally {
        setSaving(false);
      }
    },
    [tenantId],
  );

  const resubmitWithCascade = useCallback(() => {
    if (!cascade) return;
    const { conflict, toggles } = cascade;
    const trimmedReason = reason.trim();
    const cascadeState = conflict.code === 'module.dependency_missing';
    const related = new Set(conflict.relatedModuleIds);
    const merged: TenantModuleToggleRequest[] = [
      ...toggles.filter((t) => !related.has(t.moduleId)),
      ...conflict.relatedModuleIds.map((moduleId) => ({
        moduleId,
        isEnabled: cascadeState,
        reason: trimmedReason.length > 0 ? trimmedReason : null,
      })),
    ];
    void submit(merged);
  }, [cascade, reason, submit]);

  const discard = () => {
    setDraft({});
    setReason('');
    setCascade(null);
    setError(null);
  };

  if (loading) {
    return (
      <div className="flex items-center gap-2 py-6 text-sm text-[var(--color-text-secondary)]">
        <RefreshCw className="h-4 w-4 animate-spin" />
        Loading modules
      </div>
    );
  }

  if (error && modules.length === 0) {
    return (
      <div className="space-y-3">
        <div className="flex items-start gap-2 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-4 py-3 text-sm text-[var(--color-error)]">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{error}</span>
        </div>
        <Button variant="outline" size="sm" onClick={() => void load()}>
          <RefreshCw className="h-4 w-4" />
          Try again
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-[var(--color-text-secondary)]">
          {enabledCount} of {modules.length} modules enabled
          {readOnly ? '. Module state is managed by the host administrator.' : '.'}
        </p>
        <Button variant="ghost" size="sm" onClick={() => void load()} disabled={saving}>
          <RefreshCw className="h-4 w-4" />
          Refresh
        </Button>
      </div>

      {error && (
        <div className="flex items-start gap-2 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-4 py-3 text-sm text-[var(--color-error)]">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      <div className="space-y-2">
        {modules.map((item) => {
          const enabled = effectiveState(item);
          const changed = !item.isCore && draft[item.moduleId] !== undefined && draft[item.moduleId] !== item.isEnabled;
          return (
            <div
              key={item.moduleId}
              className="rounded-md border border-[var(--color-border-light)] px-4 py-3"
            >
              <div className="flex items-start justify-between gap-4">
                <div className="min-w-0 space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-sm font-medium text-[var(--color-text-primary)]">{item.name}</span>
                    {item.isCore && (
                      <Badge variant="secondary" className="gap-1">
                        <Lock className="h-3 w-3" />
                        Core
                      </Badge>
                    )}
                    <Badge variant={enabled ? 'success' : 'secondary'}>{enabled ? 'Enabled' : 'Disabled'}</Badge>
                    {changed && <Badge variant="warning">Unsaved</Badge>}
                  </div>
                  <p className="text-xs text-[var(--color-text-secondary)]">{item.description}</p>
                  {item.dependsOn.length > 0 && (
                    <p className="text-xs text-[var(--color-text-tertiary)]">
                      Needs: {item.dependsOn.map(nameOf).join(', ')}
                    </p>
                  )}
                  <p className="text-xs text-[var(--color-text-tertiary)]">
                    {item.isCore
                      ? 'Always enabled and cannot be switched off.'
                      : sourceLabels[item.source] ?? item.source}
                    {!item.isCore && item.updatedAt ? `, last changed ${formatDateTime(item.updatedAt)}` : ''}
                  </p>
                  {!item.isCore && item.reason && (
                    <p className="text-xs text-[var(--color-text-tertiary)]">Reason: {item.reason}</p>
                  )}
                </div>
                {!readOnly && !item.isCore && (
                  <Switch
                    aria-label={`${enabled ? 'Disable' : 'Enable'} ${item.name}`}
                    checked={enabled}
                    disabled={saving}
                    onCheckedChange={(next) => {
                      setCascade(null);
                      setDraft((prev) => ({ ...prev, [item.moduleId]: next }));
                    }}
                  />
                )}
              </div>
            </div>
          );
        })}
      </div>

      {!readOnly && cascade && (
        <div className="space-y-3 rounded-md border border-[var(--color-warning)] bg-[var(--color-warning-light)] px-4 py-3">
          <div className="flex items-start gap-2 text-sm text-[var(--color-text-primary)]">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-[var(--color-warning)]" />
            <div className="space-y-1">
              {cascade.conflict.code === 'module.dependency_missing' ? (
                <>
                  <p className="font-medium">
                    {nameOf(cascade.conflict.moduleId)} needs {joinNames(cascade.conflict.relatedModuleIds.map(nameOf))}.
                  </p>
                  <p>Also enable {joinNames(cascade.conflict.relatedModuleIds.map(nameOf))}?</p>
                </>
              ) : (
                <>
                  <p className="font-medium">
                    {joinNames(cascade.conflict.relatedModuleIds.map(nameOf))} depend{cascade.conflict.relatedModuleIds.length === 1 ? 's' : ''} on {nameOf(cascade.conflict.moduleId)}.
                  </p>
                  <p>Disable {joinNames(cascade.conflict.relatedModuleIds.map(nameOf))} first.</p>
                </>
              )}
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button size="sm" onClick={resubmitWithCascade} disabled={saving}>
              {cascade.conflict.code === 'module.dependency_missing'
                ? `Enable ${joinNames(cascade.conflict.relatedModuleIds.map(nameOf))} too`
                : `Also disable ${joinNames(cascade.conflict.relatedModuleIds.map(nameOf))}`}
            </Button>
            <Button variant="outline" size="sm" onClick={() => setCascade(null)} disabled={saving}>
              Keep as is
            </Button>
          </div>
        </div>
      )}

      {!readOnly && pendingToggles.length > 0 && (
        <div className="space-y-3 border-t border-[var(--color-border-light)] pt-4">
          <div className="space-y-2">
            <Label htmlFor={`module-reason-${tenantId}`}>Reason (optional)</Label>
            <Textarea
              id={`module-reason-${tenantId}`}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Why is this changing? Recorded in the audit log."
              rows={2}
              disabled={saving}
            />
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button size="sm" onClick={() => void submit(pendingToggles)} disabled={saving}>
              <Save className="h-4 w-4" />
              {saving ? 'Saving' : `Save ${pendingToggles.length} change${pendingToggles.length === 1 ? '' : 's'}`}
            </Button>
            <Button variant="outline" size="sm" onClick={discard} disabled={saving}>
              Discard
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
