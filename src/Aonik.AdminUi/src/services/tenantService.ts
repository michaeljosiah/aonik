import { api } from '@/lib/api';
import type {
  Tenant,
  CreateTenantRequest,
  UpdateTenantRequest,
  PagedResult,
  TenantListForLoginResponse,
  MyTenantsResponse,
} from '@/types';

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
  /**
   * List tenants for login dropdown (public, no auth required).
   * @deprecated Public enumeration leaks the tenant directory. Use
   *   {@link tenantService.listMyTenants} after authentication.
   */
  listForLogin: async (): Promise<TenantListForLoginResponse> => {
    return api.get<TenantListForLoginResponse>('/host/tenants/list-for-login');
  },

  // Tenants the currently-authenticated identity belongs to. Drives the
  // post-auth org picker (or auto-select when there is exactly one). Server
  // requires a valid JWT but no tenant context — this *is* the call that
  // resolves tenant context.
  listMyTenants: async (): Promise<MyTenantsResponse> => {
    return api.get<MyTenantsResponse>('/host/me/tenants');
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

  // Get current tenant settings
  getSettings: async (): Promise<Tenant> => {
    return api.get<Tenant>('/tenant/settings');
  },

  // Create a new tenant
  create: async (request: CreateTenantRequest): Promise<Tenant> => {
    return api.post<Tenant>('/admin/tenants', request);
  },

  // Update a tenant
  update: async (tenantId: string, request: UpdateTenantRequest): Promise<Tenant> => {
    return api.patch<Tenant>(`/admin/tenants/${tenantId}`, request);
  },

  // Update current tenant settings
  updateSettings: async (request: UpdateTenantRequest): Promise<Tenant> => {
    return api.patch<Tenant>('/tenant/settings', request);
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
