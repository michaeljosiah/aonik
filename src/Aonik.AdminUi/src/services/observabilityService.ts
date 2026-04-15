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

export interface AiModelPerformance {
  model: string; calls: number;
  avgLatencyMs: number; p95LatencyMs: number;
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
  byModel: AiModelPerformance[];
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

// ── Retrieval (Qdrant + embedding) ──────────────────────────────────

export interface RetrievalLatency {
  instrument: string;
  samples: number;
  avgMs: number;
  p50Ms: number;
  p95Ms: number;
  p99Ms: number;
}

export interface RetrievalCollectionStats {
  collection: string;
  searches: number;
  avgResultCount: number;
  emptySearches: number;
  avgLatencyMs: number;
  p95LatencyMs: number;
}

export interface RetrievalResponse {
  configured: boolean;
  latencies: RetrievalLatency[];
  collections: RetrievalCollectionStats[];
  embeddingErrorCount: number;
  totalSearches: number;
  totalUpserts: number;
  totalEmbeddingCalls: number;
  searchLatencyTimeSeries: TimeSeriesPoint[];
  embeddingLatencyTimeSeries: TimeSeriesPoint[];
}

// ── Topology ────────────────────────────────────────────────────────

export interface TopologyNode {
  id: string;
  label: string;
  kind: 'service' | 'external' | 'datastore';
  status: 'healthy' | 'degraded' | 'critical' | 'unknown';
  calls: number;
  errorRatePct: number;
  p95LatencyMs: number;
  lastSeen: string | null;
}

export interface TopologyEdge {
  source: string;
  target: string;
  kind: 'http' | 'sql' | 'grpc' | 'queue' | 'event';
  calls: number;
  errorRatePct: number;
  p95LatencyMs: number;
}

export interface TopologyResponse {
  configured: boolean;
  nodes: TopologyNode[];
  edges: TopologyEdge[];
  generatedAt: string;
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
  getRetrieval: (timeRange = '24h') =>
    api.get<RetrievalResponse>(
      `/admin/observability/retrieval?timeRange=${timeRange}`,
    ),
  getTopology: (timeRange = '24h') =>
    api.get<TopologyResponse>(
      `/admin/observability/topology?timeRange=${timeRange}`,
    ),
};
