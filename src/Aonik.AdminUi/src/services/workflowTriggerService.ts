import { api } from '@/lib/api';
import type { ToolApprovalPreview } from '@/lib/toolApprovalPreview';
import type { WorkflowDiagnostic } from './workflowService';

export interface TriggerDefinition {
  schemaVersion: 1; source: 'InvoiceCreated' | 'InvoiceIssued' | 'Schedule' | 'GmailMessageAdded'; workflowVersionId: string; automationIdentityId: string;
  subject: { type: 'Invoice'; mapping: 'EventSubject' | 'Fixed'; fixedIds: string[] };
  limits: { maximumStartsPerHour: number; maximumConcurrentRuns: number; maximumCausationDepth: number; ignoreSelfGenerated: boolean };
  schedule: { cron: string; timeZone: string; startAtUtc: string; endAtUtc: string; maximumOccurrences: number; maximumCatchUpOccurrences: number } | null;
  mailbox: { connectionId: string; workflowGrantId: string; labelIds: string[]; mode: 'Polling' | 'PushWithPolling'; pollIntervalMinutes: number; allowPollingFallback: boolean; backfillSinceUtc: string | null } | null;
}
export interface WorkflowTrigger {
  id: string; workflowId: string; name: string; status: string; draftRevision: number; draftHash: string; draft: TriggerDefinition;
  activeRevision: number | null; activeHash: string | null; active: TriggerDefinition | null; activationProposalId: string | null;
  taskId: string | null; subscriptionId: string | null; admittedRuns: number; lastError: string | null; expectedVersion: string;
}
const base = (slug: string) => `/ai/workflows/${encodeURIComponent(slug)}/triggers`;
export const workflowTriggerService = {
  list: (slug: string) => api.get<WorkflowTrigger[]>(base(slug)),
  save: (slug: string, current: WorkflowTrigger | null, name: string, definition: TriggerDefinition) => current
    ? api.put<WorkflowTrigger>(`${base(slug)}/${current.id}`, { name, definition, expectedVersion: current.expectedVersion })
    : api.post<WorkflowTrigger>(base(slug), { name, definition }),
  delete: (slug: string, current: WorkflowTrigger) => api.delete(`${base(slug)}/${current.id}?expectedVersion=${encodeURIComponent(current.expectedVersion)}`),
  validate: (slug: string, id: string) => api.post<{ definitionHash: string; diagnostics: WorkflowDiagnostic[] }>(`${base(slug)}/${id}/validate`, {}),
  request: (slug: string, current: WorkflowTrigger, disabling: boolean) => api.post<{ proposalId: string; approvalRequestId: string; preview: ToolApprovalPreview }>(
    `${base(slug)}/${current.id}/${disabling ? 'disable' : 'activation'}-requests`, { expectedVersion: current.expectedVersion, definitionHash: disabling ? current.activeHash : current.draftHash }),
};
