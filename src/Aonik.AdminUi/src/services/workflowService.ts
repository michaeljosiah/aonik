// Workflows API client. Wraps the workflow endpoints exposed by
// Aonik.Agents/Endpoints/Workflows/*. Reads (list / get / runs / versions)
// back the registry list page and the editor; writes (create / update /
// delete) are how the editor persists. Save replaces the whole graph —
// see WorkflowSaveRequest.cs server-side for the contract.

import { api } from '@/lib/api';

// ── Response shapes (keep aligned with C# Contracts/Models/Workflows) ──

export interface WorkflowStepSummaryDto {
  kind: string;
  label: string;
  meta: string | null;
}

export interface WorkflowSummaryDto {
  id: string;
  slug: string;
  name: string;
  description: string;
  state: string;
  version: string;
  autoRetry: boolean;
  triggerCount: number;
  runsToday: number;
  /** 0..1 ratio over recent runs. */
  success: number;
  /** Average run duration in milliseconds. */
  avgMs: number;
  ownerName: string;
  ownerColor: string;
  contributors: string[];
  steps: WorkflowStepSummaryDto[];
  updatedAt: string;
}

export interface WorkflowGraphNodeDto {
  id: string;
  kind: string;
  label: string;
  summary: string;
  notes: string;
  x: number;
  y: number;
  /** Per-kind parameter object as raw JSON. */
  paramsJson: string;
}

export interface WorkflowGraphEdgeDto {
  id: string;
  fromNodeId: string;
  toNodeId: string;
  fromIndex: number;
  label: string;
}

export interface WorkflowGraphCommentDto {
  id: string;
  x: number;
  y: number;
  author: string;
  body: string;
}

export interface WorkflowGraphDto {
  id: string;
  slug: string;
  name: string;
  description: string;
  state: string;
  version: string;
  autoRetry: boolean;
  ownerColor: string;
  ownerName: string;
  contributors: string[];
  nodes: WorkflowGraphNodeDto[];
  edges: WorkflowGraphEdgeDto[];
  comments: WorkflowGraphCommentDto[];
}

export interface WorkflowRunDto {
  id: string;
  startedAt: string;
  completedAt: string | null;
  /** Relative-time label like "2m ago" — formatted server-side. */
  when: string;
  status: string;
  /** Pre-formatted duration like "2.4s" or "7m 14s". */
  duration: string;
  durationMs: number;
  by: string;
  sequence: string[];
  total: number;
}

export interface WorkflowVersionDto {
  id: string;
  tag: string;
  message: string;
  authorName: string;
  authorColor: string;
  createdAt: string;
  /** Relative label like "today" or "8d ago". */
  when: string;
}

// ── Save payload (mirror of WorkflowSaveRequest.cs) ────────────────────

export interface WorkflowSaveNodeDto {
  /** Either an existing server Guid or a transient editor id (e.g. "n3xa9f"). */
  clientId: string;
  kind: string;
  label: string;
  summary: string;
  notes: string;
  x: number;
  y: number;
  paramsJson: string;
}

export interface WorkflowSaveEdgeDto {
  fromClientId: string;
  toClientId: string;
  fromIndex: number;
  label: string;
}

export interface WorkflowSaveRequestDto {
  slug: string;
  name: string;
  description: string;
  state: string;
  version: string;
  autoRetry: boolean;
  ownerColor: string;
  ownerAgentId: string | null;
  contributors: string[];
  nodes: WorkflowSaveNodeDto[];
  edges: WorkflowSaveEdgeDto[];
  versionMessage: string | null;
}

// ── Service ─────────────────────────────────────────────────────────────

export const workflowService = {
  list: async (): Promise<WorkflowSummaryDto[]> => {
    return api.get<WorkflowSummaryDto[]>('/ai/workflows');
  },

  getBySlug: async (slug: string): Promise<WorkflowGraphDto> => {
    return api.get<WorkflowGraphDto>(`/ai/workflows/${encodeURIComponent(slug)}`);
  },

  listRuns: async (workflowId: string, take = 20): Promise<WorkflowRunDto[]> => {
    return api.get<WorkflowRunDto[]>(`/ai/workflows/${workflowId}/runs?take=${take}`);
  },

  listVersions: async (workflowId: string): Promise<WorkflowVersionDto[]> => {
    return api.get<WorkflowVersionDto[]>(`/ai/workflows/${workflowId}/versions`);
  },

  create: async (req: WorkflowSaveRequestDto): Promise<WorkflowGraphDto> => {
    return api.post<WorkflowGraphDto>('/ai/workflows', req);
  },

  update: async (slug: string, req: WorkflowSaveRequestDto): Promise<WorkflowGraphDto> => {
    return api.put<WorkflowGraphDto>(`/ai/workflows/${encodeURIComponent(slug)}`, req);
  },

  delete: async (slug: string): Promise<void> => {
    await api.delete(`/ai/workflows/${encodeURIComponent(slug)}`);
  },
};
