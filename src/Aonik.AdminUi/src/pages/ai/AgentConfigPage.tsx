// Agents page — visual port of `ScreenAgentsPage` from
// templates/aonik-admin-starterkit/screens/agents-page.jsx, wired to
// agentConfigService and agentRunService.
//
// Card / list dual layout, filter tabs (All / System / Domain / Active /
// Inactive — Running/Paused are derived from isActive since we don't have
// a live runs-in-progress feed yet), and a slide-out AgentEditPanel.
//
// Per-card live counters (runs / lastRun) come from a per-agent
// agentRunService.list({pageSize: 1}) lookup so we can show totalCount and
// the most recent updatedAt without a dedicated stats endpoint. Confidence
// is shown as `—` until the platform aggregates it.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  AlertCircle,
  ArrowRight,
  ChevronRight,
  Edit3,
  Filter as FilterIcon,
  LayoutGrid,
  List as ListIcon,
  Plus,
  RefreshCw,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  PageHeader,
  Pill,
} from '@/components/layout/aonik';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { agentConfigService, agentRunService } from '@/services/aiService';
import type { AgentConfigurationResponse } from '@/types/ai';
import { cn } from '@/lib/utils';

import { AgentPortrait } from './agents/AgentPortrait';
import { StateDot } from './agents/StateDot';
import { AgentEditPanel } from './agents/AgentEditPanel';
import {
  countTools,
  deriveAgentColor,
  deriveAgentGlyph,
  deriveAgentState,
  deriveAgentTagline,
  deriveAutoApply,
  deriveKindLabel,
  formatRelativeTime,
  isPinnedAgent,
} from './agents/agentMeta';

type Layout = 'card' | 'list';
type Filter = 'All' | 'System' | 'Domain' | 'Running' | 'Paused';

const FILTERS: Filter[] = ['All', 'System', 'Domain', 'Running', 'Paused'];

interface AgentRunStats {
  totalRuns: number;
  lastRunAt: string | null;
}

// ── Page ────────────────────────────────────────────────────────────

export function AgentConfigPage() {
  const navigate = useNavigate();

  const [configs, setConfigs] = useState<AgentConfigurationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [layout, setLayout] = useState<Layout>('card');
  const [filter, setFilter] = useState<Filter>('All');
  const [editing, setEditing] = useState<AgentConfigurationResponse | null>(null);
  const [stats, setStats] = useState<Record<string, AgentRunStats>>({});

  const requestIdRef = useRef(0);

  const loadAgents = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);
    try {
      const list = await agentConfigService.list();
      if (requestIdRef.current !== requestId) return;
      setConfigs(list);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load agents.');
    } finally {
      if (requestIdRef.current === requestId) {
        setLoading(false);
        setInitialLoad(false);
      }
    }
  }, []);

  useEffect(() => {
    void loadAgents();
  }, [loadAgents]);

  // Prefer the tenant override per agent name (matches the previous page's
  // de-duping rule). Memoised so deriving the visible list isn't quadratic.
  const uniqueAgents = useMemo(() => {
    const map = new Map<string, AgentConfigurationResponse>();
    for (const config of configs) {
      const existing = map.get(config.name);
      if (!existing || config.isOverride) map.set(config.name, config);
    }
    return Array.from(map.values());
  }, [configs]);

  // Lazy-load per-agent run stats. Fires once per page load against each
  // unique agent — agents lists are small (typically < 20), so the N+1 is OK.
  useEffect(() => {
    if (uniqueAgents.length === 0) return;
    let cancelled = false;
    void (async () => {
      const entries = await Promise.all(
        uniqueAgents.map(async (a) => {
          try {
            const result = await agentRunService.list(a.id, 1, 1);
            const last = result.items[0];
            return [a.id, { totalRuns: result.totalCount, lastRunAt: last?.updatedAt ?? null }] as const;
          } catch {
            return [a.id, { totalRuns: 0, lastRunAt: null }] as const;
          }
        }),
      );
      if (cancelled) return;
      setStats(Object.fromEntries(entries));
    })();
    return () => {
      cancelled = true;
    };
  }, [uniqueAgents]);

  // Counts for the filter tabs (computed against the de-duped list).
  const counts = useMemo(() => {
    return {
      All: uniqueAgents.length,
      System: uniqueAgents.filter((a) => a.agentType === 1).length,
      Domain: uniqueAgents.filter((a) => a.agentType === 0).length,
      Running: uniqueAgents.filter((a) => a.isActive && (stats[a.id]?.totalRuns ?? 0) > 0).length,
      Paused: uniqueAgents.filter((a) => !a.isActive).length,
    } as Record<Filter, number>;
  }, [uniqueAgents, stats]);

  const visible = useMemo(() => {
    return uniqueAgents.filter((a) => {
      if (filter === 'System' && a.agentType !== 1) return false;
      if (filter === 'Domain' && a.agentType !== 0) return false;
      if (filter === 'Running' && (!a.isActive || (stats[a.id]?.totalRuns ?? 0) === 0)) return false;
      if (filter === 'Paused' && a.isActive) return false;
      return true;
    });
  }, [uniqueAgents, filter, stats]);

  const handleAgentSaved = (updated: AgentConfigurationResponse) => {
    setConfigs((prev) => {
      const exists = prev.some((c) => c.id === updated.id);
      if (exists) {
        return prev.map((c) => (c.id === updated.id ? updated : c));
      }
      return [...prev, updated];
    });
  };

  const handleAgentDeleted = () => {
    void loadAgents();
  };

  const subtitle =
    uniqueAgents.length > 0
      ? `${uniqueAgents.length} configured agent${uniqueAgents.length === 1 ? '' : 's'} · ${counts.Running} running now · ${counts.System} system / ${counts.Domain} domain`
      : 'Configure domain agents, assign models, and manage overrides.';

  if (initialLoad) {
    return <PageLoadingScreen message="Loading agents" />;
  }

  return (
    <div className="relative h-full overflow-hidden">
      <div className="flex h-full flex-col gap-5 overflow-auto p-6 md:px-8">
        <PageHeader
          eyebrow="AI · Agents"
          title="Agents"
          subtitle={subtitle}
          actions={
            <>
              <Button variant="outline" size="sm" onClick={() => void loadAgents()} disabled={loading}>
                <RefreshCw className={cn('h-3 w-3', loading && 'animate-spin')} />
                Re-sync
              </Button>
              <Button variant="outline" size="sm">
                <FilterIcon className="h-3 w-3" />
                Filter
              </Button>
              <Button size="sm">
                <Plus className="h-3 w-3" />
                New agent
              </Button>
            </>
          }
        />

        {/* Tabs + layout switch */}
        <div className="flex flex-wrap items-center gap-3.5 border-b border-[var(--color-border-light)] pb-3">
          <div className="flex items-center gap-1">
            {FILTERS.map((f) => {
              const active = filter === f;
              return (
                <button
                  key={f}
                  type="button"
                  onClick={() => setFilter(f)}
                  className={cn(
                    'inline-flex h-[28px] items-center gap-1.5 rounded-md px-3 text-[12px] font-medium transition-colors',
                    active
                      ? 'bg-[var(--color-brand-primary-10)] font-semibold text-[var(--color-brand-primary)]'
                      : 'text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]',
                  )}
                >
                  {f}
                  <span
                    className={cn(
                      'font-[family-name:var(--font-mono)] text-[10px] font-semibold',
                      active ? 'text-[var(--color-brand-secondary)]' : 'text-[var(--color-text-tertiary)]',
                    )}
                  >
                    {counts[f]}
                  </span>
                </button>
              );
            })}
          </div>

          <div className="flex-1" />

          {/* Layout switch */}
          <div className="inline-flex h-[28px] overflow-hidden rounded-md border border-[var(--color-border-light)]">
            {([
              { value: 'card' as const, icon: LayoutGrid, label: 'Cards' },
              { value: 'list' as const, icon: ListIcon, label: 'List' },
            ]).map((opt) => {
              const active = layout === opt.value;
              return (
                <button
                  key={opt.value}
                  type="button"
                  onClick={() => setLayout(opt.value)}
                  className={cn(
                    'inline-flex items-center gap-1.5 px-3 text-[11.5px] transition-colors',
                    active
                      ? 'bg-[var(--color-brand-primary)] font-semibold text-white'
                      : 'bg-transparent font-medium text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]',
                  )}
                >
                  <opt.icon className="h-3 w-3" />
                  {opt.label}
                </button>
              );
            })}
          </div>
        </div>

        {error && (
          <div className="flex items-center gap-2 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-[12.5px] text-[var(--color-error)]">
            <AlertCircle className="h-3.5 w-3.5 flex-none" />
            <span className="flex-1">{error}</span>
            <Button variant="outline" size="sm" onClick={() => void loadAgents()}>
              Retry
            </Button>
          </div>
        )}

        {/* List */}
        {loading && visible.length === 0 ? (
          <div className="flex flex-1 items-center justify-center text-[13px] text-[var(--color-text-secondary)]">
            Loading agents…
          </div>
        ) : visible.length === 0 ? (
          <div className="flex flex-1 flex-col items-center justify-center gap-3 rounded-[12px] border border-dashed border-[var(--color-border)] bg-[var(--color-surface-inset)] py-16 text-center">
            <div className="text-[14px] font-semibold text-[var(--color-text-primary)]">
              No agents match this filter
            </div>
            <div className="w-full max-w-[24rem] text-[12.5px] text-[var(--color-text-secondary)]">
              Adjust the active tab or search query, or create a new agent to get started.
            </div>
          </div>
        ) : layout === 'card' ? (
          <div className="grid gap-3.5" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(360px, 1fr))' }}>
            {visible.map((agent) => (
              <AgentCard
                key={agent.id}
                agent={agent}
                stats={stats[agent.id]}
                onEdit={() => setEditing(agent)}
                onOpen={() => navigate(`/ai/agents/${encodeURIComponent(agent.name)}`)}
              />
            ))}
          </div>
        ) : (
          <AgentListView
            agents={visible}
            stats={stats}
            onEdit={(agent) => setEditing(agent)}
            onOpen={(agent) => navigate(`/ai/agents/${encodeURIComponent(agent.name)}`)}
          />
        )}
      </div>

      {editing && (
        <AgentEditPanel
          agent={editing}
          onClose={() => setEditing(null)}
          onSaved={(a) => {
            handleAgentSaved(a);
            setEditing(a);
          }}
          onDeleted={handleAgentDeleted}
        />
      )}
    </div>
  );
}

// ── Card layout ─────────────────────────────────────────────────────

interface AgentCardProps {
  agent: AgentConfigurationResponse;
  stats: AgentRunStats | undefined;
  onEdit: () => void;
  onOpen: () => void;
}

function AgentCard({ agent, stats, onEdit, onOpen }: AgentCardProps) {
  const color = deriveAgentColor(agent.name);
  const glyph = deriveAgentGlyph(agent.name);
  const tagline = deriveAgentTagline(agent.description);
  const state = deriveAgentState(agent.isActive, stats?.totalRuns ? stats.totalRuns > 0 : false);
  const autoApply = deriveAutoApply(agent.riskTier);
  const pinned = isPinnedAgent(agent);
  const tools = countTools(agent.toolsetIdsJson);

  return (
    <div className="relative flex flex-col gap-3.5 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4 shadow-[var(--shadow-sm)]">
      {pinned && (
        <span
          className="absolute bottom-4 left-[-1px] top-4 w-[3px] rounded-full"
          style={{ background: 'var(--color-brand-secondary)' }}
        />
      )}

      <div className="flex items-start gap-3.5">
        <AgentPortrait name={agent.name} color={color} glyph={glyph} size={64} />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={onOpen}
              className="text-left text-[15px] font-semibold tracking-[-0.005em] text-[var(--color-text-primary)] hover:text-[var(--color-brand-primary)]"
            >
              {agent.name}
            </button>
            <Pill tone="info" size="sm">
              {deriveKindLabel(agent.agentType)}
            </Pill>
            {agent.isOverride && (
              <Pill tone="pending" size="sm">
                Override
              </Pill>
            )}
          </div>
          <div className="mt-0.5 line-clamp-1 text-[12px] text-[var(--color-text-secondary)]">
            {tagline || agent.domain}
          </div>
          <div className="mt-2">
            <StateDot state={state} />
          </div>
        </div>
        <button
          type="button"
          onClick={onEdit}
          aria-label="Edit agent"
          className="grid h-7 w-7 place-items-center rounded-full text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-brand-primary-10)] hover:text-[var(--color-brand-primary)]"
        >
          <Edit3 className="h-3.5 w-3.5" />
        </button>
      </div>

      <p className="m-0 text-[12px] leading-[1.55] text-[var(--color-text-secondary)] [text-wrap:pretty]">
        {agent.description || 'No description.'}
      </p>

      <div className="grid grid-cols-4 gap-3 border-t border-[var(--color-border-light)] pt-3">
        <CardMetaItem label="Model" value={agent.modelName ?? '—'} mono />
        <CardMetaItem label="Tools" value={tools.toString()} mono />
        <CardMetaItem
          label="Runs"
          value={stats?.totalRuns?.toLocaleString() ?? '—'}
          mono
        />
        <CardMetaItem label="Conf." value="—" mono />
      </div>

      <div className="flex items-center gap-2 pt-1">
        {autoApply ? (
          <Pill tone="success" dot size="sm">
            Auto-apply
          </Pill>
        ) : (
          <Pill tone="info" size="sm">
            Propose only
          </Pill>
        )}
        <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
          last run {formatRelativeTime(stats?.lastRunAt)}
        </span>
        <div className="flex-1" />
        <Button variant="ghost" size="sm" onClick={onEdit}>
          Configure
          <ArrowRight className="h-3 w-3" />
        </Button>
      </div>
    </div>
  );
}

function CardMetaItem({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex min-w-0 flex-col gap-0.5">
      <span className="text-[10px] font-semibold uppercase tracking-[0.05em] text-[var(--color-text-tertiary)]">
        {label}
      </span>
      <span
        className={cn(
          'truncate text-[12.5px] font-medium text-[var(--color-text-primary)]',
          mono && 'font-[family-name:var(--font-mono)] tabular-nums',
        )}
      >
        {value}
      </span>
    </div>
  );
}

// ── List layout ─────────────────────────────────────────────────────

function AgentListView({
  agents,
  stats,
  onEdit,
  onOpen,
}: {
  agents: AgentConfigurationResponse[];
  stats: Record<string, AgentRunStats>;
  onEdit: (agent: AgentConfigurationResponse) => void;
  onOpen: (agent: AgentConfigurationResponse) => void;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <div
        className="grid gap-3.5 px-3.5 text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]"
        style={{ gridTemplateColumns: '44px 1fr 130px 80px 80px 90px 28px' }}
      >
        <span />
        <span>Agent</span>
        <span>Model</span>
        <span className="text-right">Runs · 7d</span>
        <span className="text-right">Conf.</span>
        <span>State</span>
        <span />
      </div>
      {agents.map((agent) => {
        const color = deriveAgentColor(agent.name);
        const glyph = deriveAgentGlyph(agent.name);
        const state = deriveAgentState(agent.isActive, (stats[agent.id]?.totalRuns ?? 0) > 0);
        return (
          <button
            key={agent.id}
            type="button"
            onClick={() => onEdit(agent)}
            className="grid cursor-pointer items-center gap-3.5 rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] px-3.5 py-2.5 text-left transition-colors hover:border-[var(--color-text-secondary)]"
            style={{ gridTemplateColumns: '44px 1fr 130px 80px 80px 90px 28px' }}
          >
            <AgentPortrait name={agent.name} color={color} glyph={glyph} size={36} />
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <span
                  onClick={(event) => {
                    event.stopPropagation();
                    onOpen(agent);
                  }}
                  className="truncate text-[13px] font-semibold text-[var(--color-text-primary)] hover:text-[var(--color-brand-primary)]"
                >
                  {agent.name}
                </span>
                <Pill tone="info" size="sm">
                  {deriveKindLabel(agent.agentType)}
                </Pill>
              </div>
              <div className="mt-px truncate text-[11px] text-[var(--color-text-secondary)]">
                {deriveAgentTagline(agent.description) || agent.domain}
              </div>
            </div>
            <span className="truncate font-[family-name:var(--font-mono)] text-[11.5px] text-[var(--color-text-secondary)]">
              {agent.modelName ?? '—'}
            </span>
            <span className="text-right font-[family-name:var(--font-mono)] text-[11.5px] tabular-nums text-[var(--color-text-primary)]">
              {stats[agent.id]?.totalRuns?.toLocaleString() ?? '—'}
            </span>
            <span className="text-right font-[family-name:var(--font-mono)] text-[11.5px] tabular-nums text-[var(--color-text-secondary)]">
              —
            </span>
            <StateDot state={state} />
            <ChevronRight className="h-3.5 w-3.5 text-[var(--color-text-tertiary)]" />
          </button>
        );
      })}
    </div>
  );
}
