import apiClient from '@/lib/api';
import { api } from '@/lib/api';
import type {
  CreateCustomerRequest,
  CreateCustomerResponse,
  CustomerDetail,
  CustomerListItem,
  CustomerStats,
  PagedResult,
} from '@/types';

export interface ListCustomersParams {
  pageNumber?: number;
  pageSize?: number;
  status?: string;
  partyType?: string;
  search?: string;
}

export const customerService = {
  list: async (params: ListCustomersParams = {}): Promise<PagedResult<CustomerListItem>> => {
    const queryParams = new URLSearchParams();
    if (params.pageNumber) queryParams.append('pageNumber', params.pageNumber.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params.status) queryParams.append('status', params.status);
    if (params.partyType) queryParams.append('partyType', params.partyType);
    if (params.search) queryParams.append('search', params.search);

    const query = queryParams.toString();
    return api.get<PagedResult<CustomerListItem>>(`/admin/customers${query ? `?${query}` : ''}`);
  },
  get: async (partyId: string): Promise<CustomerDetail> => {
    return api.get<CustomerDetail>(`/admin/customers/${partyId}`);
  },
  getStats: async (partyId: string): Promise<CustomerStats> => {
    return api.get<CustomerStats>(`/admin/customers/${partyId}/stats`);
  },
  create: async (data: CreateCustomerRequest): Promise<CreateCustomerResponse> => {
    return api.post<CreateCustomerResponse>('/admin/customers', data);
  },
  listInsights: async (partyId: string): Promise<CustomerInsightsResponse> => {
    return api.get<CustomerInsightsResponse>(`/admin/customers/${partyId}/insights`);
  },

  /** Returns a unified activity feed (orders, payments, audit, documents). */
  getActivity: async (
    partyId: string,
    take: number = 20,
  ): Promise<CustomerActivityResponse> => {
    return api.get<CustomerActivityResponse>(
      `/admin/customers/${partyId}/activity?take=${take}`,
    );
  },

  /** Downloads the customer data bundle as a JSON file. */
  exportData: async (partyId: string): Promise<void> => {
    const response = await apiClient.get(`/admin/customers/${partyId}/export`, {
      responseType: 'blob',
    });

    // Extract filename from Content-Disposition header or use a default.
    const disposition = response.headers['content-disposition'] as string | undefined;
    const match = disposition?.match(/filename="?([^"]+)"?/);
    const fileName = match?.[1] ?? `customer-export-${partyId}.json`;

    // Trigger browser download.
    const blob = new Blob([response.data as BlobPart], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  },

  /** Imports a customer data bundle from a JSON file. */
  importData: async (
    file: File,
    conflictMode: string = 'fail',
  ): Promise<CustomerDataImportResponse> => {
    const text = await file.text();
    const bundle = JSON.parse(text);
    return api.post<CustomerDataImportResponse>('/admin/customers/import', {
      bundle,
      conflictMode,
    });
  },
};

export interface CustomerInsightAiSummaryDetail {
  id: string;
  headline: string;
  summary: string;
  keyObservations: string[];
  positivePatterns: string[];
  riskPatterns: string[];
  recommendedFocusAreas: string[];
  conversationSuggestions: string[];
  caveats: string[];
  narrativeVersion: string;
  createdUtc: string;
}

export interface CustomerInsightSnapshotOverview {
  id: string;
  asOfUtc: string;
  isPartial: boolean;
  topSignalTitle: string | null;
  topSignalDescription: string | null;
  cashflowStressLevel: string | null;
  createdUtc: string;
}

export interface CustomerDataImportResponse {
  newPartyId: string;
  entityCounts: Record<string, number>;
  totalEntities: number;
  warnings: string[];
}

export interface CustomerInsightsResponse {
  aiSummary: CustomerInsightAiSummaryDetail | null;
  snapshot: CustomerInsightSnapshotOverview | null;
}

export interface CustomerActivityEntry {
  /** ISO-8601 UTC timestamp. */
  timestamp: string;
  /** Stable kind discriminator: order_created, order_updated, payment_captured, audit_log, document_uploaded. */
  kind: string;
  title: string;
  subtitle: string | null;
  /** Optional client-side route to drill into the underlying record. */
  linkPath: string | null;
}

export interface CustomerActivityResponse {
  items: CustomerActivityEntry[];
}
