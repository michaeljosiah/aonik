import { apiConfig } from '@/auth';
import { api } from '@/lib/api';
import { getSelectedTenant } from '@/lib/tenantContext';

export interface AdminNotification {
  id: string;
  tenantId: string;
  userId: string;
  channel: string;
  type: string;
  source: string;
  title: string;
  body: string;
  severity: string;
  status: string;
  actionUrl: string | null;
  correlationId: string | null;
  aiRunId: string | null;
  metadataJson: string;
  createdAt: string;
  readAt: string | null;
  dismissedAt: string | null;
}

export interface NotificationSummaryResponse {
  unreadCount: number;
}

export interface NotificationBulkActionResponse {
  affectedCount: number;
}

export interface NotificationListParams {
  status?: string;
  take?: number;
  before?: string;
  includeDismissed?: boolean;
}

export interface NotificationStreamEvent {
  type: 'HELLO' | 'NOTIFICATION_CREATED' | 'NOTIFICATION_UPDATED' | 'HEARTBEAT';
  unreadCount?: number;
  unreadCountDelta?: number;
  notification?: AdminNotification;
  serverTimeUtc?: string;
}

interface NotificationStreamOptions {
  getAccessToken: () => Promise<string | null>;
  signal?: AbortSignal;
  onEvent: (event: NotificationStreamEvent) => void;
}

export const notificationService = {
  list: async (params: NotificationListParams = {}): Promise<AdminNotification[]> => {
    const query = new URLSearchParams();

    if (params.status) query.set('status', params.status);
    if (params.take !== undefined) query.set('take', String(params.take));
    if (params.before) query.set('before', params.before);
    if (params.includeDismissed !== undefined) query.set('includeDismissed', String(params.includeDismissed));

    const queryString = query.toString();
    return api.get<AdminNotification[]>(`/admin/notifications${queryString ? `?${queryString}` : ''}`);
  },

  getSummary: async (): Promise<NotificationSummaryResponse> => {
    return api.get<NotificationSummaryResponse>('/admin/notifications/summary');
  },

  markRead: async (id: string): Promise<AdminNotification> => {
    return api.post<AdminNotification>(`/admin/notifications/${id}/read`);
  },

  dismiss: async (id: string): Promise<AdminNotification> => {
    return api.post<AdminNotification>(`/admin/notifications/${id}/dismiss`);
  },

  markAllRead: async (): Promise<NotificationBulkActionResponse> => {
    return api.post<NotificationBulkActionResponse>('/admin/notifications/read-all');
  },

  subscribeToStream: async ({ getAccessToken, signal, onEvent }: NotificationStreamOptions): Promise<void> => {
    const token = await getAccessToken();
    const selectedTenant = getSelectedTenant();

    if (!selectedTenant?.tenantId) {
      throw new Error('Tenant context missing for notification stream.');
    }

    const headers: Record<string, string> = {
      Accept: 'text/event-stream',
      'X-Tenant-Id': selectedTenant.tenantId,
    };

    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    const response = await fetch(`${apiConfig.baseUrl}/admin/notifications/stream`, {
      method: 'GET',
      headers,
      signal,
    });

    if (!response.ok) {
      const errorText = await response.text().catch(() => 'Unknown error');
      throw new Error(`Notification stream failed: ${response.status} ${errorText}`);
    }

    if (!response.body) {
      throw new Error('No response body returned for notification stream.');
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        buffer = buffer.replace(/\r\n/g, '\n');

        let separatorIndex = buffer.indexOf('\n\n');
        while (separatorIndex >= 0) {
          const rawEvent = buffer.slice(0, separatorIndex);
          buffer = buffer.slice(separatorIndex + 2);

          const dataLines = rawEvent
            .split('\n')
            .filter((line) => line.startsWith('data: '))
            .map((line) => line.slice(6));

          if (dataLines.length > 0) {
            onEvent(JSON.parse(dataLines.join('\n')) as NotificationStreamEvent);
          }

          separatorIndex = buffer.indexOf('\n\n');
        }
      }
    } finally {
      reader.releaseLock();
    }
  },
};
