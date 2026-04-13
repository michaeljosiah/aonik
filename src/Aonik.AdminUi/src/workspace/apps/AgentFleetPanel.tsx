import { useState, useEffect, useCallback } from 'react';
import { Bot, RefreshCw, Activity, AlertTriangle, Zap } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { MetricCard } from '@/components/charts/MetricCard';
import { TimeSeriesChart } from '@/components/charts/TimeSeriesChart';
import {
  observabilityService,
  type AiMetricsResponse,
  type AiAgentMetric,
} from '@/services/observabilityService';
import type { WorkspacePanelRenderProps } from '../types';
import { useWorkspaceEvents } from '../useWorkspace';

function formatDuration(ms: number): string {
  if (ms < 1000) return `${Math.round(ms)}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

function formatTokens(n: number): string {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(1)}K`;
  return String(n);
}

export function AgentFleetPanel({ panelId, title }: WorkspacePanelRenderProps) {
  const { emit } = useWorkspaceEvents(panelId);
  const [data, setData] = useState<AiMetricsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [selectedAgent, setSelectedAgent] = useState<string | null>(null);
  const [timeRange] = useState('24h');

  const load = useCallback(async () => {
    try {
      const result = await observabilityService.getAi(timeRange);
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

  const handleRefresh = async () => {
    setRefreshing(true);
    try {
      await load();
    } finally {
      setRefreshing(false);
    }
  };

  const selectAgent = useCallback(
    (agent: AiAgentMetric) => {
      setSelectedAgent(agent.agentName);
      emit({
        type: 'agent:selected',
        payload: {
          agentName: agent.agentName,
          calls: agent.calls,
          avgDurationMs: agent.avgDurationMs,
          totalTokens: agent.totalTokens,
        },
      });
    },
    [emit],
  );

  if (loading && !data) {
    return (
      <div className="h-full overflow-auto p-4">
        <p className="text-sm text-[var(--color-text-tertiary)] py-4 text-center">
          Loading agent fleet...
        </p>
      </div>
    );
  }

  if (!data?.configured) {
    return (
      <div className="h-full overflow-auto p-4">
        <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">{title}</h2>
        <p className="text-sm text-[var(--color-text-tertiary)] py-4 text-center">
          Observability not configured. Connect Application Insights to see agent metrics.
        </p>
      </div>
    );
  }

  const agents = data.byAgent ?? [];
  const totalTokens = agents.reduce((sum, a) => sum + a.totalTokens, 0);

  return (
    <div className="h-full overflow-auto p-4 space-y-3">
      {/* Header */}
      <div className="flex items-start justify-between gap-2">
        <div>
          <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">{title}</h2>
          <p className="text-xs text-[var(--color-text-secondary)]">
            Select an agent to filter other panels.
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

      {/* Summary metrics */}
      <div className="grid grid-cols-3 gap-2">
        <MetricCard
          label="Total Calls"
          value={data.totalCalls.toLocaleString()}
          subtitle={`${agents.length} agent${agents.length !== 1 ? 's' : ''}`}
        />
        <MetricCard
          label="Avg Latency"
          value={formatDuration(data.avgDurationMs)}
          status={data.avgDurationMs > 10_000 ? 'critical' : data.avgDurationMs > 5_000 ? 'warning' : 'good'}
        />
        <MetricCard
          label="Total Tokens"
          value={formatTokens(totalTokens)}
        />
      </div>

      {/* Activity chart */}
      {data.timeSeries.length > 0 && (
        <TimeSeriesChart
          data={data.timeSeries}
          label="Call Volume"
          height={140}
        />
      )}

      {/* Agent list */}
      {agents.length === 0 ? (
        <p className="text-sm text-[var(--color-text-tertiary)] py-4 text-center">
          No agent activity in this period.
        </p>
      ) : (
        <div className="space-y-2">
          {agents
            .sort((a, b) => b.calls - a.calls)
            .map((agent) => {
              const isSelected = selectedAgent === agent.agentName;
              return (
                <button
                  key={agent.agentName}
                  type="button"
                  onClick={() => selectAgent(agent)}
                  className={`w-full text-left rounded-md border px-3 py-2.5 transition-all
                    border-[var(--color-border-light)] bg-[var(--color-surface)]
                    ${isSelected ? 'ring-2 ring-[var(--color-brand-primary)] ring-offset-1' : 'hover:shadow-sm'}`}
                >
                  <div className="flex items-center gap-2 mb-1.5">
                    <Bot className="w-3.5 h-3.5 text-[var(--color-brand-primary)]" />
                    <span className="text-sm font-semibold text-[var(--color-text-primary)] truncate">
                      {agent.agentName}
                    </span>
                    <Badge className="bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400 text-[10px] px-1.5 py-0 ml-auto">
                      {agent.calls} calls
                    </Badge>
                  </div>
                  <div className="grid grid-cols-3 gap-x-3 text-[11px] text-[var(--color-text-secondary)]">
                    <span className="flex items-center gap-0.5">
                      <Zap className="w-2.5 h-2.5" />
                      {formatDuration(agent.avgDurationMs)}
                    </span>
                    <span className="flex items-center gap-0.5">
                      <Activity className="w-2.5 h-2.5" />
                      {formatTokens(agent.totalTokens)} tokens
                    </span>
                    <span className="flex items-center gap-0.5">
                      {agent.avgDurationMs > 10_000 && (
                        <AlertTriangle className="w-2.5 h-2.5 text-amber-500" />
                      )}
                      {agent.avgDurationMs > 10_000 ? 'Slow' : 'Healthy'}
                    </span>
                  </div>
                </button>
              );
            })}
        </div>
      )}
    </div>
  );
}
