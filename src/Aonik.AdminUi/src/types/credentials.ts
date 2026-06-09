// Partner-owned connector credentials (Spec 042 §6, §12). Secret VALUES are never part of any response —
// only field state (set / not-set + rotation version) and which connectors bind a bundle.

export interface CredentialFieldState {
  name: string;
  label: string;
  required: boolean;
  isSet: boolean;
  version: number;
}

export interface CredentialBundleListItem {
  ref: string;
  name: string;
  connectorKind: string;
  fields: CredentialFieldState[];
  boundConnectorIds: string[];
  updatedAt?: string | null;
}

export interface ConnectorCredentialField {
  name: string;
  label: string;
  required: boolean;
}

export interface ConnectorConfigField {
  name: string;
  label: string;
  required: boolean;
  allowedValues?: string[] | null;
  defaultValue?: string | null;
}

export interface ConnectorKindSchema {
  kind: string;
  providerCode: string;
  port: string;
  displayName: string;
  credentialFields: ConnectorCredentialField[];
  configFields: ConnectorConfigField[];
  environments: string[];
}

export interface CreateCredentialBundleRequest {
  ref: string;
  name: string;
  connectorKind: string;
  secrets: Record<string, string>;
}

export interface UpdateCredentialBundleRequest {
  name?: string | null;
  secrets: Record<string, string>;
}

export interface RotateCredentialFieldRequest {
  field: string;
  newValue: string;
  previousTtlHours?: number | null;
}

export interface LiftLegacyFlutterwaveResult {
  partnerId: string;
  bundleRefs: string[];
  connectorIds: string[];
  payoutsBackfilled: number;
  transmissionsBackfilled: number;
}
