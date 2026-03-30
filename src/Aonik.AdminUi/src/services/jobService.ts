import { api } from '@/lib/api';

// ── Types ────────────────────────────────────────────────────────────

export interface ScheduledJobSummary {
  jobName: string;
  groupName: string;
  description: string | null;
  cronExpression: string | null;
  status: string;
  nextFireTimeUtc: string | null;
  previousFireTimeUtc: string | null;
}

export interface ScheduledJobListResponse {
  jobs: ScheduledJobSummary[];
}

export interface ScheduledJobActionResponse {
  jobName: string;
  action: string;
  success: boolean;
  message: string | null;
}

// ── Service ──────────────────────────────────────────────────────────

export const jobService = {
  listScheduledJobs: async (): Promise<ScheduledJobListResponse> => {
    return api.get<ScheduledJobListResponse>('/admin/jobs/scheduled');
  },

  triggerJob: async (jobName: string): Promise<ScheduledJobActionResponse> => {
    return api.post<ScheduledJobActionResponse>(`/admin/jobs/scheduled/${encodeURIComponent(jobName)}/trigger`);
  },

  pauseJob: async (jobName: string): Promise<ScheduledJobActionResponse> => {
    return api.post<ScheduledJobActionResponse>(`/admin/jobs/scheduled/${encodeURIComponent(jobName)}/pause`);
  },

  resumeJob: async (jobName: string): Promise<ScheduledJobActionResponse> => {
    return api.post<ScheduledJobActionResponse>(`/admin/jobs/scheduled/${encodeURIComponent(jobName)}/resume`);
  },
};
