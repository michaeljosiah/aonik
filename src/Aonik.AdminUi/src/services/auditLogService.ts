import { api } from '@/lib/api';
import type { PagedResult } from '@/types';

export interface AuditLogListItem {
  id: string;
  tenantId: string;
  timestamp: string;
  actorType: string;
  actorId: string;
  action: string;
  resourceType: string;
  resourceId: string;
  detailsJson: string;
  correlationId: string;
}

export interface AuditLogListParams {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  action?: string;
  resourceType?: string;
  resourceId?: string;
  correlationId?: string;
}

export const auditLogService = {
  list: async (params: AuditLogListParams = {}): Promise<PagedResult<AuditLogListItem>> => {
    const query = new URLSearchParams();

    if (params.pageNumber !== undefined) query.set('pageNumber', String(params.pageNumber));
    if (params.pageSize !== undefined) query.set('pageSize', String(params.pageSize));
    if (params.search) query.set('search', params.search);
    if (params.action) query.set('action', params.action);
    if (params.resourceType) query.set('resourceType', params.resourceType);
    if (params.resourceId) query.set('resourceId', params.resourceId);
    if (params.correlationId) query.set('correlationId', params.correlationId);

    const queryString = query.toString();
    return api.get<PagedResult<AuditLogListItem>>(`/admin/audit-logs${queryString ? `?${queryString}` : ''}`);
  },
};
