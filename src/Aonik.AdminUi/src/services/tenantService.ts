import { api } from '@/lib/api';
import type { Tenant, CreateTenantRequest, UpdateTenantRequest, PagedResult, TenantListForLoginResponse } from '@/types';

export interface ListTenantsParams {
  pageNumber?: number;
  pageSize?: number;
  environment?: string;
  status?: string;
  nameFilter?: string;
}

export interface TenantHealthResult {
  tenantId: string;
  isHealthy: boolean;
  checks: {
    name: string;
    status: 'Passed' | 'Failed';
    message?: string;
  }[];
}

export const tenantService = {
  // List tenants for login dropdown (public, no auth required)
  listForLogin: async (): Promise<TenantListForLoginResponse> => {
    return api.get<TenantListForLoginResponse>('/host/tenants/list-for-login');
  },

  // List all tenants (admin endpoint)
  list: async (params: ListTenantsParams = {}): Promise<PagedResult<Tenant>> => {
    const queryParams = new URLSearchParams();
    if (params.pageNumber) queryParams.append('pageNumber', params.pageNumber.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params.environment) queryParams.append('environment', params.environment);
    if (params.status) queryParams.append('status', params.status);
    if (params.nameFilter) queryParams.append('nameFilter', params.nameFilter);
    
    const query = queryParams.toString();
    return api.get<PagedResult<Tenant>>(`/admin/tenants${query ? `?${query}` : ''}`);
  },

  // Get a single tenant by ID
  get: async (tenantId: string): Promise<Tenant> => {
    return api.get<Tenant>(`/admin/tenants/${tenantId}`);
  },

  // Create a new tenant
  create: async (request: CreateTenantRequest): Promise<Tenant> => {
    return api.post<Tenant>('/admin/tenants', request);
  },

  // Update a tenant
  update: async (tenantId: string, request: UpdateTenantRequest): Promise<Tenant> => {
    return api.put<Tenant>(`/admin/tenants/${tenantId}`, request);
  },

  // Activate a tenant
  activate: async (tenantId: string): Promise<void> => {
    return api.post(`/admin/tenants/${tenantId}/activate`);
  },

  // Deactivate a tenant
  deactivate: async (tenantId: string): Promise<void> => {
    return api.post(`/admin/tenants/${tenantId}/deactivate`);
  },

  // Provision a tenant (create default resources)
  provision: async (tenantId: string): Promise<void> => {
    return api.post(`/admin/tenants/${tenantId}/provision`);
  },

  // Get tenant health
  getHealth: async (tenantId: string): Promise<TenantHealthResult> => {
    return api.get<TenantHealthResult>(`/admin/tenants/${tenantId}/health`);
  },
};
