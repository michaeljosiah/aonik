import { api } from '@/lib/api';

/// Mirrors the SharedKernel TaskResponse (Spec 034), serialized camelCase.
export interface TaskItem {
  id: string;
  tenantId: string;
  title: string;
  description: string | null;
  kind: string;
  subjectType: string | null;
  subjectId: string | null;
  assigneeType: string;
  assigneeId: string | null;
  assigneeKey: string | null;
  actionType: string;
  scheduleType: string;
  nextRunAtUtc: string | null;
  recurrenceCron: string | null;
  timezone: string | null;
  startAtUtc: string | null;
  endAtUtc: string | null;
  runCount: number;
  maxRuns: number | null;
  status: string;
  priority: number;
  sourceModule: string;
  correlationId: string | null;
  lastError: string | null;
  createdAt: string;
  updatedAt: string | null;
}

/// Subset of the SharedKernel ScheduleTaskRequest the admin UI sends when creating a task.
export interface ScheduleTaskInput {
  title: string;
  kind: string;
  actionType: string;
  actionPayloadJson: string;
  assigneeType: string;
  assigneeId?: string | null;
  subjectType?: string | null;
  subjectId?: string | null;
  runAtUtc?: string | null;
  recurrenceCron?: string | null;
  timezone?: string | null;
  maxRuns?: number | null;
  description?: string | null;
  sourceModule?: string | null;
}

export const taskService = {
  create: (input: ScheduleTaskInput): Promise<TaskItem> => api.post<TaskItem>('/tasks', input),

  list: (status?: string, take = 100): Promise<TaskItem[]> => {
    const params = new URLSearchParams();
    if (status) params.set('status', status);
    params.set('take', String(take));
    return api.get<TaskItem[]>(`/tasks?${params.toString()}`);
  },

  cancel: (id: string): Promise<TaskItem> =>
    api.post<TaskItem>(`/tasks/${encodeURIComponent(id)}/cancel`),

  pause: (id: string): Promise<TaskItem> =>
    api.post<TaskItem>(`/tasks/${encodeURIComponent(id)}/pause`),

  resume: (id: string): Promise<TaskItem> =>
    api.post<TaskItem>(`/tasks/${encodeURIComponent(id)}/resume`),
};
