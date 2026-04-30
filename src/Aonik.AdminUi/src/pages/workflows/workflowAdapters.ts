// Adapters: API DTOs → page-component types.
// The C# contracts use Title-Case kinds and states (matching the
// constants in src/Aonik.Agents/Entities/Workflows/*.cs). The page
// components were authored with lowercase kinds. This file bridges the
// two so the page code is unchanged from the de-mock — and so any future
// shape drift on either side has exactly one place to fix.

import type {
  WorkflowGraphCommentDto,
  WorkflowGraphDto,
  WorkflowGraphEdgeDto,
  WorkflowGraphNodeDto,
  WorkflowRunDto,
  WorkflowSummaryDto,
  WorkflowVersionDto,
} from '@/services/workflowService';
import type {
  EditorNodeKind,
  StepKind,
  WorkflowComment,
  WorkflowEdge,
  WorkflowGraph,
  WorkflowNode,
  WorkflowNodeParams,
  WorkflowRunSummary,
  WorkflowState,
  WorkflowStep,
  WorkflowSummary,
  WorkflowVersion,
} from './workflowTypes';

const VALID_NODE_KINDS: ReadonlySet<EditorNodeKind> = new Set([
  'trigger',
  'tool',
  'agent',
  'decision',
  'human',
  'wait',
  'notify',
  'emit',
  'loop',
  'end',
]);

function toNodeKind(apiKind: string): EditorNodeKind {
  const normalised = apiKind.toLowerCase();
  return VALID_NODE_KINDS.has(normalised as EditorNodeKind)
    ? (normalised as EditorNodeKind)
    : 'tool';
}

function toState(apiState: string): WorkflowState {
  switch (apiState) {
    case 'Active':
    case 'Paused':
    case 'Draft':
      return apiState;
    default:
      return 'Draft';
  }
}

function toRunStatus(apiStatus: string): WorkflowRunSummary['status'] {
  switch (apiStatus) {
    case 'Success':
      return 'success';
    case 'Held':
      return 'held';
    case 'Failed':
      return 'failed';
    case 'Running':
      return 'running';
    default:
      return 'failed';
  }
}

function parseParams(json: string | null | undefined): WorkflowNodeParams {
  if (!json) return {};
  try {
    return JSON.parse(json) as WorkflowNodeParams;
  } catch {
    return {};
  }
}

// ── List page ──────────────────────────────────────────────────────────

export function adaptSummary(dto: WorkflowSummaryDto): WorkflowSummary {
  return {
    id: dto.id,
    slug: dto.slug,
    name: dto.name,
    desc: dto.description,
    owner: dto.ownerName,
    ownerColor: dto.ownerColor,
    contributors: dto.contributors,
    triggers: dto.triggerCount,
    runsToday: dto.runsToday,
    success: dto.success,
    avgMs: dto.avgMs,
    state: toState(dto.state),
    version: dto.version,
    updated: relativeTime(dto.updatedAt),
    autoRetry: dto.autoRetry,
    steps: dto.steps.map((s) => adaptStep(s)),
  };
}

function adaptStep(dto: { kind: string; label: string; meta: string | null }): WorkflowStep {
  const stepKind: StepKind = toNodeKind(dto.kind);
  return {
    kind: stepKind,
    label: dto.label,
    meta: dto.meta ?? undefined,
  };
}

// ── Editor page ────────────────────────────────────────────────────────

export function adaptGraph(dto: WorkflowGraphDto): WorkflowGraph {
  return {
    id: dto.id,
    slug: dto.slug,
    name: dto.name,
    desc: dto.description,
    version: dto.version,
    state: toState(dto.state),
    ownerColor: dto.ownerColor,
    nodes: dto.nodes.map(adaptNode),
    edges: dto.edges.map(adaptEdge),
  };
}

function adaptNode(dto: WorkflowGraphNodeDto): WorkflowNode {
  return {
    id: dto.id,
    kind: toNodeKind(dto.kind),
    label: dto.label,
    x: dto.x,
    y: dto.y,
    summary: dto.summary || undefined,
    notes: dto.notes || undefined,
    params: parseParams(dto.paramsJson),
  };
}

function adaptEdge(dto: WorkflowGraphEdgeDto): WorkflowEdge {
  return {
    id: dto.id,
    from: dto.fromNodeId,
    to: dto.toNodeId,
    fromIdx: dto.fromIndex,
    label: dto.label || undefined,
  };
}

export function adaptComment(dto: WorkflowGraphCommentDto): WorkflowComment {
  return { id: dto.id, x: dto.x, y: dto.y, author: dto.author, body: dto.body };
}

// ── Runs + versions ────────────────────────────────────────────────────

export function adaptRun(dto: WorkflowRunDto): WorkflowRunSummary {
  return {
    id: dto.id,
    when: dto.when,
    status: toRunStatus(dto.status),
    duration: dto.duration,
    by: dto.by,
    sequence: dto.sequence,
    total: dto.total,
  };
}

export function adaptVersion(dto: WorkflowVersionDto): WorkflowVersion {
  return {
    id: dto.id,
    tag: dto.tag,
    when: dto.when,
    by: dto.authorName,
    byColor: dto.authorColor,
    message: dto.message,
  };
}

// ── Helpers ────────────────────────────────────────────────────────────

function relativeTime(iso: string): string {
  const past = new Date(iso);
  const now = Date.now();
  const diff = now - past.getTime();
  const min = 60_000;
  const hour = 60 * min;
  const day = 24 * hour;
  const week = 7 * day;
  const month = 30 * day;

  if (diff < min) return 'just now';
  if (diff < hour) return `${Math.floor(diff / min)}m ago`;
  if (diff < day) return `${Math.floor(diff / hour)}h ago`;
  if (diff < week) return `${Math.floor(diff / day)}d ago`;
  if (diff < month) return `${Math.floor(diff / week)}w ago`;
  return `${Math.floor(diff / month)} mo ago`;
}
