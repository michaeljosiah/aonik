import { useState, useEffect, useCallback } from 'react';
import { RefreshCw } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  PanelInfoPopover,
  type PanelCallout,
} from '@/components/ui/panel-info-popover';
import { MetricCard } from '@/components/charts/MetricCard';
import { MultiLineChart } from '@/components/charts/MultiLineChart';
import { TimeSeriesChart } from '@/components/charts/TimeSeriesChart';
import {
  observabilityService,
  type AiPerformanceResponse,
} from '@/services/observabilityService';
import type { WorkspacePanelRenderProps } from '../types';
import { useWorkspaceEvents } from '../useWorkspace';

function fmtMs(ms: number | undefined | null): string {
  if (ms == null) return '--';
  if (ms < 1000) return `${Math.round(ms)}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

function fmtTokens(n: number | undefined | null): string {
  if (n == null) return '--';
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(1)}K`;
  return String(n);
}

function formatPhaseLabel(phaseName: string): string {
  switch (phaseName) {
    case 'request_to_first_token':
      return 'Request → First Token';
    case 'user_brief':
      return 'User Brief';
    case 'history_load':
      return 'History Load';
    case 'run_started_sse':
      return 'RUN_STARTED SSE';
    case 'first_token_sse':
      return 'First Token SSE';
    default:
      return phaseName;
  }
}

export function AgentPerformancePanel({ panelId, title }: WorkspacePanelRenderProps) {
  const { onEvent } = useWorkspaceEvents(panelId);
  const [data, setData] = useState<AiPerformanceResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [selectedAgent, setSelectedAgent] = useState<string | null>(null);
  const [timeRange] = useState('24h');

  const load = useCallback(async () => {
    try {
      const result = await observabilityService.getAiPerformance(timeRange);
      setData(result);
    } catch {
      /* swallow */
    } finally {
      setLoading(false);
    }
  }, [timeRange]);

  useEffect(() => {
    void load();
    const interval = setInterval(() => void load(), 30_000);
    return () => clearInterval(interval);
  }, [load]);

  // Listen for agent selection from fleet panel
  useEffect(() => {
    const unsub = onEvent('agent:selected', (event) => {
      const name = (event.payload?.agentName as string) ?? null;
      setSelectedAgent(name);
    });
    return unsub;
  }, [onEvent]);

  const handleRefresh = async () => {
    setRefreshing(true);
    try {
      await load();
    } finally {
      setRefreshing(false);
    }
  };

  if (loading && !data) {
    return (
      <div className="h-full overflow-auto p-4">
        <p className="text-sm text-[var(--color-text-tertiary)] py-4 text-center">
          Loading performance data...
        </p>
      </div>
    );
  }

  const performanceDescription = (
    <>
      <p>How fast your agents respond to users.</p>
      <p>
        <strong>Latency percentiles</strong> — imagine lining up your last 100 requests from
        fastest to slowest:
      </p>
      <ul>
        <li>
          <strong>P50</strong> = the middle one. Half of users wait less than this, half wait
          more. The "typical" experience.
        </li>
        <li>
          <strong>P95</strong> = 95% of users had it this fast or faster; only the slowest 5%
          waited longer. Best single measure of worst-case experience.
        </li>
        <li>
          <strong>P99</strong> = catches the worst outliers — the 1% where something went wrong.
        </li>
      </ul>
      <p>
        We use percentiles instead of averages because one very slow request can drag an average
        up and hide the fact that most users are fine.
      </p>
      <p>
        <strong>TTFT</strong> (Time To First Token) — how long a user waits before anything
        appears on screen. Low TTFT feels snappy even if the full answer takes longer.
      </p>
      <p>
        <strong>Client vs server</strong> — time spent inside AONIK vs. waiting on the LLM
        provider. Helps you tell "our code is slow" from "the LLM is slow".
      </p>
    </>
  );

  if (!data?.configured) {
    return (
      <div className="h-full overflow-auto p-4">
        <div className="flex items-center gap-1.5">
          <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">{title}</h2>
          <PanelInfoPopover
            title="Performance Monitor"
            description={performanceDescription}
            callouts={[
              {
                level: 'info',
                message:
                  'Observability is not configured — connect Application Insights to populate this panel.',
              },
            ]}
            panelKind="performance"
            getMetrics={() => ({ configured: false })}
          />
        </div>
        <p className="text-sm text-[var(--color-text-tertiary)] py-4 text-center">
          Observability not configured.
        </p>
      </div>
    );
  }

  // Filter to selected agent if one is active
  const agentPerf = selectedAgent
    ? data.byAgent.find((a) => a.agentName === selectedAgent)
    : null;

  const latency = data.latency;
  const tokens = data.tokenUsage;
  const clientServer = data.clientServerComparison;
  const pfStreaming = data.personalFinanceStreaming;
  const showPfStreaming = !!pfStreaming && (!selectedAgent || selectedAgent === pfStreaming.agentName);
  const pfPhase = (name: string) =>
    pfStreaming?.phases.find((phase) => phase.phaseName === name) ?? null;
  const pfCache = (name: string) =>
    pfStreaming?.caches.find((cache) => cache.cacheName === name) ?? null;

  const callouts: PanelCallout[] = [];
  if (latency) {
    if (latency.p95Ms > 10_000) {
      callouts.push({
        level: 'critical',
        message: (
          <>
            P95 of <strong>{fmtMs(latency.p95Ms)}</strong> — the slowest 5% of users are waiting
            this long or more.
          </>
        ),
      });
    } else if (latency.p95Ms > 5_000) {
      callouts.push({
        level: 'warning',
        message: (
          <>
            P95 of <strong>{fmtMs(latency.p95Ms)}</strong> is on the slow side.
          </>
        ),
      });
    } else if (latency.p95Ms > 0) {
      callouts.push({
        level: 'good',
        message: (
          <>
            P95 of <strong>{fmtMs(latency.p95Ms)}</strong> is healthy.
          </>
        ),
      });
    }

    if (latency.p50Ms > 0 && latency.p95Ms / latency.p50Ms > 3) {
      callouts.push({
        level: 'warning',
        message: (
          <>
            P95 is <strong>{(latency.p95Ms / latency.p50Ms).toFixed(1)}×</strong> your P50 — tail
            latency is wide. A small number of outliers are much slower than typical.
          </>
        ),
      });
    } else if (latency.p50Ms > 0 && latency.p95Ms > 0) {
      callouts.push({
        level: 'good',
        message: 'Latency distribution is tight — users are getting a consistent experience.',
      });
    }
  }
  if (clientServer && clientServer.avgClientRoundTripMs > 0) {
    const overheadPct =
      (clientServer.avgNetworkOverheadMs / clientServer.avgClientRoundTripMs) * 100;
    if (clientServer.avgNetworkOverheadMs > 2_000) {
      callouts.push({
        level: 'warning',
        message: (
          <>
            Network overhead of <strong>{fmtMs(clientServer.avgNetworkOverheadMs)}</strong> is{' '}
            {overheadPct.toFixed(0)}% of total latency — check connectivity or payload size.
          </>
        ),
      });
    }
  }

  return (
    <div className="h-full overflow-auto p-4 space-y-3">
      {/* Header */}
      <div className="flex items-start justify-between gap-2">
        <div>
          <div className="flex items-center gap-1.5">
            <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">{title}</h2>
            <PanelInfoPopover
              title="Performance Monitor"
              description={performanceDescription}
              callouts={callouts}
              panelKind="performance"
              getMetrics={() => ({
                latency: latency
                  ? {
                      p50Ms: Math.round(latency.p50Ms),
                      p75Ms: Math.round(latency.p75Ms),
                      p90Ms: Math.round(latency.p90Ms),
                      p95Ms: Math.round(latency.p95Ms),
                      p99Ms: Math.round(latency.p99Ms),
                    }
                  : null,
                ttft: data.ttft
                  ? {
                      p50Ms: Math.round(data.ttft.p50Ms),
                      p95Ms: Math.round(data.ttft.p95Ms),
                    }
                  : null,
                tokenUsage: tokens
                  ? {
                      totalInputTokens: tokens.totalInputTokens,
                      totalOutputTokens: tokens.totalOutputTokens,
                      avgInputPerRun: Math.round(tokens.avgInputTokensPerRun),
                      avgOutputPerRun: Math.round(tokens.avgOutputTokensPerRun),
                    }
                  : null,
                clientServer: clientServer
                  ? {
                      avgClientRoundTripMs: Math.round(clientServer.avgClientRoundTripMs),
                      avgServerLatencyMs: Math.round(clientServer.avgServerLatencyMs),
                      avgNetworkOverheadMs: Math.round(clientServer.avgNetworkOverheadMs),
                    }
                  : null,
                agentCount: data.byAgent.length,
                selectedAgent,
              })}
            />
          </div>
          <p className="text-xs text-[var(--color-text-secondary)]">
            {selectedAgent
              ? `Filtered: ${selectedAgent}`
              : 'All agents — select one in Fleet to filter.'}
          </p>
        </div>
        <Button
          variant="secondary"
          size="sm"
          onClick={() => void handleRefresh()}
          disabled={refreshing}
        >
          <RefreshCw className={`w-3.5 h-3.5 ${refreshing ? 'animate-spin' : ''}`} />
        </Button>
      </div>

      {/* Selected agent stats (if filtered) */}
      {agentPerf && (
        <Card>
          <CardHeader className="pb-2 pt-3 px-4">
            <CardTitle className="text-sm font-medium">{agentPerf.agentName}</CardTitle>
          </CardHeader>
          <CardContent className="px-4 pb-3">
            <div className="grid grid-cols-3 gap-2 text-xs">
              <div>
                <p className="text-[var(--color-text-tertiary)]">Runs</p>
                <p className="font-semibold text-[var(--color-text-primary)]">{agentPerf.runs}</p>
              </div>
              <div>
                <p className="text-[var(--color-text-tertiary)]">Avg Latency</p>
                <p className="font-semibold text-[var(--color-text-primary)]">{fmtMs(agentPerf.avgLatencyMs)}</p>
              </div>
              <div>
                <p className="text-[var(--color-text-tertiary)]">P95 Latency</p>
                <p className="font-semibold text-[var(--color-text-primary)]">{fmtMs(agentPerf.p95LatencyMs)}</p>
              </div>
              <div>
                <p className="text-[var(--color-text-tertiary)]">Avg TTFT</p>
                <p className="font-semibold text-[var(--color-text-primary)]">{fmtMs(agentPerf.avgTtftMs)}</p>
              </div>
              <div>
                <p className="text-[var(--color-text-tertiary)]">Input Tokens</p>
                <p className="font-semibold text-[var(--color-text-primary)]">{fmtTokens(agentPerf.totalInputTokens)}</p>
              </div>
              <div>
                <p className="text-[var(--color-text-tertiary)]">Output Tokens</p>
                <p className="font-semibold text-[var(--color-text-primary)]">{fmtTokens(agentPerf.totalOutputTokens)}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Latency percentiles */}
      {latency && (
        <div className="grid grid-cols-5 gap-2">
          <MetricCard label="P50" value={fmtMs(latency.p50Ms)} status="good" />
          <MetricCard label="P75" value={fmtMs(latency.p75Ms)} />
          <MetricCard label="P90" value={fmtMs(latency.p90Ms)} />
          <MetricCard label="P95" value={fmtMs(latency.p95Ms)} status={latency.p95Ms > 10_000 ? 'warning' : undefined} />
          <MetricCard label="P99" value={fmtMs(latency.p99Ms)} status={latency.p99Ms > 15_000 ? 'critical' : undefined} />
        </div>
      )}

      {/* Client vs Server comparison */}
      {clientServer && (
        <div className="grid grid-cols-3 gap-2">
          <MetricCard
            label="Client Round-Trip"
            value={fmtMs(clientServer.avgClientRoundTripMs)}
          />
          <MetricCard
            label="Server Latency"
            value={fmtMs(clientServer.avgServerLatencyMs)}
          />
          <MetricCard
            label="Network Overhead"
            value={fmtMs(clientServer.avgNetworkOverheadMs)}
            status={clientServer.avgNetworkOverheadMs > 2_000 ? 'warning' : 'good'}
          />
        </div>
      )}

      {showPfStreaming && pfStreaming && (
        <Card>
          <CardHeader className="pb-2 pt-3 px-4">
            <CardTitle className="text-sm font-medium">Personal Finance Streaming</CardTitle>
          </CardHeader>
          <CardContent className="px-4 pb-3 space-y-3">
            <div className="grid grid-cols-2 md:grid-cols-3 gap-2">
              <MetricCard
                label="P50 First Token"
                value={pfPhase('request_to_first_token') ? fmtMs(pfPhase('request_to_first_token')!.p50Ms) : '--'}
              />
              <MetricCard
                label="P95 First Token"
                value={pfPhase('request_to_first_token') ? fmtMs(pfPhase('request_to_first_token')!.p95Ms) : '--'}
                status={pfPhase('request_to_first_token') && pfPhase('request_to_first_token')!.p95Ms > 5_000 ? 'warning' : 'good'}
              />
              <MetricCard
                label="P95 User Brief"
                value={pfPhase('user_brief') ? fmtMs(pfPhase('user_brief')!.p95Ms) : '--'}
              />
              <MetricCard
                label="P95 History Load"
                value={pfPhase('history_load') ? fmtMs(pfPhase('history_load')!.p95Ms) : '--'}
              />
              <MetricCard
                label="Brief Hit Rate"
                value={pfCache('user_brief') ? `${pfCache('user_brief')!.hitRatePercent.toFixed(1)}%` : '--'}
              />
              <MetricCard
                label="History Hit Rate"
                value={pfCache('history') ? `${pfCache('history')!.hitRatePercent.toFixed(1)}%` : '--'}
              />
            </div>

            {pfStreaming.phaseTimeSeries.some((series) => series.points.length > 0) && (
              <MultiLineChart
                series={pfStreaming.phaseTimeSeries
                  .filter((series) => series.points.length > 0)
                  .map((series, index) => ({
                    key: series.phaseName,
                    label: formatPhaseLabel(series.phaseName),
                    color: ['#8b5cf6', '#0ea5e9', '#f59e0b', '#10b981'][index % 4],
                    data: series.points,
                  }))}
                label="PF Streaming Phases"
                height={160}
                formatValue={(value) => fmtMs(value)}
              />
            )}

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
              <div className="space-y-1">
                <p className="text-[11px] font-medium uppercase tracking-wide text-[var(--color-text-tertiary)]">
                  Thread Modes
                </p>
                {pfStreaming.threadModes.map((mode) => (
                  <div
                    key={mode.mode}
                    className="grid grid-cols-4 gap-2 text-[11px] py-1 border-b border-[var(--color-border-light)] last:border-0"
                  >
                    <span className="font-medium text-[var(--color-text-primary)]">{mode.mode}</span>
                    <span>{mode.runs} runs</span>
                    <span>{fmtMs(mode.avgRequestToFirstTokenMs)}</span>
                    <span>{fmtMs(mode.p95RequestToFirstTokenMs)} P95</span>
                  </div>
                ))}
              </div>

              <div className="space-y-1">
                <p className="text-[11px] font-medium uppercase tracking-wide text-[var(--color-text-tertiary)]">
                  History Sources
                </p>
                {pfStreaming.historySources.map((source) => (
                  <div
                    key={source.mode}
                    className="grid grid-cols-4 gap-2 text-[11px] py-1 border-b border-[var(--color-border-light)] last:border-0"
                  >
                    <span className="font-medium text-[var(--color-text-primary)]">{source.mode}</span>
                    <span>{source.runs} runs</span>
                    <span>{fmtMs(source.avgRequestToFirstTokenMs)}</span>
                    <span>{fmtMs(source.p95RequestToFirstTokenMs)} P95</span>
                  </div>
                ))}
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Latency & TTFT time series */}
      {data.latencyTimeSeries.length > 0 && data.ttftTimeSeries.length > 0 && (
        <MultiLineChart
          series={[
            {
              key: 'latency',
              label: 'Latency',
              color: 'var(--color-brand-primary)',
              data: data.latencyTimeSeries,
            },
            {
              key: 'ttft',
              label: 'TTFT',
              color: '#10b981',
              data: data.ttftTimeSeries,
            },
          ]}
          label="Latency & TTFT Over Time"
          height={160}
          formatValue={(v) => fmtMs(v)}
        />
      )}

      {/* Token usage */}
      {tokens && (
        <div className="grid grid-cols-3 gap-2">
          <MetricCard label="Input Tokens" value={fmtTokens(tokens.totalInputTokens)} />
          <MetricCard label="Output Tokens" value={fmtTokens(tokens.totalOutputTokens)} />
          <MetricCard
            label="Avg per Run"
            value={fmtTokens(tokens.avgInputTokensPerRun + tokens.avgOutputTokensPerRun)}
            subtitle={`${fmtTokens(tokens.avgInputTokensPerRun)} in / ${fmtTokens(tokens.avgOutputTokensPerRun)} out`}
          />
        </div>
      )}

      {/* Token time series */}
      {data.tokenTimeSeries.length > 0 && (
        <TimeSeriesChart
          data={data.tokenTimeSeries}
          label="Token Usage Over Time"
          height={140}
          formatValue={(v) => fmtTokens(v)}
        />
      )}

      {/* Per-agent breakdown table */}
      {data.byAgent.length > 0 && !selectedAgent && (
        <Card>
          <CardHeader className="pb-2 pt-3 px-4">
            <CardTitle className="text-sm font-medium">Per-Agent Breakdown</CardTitle>
          </CardHeader>
          <CardContent className="px-4 pb-3">
            <div className="space-y-1">
              {data.byAgent
                .sort((a, b) => b.runs - a.runs)
                .map((agent) => (
                  <div
                    key={agent.agentName}
                    className="grid grid-cols-5 gap-1 text-[11px] text-[var(--color-text-secondary)] py-1 border-b border-[var(--color-border-light)] last:border-0"
                  >
                    <span className="font-medium text-[var(--color-text-primary)] truncate col-span-1">
                      {agent.agentName}
                    </span>
                    <span>{agent.runs} runs</span>
                    <span>{fmtMs(agent.avgLatencyMs)}</span>
                    <span>{fmtMs(agent.avgTtftMs)} TTFT</span>
                    <span>{fmtTokens(agent.totalInputTokens + agent.totalOutputTokens)} tok</span>
                  </div>
                ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
