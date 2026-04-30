// Right-pane detail rail for the selected workflow on the registry page.
// Shows: header (id/name/desc + Run/Edit actions), vertical step diagram,
// owner card, perf grid, recent runs.
//
// Visual port of WorkflowDetail in
// templates/aonik-admin-starterkit/screens/workflows.jsx.

import {
  Bolt,
  Check,
  Clock,
  Columns,
  Edit,
  GitFork,
  MoreHorizontal,
  Play,
  RefreshCw,
  Send,
  Sparkles,
  Users,
  Wrench,
  Zap,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { STEP_KIND } from './stepKindCatalog';
import { formatDuration, type WorkflowRunSummary, type WorkflowSummary } from './workflowTypes';

const ICON_BY_NAME: Record<string, LucideIcon> = {
  Wrench,
  Sparkles,
  GitFork,
  Users,
  Clock,
  Check,
  Play,
  Send,
  Zap,
  Columns,
  RefreshCw,
  Bolt,
};

const STATUS_TONES: Record<WorkflowRunSummary['status'], { c: string; label: string }> = {
  success: { c: 'var(--color-success, #1f7a5e)', label: 'ok' },
  held: { c: '#b4741e', label: 'held' },
  failed: { c: '#c44536', label: 'fail' },
  running: { c: 'var(--color-brand-primary)', label: 'live' },
};

function ownerInitials(name: string): string {
  return name
    .split(' ')
    .map((w) => w[0])
    .join('')
    .slice(0, 2);
}

interface SectionEyebrowProps {
  children: React.ReactNode;
}

function SectionEyebrow({ children }: SectionEyebrowProps) {
  return (
    <div
      className="text-[10.5px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]"
      style={{ marginBottom: 10 }}
    >
      {children}
    </div>
  );
}

export interface WorkflowDetailRailProps {
  wf: WorkflowSummary;
  /** Real recent runs from the API. Top 6 are rendered. */
  runs?: WorkflowRunSummary[];
  /** True while the runs request is in flight. */
  runsLoading?: boolean;
  onOpenEditor?: () => void;
}

export function WorkflowDetailRail({ wf, runs = [], runsLoading = false, onOpenEditor }: WorkflowDetailRailProps) {
  const recent = runs.slice(0, 6);

  return (
    <aside
      className="flex flex-col flex-none overflow-hidden rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)]"
      style={{ width: 420, maxHeight: 'calc(100vh - 200px)' }}
    >
      {/* Header */}
      <div
        className="border-b border-[var(--color-border-light)]"
        style={{ padding: 18 }}
      >
        <div className="mb-1.5 flex items-center gap-2">
          <span
            className="rounded-[4px] bg-[var(--color-surface-inset)] px-1.5 py-0.5 text-[10.5px] text-[var(--color-text-tertiary)]"
            style={{ fontFamily: 'var(--font-mono)' }}
          >
            WORKFLOW
          </span>
          <span
            className="text-[10.5px] text-[var(--color-text-tertiary)]"
            style={{ fontFamily: 'var(--font-mono)' }}
          >
            {wf.id}
          </span>
        </div>
        <div className="text-[18px] font-semibold text-[var(--color-text-primary)]">
          {wf.name}
        </div>
        <div
          className="mt-1.5 text-[12px] text-[var(--color-text-secondary)]"
          style={{ lineHeight: 1.5 }}
        >
          {wf.desc}
        </div>

        <div className="mt-3 flex items-center gap-2">
          <Button size="sm" className="h-7 px-3">
            <Play className="h-3 w-3" />
            Run now
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="h-7 px-3"
            onClick={onOpenEditor}
          >
            <Edit className="h-3 w-3" />
            Open editor
          </Button>
          <button
            type="button"
            className="ml-auto rounded p-1.5 text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-inset)]"
            aria-label="More options"
          >
            <MoreHorizontal className="h-3 w-3" />
          </button>
        </div>
      </div>

      <div
        className="flex flex-1 flex-col gap-[18px] overflow-auto"
        style={{ padding: 18 }}
      >
        {/* Vertical step diagram */}
        <div>
          <SectionEyebrow>Steps · {wf.steps.length}</SectionEyebrow>
          <div className="relative">
            {wf.steps.map((s, i) => {
              const meta = STEP_KIND[s.kind];
              const Icon = ICON_BY_NAME[meta.icon] ?? Bolt;
              const last = i === wf.steps.length - 1;
              return (
                <div
                  key={i}
                  className="relative"
                  style={{ paddingLeft: 36, paddingBottom: last ? 0 : 14 }}
                >
                  {!last && (
                    <span
                      className="absolute"
                      style={{
                        left: 13,
                        top: 26,
                        bottom: 0,
                        width: 1.5,
                        background: 'var(--color-border)',
                      }}
                    />
                  )}
                  <span
                    className="absolute inline-flex items-center justify-center"
                    style={{
                      left: 0,
                      top: 2,
                      width: 28,
                      height: 28,
                      borderRadius: 7,
                      background: meta.tint + '18',
                      color: meta.tint,
                      border: '1px solid ' + meta.tint + '40',
                    }}
                  >
                    <Icon size={12} />
                  </span>
                  <div
                    className="flex items-center gap-2"
                    style={{ height: 18 }}
                  >
                    <span
                      className="text-[9.5px] font-semibold uppercase tracking-[0.06em]"
                      style={{ color: meta.tint }}
                    >
                      {meta.label}
                    </span>
                  </div>
                  <div
                    className="mt-0.5 text-[12.5px] font-medium text-[var(--color-text-primary)]"
                    style={{ fontFamily: s.kind === 'tool' ? 'var(--font-mono)' : 'inherit' }}
                  >
                    {s.label}
                  </div>
                  {s.meta && (
                    <div className="mt-px text-[11px] text-[var(--color-text-tertiary)]">
                      {s.meta}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>

        {/* Owner + contributors */}
        <div>
          <SectionEyebrow>Owned by</SectionEyebrow>
          <div
            className="flex items-center gap-2.5 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)]"
            style={{ padding: '10px 12px' }}
          >
            <span
              className="inline-flex flex-none items-center justify-center"
              style={{
                width: 28,
                height: 28,
                borderRadius: 8,
                background: wf.ownerColor + '20',
                color: wf.ownerColor,
                fontWeight: 700,
                fontSize: 11,
                letterSpacing: '0.04em',
              }}
            >
              {ownerInitials(wf.owner)}
            </span>
            <div className="flex-1">
              <div className="text-[12.5px] font-medium text-[var(--color-text-primary)]">
                {wf.owner}
              </div>
              {wf.contributors.length > 0 && (
                <div className="mt-0.5 text-[11px] text-[var(--color-text-secondary)]">
                  with {wf.contributors.join(' · ')}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Perf */}
        <div>
          <SectionEyebrow>Performance · last 24h</SectionEyebrow>
          <div className="grid grid-cols-2 gap-2">
            {[
              { l: 'Runs', v: String(wf.runsToday) },
              { l: 'Success', v: `${(wf.success * 100).toFixed(1)}%` },
              { l: 'Avg', v: formatDuration(wf.avgMs) },
              { l: 'p95', v: formatDuration(wf.avgMs * 1.8) },
            ].map((s) => (
              <div
                key={s.l}
                className="rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)]"
                style={{ padding: '10px 12px' }}
              >
                <div className="text-[10.5px] uppercase tracking-[0.04em] text-[var(--color-text-tertiary)]">
                  {s.l}
                </div>
                <div
                  className="mt-0.5 text-[16px] font-semibold text-[var(--color-text-primary)]"
                  style={{ fontFamily: 'var(--font-mono)' }}
                >
                  {s.v}
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Recent runs */}
        <div>
          <div className="mb-2 flex items-center">
            <div className="text-[10.5px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
              Recent runs
            </div>
            <div className="flex-1" />
            <a className="text-[11px] font-medium text-[var(--color-brand-primary)] cursor-pointer">
              Open in Traces →
            </a>
          </div>
          <div className="flex flex-col gap-1">
            {runsLoading && recent.length === 0 ? (
              <div className="text-[11px] text-[var(--color-text-tertiary)]" style={{ padding: '8px 2px' }}>
                Loading…
              </div>
            ) : recent.length === 0 ? (
              <div className="text-[11px] text-[var(--color-text-tertiary)]" style={{ padding: '8px 2px' }}>
                No runs in the last 24h.
              </div>
            ) : (
              recent.map((r) => {
                const tone = STATUS_TONES[r.status];
                return (
                  <div
                    key={r.id}
                    className="grid items-center gap-2.5 rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)]"
                    style={{
                      gridTemplateColumns: '60px 1fr auto auto',
                      padding: '8px 10px',
                    }}
                  >
                    <span
                      className="inline-flex items-center gap-1.5 text-[10.5px] font-medium"
                      style={{ color: tone.c }}
                    >
                      <span
                        className="rounded-full"
                        style={{ width: 6, height: 6, background: tone.c }}
                      />
                      {tone.label}
                    </span>
                    <span className="overflow-hidden text-ellipsis whitespace-nowrap text-[11px] text-[var(--color-text-secondary)]">
                      {r.by}
                    </span>
                    <span
                      className="text-[10.5px] text-[var(--color-text-tertiary)]"
                      style={{ fontFamily: 'var(--font-mono)' }}
                    >
                      {r.duration}
                    </span>
                    <span className="text-[10.5px] text-[var(--color-text-tertiary)]">
                      {r.when}
                    </span>
                  </div>
                );
              })
            )}
          </div>
        </div>
      </div>
    </aside>
  );
}
