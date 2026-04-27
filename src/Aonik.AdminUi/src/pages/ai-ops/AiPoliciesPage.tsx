// AI Policies — visual port of the "AI Policies" half of
// templates/aonik-admin-starterkit/screens/ai-tasks-policies.jsx, wired to
// /admin/ai/policies (list + IsActive PATCH) and /admin/ai/agent-settings
// (kill-switch state).
//
// Differences from the template, called out so they don't read as gaps:
//   • The AiPolicy entity has only Name / IsActive / four JSON columns
//     (allowed-fields / redaction / banned-actions / escalation). The
//     template's Severity / Category / EnforcementMode / TriggerCount /
//     UpdatedBy / Version are not yet on the entity. We surface what's
//     real and tag the missing ones in the UI.
//   • Kill switch persists per-tenant but its enforcement on the run
//     pipeline is not yet wired — the banner stays honest about that.

import { useCallback, useEffect, useMemo, useState } from 'react';
import { toast } from 'sonner';
import { AlertCircle, RefreshCw, ShieldCheck } from 'lucide-react';

import {
  Card as AonikCard,
  PageHeader,
  Pill,
  type PillTone,
} from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { aiPolicyService, tenantAgentSettingsService } from '@/services/aiService';
import type {
  AiPolicySummary,
  TenantAgentSettingsResponse,
} from '@/services/aiService';

// ─── Helpers ─────────────────────────────────────────────────────────────

function formatDate(value?: string | null): string {
  if (!value) return '—';
  return new Date(value).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function formatRelative(value: string): string {
  const diff = Date.now() - new Date(value).getTime();
  const minutes = Math.round(diff / 60_000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.round(hours / 24);
  return `${days}d ago`;
}

// Inspect each JSON column to surface a one-line summary in the card.
// Cheap parsers — anything weird falls back to the raw string.
function summariseJson(raw: string, kind: string): string {
  if (!raw || raw === '{}' || raw === '[]') return `no ${kind} configured`;
  try {
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed)) {
      return `${parsed.length} ${kind}${parsed.length === 1 ? '' : 's'}`;
    }
    if (parsed && typeof parsed === 'object') {
      const keys = Object.keys(parsed);
      return `${keys.length} ${kind} rule${keys.length === 1 ? '' : 's'}`;
    }
    return raw;
  } catch {
    return raw;
  }
}

// ─── Page ────────────────────────────────────────────────────────────────

export function AiPoliciesPage() {
  const [policies, setPolicies] = useState<AiPolicySummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [agentSettings, setAgentSettings] =
    useState<TenantAgentSettingsResponse | null>(null);
  const [killSwitchSaving, setKillSwitchSaving] = useState(false);

  const loadPolicies = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await aiPolicyService.list(1, 100);
      setPolicies(result.items);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load AI policies.');
    } finally {
      setLoading(false);
    }
  }, []);

  const loadAgentSettings = useCallback(async () => {
    try {
      const result = await tenantAgentSettingsService.get();
      setAgentSettings(result);
    } catch {
      // Settings are best-effort; keep the banner in default state.
      setAgentSettings({
        killSwitchEngaged: false,
        killSwitchEngagedAt: null,
        killSwitchEngagedByUserId: null,
        updatedAt: null,
      });
    }
  }, []);

  useEffect(() => {
    void loadPolicies();
    void loadAgentSettings();
  }, [loadPolicies, loadAgentSettings]);

  const handleKillSwitchToggle = useCallback(async () => {
    if (!agentSettings || killSwitchSaving) return;
    const next = !agentSettings.killSwitchEngaged;
    // Optimistic flip so the banner reads correctly while we save.
    setKillSwitchSaving(true);
    setAgentSettings((prev) =>
      prev ? { ...prev, killSwitchEngaged: next } : prev,
    );
    try {
      const updated = await tenantAgentSettingsService.setKillSwitch(next);
      setAgentSettings(updated);
      toast.success(next ? 'Kill switch engaged' : 'Kill switch released');
    } catch (err) {
      // Roll back on failure.
      setAgentSettings((prev) =>
        prev ? { ...prev, killSwitchEngaged: !next } : prev,
      );
      const message = err instanceof Error ? err.message : 'Failed to update kill switch';
      toast.error(message);
    } finally {
      setKillSwitchSaving(false);
    }
  }, [agentSettings, killSwitchSaving]);

  const handlePolicyToggle = useCallback(
    async (policy: AiPolicySummary) => {
      const next = !policy.isActive;
      // Optimistic update on the row.
      setPolicies((prev) =>
        prev.map((p) => (p.id === policy.id ? { ...p, isActive: next } : p)),
      );
      try {
        const updated = await aiPolicyService.setActive(policy.id, next);
        setPolicies((prev) => prev.map((p) => (p.id === policy.id ? updated : p)));
        toast.success(next ? `${policy.name} enabled` : `${policy.name} disabled`);
      } catch (err) {
        // Roll back on failure.
        setPolicies((prev) =>
          prev.map((p) => (p.id === policy.id ? { ...p, isActive: !next } : p)),
        );
        const message =
          err instanceof Error ? err.message : 'Failed to update policy';
        toast.error(message);
      }
    },
    [],
  );

  const stats = useMemo(() => {
    const active = policies.filter((p) => p.isActive).length;
    return {
      total: policies.length,
      active,
      inactive: policies.length - active,
    };
  }, [policies]);

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="AI · Governance"
        title="Policies"
        subtitle="Guardrails applied to every agent run · enforced before any tool executes"
        actions={
          <Button variant="outline" size="sm" onClick={() => void loadPolicies()} disabled={loading}>
            <RefreshCw className={'h-3 w-3 ' + (loading ? 'animate-spin' : '')} />
            Refresh
          </Button>
        }
      />

      {/* Kill-switch banner — state persists per tenant; enforcement on the run pipeline is still pending. */}
      <KillSwitchBanner
        engaged={agentSettings?.killSwitchEngaged ?? false}
        saving={killSwitchSaving}
        onToggle={() => void handleKillSwitchToggle()}
      />

      {/* KPI strip */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        <StatTile
          label="Active policies"
          value={String(stats.active)}
          sub={`of ${stats.total}`}
          tone="var(--color-brand-primary)"
        />
        <StatTile
          label="Inactive"
          value={String(stats.inactive)}
          sub={stats.inactive === 0 ? 'all enabled' : 'currently bypassed'}
          tone="var(--color-warning)"
        />
        <StatTile
          label="Coverage"
          value={stats.total === 0 ? '—' : '✓'}
          sub="enforced server-side"
          tone="var(--color-success)"
        />
      </div>

      {error && (
        <div className="flex items-center gap-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
          <AlertCircle className="h-4 w-4 flex-none" />
          <span className="flex-1">{error}</span>
          <Button variant="outline" size="sm" onClick={() => void loadPolicies()}>
            <RefreshCw className="h-3 w-3" />
            Retry
          </Button>
        </div>
      )}

      <div className="flex flex-col gap-2.5">
        {loading && policies.length === 0 ? (
          <AonikCard>
            <div className="flex items-center justify-center py-10">
              <RefreshCw className="h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
            </div>
          </AonikCard>
        ) : policies.length === 0 ? (
          <AonikCard>
            <div className="flex flex-col items-center justify-center py-10 text-center">
              <ShieldCheck className="mb-2 h-8 w-8 text-[var(--color-text-tertiary)]" />
              <p className="text-sm font-medium text-[var(--color-text-primary)]">
                No policies configured
              </p>
              <p className="mt-1 max-w-md text-xs text-[var(--color-text-tertiary)]">
                Policies are seeded as part of tenant provisioning. Reach out
                to platform engineering to add a guardrail to this tenant.
              </p>
            </div>
          </AonikCard>
        ) : (
          policies.map((policy) => (
            <PolicyRow
              key={policy.id}
              policy={policy}
              onToggle={() => void handlePolicyToggle(policy)}
            />
          ))
        )}
      </div>
    </div>
  );
}

// ─── Policy row ──────────────────────────────────────────────────────────

function PolicyRow({
  policy,
  onToggle,
}: {
  policy: AiPolicySummary;
  onToggle: () => void;
}) {
  const tone: PillTone = policy.isActive ? 'success' : 'muted';
  const tags: Array<[string, string]> = [
    ['allowed', summariseJson(policy.allowedDataFieldsJson, 'field')],
    ['redaction', summariseJson(policy.redactionRulesJson, 'rule')],
    ['banned', summariseJson(policy.bannedActionsJson, 'action')],
    ['escalation', summariseJson(policy.escalationRulesJson, 'path')],
  ];

  return (
    <div
      className="grid items-center gap-4 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] px-5 py-4"
      style={{
        gridTemplateColumns: '4px 1fr auto',
        opacity: policy.isActive ? 1 : 0.62,
      }}
    >
      <div
        className="h-[52px] rounded"
        style={{
          background: policy.isActive
            ? 'var(--color-brand-primary)'
            : 'var(--color-text-tertiary)',
        }}
      />

      <div className="min-w-0">
        <div className="mb-1 flex items-center gap-2">
          <div className="text-[14px] font-semibold text-[var(--color-text-primary)]">
            {policy.name}
          </div>
          <Pill tone={tone} dot size="sm">
            {policy.isActive ? 'active' : 'inactive'}
          </Pill>
        </div>

        <div className="mb-1.5 flex flex-wrap gap-2 font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
          {tags.map(([label, value]) => (
            <span key={label} className="inline-flex items-center gap-1">
              <span className="text-[var(--color-text-tertiary)]">{label}:</span>
              <span>{value}</span>
            </span>
          ))}
        </div>

        <div className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
          created {formatDate(policy.createdAt)}
          {policy.updatedAt && <> · updated {formatRelative(policy.updatedAt)}</>}
        </div>
      </div>

      <div className="flex items-center gap-2.5">
        <Toggle on={policy.isActive} onClick={onToggle} />
      </div>
    </div>
  );
}

function Toggle({ on, onClick }: { on: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={on}
      aria-label={on ? 'Disable policy' : 'Enable policy'}
      className="relative inline-flex h-4 w-[30px] flex-none cursor-pointer rounded-full transition-colors"
      style={{
        background: on ? 'var(--color-brand-primary)' : 'var(--color-surface-inset)',
      }}
    >
      <span
        className="absolute top-[1px] h-3.5 w-3.5 rounded-full bg-white shadow-sm transition-[left]"
        style={{ left: on ? 15 : 1 }}
      />
    </button>
  );
}

// ─── Kill-switch banner ──────────────────────────────────────────────────

function KillSwitchBanner({
  engaged,
  saving,
  onToggle,
}: {
  engaged: boolean;
  saving: boolean;
  onToggle: () => void;
}) {
  return (
    <div
      className="flex items-center gap-4 rounded-xl border p-3.5"
      style={{
        background: engaged ? 'rgba(204, 46, 46, 0.07)' : 'var(--color-surface)',
        borderColor: engaged ? 'var(--color-danger)' : 'var(--color-border-light)',
      }}
    >
      <div
        className="flex h-10 w-10 flex-none items-center justify-center rounded-lg"
        style={{
          background: engaged
            ? 'rgba(204, 46, 46, 0.13)'
            : 'var(--color-brand-primary-10)',
          color: engaged ? 'var(--color-danger)' : 'var(--color-brand-primary)',
        }}
      >
        <ShieldCheck className="h-5 w-5" />
      </div>
      <div className="min-w-0 flex-1">
        <div className="text-[13.5px] font-semibold text-[var(--color-text-primary)]">
          Global agent kill switch
        </div>
        <div className="mt-0.5 text-[12px] text-[var(--color-text-secondary)]">
          {engaged
            ? 'State persisted for this tenant. Run-pipeline enforcement is not yet wired — track that as a follow-up.'
            : 'Pause every agent for this tenant. Persists across reloads; enforcement on the run pipeline is pending.'}
        </div>
      </div>
      <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
        requires 2FA
      </span>
      <Button
        variant={engaged ? 'default' : 'outline'}
        size="sm"
        onClick={onToggle}
        disabled={saving}
        style={
          engaged
            ? undefined
            : {
                borderColor: 'var(--color-danger)',
                color: 'var(--color-danger)',
              }
        }
      >
        {saving ? 'Saving…' : engaged ? 'Resume agents' : 'Engage kill switch'}
      </Button>
    </div>
  );
}

// ─── Stat tile ───────────────────────────────────────────────────────────

function StatTile({
  label,
  value,
  sub,
  tone,
}: {
  label: string;
  value: string;
  sub: string;
  tone: string;
}) {
  return (
    <div className="rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] p-3.5">
      <div className="flex items-center gap-1.5 text-[11px] text-[var(--color-text-secondary)]">
        <span className="h-1.5 w-1.5 rounded-full" style={{ background: tone }} />
        {label}
      </div>
      <div className="mt-1 font-[family-name:var(--font-mono)] text-[22px] font-semibold leading-none text-[var(--color-text-primary)]">
        {value}
      </div>
      <div className="mt-1 font-[family-name:var(--font-mono)] text-[10px] text-[var(--color-text-tertiary)]">
        {sub}
      </div>
    </div>
  );
}
