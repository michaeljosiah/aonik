import { api } from '@/lib/api';

export interface ConnectorActionRef { key: string; version: number }
export interface ConnectorAction extends ConnectorActionRef {
  connectorKey: string; toolName: string; requiredScopes: string[]; approval: { tier: number | string } | null;
  inputSchema: Record<string, unknown>; outputSchema: Record<string, unknown>;
}
export interface ConnectorDescriptor { key: string; displayName: string; actions: ConnectorAction[] }
export interface ConnectorConnection {
  id: string; connectorKey: string; ownership: 'User' | 'Tenant'; ownerUserId: string | null;
  displayName: string; status: string; grantedScopes: string[]; expectedVersion: string;
}
export interface ConnectorGrant {
  id: string; connectionId: string; granteeType: string; granteeId: string; actions: ConnectorActionRef[];
  expiresAt: string | null; revokedAt: string | null; expectedVersion: string;
}
export interface ConnectionRequest {
  id: string; connectorKey: string; requesterUserId: string; ownership: 'User' | 'Tenant'; ownerUserId: string | null;
  actions: ConnectorActionRef[]; requiredScopes: string[]; purpose: string; status: string; reasonCode: string; nextAction: string;
  workflowId: string | null; runId: string | null; connectionId: string | null; grantId: string | null; workflowGrantId: string | null;
  reviewerRoleId: string | null; expiresAt: string; expectedVersion: string; canConnect: boolean; canReview: boolean; canConfirm: boolean; canCancel: boolean;
  reviewPath: string; consentedAccountLabel: string | null; desiredCapability: string | null;
  automationIdentityId?: string | null; automationGrantId?: string | null;
}
export interface NewConnectionRequest {
  connectorKey: string; ownership: 'User' | 'Tenant'; actions: ConnectorActionRef[]; purpose: string; idempotencyKey: string;
  connectionId?: string; workflowId?: string; runId?: string; desiredCapability?: string;
}
const root = '/v1/integrations';
export const connectorService = {
  catalog: () => api.get<ConnectorDescriptor[]>(`${root}/connectors`),
  connections: (key: string) => api.get<ConnectorConnection[]>(`${root}/connectors/${encodeURIComponent(key)}/connections`),
  managed: () => api.get<ConnectorConnection[]>(`${root}/managed-connections`),
  readiness: (key: string, action: ConnectorActionRef, connectionId?: string) => api.get<{ code: string; nextAction: string; connectionId: string | null }>(
    `${root}/connectors/${encodeURIComponent(key)}/readiness`, { params: { actionKey: action.key, version: action.version, connectionId } }),
  requests: () => api.get<ConnectionRequest[]>(`${root}/connection-requests?take=100`),
  request: (id: string) => api.get<ConnectionRequest>(`${root}/connection-requests/${encodeURIComponent(id)}`),
  createRequest: (request: NewConnectionRequest) => api.post<ConnectionRequest>(`${root}/connection-requests`, request),
  cancelRequest: (request: ConnectionRequest) => api.post<ConnectionRequest>(`${root}/connection-requests/${request.id}/cancel`, { expectedVersion: request.expectedVersion }),
  decideRequest: (request: ConnectionRequest, decision: 'Approve' | 'Reject', connectionId?: string) => api.post<ConnectionRequest>(`${root}/connection-requests/${request.id}/decide`,
    { expectedVersion: request.expectedVersion, decision, connectionId }),
  beginConsent: (request: ConnectionRequest) => api.post<{ authorizationUrl: string; expiresAt: string; scopes: string[] }>(`${root}/connection-requests/${request.id}/consent`,
    { expectedVersion: request.expectedVersion }, { withCredentials: true }),
  confirmAccount: (request: ConnectionRequest) => api.post<ConnectionRequest>(`${root}/connection-requests/${request.id}/confirm-account`, { expectedVersion: request.expectedVersion }),
  disconnect: (connection: ConnectorConnection) => api.post<ConnectorConnection>(`${root}/connections/${connection.id}/disconnect`, { expectedVersion: connection.expectedVersion }),
  rename: (connection: ConnectorConnection, displayName: string) => api.put<ConnectorConnection>(`${root}/connections/${connection.id}`, { expectedVersion: connection.expectedVersion, displayName }),
  grants: (id: string) => api.get<ConnectorGrant[]>(`${root}/connections/${encodeURIComponent(id)}/grants`),
  binding: (id: string, workflowId: string, action: ConnectorActionRef) => api.get<{ connectionId: string; workflowGrantId: string; allowedActions: ConnectorActionRef[] }>(`${root}/connections/${encodeURIComponent(id)}/workflow-binding`, { params: { workflowId, actionKey: action.key, version: action.version } }),
  issueGrant: (connection: ConnectorConnection, granteeType: string, granteeId: string, actions: ConnectorActionRef[], expiresAt?: string, existing?: ConnectorGrant) => api.post<ConnectorGrant>(`${root}/connections/${connection.id}/grants`,
    { granteeType, granteeId, actions, expectedConnectionVersion: connection.expectedVersion, expiresAt, existingGrantId: existing?.id, expectedGrantVersion: existing?.expectedVersion }),
  revokeGrant: (grant: ConnectorGrant) => api.post(`${root}/grants/${grant.id}/revoke`, { expectedVersion: grant.expectedVersion }),
  setDefault: (connectionId: string | null, tenant = false) => api.put(tenant ? '/tenant/settings/values/Integrations.Gmail.DefaultConnectionId' : '/v1/settings/user/Integrations.Gmail.DefaultConnectionId', { value: connectionId }),
};

export function connectorError(error: unknown): string {
  if (error && typeof error === 'object' && 'userMessage' in error) return String(error.userMessage);
  return error instanceof Error ? error.message : 'The connection operation failed. Reload its current state before trying again.';
}

export function consentUrl(value: string): string {
  const uri = new URL(value);
  if (uri.protocol !== 'https:' || uri.hostname !== 'accounts.google.com' || uri.username || uri.password) throw new Error('The server did not return a trusted Google consent URL.');
  return uri.href;
}
