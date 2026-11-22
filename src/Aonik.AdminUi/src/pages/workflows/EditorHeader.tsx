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
  Play,
  Zap,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { WorkflowGraph } from './workflowTypes';
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
          ? 'bg-card text-foreground shadow-xs'
          : 'bg-transparent text-muted-foreground hover:text-foreground',
      )}
      style={{ padding: '5px 10px' }}
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
  saving?: boolean;
  saveError?: string | null;
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
  saving = false,
  saveError = null,
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
      className="flex min-h-13 flex-none flex-wrap items-center gap-2 border-b border-border bg-card px-3 py-2"
    >
      <Button
        variant="ghost"
        size="sm"
        onClick={onClose}
        aria-label="Back to Workflows"
        className="h-7 shrink-0 gap-1.5 px-2.5 text-[12px] font-normal text-muted-foreground"
      >
        <ArrowLeft size={12} /><span className="hidden md:inline">Back to Workflows</span>
      </Button>
      <span className="h-5 w-px bg-border" />

      {/* Title block */}
      <div className="flex min-w-0 flex-1 basis-40 items-center gap-2">
        <span
          className="inline-flex flex-none items-center justify-center"
          style={{
            width: 26,
            height: 26,
            borderRadius: 6,
            background: `color-mix(in oklab, ${workflow.ownerColor} 12.5%, transparent)`,
            color: workflow.ownerColor,
          }}
        >
          <Zap size={13} />
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex min-w-0 items-center gap-2">
            <span className="truncate text-[14px] font-semibold text-foreground" title={workflow.name}>
              {workflow.name}
            </span>
            <span
              className="hidden shrink-0 text-[10.5px] text-muted-foreground 2xl:inline"
              style={{ fontFamily: 'var(--font-mono)' }}
            >
              {workflow.id}
            </span>
            <span
              className="shrink-0 rounded-sm bg-muted px-1.5 py-px text-[10px] text-muted-foreground"
              style={{ fontFamily: 'var(--font-mono)' }}
            >
              {workflow.version}
            </span>
            {hasChanges && (
              <span className="rounded-sm bg-warning-subtle px-1.5 py-px text-[10px] font-medium text-warning-foreground">
                Unsaved
              </span>
            )}
          </div>
        </div>
      </div>

      {/* Validation errors summary */}
      {validationErrors.length > 0 && (
        <div className="ml-2 inline-flex items-center gap-1.5 rounded-full bg-destructive/10 px-[9px] py-[3px] text-[11px] font-medium text-destructive">
          <AlertTriangle size={11} /> {validationErrors.length} issue
          {validationErrors.length === 1 ? '' : 's'}
        </div>
      )}

      {saveError && (
        <div
          className="ml-2 inline-flex items-center gap-1.5 rounded-full bg-destructive/10 px-[9px] py-[3px] text-[11px] font-medium text-destructive"
          title={saveError}
        >
          <AlertTriangle size={11} /> Save failed
        </div>
      )}

      {/* View toggles */}
      <div
        className="flex shrink-0 items-center gap-0.5 rounded-md bg-muted"
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
        <Button
          size="sm"
          onClick={onSave}
          disabled={saving || (!hasChanges && validationErrors.length === 0)}
          className="h-7 px-3"
        >
          <Check size={11} />
          {saving ? 'Saving…' : hasChanges ? 'Save changes' : 'Saved'}
        </Button>
      </div>
    </div>
  );
}
