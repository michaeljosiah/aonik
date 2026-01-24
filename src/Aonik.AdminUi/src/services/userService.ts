import { api } from '@/lib/api';
import type {
  AccessUserDetail,
  AccessUserSummary,
  InviteUserRequest,
  PagedResult,
  UpdateUserRolesRequest,
} from '@/types';

export interface ListUsersParams {
  pageNumber?: number;
  pageSize?: number;
  status?: string;
  search?: string;
}

export const userService = {
  list: async (params: ListUsersParams = {}): Promise<PagedResult<AccessUserSummary>> => {
    const queryParams = new URLSearchParams();
    if (params.pageNumber) queryParams.append('pageNumber', params.pageNumber.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params.status) queryParams.append('status', params.status);
    if (params.search) queryParams.append('search', params.search);

    const query = queryParams.toString();
    return api.get<PagedResult<AccessUserSummary>>(`/admin/users${query ? `?${query}` : ''}`);
  },
  get: async (userId: string): Promise<AccessUserDetail> => {
    return api.get<AccessUserDetail>(`/admin/users/${userId}`);
  },
  invite: async (request: InviteUserRequest): Promise<void> => {
    return api.post('/admin/users/invite', request);
  },
  updateRoles: async (userId: string, request: UpdateUserRolesRequest): Promise<void> => {
    return api.put(`/admin/users/${userId}/roles`, request);
  },
  deactivate: async (userId: string): Promise<void> => {
    return api.post(`/admin/users/${userId}/deactivate`);
  },
  activate: async (userId: string): Promise<void> => {
    return api.post(`/admin/users/${userId}/activate`);
  },
};
