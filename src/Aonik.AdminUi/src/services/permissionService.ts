import { api } from '@/lib/api';
import type { PermissionDefinition } from '@/types';

export const permissionService = {
  list: async (): Promise<PermissionDefinition[]> => {
    return api.get<PermissionDefinition[]>('/admin/permissions');
  },
};
