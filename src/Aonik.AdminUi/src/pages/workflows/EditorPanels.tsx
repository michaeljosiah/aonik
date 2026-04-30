// Bottom test panel + history sidebar + trace replay bar. Each is a small
// presentational component that the parent screen toggles open/closed.
//
// 1:1 port of TestPanel, HistoryPanel, TraceBar from
// templates/aonik-admin-starterkit/screens/workflow-editor-chrome.jsx.

import { useState } from 'react';
import {
  ChevronLeft,
  ChevronRight,
  Clock,
  Play,
  RefreshCw,
  Trash2,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Pill } from '@/components/layout/aonik';
import type {
  WorkflowGraph,
  WorkflowRunSummary,
  WorkflowVersion,
} from './workflowTypes';
import type { TraceState } from './WorkflowCanvas';

// ─── Bottom test panel ──────────────────────────────────────────────

interface LogEntry {
  t: 'idle' | 'info' | 'tool' | 'agent' | 'decision' | 'ledger' | 'notify' | 'ok';
  msg: string;
}

const TEST_STAGES: LogEntry[] = [
  { t: 'tool', msg: 'tool · search_invoices · 142ms · 1 match found' },
  { t: 'agent', msg: 'agent · Billing · scoring confidence 0.94' },
  { t: 'decision', msg: 'decision · amount > 50000? → no · proceeding' },
  { t: 'ledger', msg: 'tool · draft_journal_entry · DR 1200 / CR 4000' },
  { t: 'notify', msg: 'notify · email · receipt sent to ar@primrose.io' },
  { t: 'ok', msg: 'Run complete · 2.4s · success' },
];

export interface TestPanelProps {
  workflow: WorkflowGraph;
  onClose: () => void;
  onStartRun: () => void;
}

export function TestPanel({ onClose, onStartRun }: TestPanelProps) {
  const [input, setInput] = useState(
    JSON.stringify(
      {
        txn_id: 'tx_9f2c1a',
        amount: 12480.0,
        currency: 'GBP',
        counterparty: 'Primrose Logistics',
        memo: 'INV-2041',
      },
      null,
      2,
    ),
  );
  const [running, setRunning] = useState(false);
  const [logs, setLogs] = useState<LogEntry[]>([
    { t: 'idle', msg: 'Ready. Edit input and run a test trace through the canvas.' },
  ]);

  const run = () => {
    setRunning(true);
    setLogs([{ t: 'info', msg: 'Starting test run · ' + new Date().toLocaleTimeString() }]);
    onStartRun();
    let i = 0;
    const tick = () => {
      if (i >= TEST_STAGES.length) {
        setRunning(false);
        return;
      }
      // Capture stage before incrementing — the functional updater runs
      // asynchronously and closes over `i`, so without a snapshot it would
      // read the mutated value and push `TEST_STAGES[i+1]` (or undefined on
      // the last tick).
      const stage = TEST_STAGES[i];
      setLogs((l) => [...l, stage]);
      i++;
      setTimeout(tick, 600);
    };
    setTimeout(tick, 400);
  };

  return (
    <div
      className="flex flex-none overflow-hidden border-t border-[var(--color-border-light)] bg-[var(--color-surface)]"
      style={{ height: 280 }}
    >
      {/* Left: input */}
      <div
        className="flex flex-none flex-col border-r border-[var(--color-border-light)]"
        style={{ width: 360, padding: 14 }}
      >
        <div className="mb-2.5 flex items-center gap-2">
          <Play size={12} className="text-[var(--color-brand-primary)]" />
          <span className="text-[11.5px] font-semibold text-[var(--color-text-primary)]">
            Test input
          </span>
          <div className="flex-1" />
          <select
            defaultValue="banking.transaction.received"
            className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)]"
            style={{ fontSize: 12, padding: '4px 8px' }}
          >
            <option>banking.transaction.received</option>
            <option>invoice.overdue</option>
            <option>manual</option>
          </select>
        </div>
        <textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          className="flex-1 resize-none rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] text-[var(--color-text-primary)]"
          style={{
            fontFamily: 'var(--font-mono)',
            fontSize: 11.5,
            padding: 10,
            lineHeight: 1.5,
          }}
        />
        <Button
          size="sm"
          onClick={run}
          disabled={running}
          className="mt-2.5 h-8 justify-center"
        >
          {running ? (
            <>
              <RefreshCw size={11} /> Running…
            </>
          ) : (
            <>
              <Play size={11} /> Run test
            </>
          )}
        </Button>
      </div>

      {/* Right: log stream */}
      <div className="flex flex-1 flex-col overflow-hidden">
        <div
          className="flex items-center gap-2 border-b border-[var(--color-border-light)]"
          style={{ padding: '10px 14px' }}
        >
          <span className="text-[11.5px] font-semibold text-[var(--color-text-primary)]">
            Run output
          </span>
          <span
            className="text-[10.5px] text-[var(--color-text-tertiary)]"
            style={{ fontFamily: 'var(--font-mono)' }}
          >
            {logs.length} events
          </span>
          <div className="flex-1" />
          <Button variant="ghost" size="sm" onClick={() => setLogs([])} className="h-7">
            <Trash2 size={11} /> Clear
          </Button>
          <Button variant="ghost" size="sm" onClick={onClose} className="h-7">
            <X size={11} />
          </Button>
        </div>
        <div className="flex-1 overflow-y-auto bg-[var(--color-surface-inset)]" style={{ padding: 12 }}>
          {logs.map((l, i) => {
            let color = 'var(--color-text-primary)';
            if (l.t === 'ok') color = 'var(--color-success, #1f7a5e)';
            else if (l.t === 'idle') color = 'var(--color-text-tertiary)';
            return (
              <div
                key={i}
                className="flex items-start gap-2.5"
                style={{
                  fontFamily: 'var(--font-mono)',
                  fontSize: 11.5,
                  lineHeight: 1.6,
                  color,
                }}
              >
                <span className="flex-none text-[var(--color-text-tertiary)]">
                  {String(i).padStart(2, '0')}
                </span>
                <span>{l.msg}</span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

// ─── History sidebar ────────────────────────────────────────────────

export interface HistoryPanelProps {
  versions: WorkflowVersion[];
  onClose: () => void;
  onRestore: (id: string) => void;
}

export function HistoryPanel({ versions, onClose, onRestore }: HistoryPanelProps) {
  return (
    <aside
      className="flex flex-none flex-col overflow-hidden border-l border-[var(--color-border-light)] bg-[var(--color-surface)]"
      style={{ width: 280 }}
    >
      <div
        className="flex items-center gap-2 border-b border-[var(--color-border-light)]"
        style={{ padding: '12px 14px' }}
      >
        <Clock size={12} className="text-[var(--color-text-secondary)]" />
        <span className="text-[11.5px] font-semibold text-[var(--color-text-primary)]">
          Version history
        </span>
        <div className="flex-1" />
        <button
          type="button"
          onClick={onClose}
          className="rounded p-1 text-[var(--color-text-tertiary)] hover:bg-[var(--color-surface-inset)]"
          aria-label="Close history"
        >
          <X size={11} />
        </button>
      </div>
      <div className="flex-1 overflow-y-auto" style={{ padding: 8 }}>
        {versions.map((v, i) => (
          <div
            key={v.id}
            className="mb-1 cursor-pointer rounded-md"
            style={{
              padding: '10px 12px',
              background: i === 0 ? 'var(--color-brand-primary-10)' : 'transparent',
              border: '1px solid ' + (i === 0 ? 'var(--color-brand-primary)' : 'transparent'),
            }}
          >
            <div className="flex items-center gap-1.5">
              <span
                className="text-[11px] font-semibold"
                style={{
                  fontFamily: 'var(--font-mono)',
                  color: i === 0 ? 'var(--color-brand-primary)' : 'var(--color-text-primary)',
                }}
              >
                {v.tag}
              </span>
              {i === 0 && (
                <Pill tone="info" size="sm">
                  current
                </Pill>
              )}
              <div className="flex-1" />
              <span className="text-[10.5px] text-[var(--color-text-tertiary)]">{v.when}</span>
            </div>
            <div
              className="mt-1 text-[11.5px] text-[var(--color-text-secondary)]"
              style={{ lineHeight: 1.45 }}
            >
              {v.message}
            </div>
            <div className="mt-1 flex items-center gap-1.5 text-[10.5px] text-[var(--color-text-tertiary)]">
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
            {i !== 0 && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => onRestore(v.id)}
                className="mt-1.5 h-6"
                style={{ padding: '3px 8px', fontSize: 10.5 }}
              >
                <RefreshCw size={10} />
                Restore
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
      className="flex flex-none items-center gap-3 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]"
      style={{ padding: '8px 14px' }}
    >
      <div
        className="inline-flex items-center gap-1.5 rounded-full text-[11px] font-medium"
        style={{
          padding: '3px 9px',
          background: '#3ab79518',
          color: '#1f7a5e',
        }}
      >
        <span
          className="rounded-full"
          style={{
            width: 6,
            height: 6,
            background: '#3ab795',
            animation: 'aonik-pulse 1.6s infinite',
          }}
        />
        Replaying
      </div>
      <select
        value={trace?.runId ?? ''}
        onChange={(e) => onPick(e.target.value)}
        className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)]"
        style={{ fontSize: 12, padding: '4px 8px' }}
      >
        {runs.map((r) => (
          <option key={r.id} value={r.id}>
            {r.id} · {r.when} · {r.status}
          </option>
        ))}
      </select>
      <span className="text-[11.5px] text-[var(--color-text-secondary)]">
        Step{' '}
        <span style={{ fontFamily: 'var(--font-mono)' }}>
          {(trace?.completed.length ?? 0) + 1}
        </span>{' '}
        of <span style={{ fontFamily: 'var(--font-mono)' }}>{run?.total ?? 0}</span>
      </span>
      <Button variant="ghost" size="sm" onClick={() => onStep(-1)} className="h-7">
        <ChevronLeft size={11} />
      </Button>
      <Button variant="ghost" size="sm" onClick={() => onStep(1)} className="h-7">
        Next <ChevronRight size={11} />
      </Button>
      <div className="flex-1" />
      <span className="text-[11px] text-[var(--color-text-tertiary)]">
        {run?.duration} · started by {run?.by}
      </span>
      <button
        type="button"
        onClick={onClose}
        className="rounded p-1 text-[var(--color-text-tertiary)] hover:bg-[var(--color-surface-inset)]"
        aria-label="Close trace"
      >
        <X size={11} />
      </button>
    </div>
  );
}
