// AI Run Queue — visual port of the "AI Tasks" half of
// templates/aonik-admin-starterkit/screens/ai-tasks-policies.jsx, wired to
// the existing /ai/runs endpoint (the useCase param is now optional after
// Wave 7b).
//
// Naming note: the template calls these "tasks" but Aonik already has an
// AiTask entity (prompt template). We render AiRun rows here under the
// "Run Queue" label so the two don't collide.
//
// Differences from the template, called out so they don't read as gaps:
//   • Template's per-task "tools count" / "ceiling" / "owner" fields don't
//     exist on AiRun — we surface UseCase / ModelName / Tokens / Cost /
//     Latency instead, all real DTO fields.
//   • Status values map AiRun.Outcome (Success / Failed / …) plus a
//     synthetic "Running" bucket when a run is recent and has no terminal
//     outcome. The held/scheduled/error categories from the template
//     don't have direct equivalents — collapsed into Outcome buckets.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AlertCircle, RefreshCw } from 'lucide-react';

import {
  Card as AonikCard,
  FilterBar,
  type FilterBarTab,
  PageHeader,
  Pill,
  type PillTone,
} from '@/components/layout/aonik';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { Button } from '@/components/ui/button';
import { aiRunService } from '@/services/aiService';
import type { AiRunSummaryResponse } from '@/services/aiService';

// ─── Helpers ─────────────────────────────────────────────────────────────

const OUTCOME_TONE: Record<string, PillTone> = {
  Success: 'success',
  Succeeded: 'success',
  Failed: 'danger',
  Error: 'danger',
  Cancelled: 'muted',
  Pending: 'warning',
  Running: 'info',
};

const FILTER_TABS: FilterBarTab[] = [
  { value: '', label: 'All' },
  { value: 'Success', label: 'Success' },
  { value: 'Failed', label: 'Failed' },
  { value: 'Pending', label: 'Pending' },
];

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

function formatLatency(ms: number): string {
  if (ms <= 0) return '—';
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(2)}s`;
}

function formatCost(cost: number): string {
  if (cost <= 0) return '—';
  return `$${cost.toFixed(4)}`;
}

function shortRunId(id: string): string {
  return `RUN-${id.replace(/-/g, '').slice(0, 8).toUpperCase()}`;
}

// ─── Page ────────────────────────────────────────────────────────────────

export function AiRunQueuePage() {
  const [runs, setRuns] = useState<AiRunSummaryResponse[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [searchQuery, setSearchQuery] = useState('');
  const [outcomeFilter, setOutcomeFilter] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize] = useState(50);
  const requestIdRef = useRef(0);

  const loadRuns = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);
    try {
      const result = await aiRunService.list({
        outcome: outcomeFilter || undefined,
        page,
        pageSize,
      });
      if (requestIdRef.current !== requestId) return;
      setRuns(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load AI runs.');
    } finally {
      if (requestIdRef.current === requestId) {
        setLoading(false);
        setInitialLoad(false);
      }
    }
  }, [outcomeFilter, page, pageSize]);

  useEffect(() => {
    void loadRuns();
  }, [loadRuns]);

  useEffect(() => {
    setPage(1);
  }, [searchQuery, outcomeFilter]);

  const filtered = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    if (!q) return runs;
    return runs.filter(
      (run) =>
        run.useCase.toLowerCase().includes(q) ||
        (run.modelName ?? '').toLowerCase().includes(q) ||
        run.id.toLowerCase().includes(q),
    );
  }, [runs, searchQuery]);

  const stats = useMemo(() => {
    const totals = {
      total: runs.length,
      success: 0,
      failed: 0,
      pending: 0,
      tokens: 0,
      cost: 0,
      latencySum: 0,
      latencyCount: 0,
    };
    for (const r of runs) {
      if (r.outcome === 'Success' || r.outcome === 'Succeeded') totals.success += 1;
      else if (r.outcome === 'Failed' || r.outcome === 'Error') totals.failed += 1;
      else totals.pending += 1; // includes Running, Started, empty, etc.
      totals.tokens += r.tokensUsed;
      totals.cost += r.costEstimate;
      if (r.latencyMs > 0) {
        totals.latencySum += r.latencyMs;
        totals.latencyCount += 1;
      }
    }
    return totals;
  }, [runs]);

  const subtitle = totalCount > 0
    ? `${totalCount.toLocaleString()} total runs · ${stats.failed} failed on this page`
    : 'Every agent run across the tenant';

  const avgLatency = stats.latencyCount > 0 ? stats.latencySum / stats.latencyCount : 0;

  if (initialLoad) {
    return <PageLoadingScreen message="Loading run queue" />;
  }

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="AI · Run queue"
        title="Run queue"
        subtitle={subtitle}
        actions={
          <Button variant="outline" size="sm" onClick={() => void loadRuns()} disabled={loading}>
            <RefreshCw className={'h-3 w-3 ' + (loading ? 'animate-spin' : '')} />
            Refresh
          </Button>
        }
      />

      {/* KPI strip — 5-tile layout matching the template (In flight,
          Awaiting review, Completed, Avg duration, Error rate). Aonik
          doesn't yet emit "Awaiting review" runs distinct from in-flight,
          so that bucket reads from the same Pending count and is honest
          about the merge in its sub-line. */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
        <StatTile
          label="In flight"
          value={stats.pending.toLocaleString()}
          sub={stats.pending === 0 ? 'idle' : 'live'}
          tone="var(--color-brand-primary)"
        />
        <StatTile
          label="Awaiting review"
          value="—"
          sub="needs proposal hold tracking"
          tone="var(--color-warning)"
        />
        <StatTile
          label="Completed"
          value={stats.success.toLocaleString()}
          sub={
            stats.total === 0
              ? 'this page'
              : `${Math.round((stats.success / stats.total) * 100)}% success`
          }
          tone="var(--color-success)"
        />
        <StatTile
          label="Avg duration"
          value={formatLatency(avgLatency)}
          sub={`${stats.latencyCount} timed`}
          tone="var(--color-accent-team)"
        />
        <StatTile
          label="Error rate"
          value={
            stats.total === 0
              ? '—'
              : `${((stats.failed / stats.total) * 100).toFixed(1)}%`
          }
          sub={`${stats.failed} failed`}
          tone="var(--color-danger)"
        />
      </div>

      {error && (
        <div className="flex items-center gap-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
          <AlertCircle className="h-4 w-4 flex-none" />
          <span className="flex-1">{error}</span>
          <Button variant="outline" size="sm" onClick={() => void loadRuns()}>
            <RefreshCw className="h-3 w-3" />
            Retry
          </Button>
        </div>
      )}

      <FilterBar
        tabs={FILTER_TABS}
        active={outcomeFilter}
        onTabChange={setOutcomeFilter}
        search={searchQuery}
        onSearchChange={setSearchQuery}
        searchPlaceholder="Filter by use case, model, run id…"
        hideFilterButton
      />

      <AonikCard padding={0}>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)] text-left text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                <th className="px-4 py-3 w-[160px]">Run</th>
                <th className="px-4 py-3">Use case</th>
                <th className="px-4 py-3 w-[160px]">Model</th>
                <th className="px-4 py-3 w-[120px]">Outcome</th>
                <th className="px-4 py-3 w-[100px] text-right">Tokens</th>
                <th className="px-4 py-3 w-[100px] text-right">Latency</th>
                <th className="px-4 py-3 w-[100px] text-right">Cost</th>
                <th className="px-4 py-3 w-[110px] text-right">Age</th>
              </tr>
            </thead>
            <tbody>
              {loading && filtered.length === 0 ? (
                <tr>
                  <td colSpan={8} className="px-4 py-12 text-center">
                    <RefreshCw className="mx-auto mb-2 h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
                    <p className="text-sm text-[var(--color-text-secondary)]">Loading runs…</p>
                  </td>
                </tr>
              ) : filtered.length === 0 ? (
                <tr>
                  <td colSpan={8} className="px-4 py-12 text-center">
                    <p className="text-sm font-medium text-[var(--color-text-primary)]">
                      No runs match
                    </p>
                    <p className="mt-1 text-xs text-[var(--color-text-tertiary)]">
                      {searchQuery || outcomeFilter
                        ? 'Try adjusting the active tab or search.'
                        : 'AI runs will appear here as agents execute prompts.'}
                    </p>
                  </td>
                </tr>
              ) : (
                filtered.map((run) => (
                  <tr
                    key={run.id}
                    className="border-b border-[var(--color-border-light)] transition-colors hover:bg-[var(--color-surface-inset)]"
                  >
                    <td className="px-4 py-3 font-[family-name:var(--font-mono)] text-[11px] font-medium text-[var(--color-brand-primary)]">
                      {shortRunId(run.id)}
                    </td>
                    <td className="px-4 py-3 text-[12.5px] text-[var(--color-text-primary)]">
                      {run.useCase || '—'}
                    </td>
                    <td className="px-4 py-3 font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
                      {run.modelName ?? '—'}
                    </td>
                    <td className="px-4 py-3">
                      <Pill tone={OUTCOME_TONE[run.outcome] ?? 'default'} dot size="sm">
                        {run.outcome || 'Pending'}
                      </Pill>
                    </td>
                    <td className="px-4 py-3 text-right font-[family-name:var(--font-mono)] text-[11.5px] text-[var(--color-text-primary)]">
                      {run.tokensUsed.toLocaleString()}
                    </td>
                    <td className="px-4 py-3 text-right font-[family-name:var(--font-mono)] text-[11.5px] text-[var(--color-text-secondary)]">
                      {formatLatency(run.latencyMs)}
                    </td>
                    <td className="px-4 py-3 text-right font-[family-name:var(--font-mono)] text-[11.5px] text-[var(--color-text-secondary)]">
                      {formatCost(run.costEstimate)}
                    </td>
                    <td className="px-4 py-3 text-right font-[family-name:var(--font-mono)] text-[10.5px] text-[var(--color-text-tertiary)]">
                      {formatRelative(run.createdAt)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </AonikCard>

      {totalCount > pageSize && (
        <div className="flex items-center justify-between text-xs text-[var(--color-text-secondary)]">
          <span>
            Page {page} of {Math.ceil(totalCount / pageSize)} · {totalCount.toLocaleString()} runs
          </span>
          <div className="flex gap-1.5">
            <Button
              variant="outline"
              size="sm"
              disabled={page <= 1 || loading}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={page >= Math.ceil(totalCount / pageSize) || loading}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </Button>
          </div>
        </div>
      )}
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
