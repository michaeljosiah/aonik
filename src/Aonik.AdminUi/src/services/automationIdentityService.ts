import { api } from '@/lib/api';

export interface AutomationAction { key: string; version: number }
export interface AutomationGrant { workflowVersionId: string; subjectType: 'Invoice'; allSubjects: boolean; subjectIds: string[]; actions: AutomationAction[] }
export interface AutomationIdentityCommand { name: string; accountableUserId: string; expiresAt: string | null; grants: AutomationGrant[]; expectedVersion?: string }
export interface AutomationIdentity extends AutomationIdentityCommand { id: string; status: string; expectedVersion: string; configuredByUserId: string | null }
export interface AutomationUsage { activeRuns: number; workflowVersionIds: string[] }
export interface AutomationCapability { workflowId: string; workflowVersionId: string; definitionHash: string; actions: (AutomationAction & { requiredPermission: string })[] }

const base = '/v1/integrations/automation-identities';
export const automationIdentityService = {
  list: () => api.get<AutomationIdentity[]>(base),
  usage: (id: string) => api.get<AutomationUsage>(`${base}/${encodeURIComponent(id)}/usage`),
  capabilities: (versionId: string) => api.get<AutomationCapability>(`${base}/workflow-versions/${encodeURIComponent(versionId)}`),
  save: (id: string | null, command: AutomationIdentityCommand) => id
    ? api.put<AutomationIdentity>(`${base}/${encodeURIComponent(id)}`, command) : api.post<AutomationIdentity>(base, command),
  disable: (identity: AutomationIdentity) => api.post<AutomationIdentity>(`${base}/${encodeURIComponent(identity.id)}/disable`, { expectedVersion: identity.expectedVersion }),
};
