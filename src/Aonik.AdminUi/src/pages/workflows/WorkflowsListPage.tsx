// Agent Workflows registry — visual port of
// templates/aonik-admin-starterkit/screens/workflows.jsx, now sourced from
// the live API (workflowService.list). When the API returns zero rows we
// show an empty-state CTA pointing at System Tools so the operator can
// run the demo seed.
//
// Layout: PageHeader → KPI strip (4) → filter pills + sort → two-pane
// (workflow cards left · selected workflow detail rail right).

import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, RefreshCw, Search, Upload, Workflow } from 'lucide-react';
// `Plus` is still used by the page header's "New workflow" action; the
// empty-state CTA was removed because no create-workflow flow exists yet.
import { Button } from '@/components/ui/button';
import { KpiTile, PageHeader } from '@/components/layout/aonik';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { cn } from '@/lib/utils';
import { useWorkflows, useWorkflowRuns } from '@/hooks/useWorkflows';
import { adaptRun, adaptSummary } from './workflowAdapters';
import { WorkflowCard } from './WorkflowCard';
import { WorkflowDetailRail } from './WorkflowDetailRail';
import type { WorkflowState, WorkflowSummary } from './workflowTypes';

type Filter = 'All' | 'Active' | 'Paused' | 'Draft';
type Sort = 'Most run' | 'Recent' | 'Success';

const FILTER_LABELS: Filter[] = ['All', 'Active', 'Paused', 'Draft'];
const FILTER_TO_STATE: Record<Filter, WorkflowState | null> = {
  All: null,
  Active: 'Active',
  Paused: 'Paused',
  Draft: 'Draft',
};

const SPARK_TEAL = '#055a60';
const SPARK_JADE = '#1f7a5e';
const SPARK_MINT = '#3ab795';

export function WorkflowsListPage() {
  const navigate = useNavigate();
  const { workflows: rawWorkflows, loading, error, refresh } = useWorkflows();

  const workflows = useMemo<WorkflowSummary[]>(
    () => rawWorkflows.map(adaptSummary),
    [rawWorkflows],
  );

  const [selectedSlug, setSelectedSlug] = useState<string>('');
  const [filter, setFilter] = useState<Filter>('All');
  const [sort, setSort] = useState<Sort>('Most run');
  const [initialLoad, setInitialLoad] = useState(true);

  // Clear initial load once the workflows hook finishes its first fetch.
  useEffect(() => {
    if (!loading) {
      setInitialLoad(false);
    }
  }, [loading]);

  // Default-select the first workflow once data arrives.
  useEffect(() => {
    if (!selectedSlug && workflows.length > 0) {
      setSelectedSlug(workflows[0].slug);
    }
  }, [selectedSlug, workflows]);

  const counts = useMemo(() => {
    const acc: Record<Filter, number> = { All: 0, Active: 0, Paused: 0, Draft: 0 };
    acc.All = workflows.length;
    for (const wf of workflows) {
      if (wf.state === 'Active') acc.Active += 1;
      else if (wf.state === 'Paused') acc.Paused += 1;
      else if (wf.state === 'Draft') acc.Draft += 1;
    }
    return acc;
  }, [workflows]);

  const list = useMemo(() => {
    const stateFilter = FILTER_TO_STATE[filter];
    let next: WorkflowSummary[] = stateFilter
      ? workflows.filter((w) => w.state === stateFilter)
      : [...workflows];
    if (sort === 'Most run') next = next.sort((a, b) => b.runsToday - a.runsToday);
    else if (sort === 'Recent') next = next.sort((a, b) => a.updated.localeCompare(b.updated));
    else if (sort === 'Success') next = next.sort((a, b) => b.success - a.success);
    return next;
  }, [filter, sort, workflows]);

  const selected = useMemo(
    () => workflows.find((w) => w.slug === selectedSlug) ?? workflows[0],
    [selectedSlug, workflows],
  );

  // Recent runs for the detail rail. Hook is no-op while selected.id is empty.
  const { runs: rawRuns, loading: runsLoading } = useWorkflowRuns(selected?.id);
  const runs = useMemo(() => rawRuns.map(adaptRun), [rawRuns]);

  // KPI summary
  const totalRuns = workflows.reduce((acc, w) => acc + w.runsToday, 0);
  const wAvgSuccess =
    workflows.reduce((acc, w) => acc + w.success * w.runsToday, 0) / Math.max(1, totalRuns);
  const totalTriggers = workflows.reduce((acc, w) => acc + w.triggers, 0);

  // ── States: error / loading / empty / loaded ──────────────────────────

  if (initialLoad) {
    return <PageLoadingScreen message="Loading workflows" />;
  }

  if (error) {
    return (
      <div
        className="flex flex-col gap-5 p-6 lg:p-8"
        style={{ width: '100%', minWidth: 0, flex: 1, boxSizing: 'border-box' }}
      >
        <PageHeader
          eyebrow="AI · Workflows"
          title="Agent Workflows"
          subtitle="Reusable procedures that agents run when triggered. Wire them to events, schedules, or human actions."
        />
        <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-8 text-center">
          <div className="mb-2 text-[14px] font-semibold text-[var(--color-text-primary)]">
            Couldn't load workflows
          </div>
          <div className="mb-4 text-[12px] text-[var(--color-text-secondary)]">{error}</div>
          <Button size="sm" variant="outline" onClick={refresh}>
            <RefreshCw className="h-3 w-3" />
            Retry
          </Button>
        </div>
      </div>
    );
  }

  if (!loading && workflows.length === 0) {
    return (
      <div
        className="flex flex-col gap-5 p-6 lg:p-8"
        style={{ width: '100%', minWidth: 0, flex: 1, boxSizing: 'border-box' }}
      >
        <PageHeader
          eyebrow="AI · Workflows"
          title="Agent Workflows"
          subtitle="Reusable procedures that agents run when triggered. Wire them to events, schedules, or human actions."
        />
        <EmptyState />
      </div>
    );
  }

  return (
    <div
      className="flex flex-col gap-5 p-6 lg:p-8"
      style={{ width: '100%', minWidth: 0, flex: 1, boxSizing: 'border-box' }}
    >
      <PageHeader
        eyebrow="AI · Workflows"
        title="Agent Workflows"
        subtitle="Reusable procedures that agents run when triggered. Wire them to events, schedules, or human actions."
        actions={
          <>
            <Button variant="outline" size="sm">
              <Search className="h-3 w-3" />
              Browse library
            </Button>
            <Button variant="outline" size="sm">
              <Upload className="h-3 w-3" />
              Import
            </Button>
            <Button size="sm">
              <Plus className="h-3 w-3" />
              New workflow
            </Button>
          </>
        }
      />

      {/* KPI strip */}
      <div className="grid grid-cols-1 gap-3.5 md:grid-cols-2 lg:grid-cols-4">
        <KpiTile
          label="Workflows · active"
          value={String(counts.Active)}
          delta={`${counts.Draft} draft`}
          deltaTone="neutral"
          sparkline={[12, 10, 11, 9, 8]}
          sparkColor={SPARK_TEAL}
        />
        <KpiTile
          label="Runs · today"
          value={totalRuns.toLocaleString()}
          delta="last 24h"
          deltaTone="neutral"
          sparkline={[18, 15, 12, 10, 7]}
          sparkColor={SPARK_JADE}
        />
        <KpiTile
          label="Wired triggers"
          value={String(totalTriggers)}
          delta={`across ${workflows.length} workflows`}
          deltaTone="neutral"
          sparkline={[11, 11, 10, 9, 9]}
          sparkColor={SPARK_MINT}
        />
        <KpiTile
          label="Weighted success"
          value={totalRuns > 0 ? `${(wAvgSuccess * 100).toFixed(1)}%` : '—'}
          delta="weighted by runs"
          deltaTone="neutral"
          sparkline={[8, 7, 6, 5, 4]}
          sparkColor={SPARK_JADE}
        />
      </div>

      {/* Filter + sort bar */}
      <div className="flex items-center gap-1.5 border-b border-[var(--color-border-light)] pb-3">
        {FILTER_LABELS.map((f) => {
          const active = filter === f;
          return (
            <button
              key={f}
              type="button"
              onClick={() => setFilter(f)}
              className={cn(
                'rounded-full px-3 py-1 text-xs cursor-pointer transition-colors',
                active
                  ? 'border border-[var(--color-brand-primary)] bg-[var(--color-brand-primary-10)] font-semibold text-[var(--color-brand-primary)]'
                  : 'border border-[var(--color-border-light)] bg-[var(--color-surface)] text-[var(--color-text-primary)] hover:bg-[var(--color-surface-inset)]',
              )}
            >
              {f}{' '}
              <span
                className="ml-1 text-[11px]"
                style={{ fontFamily: 'var(--font-mono)', opacity: 0.7 }}
              >
                {counts[f]}
              </span>
            </button>
          );
        })}
        <div className="flex-1" />
        <span className="text-[11.5px] text-[var(--color-text-tertiary)]">Sort by</span>
        <select
          value={sort}
          onChange={(e) => setSort(e.target.value as Sort)}
          className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] px-2 py-1 text-xs"
        >
          <option>Most run</option>
          <option>Recent</option>
          <option>Success</option>
        </select>
        <Button variant="ghost" size="sm" className="ml-1">
          <Search className="h-3 w-3" />
          Filter…
        </Button>
      </div>

      {/* Two-pane: list + detail */}
      <div
        className="grid items-start gap-4"
        style={{ gridTemplateColumns: 'minmax(0, 1fr) 420px' }}
      >
        <div className="flex min-w-0 flex-col gap-2.5">
          {loading && list.length === 0 ? (
            <LoadingPlaceholder />
          ) : (
            <>
              {list.map((wf) => (
                <WorkflowCard
                  key={wf.id}
                  wf={wf}
                  active={wf.slug === selectedSlug}
                  onClick={() => setSelectedSlug(wf.slug)}
                />
              ))}
              {list.length === 0 && (
                <div
                  className="rounded-[10px] border border-dashed border-[var(--color-border-light)] bg-[var(--color-surface-inset)] text-center text-[12.5px] text-[var(--color-text-tertiary)]"
                  style={{ padding: 40 }}
                >
                  No workflows in this state yet.
                </div>
              )}
            </>
          )}
        </div>
        {selected && (
          <WorkflowDetailRail
            wf={selected}
            runs={runs}
            runsLoading={runsLoading}
            onOpenEditor={() => navigate(`/ai/workflows/${selected.slug}`)}
          />
        )}
      </div>
    </div>
  );
}

// ── Sub-components ────────────────────────────────────────────────────

function LoadingPlaceholder() {
  return (
    <>
      {[0, 1, 2].map((i) => (
        <div
          key={i}
          className="animate-pulse rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)]"
          style={{ height: 132, padding: 18 }}
        />
      ))}
    </>
  );
}

function EmptyState() {
  return (
    <div
      className="flex flex-col items-center rounded-xl border border-dashed border-[var(--color-border-light)] bg-[var(--color-surface)] text-center"
      style={{
        // Inline width: Tailwind's `w-full` wasn't sticking on dev — likely a
        // build cache or specificity issue with the parent flex container.
        // Inline style bypasses all of that.
        width: '100%',
        boxSizing: 'border-box',
        padding: '64px 32px',
      }}
    >
      <span
        className="mb-4 inline-flex items-center justify-center rounded-full bg-[var(--color-brand-primary-10)] text-[var(--color-brand-primary)]"
        style={{ width: 48, height: 48 }}
      >
        <Workflow className="h-6 w-6" />
      </span>
      <div className="mb-1.5 text-[15px] font-semibold text-[var(--color-text-primary)]">
        No workflows yet
      </div>
      <div
        className="text-[12.5px] text-[var(--color-text-secondary)]"
        style={{ lineHeight: 1.5, maxWidth: 480 }}
      >
        Workflows are reusable procedures agents run when a trigger fires.
        Run the demo seed from System Tools to populate seven sample
        workflows — or come back here once a "New workflow" wizard ships.
      </div>
    </div>
  );
}
