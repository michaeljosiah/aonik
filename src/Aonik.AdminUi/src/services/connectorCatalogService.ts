import { api } from '@/lib/api';
import type { ConnectorActionRef, ConnectorConnection } from './connectorService';

export interface ConnectorPackage {
  packageKey: string;
  packageVersion: string;
  connectorKey: string;
  displayName: string;
  publisherId: string;
  digest: string;
  adapterKind: 'Native' | 'Mcp' | 'Http';
  actionCount: number;
}

export interface ConnectorPackageAction extends ConnectorActionRef {
  displayName: string;
  minimumRiskTier: string;
  requiredScopes: string[];
  scopeAlternatives?: string[][] | null;
  endpoint: string;
  remoteToolName?: string;
  httpMethod?: string;
  inputSchema: unknown;
  outputSchema: unknown;
}

export interface ConnectorPackageDetail {
  package: ConnectorPackage;
  manifest: {
    authentication: { kind: string; headerName?: string };
    actions: ConnectorPackageAction[];
    limits: { maximumInputBytes: number; maximumOutputBytes: number; timeoutSeconds: number };
  };
}

export interface ConnectorInstallation {
  id: string;
  adapterKind: 'Mcp' | 'Http';
  packageKey: string;
  packageVersion: string;
  digest: string;
  action: ConnectorActionRef | null;
  approvalState: string;
  isActive: boolean;
  authConfigured: boolean;
  credentialVersion: number;
  expectedVersion: string;
}

interface ReviewedAction { action: ConnectorActionRef; tier: string; scopes: string[]; contractHash: string }
export interface ConnectorPackageImpact {
  installation: ConnectorInstallation;
  target: ConnectorPackage | null;
  currentActions: ReviewedAction[];
  targetActions: ReviewedAction[];
  workflows: { workflowId: string; workflowVersionId: string; slug: string; version: string; current: boolean }[];
  runs: { runId: string; workflowVersionId: string; status: string }[];
  truncated: boolean;
}

const root = '/v1/integrations/catalog';
const versionPath = (release: ConnectorPackage) => `${root}/packages/${encodeURIComponent(release.packageKey)}/${encodeURIComponent(release.packageVersion)}`;
const installationPath = (installation: ConnectorInstallation) => `${root}/installations/${encodeURIComponent(installation.id)}`;

export const connectorCatalogService = {
  releases: () => api.get<ConnectorPackage[]>(`${root}/packages`),
  inspect: (release: ConnectorPackage) => api.get<ConnectorPackageDetail>(versionPath(release)),
  download: (release: ConnectorPackage) => api.get<Blob>(`${versionPath(release)}/artifact`, { responseType: 'blob' }),
  installations: () => api.get<ConnectorInstallation[]>(`${root}/installations`),
  importRelease: (release: ConnectorPackage, authSecret?: string, authUsername?: string) => api.post<ConnectorInstallation[]>(`${root}/packages/import`, {
    packageKey: release.packageKey, packageVersion: release.packageVersion, digest: release.digest, authSecret, authUsername,
  }),
  command: (installation: ConnectorInstallation, command: 'submit' | 'activate' | 'disable') => api.post<ConnectorInstallation>(`${installationPath(installation)}/${command}`, { expectedVersion: installation.expectedVersion }),
  review: (installation: ConnectorInstallation, approve: boolean, notes?: string) => api.post<ConnectorInstallation>(`${installationPath(installation)}/review`, { expectedVersion: installation.expectedVersion, approve, notes }),
  rotate: (installation: ConnectorInstallation, authSecret: string, authUsername?: string) => api.post<ConnectorInstallation>(`${installationPath(installation)}/credential`, { expectedVersion: installation.expectedVersion, authSecret, authUsername }),
  impact: (installation: ConnectorInstallation, targetVersion?: string) => api.get<ConnectorPackageImpact>(`${installationPath(installation)}/impact`, { params: { targetVersion } }),
  connect: (installation: ConnectorInstallation, release: ConnectorPackage, displayName: string) => api.post<ConnectorConnection>(`/v1/integrations/connectors/${encodeURIComponent(release.connectorKey)}/service-accounts`, { adapterKind: installation.adapterKind, installationId: installation.id, displayName }),
};
