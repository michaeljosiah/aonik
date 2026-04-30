// Editor top bar — back link, title block, validation badge, view toggles
// (Test / Trace / History) and Save/Discard actions.
//
// 1:1 port of EditorHeader from
// templates/aonik-admin-starterkit/screens/workflow-editor-chrome.jsx.

import {
  AlertTriangle,
  ArrowLeft,
  Check,
  Clock,
  Eye,
  MoreHorizontal,
  Play,
  Zap,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { WorkflowGraph } from './workflowMockData';
import type { ValidationError } from './WorkflowCanvas';

interface ToggleProps {
  id: string;
  label: string;
  icon: React.ReactNode;
  open: boolean;
  set: (v: boolean) => void;
}

function ViewToggle({ open, set, label, icon }: ToggleProps) {
  return (
    <button
      type="button"
      onClick={() => set(!open)}
      className={cn(
        'inline-flex items-center gap-1.5 rounded border-0 text-[11.5px] font-medium transition-colors',
        open
          ? 'bg-[var(--color-surface)] text-[var(--color-text-primary)]'
          : 'bg-transparent text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]',
      )}
      style={{
        padding: '5px 10px',
        boxShadow: open ? '0 1px 2px rgba(0,0,0,0.06)' : 'none',
      }}
    >
      {icon}
      {label}
    </button>
  );
}

export interface EditorHeaderProps {
  workflow: WorkflowGraph;
  onClose: () => void;
  hasChanges: boolean;
  onSave: () => void;
  onDiscard: () => void;
  testOpen: boolean;
  setTestOpen: (v: boolean) => void;
  traceOpen: boolean;
  setTraceOpen: (v: boolean) => void;
  historyOpen: boolean;
  setHistoryOpen: (v: boolean) => void;
  validationErrors: ValidationError[];
}

export function EditorHeader({
  workflow,
  onClose,
  hasChanges,
  onSave,
  onDiscard,
  testOpen,
  setTestOpen,
  traceOpen,
  setTraceOpen,
  historyOpen,
  setHistoryOpen,
  validationErrors,
}: EditorHeaderProps) {
  return (
    <div
      className="flex flex-none items-center gap-3 border-b border-[var(--color-border-light)] bg-[var(--color-surface)]"
      style={{ height: 52, padding: '0 16px' }}
    >
      <button
        type="button"
        onClick={onClose}
        className="inline-flex items-center gap-1.5 rounded-md text-[12px] text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-inset)]"
        style={{ padding: '6px 10px' }}
      >
        <ArrowLeft size={12} /> Back to Workflows
      </button>
      <span className="h-5 w-px bg-[var(--color-border-light)]" />

      {/* Title block */}
      <div className="flex min-w-0 items-center gap-2.5">
        <span
          className="inline-flex flex-none items-center justify-center"
          style={{
            width: 26,
            height: 26,
            borderRadius: 6,
            background: workflow.ownerColor + '20',
            color: workflow.ownerColor,
          }}
        >
          <Zap size={13} />
        </span>
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-[14px] font-semibold text-[var(--color-text-primary)]">
              {workflow.name}
            </span>
            <span
              className="text-[10.5px] text-[var(--color-text-tertiary)]"
              style={{ fontFamily: 'var(--font-mono)' }}
            >
              {workflow.id}
            </span>
            <span
              className="rounded-[3px] bg-[var(--color-surface-inset)] px-1.5 py-px text-[10px] text-[var(--color-text-tertiary)]"
              style={{ fontFamily: 'var(--font-mono)' }}
            >
              {workflow.version}
            </span>
            {hasChanges && (
              <span
                className="rounded-[3px] px-1.5 py-px text-[10px] font-medium"
                style={{ color: '#b4741e', background: '#b4741e18' }}
              >
                UNSAVED
              </span>
            )}
          </div>
        </div>
      </div>

      {/* Validation errors summary */}
      {validationErrors.length > 0 && (
        <div
          className="ml-2 inline-flex items-center gap-1.5 rounded-full text-[11px] font-medium"
          style={{
            padding: '3px 9px',
            background: '#c4453618',
            color: '#c44536',
          }}
        >
          <AlertTriangle size={11} /> {validationErrors.length} issue
          {validationErrors.length === 1 ? '' : 's'}
        </div>
      )}

      <div className="flex-1" />

      {/* View toggles */}
      <div
        className="flex items-center gap-0.5 rounded-md bg-[var(--color-surface-inset)]"
        style={{ padding: 2 }}
      >
        <ViewToggle
          id="test"
          label="Test"
          icon={<Play size={11} />}
          open={testOpen}
          set={setTestOpen}
        />
        <ViewToggle
          id="trace"
          label="Trace"
          icon={<Eye size={11} />}
          open={traceOpen}
          set={setTraceOpen}
        />
        <ViewToggle
          id="history"
          label="History"
          icon={<Clock size={11} />}
          open={historyOpen}
          set={setHistoryOpen}
        />
      </div>

      <div className="flex gap-1.5">
        {hasChanges && (
          <Button variant="ghost" size="sm" onClick={onDiscard} className="h-7 px-2.5">
            Discard
          </Button>
        )}
        <Button variant="outline" size="sm" className="h-7 px-2.5">
          <MoreHorizontal size={11} />
        </Button>
        <Button
          size="sm"
          onClick={onSave}
          disabled={!hasChanges && validationErrors.length === 0}
          className="h-7 px-3"
        >
          <Check size={11} />
          {hasChanges ? 'Save changes' : 'Saved'}
        </Button>
      </div>
    </div>
  );
}
