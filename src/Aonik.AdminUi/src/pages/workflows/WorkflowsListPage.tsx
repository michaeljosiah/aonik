// Agent Workflows registry — visual port of
// templates/aonik-admin-starterkit/screens/workflows.jsx.
//
// Mock-only: data comes from `workflowMockData.MOCK_WORKFLOWS`. Layout:
//   PageHeader → KPI strip (4) → filter pills + sort → two-pane
//   (workflow cards left · selected workflow detail rail right).

import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Search, Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { KpiTile, PageHeader } from '@/components/layout/aonik';
import { cn } from '@/lib/utils';
import { WorkflowCard } from './WorkflowCard';
import { WorkflowDetailRail } from './WorkflowDetailRail';
import { MOCK_WORKFLOWS, type WorkflowState, type WorkflowSummary } from './workflowMockData';

type Filter = 'All' | 'Active' | 'Paused' | 'Draft';
type Sort = 'Most run' | 'Recent' | 'Success';

const FILTER_LABELS: Filter[] = ['All', 'Active', 'Paused', 'Draft'];
const FILTER_TO_STATE: Record<Filter, WorkflowState | null> = {
  All: null,
  Active: 'active',
  Paused: 'paused',
  Draft: 'draft',
};

const SPARK_TEAL = '#055a60';
const SPARK_JADE = '#1f7a5e';
const SPARK_MINT = '#3ab795';

export function WorkflowsListPage() {
  const navigate = useNavigate();
  const [selectedId, setSelectedId] = useState<string>(MOCK_WORKFLOWS[0]?.id ?? '');
  const [filter, setFilter] = useState<Filter>('All');
  const [sort, setSort] = useState<Sort>('Most run');

  const counts = useMemo(() => {
    const acc: Record<Filter, number> = { All: 0, Active: 0, Paused: 0, Draft: 0 };
    acc.All = MOCK_WORKFLOWS.length;
    for (const wf of MOCK_WORKFLOWS) {
      if (wf.state === 'active') acc.Active += 1;
      else if (wf.state === 'paused') acc.Paused += 1;
      else if (wf.state === 'draft') acc.Draft += 1;
    }
    return acc;
  }, []);

  const list = useMemo(() => {
    const stateFilter = FILTER_TO_STATE[filter];
    let next: WorkflowSummary[] = stateFilter
      ? MOCK_WORKFLOWS.filter((w) => w.state === stateFilter)
      : [...MOCK_WORKFLOWS];
    if (sort === 'Most run') next = next.sort((a, b) => b.runsToday - a.runsToday);
    else if (sort === 'Recent') next = next.sort((a, b) => a.updated.localeCompare(b.updated));
    else if (sort === 'Success') next = next.sort((a, b) => b.success - a.success);
    return next;
  }, [filter, sort]);

  const selected = useMemo(
    () => MOCK_WORKFLOWS.find((w) => w.id === selectedId) ?? MOCK_WORKFLOWS[0],
    [selectedId],
  );

  // KPI summary
  const totalRuns = MOCK_WORKFLOWS.reduce((acc, w) => acc + w.runsToday, 0);
  const wAvgSuccess =
    MOCK_WORKFLOWS.reduce((acc, w) => acc + w.success * w.runsToday, 0) / Math.max(1, totalRuns);
  const totalTriggers = MOCK_WORKFLOWS.reduce((acc, w) => acc + w.triggers, 0);

  return (
    <div className="flex flex-col gap-5 p-6 lg:p-8">
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
          delta="+218 vs. yesterday"
          deltaTone="up"
          sparkline={[18, 15, 12, 10, 7]}
          sparkColor={SPARK_JADE}
        />
        <KpiTile
          label="Wired triggers"
          value={String(totalTriggers)}
          delta={`across ${MOCK_WORKFLOWS.length} workflows`}
          deltaTone="neutral"
          sparkline={[11, 11, 10, 9, 9]}
          sparkColor={SPARK_MINT}
        />
        <KpiTile
          label="Weighted success"
          value={`${(wAvgSuccess * 100).toFixed(1)}%`}
          delta="+0.4%"
          deltaTone="up"
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
          {list.map((wf) => (
            <WorkflowCard
              key={wf.id}
              wf={wf}
              active={wf.id === selectedId}
              onClick={() => setSelectedId(wf.id)}
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
        </div>
        {selected && (
          <WorkflowDetailRail
            wf={selected}
            onOpenEditor={() => navigate(`/ai/workflows/${selected.id}`)}
          />
        )}
      </div>
    </div>
  );
}
