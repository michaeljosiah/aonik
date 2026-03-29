import { api } from '@/lib/api';
import type {
  NotificationTemplateSummary,
  NotificationTemplateResponse,
  CreateNotificationTemplateRequest,
  UpdateNotificationTemplateRequest,
  PreviewNotificationTemplateRequest,
  PreviewNotificationTemplateResponse,
  NotificationTemplateBindingResponse,
  CreateNotificationTemplateBindingRequest,
  UpdateNotificationTemplateBindingRequest,
} from '@/types';

export const notificationTemplateService = {
  list: async (channel?: string, isActive?: boolean): Promise<NotificationTemplateSummary[]> => {
    const params = new URLSearchParams();
    if (channel) params.set('channel', channel);
    if (isActive !== undefined) params.set('isActive', String(isActive));
    const qs = params.toString();
    return api.get<NotificationTemplateSummary[]>(`/admin/notification-templates${qs ? `?${qs}` : ''}`);
  },

  get: async (id: string): Promise<NotificationTemplateResponse> => {
    return api.get<NotificationTemplateResponse>(`/admin/notification-templates/${id}`);
  },

  create: async (request: CreateNotificationTemplateRequest): Promise<NotificationTemplateResponse> => {
    return api.post<NotificationTemplateResponse>('/admin/notification-templates', request);
  },

  update: async (id: string, request: UpdateNotificationTemplateRequest): Promise<NotificationTemplateResponse> => {
    return api.put<NotificationTemplateResponse>(`/admin/notification-templates/${id}`, request);
  },

  delete: async (id: string): Promise<void> => {
    await api.delete(`/admin/notification-templates/${id}`);
  },

  preview: async (request: PreviewNotificationTemplateRequest): Promise<PreviewNotificationTemplateResponse> => {
    return api.post<PreviewNotificationTemplateResponse>('/admin/notification-templates/preview', request);
  },

  listBindings: async (): Promise<NotificationTemplateBindingResponse[]> => {
    return api.get<NotificationTemplateBindingResponse[]>('/admin/notification-template-bindings');
  },

  createBinding: async (request: CreateNotificationTemplateBindingRequest): Promise<NotificationTemplateBindingResponse> => {
    return api.post<NotificationTemplateBindingResponse>('/admin/notification-template-bindings', request);
  },

  updateBinding: async (id: string, request: UpdateNotificationTemplateBindingRequest): Promise<NotificationTemplateBindingResponse> => {
    return api.put<NotificationTemplateBindingResponse>(`/admin/notification-template-bindings/${id}`, request);
  },

  deleteBinding: async (id: string): Promise<void> => {
    await api.delete(`/admin/notification-template-bindings/${id}`);
  },
};
