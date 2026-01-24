import { api } from '@/lib/api';
import type {
  AccessRoleDetail,
  AccessRoleSummary,
  CreateRoleRequest,
  PagedResult,
  UpdateRoleRequest,
} from '@/types';

export interface ListRolesParams {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
}

export const roleService = {
  list: async (params: ListRolesParams = {}): Promise<PagedResult<AccessRoleSummary>> => {
    const queryParams = new URLSearchParams();
    if (params.pageNumber) queryParams.append('pageNumber', params.pageNumber.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params.search) queryParams.append('search', params.search);

    const query = queryParams.toString();
    return api.get<PagedResult<AccessRoleSummary>>(`/admin/roles${query ? `?${query}` : ''}`);
  },
  get: async (roleId: string): Promise<AccessRoleDetail> => {
    return api.get<AccessRoleDetail>(`/admin/roles/${roleId}`);
  },
  create: async (request: CreateRoleRequest): Promise<AccessRoleDetail> => {
    return api.post<AccessRoleDetail>('/admin/roles', request);
  },
  update: async (roleId: string, request: UpdateRoleRequest): Promise<AccessRoleDetail> => {
    return api.put<AccessRoleDetail>(`/admin/roles/${roleId}`, request);
  },
  delete: async (roleId: string): Promise<void> => {
    return api.delete(`/admin/roles/${roleId}`);
  },
  updatePermissions: async (roleId: string, permissionKeys: string[]): Promise<void> => {
    return api.put(`/admin/roles/${roleId}/permissions`, { permissionKeys });
  },
};
