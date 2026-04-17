import { useState, useEffect, useCallback } from 'react';
import { RefreshCw, DollarSign, TrendingUp } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  PanelInfoPopover,
  type PanelCallout,
} from '@/components/ui/panel-info-popover';
import { MetricCard } from '@/components/charts/MetricCard';
import { TimeSeriesChart } from '@/components/charts/TimeSeriesChart';
import {
  observabilityService,
  type AiPerformanceResponse,
  type AiMetricsResponse,
} from '@/services/observabilityService';
import type { WorkspacePanelRenderProps } from '../types';
import { useWorkspaceEvents } from '../useWorkspace';

function fmtTokens(n: number | undefined | null): string {
  if (n == null) return '--';
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(2)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(1)}K`;
  return String(n);
}

export function AgentCostPanel({ panelId, title }: WorkspacePanelRenderProps) {
  const { onEvent } = useWorkspaceEvents(panelId);
  const [perf, setPerf] = useState<AiPerformanceResponse | null>(null);
  const [ai, setAi] = useState<AiMetricsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [selectedAgent, setSelectedAgent] = useState<string | null>(null);
  const [timeRange] = useState('24h');

  const load = useCallback(async () => {
    try {
      const [perfRes, aiRes] = await Promise.all([
        observabilityService.getAiPerformance(timeRange),
        observabilityService.getAi(timeRange),
      ]);
      setPerf(perfRes);
      setAi(aiRes);
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

  useEffect(() => {
    const unsub = onEvent('agent:selected', (event) => {
      setSelectedAgent((event.payload?.agentName as string) ?? null);
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

  if (loading && !perf) {
    return (
      <div className="h-full overflow-auto p-4">
        <p className="text-sm text-[var(--color-text-tertiary)] py-4 text-center">
          Loading cost data...
        </p>
      </div>
    );
  }

  const tokens = perf?.tokenUsage;
  const agents = perf?.byAgent ?? [];
  const filteredAgents = selectedAgent
    ? agents.filter((a) => a.agentName === selectedAgent)
    : agents;

  // Total tokens across all agents
  const totalInput = filteredAgents.reduce((s, a) => s + a.totalInputTokens, 0);
  const totalOutput = filteredAgents.reduce((s, a) => s + a.totalOutputTokens, 0);
  const totalTokens = totalInput + totalOutput;

  const callouts: PanelCallout[] = [];
  if (totalTokens === 0) {
    callouts.push({ level: 'info', message: 'No token usage in this window.' });
  } else if (filteredAgents.length > 0) {
    const top = filteredAgents.reduce((a, b) =>
      a.totalInputTokens + a.totalOutputTokens > b.totalInputTokens + b.totalOutputTokens ? a : b,
    );
    const topTokens = top.totalInputTokens + top.totalOutputTokens;
    const topPct = totalTokens > 0 ? (topTokens / totalTokens) * 100 : 0;
    if (filteredAgents.length > 1 && topPct > 50) {
      callouts.push({
        level: 'info',
        message: (
          <>
            <strong>{top.agentName}</strong> uses {topPct.toFixed(0)}% of tokens — best candidate
            for prompt caching or a cheaper model.
          </>
        ),
      });
    } else if (filteredAgents.length > 1 && topPct < 30) {
      callouts.push({
        level: 'good',
        message: 'Token usage is spread evenly — no single agent is dominating spend.',
      });
    }

    const outputHeavy = filteredAgents.find(
      (a) => a.totalInputTokens > 0 && a.totalOutputTokens / a.totalInputTokens > 2,
    );
    if (outputHeavy) {
      callouts.push({
        level: 'info',
        message: (
          <>
            <strong>{outputHeavy.agentName}</strong> is output-heavy (
            {fmtTokens(outputHeavy.totalOutputTokens)} out vs{' '}
            {fmtTokens(outputHeavy.totalInputTokens)} in) — check whether responses can be
            tightened.
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
              title="Cost & Tokens"
              description={
                <>
                  <p>What your AI usage is costing you, broken down by agent.</p>
                  <ul>
                    <li>
                      <strong>Input tokens</strong> — prompt, context, and tool results sent to the
                      LLM. Usually the larger share.
                    </li>
                    <li>
                      <strong>Output tokens</strong> — what the LLM generates back. Typically more
                      expensive per-token than input.
                    </li>
                    <li>
                      <strong>Per-agent breakdown</strong> — who's consuming the most. Dominant
                      agents are good candidates for prompt caching, model downgrade, or context
                      trimming.
                    </li>
                  </ul>
                  <p>
                    We show raw tokens rather than dollars because model pricing varies per
                    provider — multiply by your current rate for exact cost.
                  </p>
                </>
              }
              callouts={callouts}
              panelKind="cost"
              getMetrics={() => ({
                totalTokens,
                totalInputTokens: totalInput,
                totalOutputTokens: totalOutput,
                totalCalls: ai?.totalCalls ?? null,
                agentCount: filteredAgents.length,
                selectedAgent,
                topAgents: [...filteredAgents]
                  .sort(
                    (a, b) =>
                      b.totalInputTokens +
                      b.totalOutputTokens -
                      (a.totalInputTokens + a.totalOutputTokens),
                  )
                  .slice(0, 5)
                  .map((a) => ({
                    name: a.agentName,
                    inputTokens: a.totalInputTokens,
                    outputTokens: a.totalOutputTokens,
                    runs: a.runs,
                  })),
              })}
            />
          </div>
          <p className="text-xs text-[var(--color-text-secondary)]">
            {selectedAgent ? `Filtered: ${selectedAgent}` : 'All agents'}
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

      {/* Token summary */}
      <div className="grid grid-cols-3 gap-2">
        <MetricCard
          label="Total Tokens"
          value={fmtTokens(totalTokens)}
          subtitle={`${fmtTokens(totalInput)} in / ${fmtTokens(totalOutput)} out`}
        />
        <MetricCard
          label="Avg per Run"
          value={fmtTokens(tokens ? tokens.avgInputTokensPerRun + tokens.avgOutputTokensPerRun : 0)}
        />
        <MetricCard
          label="Total Calls"
          value={ai?.totalCalls?.toLocaleString() ?? '--'}
        />
      </div>

      {/* Token time series */}
      {perf?.tokenTimeSeries && perf.tokenTimeSeries.length > 0 && (
        <TimeSeriesChart
          data={perf.tokenTimeSeries}
          label="Token Consumption Over Time"
          height={160}
          formatValue={(v) => fmtTokens(v)}
        />
      )}

      {/* Per-agent token breakdown */}
      {filteredAgents.length > 0 && (
        <Card>
          <CardHeader className="pb-2 pt-3 px-4">
            <CardTitle className="text-sm font-medium flex items-center gap-1.5">
              <DollarSign className="w-3.5 h-3.5" />
              Token Usage by Agent
            </CardTitle>
          </CardHeader>
          <CardContent className="px-4 pb-3">
            <div className="space-y-2">
              {filteredAgents
                .sort((a, b) => (b.totalInputTokens + b.totalOutputTokens) - (a.totalInputTokens + a.totalOutputTokens))
                .map((agent) => {
                  const agentTotal = agent.totalInputTokens + agent.totalOutputTokens;
                  const pct = totalTokens > 0 ? (agentTotal / totalTokens) * 100 : 0;
                  return (
                    <div key={agent.agentName} className="space-y-1">
                      <div className="flex items-center justify-between text-xs">
                        <span className="font-medium text-[var(--color-text-primary)] truncate">
                          {agent.agentName}
                        </span>
                        <span className="text-[var(--color-text-secondary)] flex items-center gap-1">
                          <TrendingUp className="w-2.5 h-2.5" />
                          {fmtTokens(agentTotal)} ({pct.toFixed(1)}%)
                        </span>
                      </div>
                      <div className="h-1.5 bg-zinc-100 dark:bg-zinc-800 rounded-full overflow-hidden">
                        <div
                          className="h-full bg-[var(--color-brand-primary)] rounded-full transition-all"
                          style={{ width: `${Math.min(pct, 100)}%` }}
                        />
                      </div>
                      <div className="flex justify-between text-[10px] text-[var(--color-text-tertiary)]">
                        <span>{fmtTokens(agent.totalInputTokens)} input</span>
                        <span>{fmtTokens(agent.totalOutputTokens)} output</span>
                        <span>{agent.runs} runs</span>
                      </div>
                    </div>
                  );
                })}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Cost note */}
      <Card className="border-dashed">
        <CardContent className="p-3">
          <p className="text-[11px] text-[var(--color-text-tertiary)]">
            Cost estimates will appear here once model cost profiles are configured.
            Token volumes are tracked in real time via the AG-UI streaming pipeline.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
