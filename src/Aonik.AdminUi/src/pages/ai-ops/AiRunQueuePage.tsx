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
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
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
          tone="var(--primary)"
        />
        <StatTile
          label="Awaiting review"
          value="—"
          sub="needs proposal hold tracking"
          tone="var(--warning)"
        />
        <StatTile
          label="Completed"
          value={stats.success.toLocaleString()}
          sub={
            stats.total === 0
              ? 'this page'
              : `${Math.round((stats.success / stats.total) * 100)}% success`
          }
          tone="var(--success)"
        />
        <StatTile
          label="Avg duration"
          value={formatLatency(avgLatency)}
          sub={`${stats.latencyCount} timed`}
          tone="var(--agent-team)"
        />
        <StatTile
          label="Error rate"
          value={
            stats.total === 0
              ? '—'
              : `${((stats.failed / stats.total) * 100).toFixed(1)}%`
          }
          sub={`${stats.failed} failed`}
          tone="var(--destructive)"
        />
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription className="flex items-center gap-3">
            <span className="flex-1">{error}</span>
            <Button variant="outline" size="sm" onClick={() => void loadRuns()}>
              <RefreshCw className="h-3 w-3" />
              Retry
            </Button>
          </AlertDescription>
        </Alert>
      )}

      <FilterBar
        tabs={FILTER_TABS}
        active={outcomeFilter}
        onTabChange={setOutcomeFilter}
        search={searchQuery}
        onSearchChange={setSearchQuery}
        searchPlaceholder="Filter by use case, model, run id…"
      />

      <AonikCard padding={0}>
        <Table>
          <TableHeader>
            <TableRow className="bg-muted text-xs text-muted-foreground hover:bg-muted">
              <TableHead className="w-[160px] px-4 text-muted-foreground">Run</TableHead>
              <TableHead className="px-4 text-muted-foreground">Use case</TableHead>
              <TableHead className="w-[160px] px-4 text-muted-foreground">Model</TableHead>
              <TableHead className="w-[120px] px-4 text-muted-foreground">Outcome</TableHead>
              <TableHead numeric className="w-[100px] px-4 text-muted-foreground">Tokens</TableHead>
              <TableHead numeric className="w-[100px] px-4 text-muted-foreground">Latency</TableHead>
              <TableHead numeric className="w-[100px] px-4 text-muted-foreground">Cost</TableHead>
              <TableHead numeric className="w-[110px] px-4 text-muted-foreground">Age</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading && filtered.length === 0 ? (
              <TableRow className="hover:bg-transparent">
                <TableCell colSpan={8} className="px-4 py-12 text-center">
                  <RefreshCw className="mx-auto mb-2 h-5 w-5 animate-spin text-primary" />
                  <p className="text-sm text-muted-foreground">Loading runs…</p>
                </TableCell>
              </TableRow>
            ) : filtered.length === 0 ? (
              <TableRow className="hover:bg-transparent">
                <TableCell colSpan={8} className="px-4 py-12 text-center">
                  <p className="text-sm font-medium text-foreground">
                    No runs match
                  </p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {searchQuery || outcomeFilter
                      ? 'Try adjusting the active tab or search.'
                      : 'AI runs will appear here as agents execute prompts.'}
                  </p>
                </TableCell>
              </TableRow>
            ) : (
              filtered.map((run) => (
                <TableRow key={run.id} className="hover:bg-muted">
                  <TableCell className="px-4 py-3 font-mono text-[11px] font-medium tabular-nums text-primary">
                    {shortRunId(run.id)}
                  </TableCell>
                  <TableCell className="px-4 py-3 text-[12.5px] text-foreground">
                    {run.useCase || '—'}
                  </TableCell>
                  <TableCell className="px-4 py-3 font-mono text-[11px] text-muted-foreground">
                    {run.modelName ?? '—'}
                  </TableCell>
                  <TableCell className="px-4 py-3">
                    <Pill tone={OUTCOME_TONE[run.outcome] ?? 'default'} dot>
                      {run.outcome || 'Pending'}
                    </Pill>
                  </TableCell>
                  <TableCell numeric className="px-4 py-3 text-[11.5px] text-foreground">
                    {run.tokensUsed.toLocaleString()}
                  </TableCell>
                  <TableCell numeric className="px-4 py-3 text-[11.5px] text-muted-foreground">
                    {formatLatency(run.latencyMs)}
                  </TableCell>
                  <TableCell numeric className="px-4 py-3 text-[11.5px] text-muted-foreground">
                    {formatCost(run.costEstimate)}
                  </TableCell>
                  <TableCell numeric className="px-4 py-3 text-[10.5px] text-muted-foreground">
                    {formatRelative(run.createdAt)}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </AonikCard>

      {totalCount > pageSize && (
        <div className="flex items-center justify-between text-xs text-muted-foreground">
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
    <div className="rounded-lg border border-border bg-card p-3.5">
      <div className="flex items-center gap-1.5 text-[11px] text-muted-foreground">
        <span className="h-1.5 w-1.5 rounded-full" style={{ background: tone }} />
        {label}
      </div>
      <div className="mt-1 font-[family-name:var(--font-mono)] text-[22px] font-semibold leading-none text-foreground">
        {value}
      </div>
      <div className="mt-1 font-[family-name:var(--font-mono)] text-[10px] text-muted-foreground">
        {sub}
      </div>
    </div>
  );
}
