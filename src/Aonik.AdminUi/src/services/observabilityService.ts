import { api } from '@/lib/api';

// ── Shared ──────────────────────────────────────────────────────────

export interface TimeSeriesPoint {
  timestamp: string;
  value: number;
}

// ── Overview ────────────────────────────────────────────────────────

export interface RequestMetrics {
  total: number;
  ratePerMinute: number;
  timeSeries: TimeSeriesPoint[];
}

export interface ErrorMetrics {
  total: number;
  errorRatePercent: number;
  timeSeries: TimeSeriesPoint[];
  topErrors: ErrorGroup[];
}

export interface LatencyMetrics {
  p50Ms: number;
  p95Ms: number;
  p99Ms: number;
  timeSeries: TimeSeriesPoint[];
}

export interface ObservabilityOverviewResponse {
  configured: boolean;
  requests: RequestMetrics | null;
  errors: ErrorMetrics | null;
  latency: LatencyMetrics | null;
}

// ── Errors ──────────────────────────────────────────────────────────

export interface ErrorGroup {
  type: string;
  outerMessage: string;
  innermostMessage: string;
  count: number;
  lastSeen: string;
}

export interface ErrorsResponse {
  configured: boolean;
  errors: ErrorGroup[];
}

// ── Dependencies ────────────────────────────────────────────────────

export interface DependencyHealth {
  name: string;
  type: string;
  totalCalls: number;
  failedCalls: number;
  successRatePercent: number;
  avgDurationMs: number;
}

export interface DependencyMetricsResponse {
  configured: boolean;
  dependencies: DependencyHealth[];
}

// ── AI ──────────────────────────────────────────────────────────────

export interface AiAgentMetric {
  agentName: string;
  calls: number;
  avgDurationMs: number;
  totalTokens: number;
}

export interface AiMetricsResponse {
  configured: boolean;
  totalCalls: number;
  avgDurationMs: number;
  timeSeries: TimeSeriesPoint[];
  byAgent: AiAgentMetric[];
}

// ── AI Performance (detailed) ──────────────────────────────────────

export interface AiLatencyDistribution {
  p50Ms: number; p75Ms: number; p90Ms: number; p95Ms: number; p99Ms: number;
}

export interface AiTtftDistribution {
  p50Ms: number; p75Ms: number; p90Ms: number; p95Ms: number; p99Ms: number;
}

export interface AiTokenUsage {
  totalInputTokens: number; totalOutputTokens: number; totalTokens: number;
  avgInputTokensPerRun: number; avgOutputTokensPerRun: number;
}

export interface AiAgentPerformance {
  agentName: string; runs: number;
  avgLatencyMs: number; p95LatencyMs: number;
  avgTtftMs: number; p95TtftMs: number;
  totalInputTokens: number; totalOutputTokens: number;
}

export interface AiUseCasePerformance {
  useCase: string; calls: number;
  avgLatencyMs: number; p95LatencyMs: number;
  avgTtftMs: number; p95TtftMs: number;
  totalInputTokens: number; totalOutputTokens: number;
  estimatedCostUsd: number;
}

export interface AiClientServerComparison {
  avgClientRoundTripMs: number; avgServerLatencyMs: number;
  avgNetworkOverheadMs: number;
  avgClientTtftMs: number; avgServerTtftMs: number;
}

export interface AiPerformanceResponse {
  configured: boolean;
  latency: AiLatencyDistribution | null;
  ttft: AiTtftDistribution | null;
  tokenUsage: AiTokenUsage | null;
  byAgent: AiAgentPerformance[];
  clientServerComparison: AiClientServerComparison | null;
  latencyTimeSeries: TimeSeriesPoint[];
  ttftTimeSeries: TimeSeriesPoint[];
  tokenTimeSeries: TimeSeriesPoint[];
  byUseCase: AiUseCasePerformance[];
}

// ── Jobs ────────────────────────────────────────────────────────────

export interface JobExecutionMetric {
  jobName: string;
  total: number;
  successes: number;
  failures: number;
  avgDurationMs: number;
}

export interface JobMetricsResponse {
  configured: boolean;
  jobs: JobExecutionMetric[];
}

// ── Service ─────────────────────────────────────────────────────────

export const observabilityService = {
  getOverview: (timeRange = '24h') =>
    api.get<ObservabilityOverviewResponse>(
      `/admin/observability/overview?timeRange=${timeRange}`,
    ),
  getErrors: (timeRange = '24h') =>
    api.get<ErrorsResponse>(
      `/admin/observability/errors?timeRange=${timeRange}`,
    ),
  getDependencies: (timeRange = '24h') =>
    api.get<DependencyMetricsResponse>(
      `/admin/observability/dependencies?timeRange=${timeRange}`,
    ),
  getAi: (timeRange = '24h') =>
    api.get<AiMetricsResponse>(
      `/admin/observability/ai?timeRange=${timeRange}`,
    ),
  getAiPerformance: (timeRange = '24h') =>
    api.get<AiPerformanceResponse>(
      `/admin/observability/ai-performance?timeRange=${timeRange}`,
    ),
  getJobs: (timeRange = '24h') =>
    api.get<JobMetricsResponse>(
      `/admin/observability/jobs?timeRange=${timeRange}`,
    ),
};
