// History sidebar and trace replay bar. Each is a small
// presentational component that the parent screen toggles open/closed.
//
// 1:1 port of TestPanel, HistoryPanel, TraceBar from
// templates/aonik-admin-starterkit/screens/workflow-editor-chrome.jsx.

import {
  ChevronLeft,
  ChevronRight,
  Clock,
  RefreshCw,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import { Pill } from '@/components/layout/aonik';
import type {
  WorkflowRunSummary,
  WorkflowVersion,
} from './workflowTypes';
import type { TraceState } from './WorkflowCanvas';

// ─── History sidebar ────────────────────────────────────────────────

export interface HistoryPanelProps {
  versions: WorkflowVersion[];
  onClose: () => void;
  onRestore: (id: string) => void;
}

export function HistoryPanel({ versions, onClose, onRestore }: HistoryPanelProps) {
  return (
    <aside
      className="flex flex-none flex-col overflow-hidden border-l border-border bg-card"
      style={{ width: 280 }}
    >
      <div
        className="flex items-center gap-2 border-b border-border"
        style={{ padding: '12px 14px' }}
      >
        <Clock size={12} className="text-muted-foreground" />
        <span className="text-[11.5px] font-semibold text-foreground">
          Version history
        </span>
        <div className="flex-1" />
        <Tooltip>
          <TooltipTrigger asChild>
            <Button variant="ghost" size="icon-sm" onClick={onClose} className="size-6" aria-label="Close history">
              <X size={11} />
            </Button>
          </TooltipTrigger>
          <TooltipContent>Close</TooltipContent>
        </Tooltip>
      </div>
      <div className="flex-1 overflow-y-auto" style={{ padding: 8 }}>
        {versions.map((v, i) => (
          <div
            key={v.id}
            className={cn(
              'mb-1 cursor-pointer rounded-md border px-3 py-2.5',
              i === 0 ? 'border-primary bg-primary/10' : 'border-transparent',
            )}
          >
            <div className="flex items-center gap-1.5">
              <span
                className="text-[11px] font-semibold"
                style={{
                  fontFamily: 'var(--font-mono)',
                  color: i === 0 ? 'var(--primary)' : 'var(--foreground)',
                }}
              >
                {v.tag}
              </span>
              {i === 0 && (
                <Pill tone="info">
                  current
                </Pill>
              )}
              <div className="flex-1" />
              <span className="text-[10.5px] text-muted-foreground">{v.when}</span>
            </div>
            <div
              className="mt-1 text-[11.5px] text-muted-foreground"
              style={{ lineHeight: 1.45 }}
            >
              {v.message}
            </div>
            <div className="mt-1 flex items-center gap-1.5 text-[10.5px] text-muted-foreground">
              <span
                className="inline-flex items-center justify-center rounded-full text-white"
                style={{
                  width: 14,
                  height: 14,
                  fontSize: 9,
                  fontWeight: 600,
                  background: v.byColor,
                }}
              >
                {v.by[0]}
              </span>
              {v.by}
            </div>
            {v.publishedAt && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => onRestore(v.id)}
                className="mt-1.5 h-6"
                style={{ padding: '3px 8px', fontSize: 10.5 }}
              >
                <RefreshCw size={10} />
                Review rollback
              </Button>
            )}
          </div>
        ))}
      </div>
    </aside>
  );
}

// ─── Trace replay bar ───────────────────────────────────────────────

export interface TraceBarProps {
  trace: TraceState | null;
  runs: WorkflowRunSummary[];
  onPick: (id: string) => void;
  onStep: (delta: number) => void;
  onClose: () => void;
}

export function TraceBar({ trace, runs, onPick, onStep, onClose }: TraceBarProps) {
  const run = runs.find((r) => r.id === trace?.runId) ?? runs[0];
  return (
    <div
      className="flex flex-none items-center gap-3 border-b border-border bg-muted"
      style={{ padding: '8px 14px' }}
    >
      <div className="inline-flex items-center gap-1.5 rounded-full bg-success-subtle px-[9px] py-[3px] text-[11px] font-medium text-success-foreground">
        <span
          className="size-1.5 rounded-full bg-success"
          style={{ animation: 'aonik-pulse 1.6s infinite' }}
        />
        Replaying
      </div>
      <Select value={trace?.runId ?? ''} onValueChange={onPick}>
        <SelectTrigger size="sm" className="h-7 w-auto bg-card px-2 text-xs" aria-label="Run to replay">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {runs.map((r) => (
            <SelectItem key={r.id} value={r.id}>
              {r.id} · {r.when} · {r.status}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <span className="text-[11.5px] text-muted-foreground">
        Step{' '}
        <span style={{ fontFamily: 'var(--font-mono)' }}>
          {(trace?.completed.length ?? 0) + 1}
        </span>{' '}
        of <span style={{ fontFamily: 'var(--font-mono)' }}>{run?.total ?? 0}</span>
      </span>
      <Button variant="ghost" size="sm" onClick={() => onStep(-1)} className="h-7" aria-label="Previous step">
        <ChevronLeft size={11} />
      </Button>
      <Button variant="ghost" size="sm" onClick={() => onStep(1)} className="h-7">
        Next <ChevronRight size={11} />
      </Button>
      <div className="flex-1" />
      <span className="text-[11px] text-muted-foreground">
        {run?.duration} · started by {run?.by}
      </span>
      <Tooltip>
        <TooltipTrigger asChild>
          <Button variant="ghost" size="icon-sm" onClick={onClose} className="size-6" aria-label="Close trace">
            <X size={11} />
          </Button>
        </TooltipTrigger>
        <TooltipContent>Close</TooltipContent>
      </Tooltip>
    </div>
  );
}
