import { api } from '@/lib/api';
import type { PagedResult } from '@/types';

// ── Types ────────────────────────────────────────────────────────────

export interface ScheduledJobSummary {
  jobName: string;
  groupName: string;
  description: string | null;
  cronExpression: string | null;
  status: string;
  nextFireTimeUtc: string | null;
  previousFireTimeUtc: string | null;
  displayName: string | null;
  lastOutcome: string | null;
  lastOutcomeSummary: string | null;
  lastDurationMs: number | null;
}

export interface ScheduledJobListResponse {
  jobs: ScheduledJobSummary[];
}

export interface ScheduledJobActionResponse {
  jobName: string;
  action: string;
  success: boolean;
  message: string | null;
  commandId: string | null;
  commandStatus: string | null;
}

export interface ScheduledJobDetailResponse {
  jobName: string;
  groupName: string;
  displayName: string;
  description: string;
  cronExpression: string;
  timeZoneId: string;
  state: string;
  nextFireTimeUtc: string | null;
  previousFireTimeUtc: string | null;
  lastOutcome: string | null;
  lastOutcomeSummary: string | null;
  lastDurationMs: number | null;
  lastSyncedAtUtc: string;
}

export interface ScheduledJobRunSummary {
  id: string;
  outcome: string;
  errorMessage: string | null;
  durationMs: number;
  triggeredBy: string;
  firedAtUtc: string;
  completedAtUtc: string;
  fireInstanceId: string | null;
}

export interface ScheduledJobCommandSummary {
  id: string;
  commandType: string;
  status: string;
  resultMessage: string | null;
  requestedByUserId: string | null;
  createdAt: string;
  processedAtUtc: string | null;
}

export interface SchedulerHealthResponse {
  schedulerName: string;
  schedulerInstanceId: string;
  isStarted: boolean;
  inStandbyMode: boolean;
  threadPoolSize: number;
  activeJobCount: number;
  totalJobCount: number;
  totalTriggerCount: number;
  recordedAtUtc: string;
}

// ── Service ──────────────────────────────────────────────────────────

export const jobService = {
  listScheduledJobs: async (): Promise<ScheduledJobListResponse> => {
    return api.get<ScheduledJobListResponse>('/admin/jobs/scheduled');
  },

  getJobDetail: async (jobName: string): Promise<ScheduledJobDetailResponse> => {
    return api.get<ScheduledJobDetailResponse>(`/admin/jobs/scheduled/${encodeURIComponent(jobName)}`);
  },

  listJobRuns: async (
    jobName: string,
    pageNumber = 1,
    pageSize = 20,
  ): Promise<PagedResult<ScheduledJobRunSummary>> => {
    const params = new URLSearchParams();
    params.append('pageNumber', pageNumber.toString());
    params.append('pageSize', pageSize.toString());
    return api.get<PagedResult<ScheduledJobRunSummary>>(
      `/admin/jobs/scheduled/${encodeURIComponent(jobName)}/runs?${params.toString()}`,
    );
  },

  listJobCommands: async (
    jobName: string,
    pageNumber = 1,
    pageSize = 20,
  ): Promise<PagedResult<ScheduledJobCommandSummary>> => {
    const params = new URLSearchParams();
    params.append('pageNumber', pageNumber.toString());
    params.append('pageSize', pageSize.toString());
    return api.get<PagedResult<ScheduledJobCommandSummary>>(
      `/admin/jobs/scheduled/${encodeURIComponent(jobName)}/commands?${params.toString()}`,
    );
  },

  getSchedulerHealth: async (): Promise<SchedulerHealthResponse | null> => {
    try {
      return await api.get<SchedulerHealthResponse>('/admin/scheduler/health');
    } catch {
      return null;
    }
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
