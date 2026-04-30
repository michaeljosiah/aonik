// Single row in the workflow registry — header (name/id/version + state pill),
// step rail, footer stats. Click selects the row; the detail rail mirrors it.
//
// Visual port of WorkflowCard from
// templates/aonik-admin-starterkit/screens/workflows.jsx.

import { Play, Zap } from 'lucide-react';
import { cn } from '@/lib/utils';
import { StepRail } from './StepRail';
import { formatDuration, type WorkflowSummary } from './workflowTypes';

export interface WorkflowCardProps {
  wf: WorkflowSummary;
  active: boolean;
  onClick: () => void;
}

interface StateTone {
  c: string;
  label: string;
  pulse: boolean;
}

const STATE_TONES: Record<WorkflowSummary['state'], StateTone> = {
  Active: { c: 'var(--color-success, #1f7a5e)', label: 'active', pulse: true },
  Paused: { c: '#b4741e', label: 'paused', pulse: false },
  Draft: { c: 'var(--color-text-tertiary)', label: 'draft', pulse: false },
};

function successColor(value: number): string {
  if (value >= 0.95) return 'var(--color-success, #1f7a5e)';
  if (value >= 0.85) return 'var(--color-text-primary)';
  return '#b4741e';
}

export function WorkflowCard({ wf, active, onClick }: WorkflowCardProps) {
  const tone = STATE_TONES[wf.state];

  return (
    <div
      onClick={onClick}
      className={cn(
        'flex flex-col gap-3 cursor-pointer rounded-[10px] transition-[border-color,box-shadow] duration-150',
        'bg-[var(--color-surface)]',
      )}
      style={{
        padding: '16px 18px',
        border: '1px solid ' + (active ? 'var(--color-brand-primary)' : 'var(--color-border-light)'),
        boxShadow: active ? '0 0 0 3px var(--color-brand-primary-10)' : 'none',
      }}
    >
      {/* Header row */}
      <div className="flex items-start gap-3">
        <div className="flex-1 min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-[14px] font-semibold text-[var(--color-text-primary)]">
              {wf.name}
            </span>
            <span
              className="text-[10.5px] text-[var(--color-text-tertiary)]"
              style={{ fontFamily: 'var(--font-mono)' }}
            >
              {wf.id}
            </span>
            <span
              className="rounded-[3px] bg-[var(--color-surface-inset)] px-1.5 py-px text-[10px] text-[var(--color-text-tertiary)]"
              style={{ fontFamily: 'var(--font-mono)' }}
            >
              {wf.version}
            </span>
          </div>
          <div
            className="mt-1 text-[12px] text-[var(--color-text-secondary)]"
            style={{ lineHeight: 1.5 }}
          >
            {wf.desc}
          </div>
        </div>
        <div className="flex flex-none items-center gap-2">
          <span
            className="inline-flex items-center gap-1.5 rounded-full px-[9px] py-[3px] text-[10.5px] font-medium"
            style={{ color: tone.c, background: tone.c + '18' }}
          >
            <span
              className="rounded-full"
              style={{
                width: 6,
                height: 6,
                background: tone.c,
                animation: tone.pulse ? 'aonik-pulse 1.6s infinite' : 'none',
              }}
            />
            {tone.label}
          </span>
        </div>
      </div>

      {/* Step rail */}
      <StepRail steps={wf.steps} />

      {/* Footer row — owner, stats */}
      <div className="flex items-center gap-4 text-[11.5px] text-[var(--color-text-secondary)]">
        <div className="flex items-center gap-1.5">
          <span
            className="rounded-full flex-none"
            style={{ width: 8, height: 8, background: wf.ownerColor }}
          />
          <span className="font-medium text-[var(--color-text-primary)]">{wf.owner}</span>
          {wf.contributors.length > 0 && (
            <span className="text-[var(--color-text-tertiary)]">· +{wf.contributors.length}</span>
          )}
        </div>
        <span className="h-3.5 w-px bg-[var(--color-border-light)]" />
        <div className="flex items-center gap-1">
          <Zap size={11} />
          <span style={{ fontFamily: 'var(--font-mono)' }}>{wf.triggers}</span>
          <span>trigger{wf.triggers === 1 ? '' : 's'}</span>
        </div>
        <div className="flex items-center gap-1">
          <Play size={11} />
          <span style={{ fontFamily: 'var(--font-mono)' }}>{wf.runsToday}</span>
          <span>runs today</span>
        </div>
        <div>
          Success{' '}
          <span
            className="font-medium"
            style={{ fontFamily: 'var(--font-mono)', color: successColor(wf.success) }}
          >
            {(wf.success * 100).toFixed(1)}%
          </span>
        </div>
        <div>
          Avg{' '}
          <span style={{ fontFamily: 'var(--font-mono)' }}>{formatDuration(wf.avgMs)}</span>
        </div>
        <div className="flex-1" />
        <span className="text-[11px] text-[var(--color-text-tertiary)]">updated {wf.updated}</span>
      </div>
    </div>
  );
}
