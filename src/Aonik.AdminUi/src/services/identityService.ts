import { api } from '@/lib/api';
import type { CurrentUserResponse, UserInfoResponse } from '@/types';

export const identityService = {
  getCurrentUser: async (): Promise<CurrentUserResponse> => {
    return api.get<CurrentUserResponse>('/v1/me');
  },
  getUserInfo: async (): Promise<UserInfoResponse> => {
    return api.get<UserInfoResponse>('/identity/userinfo');
  },
};
