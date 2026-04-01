import { api } from '@/lib/api';

export interface AlertSummary {
  id: string;
  alertRuleName: string;
  monitorCondition: string;
  severity: string;
  signalType: string;
  normalizedType: string;
  status: string;
  analysisSummary: string;
  receivedAtUtc: string;
  firedAtUtc: string | null;
  resolvedAtUtc: string | null;
  resourceIds: string[];
}

export interface AlertListResponse {
  alerts: AlertSummary[];
}

export interface AlertAnalysis {
  summary: string;
  likelyCause: string;
  impact: string;
  affectedComponent: string;
  recommendedActions: string[];
  confidence: string;
}

export interface AlertDetail {
  id: string;
  provider: string;
  externalAlertId: string;
  alertRuleName: string;
  alertRuleId: string;
  monitorCondition: string;
  severity: string;
  signalType: string;
  monitoringService: string;
  normalizedType: string;
  status: string;
  correlationKey: string;
  receivedAtUtc: string;
  firedAtUtc: string | null;
  resolvedAtUtc: string | null;
  processedAtUtc: string | null;
  aiRunId: string | null;
  description: string;
  investigationLink: string;
  resourceIds: string[];
  customProperties: Record<string, string>;
  analysis: AlertAnalysis | null;
  essentialsJson: string;
  alertContextJson: string;
}

export const alertService = {
  list: async (take = 50): Promise<AlertListResponse> => {
    return api.get<AlertListResponse>(`/admin/alerts?take=${take}`);
  },

  get: async (id: string): Promise<AlertDetail> => {
    return api.get<AlertDetail>(`/admin/alerts/${encodeURIComponent(id)}`);
  },
};
