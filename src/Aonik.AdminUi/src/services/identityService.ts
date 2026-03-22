import { api } from '@/lib/api';
import type { CurrentUserResponse, UserInfoResponse } from '@/types';

const USER_INFO_CACHE_TTL_MS = 30_000;

let userInfoCache: { data: UserInfoResponse; timestamp: number } | null = null;
let userInfoInFlight: Promise<UserInfoResponse> | null = null;

export const identityService = {
  getCurrentUser: async (): Promise<CurrentUserResponse> => {
    return api.get<CurrentUserResponse>('/v1/me');
  },

  getUserInfo: async (forceRefresh = false): Promise<UserInfoResponse> => {
    if (
      !forceRefresh
      && userInfoCache
      && Date.now() - userInfoCache.timestamp < USER_INFO_CACHE_TTL_MS
    ) {
      return userInfoCache.data;
    }

    if (userInfoInFlight) {
      return userInfoInFlight;
    }

    userInfoInFlight = api.get<UserInfoResponse>('/identity/userinfo')
      .then((data) => {
        userInfoCache = { data, timestamp: Date.now() };
        return data;
      })
      .finally(() => {
        userInfoInFlight = null;
      });

    return userInfoInFlight;
  },

  clearUserInfoCache: (): void => {
    userInfoCache = null;
    userInfoInFlight = null;
  },
};
