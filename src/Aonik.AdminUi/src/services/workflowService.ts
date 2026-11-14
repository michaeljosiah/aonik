// Workflows API client. Wraps the workflow endpoints exposed by
// Aonik.Agents/Endpoints/Workflows/*. Reads (list / get / runs / versions)
// back the registry list page and the editor; writes (create / update /
// delete) are how the editor persists. Save replaces the whole graph —
// see WorkflowSaveRequest.cs server-side for the contract.

import { api } from '@/lib/api';
import type { ToolApprovalPreview } from '@/lib/toolApprovalPreview';

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
  key?: string;
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
  routeKey?: string | null;
  key?: string;
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
  expectedVersion?: string;
  definitionKind?: string;
  publishedVersionId?: string | null;
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
  mode?: string;
  workflowVersionId?: string | null;
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
  definitionHash?: string | null;
  publishedAt?: string | null;
  isExecutable?: boolean;
  tag: string;
  message: string;
  authorName: string;
  authorColor: string;
  createdAt: string;
  /** Relative label like "today" or "8d ago". */
  when: string;
}

export interface WorkflowDiagnostic { code: string; path: string; message: string; severity: string }
export interface WorkflowValidation { isValid: boolean; definitionHash: string; diagnostics: WorkflowDiagnostic[] }
export interface WorkflowActionCapability { key: string; contractVersion: number; requiredPermission: string; riskTier: string; inputSchema: Record<string, unknown>; outputSchema: Record<string, unknown> }
export interface WorkflowCapabilities { schemaVersion: number; nodeKinds: { kind: string; canSimulate: boolean; canExecute: boolean }[]; invoiceFacts: { name: string; type: string }[]; comparisonOperators: string[]; actions: WorkflowActionCapability[] }
export interface WorkflowSimulation { mode: string; status: string; definitionHash: string; steps: { nodeKey: string; kind: string; status: string; decision?: unknown; fixtureOutput?: unknown }[]; diagnostics: WorkflowDiagnostic[] }
export interface WorkflowRunDetail {
  id: string; workflowId: string; workflowVersionId: string; definitionHash: string; status: string; outcome: string | null; errorCode: string | null;
  actorType: string; actorId: string; initiatingUserId: string | null; subjectType: string; subjectId: string; expectedVersion: string; startedAt: string; completedAt: string | null;
  steps: { id: string; nodeKey: string; invocationPath: string; status: string; actionKey: string | null; actionVersion: number | null; selectedRoute: string | null;
    decisionTrace: unknown; result: unknown; approvalRequestId: string | null; proposalId: string | null; aiRunId: string | null; errorCode: string | null;
    approval: { id: string; riskTier: string; actionKind: string; status: string; expiresAt: string; canDecide: boolean; preview: ToolApprovalPreview | null } | null }[];
}

// ── Save payload (mirror of WorkflowSaveRequest.cs) ────────────────────

export interface WorkflowSaveNodeDto {
  /** Either an existing server Guid or a transient editor id (e.g. "n3xa9f"). */
  clientId: string;
  key?: string;
  kind: string;
  label: string;
  summary: string;
  notes: string;
  x: number;
  y: number;
  paramsJson: string;
}

export interface WorkflowSaveEdgeDto {
  routeKey?: string | null;
  fromClientId: string;
  toClientId: string;
  fromIndex: number;
  label: string;
}

export interface WorkflowSaveRequestDto {
  expectedVersion?: string;
  definitionKind?: string;
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
  capabilities: () => api.get<WorkflowCapabilities>('/ai/workflows/capabilities'),
  validate: (slug: string) => api.post<WorkflowValidation>(`/ai/workflows/${encodeURIComponent(slug)}/validate`, {}),
  simulate: (slug: string, fixture: { facts: Record<string, unknown>; actionOutputs?: Record<string, unknown> }) => api.post<WorkflowSimulation>(`/ai/workflows/${encodeURIComponent(slug)}/simulate`, fixture),
  requestPublication: (command: { slug: string; expectedVersion: string; definitionHash: string; expectedPublishedVersionId: string | null; targetVersionId?: string }) => api.post<{ proposalId: string; approvalRequestId: string; preview: ToolApprovalPreview }>(`/ai/workflows/${encodeURIComponent(command.slug)}/publication-requests`, command),
  requestRetirement: (command: { slug: string; expectedVersion: string; definitionHash: string; expectedPublishedVersionId: string }) => api.post<{ proposalId: string; approvalRequestId: string; preview: ToolApprovalPreview }>(`/ai/workflows/${encodeURIComponent(command.slug)}/retirement-requests`, command),
  start: (slug: string, workflowVersionId: string, subjectId: string, idempotencyKey: string) => api.post<WorkflowRunDetail>(`/ai/workflows/${encodeURIComponent(slug)}/runs`, { workflowVersionId, subjectType: 'Invoice', subjectId, idempotencyKey }),
  run: (id: string) => api.get<WorkflowRunDetail>(`/ai/workflow-runs/${encodeURIComponent(id)}`),
  resume: (run: WorkflowRunDetail) => api.post<WorkflowRunDetail>(`/ai/workflow-runs/${run.id}/resume`, { expectedVersion: run.expectedVersion }),
  cancelRun: (run: WorkflowRunDetail) => api.post<WorkflowRunDetail>(`/ai/workflow-runs/${run.id}/cancel`, { expectedVersion: run.expectedVersion }),
  decide: (id: string, decision: 'Approve' | 'Reject') => api.post(`/ai/tool-approvals/${encodeURIComponent(id)}/decide`, { decision }),
  list: async (skip = 0, take = 50): Promise<WorkflowSummaryDto[]> => {
    return api.get<WorkflowSummaryDto[]>(`/ai/workflows?skip=${skip}&take=${take}`);
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

  delete: async (slug: string, expectedVersion: string): Promise<void> => {
    await api.delete(`/ai/workflows/${encodeURIComponent(slug)}?expectedVersion=${encodeURIComponent(expectedVersion)}`);
  },
};
