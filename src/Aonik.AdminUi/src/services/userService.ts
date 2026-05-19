import { api } from '@/lib/api';
import type {
  AccessUserDetail,
  AccessUserSummary,
  DeleteUserRequest,
  DeleteUserResponse,
  InviteUserRequest,
  InviteUserResponse,
  PagedResult,
  ResendInviteResponse,
  RevokeUserSessionsRequest,
  RevokeUserSessionsResponse,
  UpdateUserRolesRequest,
  UpdateUserProfileRequest,
  UserDiagnosticResult,
  UserRepairResult,
  UserTombstoneSummary,
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
  invite: async (request: InviteUserRequest): Promise<InviteUserResponse> => {
    return api.post<InviteUserResponse>('/admin/users/invite', request);
  },
  // Spec 026 Part 1
  resendInvite: async (userId: string): Promise<ResendInviteResponse> => {
    return api.post<ResendInviteResponse>(`/admin/users/${userId}/resend-invite`);
  },
  updateRoles: async (userId: string, request: UpdateUserRolesRequest): Promise<void> => {
    return api.put(`/admin/users/${userId}/roles`, request);
  },
  updateProfile: async (userId: string, request: UpdateUserProfileRequest): Promise<void> => {
    return api.put(`/admin/users/${userId}/profile`, request);
  },
  uploadPhoto: async (userId: string, file: File): Promise<string> => {
    const formData = new FormData();
    formData.append('file', file);

    const response = await api.post<{ photoUrl: string }>(`/admin/users/${userId}/photo`, formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return response.photoUrl;
  },
  deletePhoto: async (userId: string): Promise<void> => {
    return api.delete(`/admin/users/${userId}/photo`);
  },
  deactivate: async (userId: string): Promise<void> => {
    return api.post(`/admin/users/${userId}/deactivate`);
  },
  activate: async (userId: string): Promise<void> => {
    return api.post(`/admin/users/${userId}/activate`);
  },
  // Spec 026 Part 3
  revokeSessions: async (
    userId: string,
    request: RevokeUserSessionsRequest = {},
  ): Promise<RevokeUserSessionsResponse> => {
    return api.post<RevokeUserSessionsResponse>(`/admin/users/${userId}/revoke-sessions`, request);
  },
  // Spec 026 Part 2 — destructive. The dialog should confirm the
  // operator typed the email back and supplied a reason ≥ 10 chars.
  delete: async (
    userId: string,
    request: DeleteUserRequest,
  ): Promise<DeleteUserResponse> => {
    return api.delete<DeleteUserResponse>(`/admin/users/${userId}`, { data: request });
  },
  listTombstones: async (params: ListUsersParams = {}): Promise<PagedResult<UserTombstoneSummary>> => {
    const queryParams = new URLSearchParams();
    if (params.pageNumber) queryParams.append('pageNumber', params.pageNumber.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());
    if (params.search) queryParams.append('search', params.search);
    const query = queryParams.toString();
    return api.get<PagedResult<UserTombstoneSummary>>(`/admin/users/tombstones${query ? `?${query}` : ''}`);
  },
  diagnose: async (userId: string): Promise<UserDiagnosticResult> => {
    return api.get<UserDiagnosticResult>(`/admin/users/${userId}/diagnose`);
  },
  repair: async (userId: string): Promise<UserRepairResult> => {
    return api.post<UserRepairResult>(`/admin/users/${userId}/repair`);
  },
};
