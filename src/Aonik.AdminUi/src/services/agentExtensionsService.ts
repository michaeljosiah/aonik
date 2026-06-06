import { api } from '@/lib/api';

// ─── DTOs (mirror Aonik.Agents.Contracts.Models.Tenant) ────────────────────

export interface TenantSkill {
  id: string;
  name: string;
  version: string;
  description: string;
  scriptsPresent: boolean;
  scriptsEnabled: boolean;
  approvalState: string;
  isActive: boolean;
  allowedTools: string[];
  createdAt: string;
  reviewedAt?: string | null;
  reviewNotes?: string | null;
}

export interface SkillValidation {
  isValid: boolean;
  errors: string[];
  name: string;
  description: string;
  allowedTools: string[];
  scriptsPresent: boolean;
}

export interface SkillPreview {
  name: string;
  description: string;
  allowedTools: string[];
  catalogueText: string;
  markdown: string;
}

export interface TenantMcpServer {
  id: string;
  name: string;
  endpoint: string;
  transportType: string;
  authKind: string;
  authConfigured: boolean;
  allowedToolPrefixes: string[];
  defaultRiskTier: string;
  approvalState: string;
  isActive: boolean;
  credentialVersion: number;
  createdAt: string;
  reviewedAt?: string | null;
  reviewNotes?: string | null;
}

export interface McpDiscoveredTool {
  name: string;
  description: string;
  tier: string;
}

export interface McpDryRun {
  connected: boolean;
  error?: string | null;
  tools: McpDiscoveredTool[];
}

export interface TenantHttpTool {
  id: string;
  name: string;
  description: string;
  method: string;
  urlTemplate: string;
  parameterSchemaJson: string;
  authKind: string;
  authConfigured: boolean;
  riskTier: string;
  actionKind?: string | null;
  approvalState: string;
  isActive: boolean;
  createdAt: string;
  reviewedAt?: string | null;
  reviewNotes?: string | null;
}

export interface HttpToolTest {
  name: string;
  tier: string;
  parameterSchemaJson: string;
  note: string;
}

export interface SaveMcpServerRequest {
  name: string;
  endpoint: string;
  transportType: string;
  authKind: string;
  authSecret?: string | null;
  authUsername?: string | null;
  authHeaderName?: string | null;
  allowedToolPrefixes?: string[] | null;
}

export interface SaveHttpToolRequest {
  name: string;
  description: string;
  method: string;
  urlTemplate: string;
  parameterSchemaJson: string;
  authKind: string;
  authSecret?: string | null;
  authUsername?: string | null;
  authHeaderName?: string | null;
}

const enc = encodeURIComponent;

export const agentExtensionsService = {
  // ── Skills ──
  skills: {
    list: () => api.get<TenantSkill[]>('/ai/tenant-skills'),
    validate: (markdown: string) =>
      api.post<SkillValidation>('/ai/tenant-skills/validate', { markdown }),
    upload: (markdown: string) =>
      api.post<TenantSkill>('/ai/tenant-skills', { markdown }),
    preview: (id: string) => api.get<SkillPreview>(`/ai/tenant-skills/${enc(id)}/preview`),
    submit: (id: string) => api.post<TenantSkill>(`/ai/tenant-skills/${enc(id)}/submit`),
    activate: (id: string) => api.post<TenantSkill>(`/ai/tenant-skills/${enc(id)}/activate`),
    deactivate: (id: string) => api.post<TenantSkill>(`/ai/tenant-skills/${enc(id)}/deactivate`),
    remove: (id: string) => api.delete<void>(`/ai/tenant-skills/${enc(id)}`),
    review: (id: string, approve: boolean, notes?: string) =>
      api.post<TenantSkill>(`/ai/tenant-skills/${enc(id)}/review`, { approve, notes }),
    enableScripts: (id: string, enabled: boolean, notes?: string) =>
      api.post<TenantSkill>(`/ai/tenant-skills/${enc(id)}/enable-scripts`, { enabled, notes }),
  },

  // ── Remote MCP servers ──
  mcp: {
    list: () => api.get<TenantMcpServer[]>('/ai/tenant-mcp-servers'),
    get: (id: string) => api.get<TenantMcpServer>(`/ai/tenant-mcp-servers/${enc(id)}`),
    create: (req: SaveMcpServerRequest) => api.post<TenantMcpServer>('/ai/tenant-mcp-servers', req),
    update: (id: string, req: SaveMcpServerRequest) =>
      api.put<TenantMcpServer>(`/ai/tenant-mcp-servers/${enc(id)}`, req),
    remove: (id: string) => api.delete<void>(`/ai/tenant-mcp-servers/${enc(id)}`),
    submit: (id: string) => api.post<TenantMcpServer>(`/ai/tenant-mcp-servers/${enc(id)}/submit`),
    activate: (id: string) => api.post<TenantMcpServer>(`/ai/tenant-mcp-servers/${enc(id)}/activate`),
    deactivate: (id: string) => api.post<TenantMcpServer>(`/ai/tenant-mcp-servers/${enc(id)}/deactivate`),
    test: (id: string) => api.post<McpDryRun>(`/ai/tenant-mcp-servers/${enc(id)}/test`),
    review: (id: string, approve: boolean, notes?: string, defaultRiskTier?: string) =>
      api.post<TenantMcpServer>(`/ai/tenant-mcp-servers/${enc(id)}/review`, { approve, notes, defaultRiskTier }),
  },

  // ── Declarative HTTP tools ──
  http: {
    list: () => api.get<TenantHttpTool[]>('/ai/tenant-http-tools'),
    get: (id: string) => api.get<TenantHttpTool>(`/ai/tenant-http-tools/${enc(id)}`),
    create: (req: SaveHttpToolRequest) => api.post<TenantHttpTool>('/ai/tenant-http-tools', req),
    update: (id: string, req: SaveHttpToolRequest) =>
      api.put<TenantHttpTool>(`/ai/tenant-http-tools/${enc(id)}`, req),
    remove: (id: string) => api.delete<void>(`/ai/tenant-http-tools/${enc(id)}`),
    submit: (id: string) => api.post<TenantHttpTool>(`/ai/tenant-http-tools/${enc(id)}/submit`),
    activate: (id: string) => api.post<TenantHttpTool>(`/ai/tenant-http-tools/${enc(id)}/activate`),
    deactivate: (id: string) => api.post<TenantHttpTool>(`/ai/tenant-http-tools/${enc(id)}/deactivate`),
    test: (id: string) => api.post<HttpToolTest>(`/ai/tenant-http-tools/${enc(id)}/test`),
    review: (id: string, approve: boolean, notes?: string, riskTier?: string) =>
      api.post<TenantHttpTool>(`/ai/tenant-http-tools/${enc(id)}/review`, { approve, notes, riskTier }),
  },
};

// ─── Unified view model the hub renders ───────────────────────────────────

export type ExtType = 'skill' | 'mcp' | 'http';
export type ExtState = 'draft' | 'review' | 'approved' | 'active' | 'rejected';

export interface Extension {
  id: string;
  type: ExtType;
  name: string;
  slug: string;
  description: string;
  state: ExtState;
  tier: string; // 'na' | 'readonly' | 'low' | 'medium' | 'high' | 'mixed'
  scriptsPresent: boolean;
  scriptsEnabled: boolean;
  authConfigured: boolean;
  reviewNotes?: string | null;
  raw: TenantSkill | TenantMcpServer | TenantHttpTool;
}

function mapState(approvalState: string, isActive: boolean): ExtState {
  switch (approvalState) {
    case 'PendingPlatformReview':
      return 'review';
    case 'Rejected':
      return 'rejected';
    case 'Approved':
      return isActive ? 'active' : 'approved';
    default:
      return 'draft';
  }
}

export function toExtensions(
  skills: TenantSkill[],
  servers: TenantMcpServer[],
  tools: TenantHttpTool[],
): Extension[] {
  const s = skills.map<Extension>((x) => ({
    id: x.id,
    type: 'skill',
    name: x.name,
    slug: x.name,
    description: x.description,
    state: mapState(x.approvalState, x.isActive),
    tier: 'na',
    scriptsPresent: x.scriptsPresent,
    scriptsEnabled: x.scriptsEnabled,
    authConfigured: true,
    reviewNotes: x.reviewNotes,
    raw: x,
  }));
  const m = servers.map<Extension>((x) => ({
    id: x.id,
    type: 'mcp',
    name: x.name,
    slug: x.endpoint,
    description: `Remote MCP server (${x.transportType}).`,
    state: mapState(x.approvalState, x.isActive),
    tier: x.defaultRiskTier.toLowerCase(),
    scriptsPresent: false,
    scriptsEnabled: false,
    authConfigured: x.authConfigured,
    reviewNotes: x.reviewNotes,
    raw: x,
  }));
  const h = tools.map<Extension>((x) => ({
    id: x.id,
    type: 'http',
    name: x.name,
    slug: `${x.method} ${x.urlTemplate}`,
    description: x.description,
    state: mapState(x.approvalState, x.isActive),
    tier: x.riskTier.toLowerCase(),
    scriptsPresent: false,
    scriptsEnabled: false,
    authConfigured: x.authConfigured,
    reviewNotes: x.reviewNotes,
    raw: x,
  }));
  return [...s, ...m, ...h];
}
