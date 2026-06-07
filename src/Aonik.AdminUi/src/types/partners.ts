export interface PartnerListItem {
  partnerId: string;
  name: string;
  status: string;
  branchCount: number;
  connectorCount: number;
  activeRoutingRuleCount: number;
  linkedBillerCount: number;
  coverageCountries: string[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface PartnerBranchItem {
  branchId: string;
  name: string;
  country: string;
  city: string;
  metadataJson?: string | null;
  createdAt?: string | null;
  updatedAt?: string | null;
}

export interface PartnerConnectorItem {
  connectorId: string;
  connectorType: string;
  status: string;
  credentialsRef?: string | null;
  configJson?: string | null;
  createdAt?: string | null;
  updatedAt?: string | null;
}

export interface PartnerRoutingRuleItem {
  routingRuleId: string;
  priority: number;
  isActive: boolean;
  conditionsJson?: string | null;
  targetConnectorId?: string | null;
  createdAt?: string | null;
  updatedAt?: string | null;
}

export interface PartnerTransmissionItem {
  transmissionId: string;
  connectorId: string;
  connectorType?: string | null;
  status: string;
  retryCount: number;
  lastError?: string | null;
  createdAt?: string | null;
  updatedAt?: string | null;
}

export interface PartnerLinkedBillerItem {
  billerId: string;
  name: string;
  countryCode: string;
  isActive: boolean;
  serviceCount: number;
}

export interface PartnerDetail {
  partnerId: string;
  name: string;
  status: string;
  capabilitiesJson?: string | null;
  operatingHoursJson?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  branchCount?: number;
  connectorCount?: number;
  activeRoutingRuleCount?: number;
  linkedBillerCount?: number;
  branches?: PartnerBranchItem[] | null;
  connectors?: PartnerConnectorItem[] | null;
  routingRules?: PartnerRoutingRuleItem[] | null;
  recentTransmissions?: PartnerTransmissionItem[] | null;
  linkedBillers?: PartnerLinkedBillerItem[] | null;
}

export interface CreatePartnerRequest {
  name: string;
  status: string;
  capabilitiesJson?: string | null;
  operatingHoursJson?: string | null;
}

export interface UpdatePartnerRequest {
  name?: string;
  status?: string;
  capabilitiesJson?: string | null;
  operatingHoursJson?: string | null;
}

export interface CreatePartnerResponse {
  partnerId: string;
  name: string;
  status: string;
  createdAt: string;
}

export interface CreatePartnerConnectorRequest {
  connectorType: string;
  status?: string | null;
  credentialsRef?: string | null;
  configJson?: string | null;
}

export interface UpdatePartnerConnectorRequest {
  connectorType?: string | null;
  status?: string | null;
  credentialsRef?: string | null;
  configJson?: string | null;
}
