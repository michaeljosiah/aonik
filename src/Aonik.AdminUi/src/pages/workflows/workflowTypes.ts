// Page-component types for the workflow surfaces. Replaces the data-bearing
// `workflowMockData.ts` — the data now lives in the API/seed; only the type
// definitions (used across the canvas, inspector, step rails) remain here.
//
// Kinds align with the C# WorkflowNodeKinds constants in
// src/Aonik.Agents/Entities/Workflows/WorkflowNodeKinds.cs (lowercased).

export type WorkflowNodeKind =
  | 'trigger'
  | 'tool'
  | 'agent'
  | 'decision'
  | 'human'
  | 'wait'
  | 'notify'
  | 'emit'
  | 'loop'
  | 'end';

/**
 * Step kind used by the inline list-page rail. Currently the same set as
 * <see cref="WorkflowNodeKind"/>; kept as a separate alias so the rail can
 * later collapse some kinds to a single visual chip without rippling
 * through every editor file.
 */
export type StepKind = WorkflowNodeKind;

/** Equivalent of WorkflowNodeKind for the editor canvas. */
export type EditorNodeKind = WorkflowNodeKind;

export type WorkflowState = 'Active' | 'Paused' | 'Draft';

export interface WorkflowStep {
  kind: StepKind;
  label: string;
  meta?: string;
}

export interface WorkflowSummary {
  id: string;
  slug: string;
  name: string;
  desc: string;
  owner: string;
  ownerColor: string;
  contributors: string[];
  triggers: number;
  runsToday: number;
  /** 0..1 ratio over recent runs. */
  success: number;
  /** Average run duration in milliseconds. */
  avgMs: number;
  state: WorkflowState;
  version: string;
  /** Human-readable relative time, e.g. "3d ago". */
  updated: string;
  autoRetry: boolean;
  steps: WorkflowStep[];
}

export interface WorkflowNodeParams {
  // trigger
  source?: string;
  filter?: string;
  // tool
  tool?: string;
  params?: string;
  // agent
  agent?: string;
  task?: string;
  // decision
  expr?: string;
  yesLabel?: string;
  noLabel?: string;
  // human
  group?: string;
  sla?: string;
  // wait
  duration?: string;
  // notify
  channel?: string;
  template?: string;
  // emit
  event?: string;
  // loop
  over?: string;
  maxIterations?: number | string;
}

export interface WorkflowNode {
  id: string;
  kind: EditorNodeKind;
  label: string;
  x: number;
  y: number;
  summary?: string;
  notes?: string;
  params: WorkflowNodeParams;
}

export interface WorkflowEdge {
  id: string;
  from: string;
  to: string;
  /** Output port index on the `from` node (decision: 0=yes, 1=no; loop: 0=body, 1=done). */
  fromIdx?: number;
  label?: string;
}

export interface WorkflowComment {
  id: string;
  x: number;
  y: number;
  author: string;
  body: string;
}

export interface WorkflowGraph {
  id: string;
  slug: string;
  name: string;
  desc: string;
  version: string;
  state: WorkflowState;
  ownerColor: string;
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
}

export interface WorkflowRunSummary {
  id: string;
  when: string;
  status: 'success' | 'held' | 'failed' | 'running';
  duration: string;
  by: string;
  /** Ordered node ids visited in this run. */
  sequence: string[];
  total: number;
}

export interface WorkflowVersion {
  id: string;
  tag: string;
  when: string;
  by: string;
  byColor: string;
  message: string;
}

// ── Helpers ────────────────────────────────────────────────────────────

export function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)}s`;
  if (ms < 3_600_000) return `${Math.round(ms / 60_000)}m`;
  return `${(ms / 3_600_000).toFixed(1)}h`;
}
