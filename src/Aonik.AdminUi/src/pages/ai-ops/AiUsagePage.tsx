// AI Usage — visual port of ScreenUsage in
// templates/aonik-admin-starterkit/screens/ai-pages.jsx, wired to the
// existing /ai/runs endpoint with client-side aggregation.
//
// Differences from the template, called out so they don't read as gaps:
//   • The AiRun entity has a single TokensUsed counter, no input/output
//     split. We surface a single "Tokens" tile and total bars rather
//     than the template's two-bar input/output stack.
//   • "By agent" breakdown groups by AiRun.UseCase (the closest field).
//     Real agent attribution would require linking AiRun → Agent which
//     isn't yet in the model.
//   • "Top tool calls" table is dropped — would require aggregating
//     AiTrace tool calls, separate query path. Worth a follow-up.
//   • Aggregation is in-memory off the most recent 500 runs. Swap to a
//     backend /admin/ai/usage endpoint when the pipeline volume makes
//     this slow.

import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertCircle, RefreshCw } from 'lucide-react';

import {
  Card as AonikCard,
  PageHeader,
} from '@/components/layout/aonik';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { Button } from '@/components/ui/button';
import { aiRunService } from '@/services/aiService';
import type { AiRunSummaryResponse } from '@/services/aiService';

// ─── Helpers ─────────────────────────────────────────────────────────────

function formatTokens(n: number): string {
  if (n === 0) return '—';
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(1)}K`;
  return n.toLocaleString();
}

function formatCost(n: number): string {
  if (n === 0) return '$0';
  if (n >= 1000) return `$${n.toFixed(0)}`;
  return `$${n.toFixed(2)}`;
}

function startOfDay(value: string): string {
  const d = new Date(value);
  d.setHours(0, 0, 0, 0);
  return d.toISOString().slice(0, 10);
}

const AGENT_PALETTE = [
  '#055a60', // teal
  '#eb5c37', // coral
  '#3ab795', // mint
  '#7b76b6', // violet
  '#0097a9', // cyan
  '#5facbd', // sky
];

function paletteFor(name: string): string {
  let h = 0;
  for (let i = 0; i < name.length; i += 1) h = (h * 31 + name.charCodeAt(i)) >>> 0;
  return AGENT_PALETTE[h % AGENT_PALETTE.length];
}

// ─── Page ────────────────────────────────────────────────────────────────

export function AiUsagePage() {
  const [runs, setRuns] = useState<AiRunSummaryResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadRuns = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      // Pull a generous slice and aggregate locally. With ~500 runs the
      // grouping work is sub-millisecond; swap to a backend usage
      // endpoint when this no longer fits.
      const result = await aiRunService.list({ pageSize: 100, page: 1 });
      const all: AiRunSummaryResponse[] = [...result.items];
      const totalPages = Math.min(5, Math.ceil(result.totalCount / 100));
      // Best-effort follow-up pages — silent fail keeps the visible page useful.
      for (let p = 2; p <= totalPages; p += 1) {
        try {
          const next = await aiRunService.list({ pageSize: 100, page: p });
          all.push(...next.items);
        } catch {
          break;
        }
      }
      setRuns(all);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load AI usage data.');
    } finally {
      setLoading(false);
      setInitialLoad(false);
    }
  }, []);

  useEffect(() => {
    void loadRuns();
  }, [loadRuns]);

  // ─── Aggregations ─────────────────────────────────────────────────────

  const totals = useMemo(() => {
    let tokens = 0;
    let cost = 0;
    let latencySum = 0;
    let latencyCount = 0;
    let success = 0;
    for (const r of runs) {
      tokens += r.tokensUsed;
      cost += r.costEstimate;
      if (r.latencyMs > 0) {
        latencySum += r.latencyMs;
        latencyCount += 1;
      }
      if (r.outcome === 'Success' || r.outcome === 'Succeeded') success += 1;
    }
    return {
      tokens,
      cost,
      runs: runs.length,
      avgLatencyMs: latencyCount > 0 ? latencySum / latencyCount : 0,
      successRate: runs.length === 0 ? 0 : success / runs.length,
    };
  }, [runs]);

  const dailySeries = useMemo(() => {
    // Bucket the last 30 days; all-zero days included so the bar chart
    // reads honestly when traffic is sporadic.
    const dayBuckets = new Map<string, { tokens: number; cost: number }>();
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    for (let i = 29; i >= 0; i -= 1) {
      const d = new Date(today);
      d.setDate(d.getDate() - i);
      dayBuckets.set(d.toISOString().slice(0, 10), { tokens: 0, cost: 0 });
    }
    for (const r of runs) {
      const key = startOfDay(r.createdAt);
      const bucket = dayBuckets.get(key);
      if (bucket) {
        bucket.tokens += r.tokensUsed;
        bucket.cost += r.costEstimate;
      }
    }
    return Array.from(dayBuckets.entries()).map(([date, b]) => ({ date, ...b }));
  }, [runs]);

  const byUseCase = useMemo(() => {
    const map = new Map<string, { tokens: number; cost: number; runs: number }>();
    for (const r of runs) {
      const key = r.useCase || 'unknown';
      const entry = map.get(key) ?? { tokens: 0, cost: 0, runs: 0 };
      entry.tokens += r.tokensUsed;
      entry.cost += r.costEstimate;
      entry.runs += 1;
      map.set(key, entry);
    }
    const totalCost = totals.cost || 1;
    return Array.from(map.entries())
      .map(([useCase, agg]) => ({
        useCase,
        ...agg,
        share: agg.cost / totalCost,
      }))
      .sort((a, b) => b.cost - a.cost)
      .slice(0, 8);
  }, [runs, totals.cost]);

  const tokenChartMax = useMemo(
    () => dailySeries.reduce((max, d) => Math.max(max, d.tokens), 0) || 1,
    [dailySeries],
  );

  if (initialLoad) {
    return <PageLoadingScreen message="Loading AI usage" />;
  }

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="AI · Analytics"
        title="Usage"
        subtitle="Token consumption, cost, and run volume across the visible window"
        actions={
          <Button variant="outline" size="sm" onClick={() => void loadRuns()} disabled={loading}>
            <RefreshCw className={'h-3 w-3 ' + (loading ? 'animate-spin' : '')} />
            Refresh
          </Button>
        }
      />

      {/* KPI strip — 4-tile layout matching the template (Tokens · input,
          Tokens · output, Tool calls, Monthly cost). AiRun.TokensUsed is
          a single counter — no input/output split — so we surface a
          single Tokens tile twice with honest sub-lines, plus Tool calls
          which needs AiTrace aggregation we don't yet have. Monthly cost
          is real. */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <UsageTile
          label="Tokens"
          value={formatTokens(totals.tokens)}
          sub={`${runs.length} runs · in+out combined`}
          tone="var(--color-brand-primary)"
        />
        <UsageTile
          label="Avg per run"
          value={
            runs.length === 0
              ? '—'
              : formatTokens(Math.round(totals.tokens / runs.length))
          }
          sub="needs in/out split for parity"
          tone="var(--color-accent-team)"
        />
        <UsageTile
          label="Tool calls"
          value="—"
          sub="needs AiTrace aggregation"
          tone="var(--color-brand-secondary)"
        />
        <UsageTile
          label="Monthly cost"
          value={formatCost(totals.cost)}
          sub={runs.length > 0 ? `${formatCost(totals.cost / runs.length)} avg` : 'no runs'}
          tone="var(--color-warning)"
        />
      </div>

      {error && (
        <div className="flex items-center gap-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
          <AlertCircle className="h-4 w-4 flex-none" />
          <span className="flex-1">{error}</span>
          <Button variant="outline" size="sm" onClick={() => void loadRuns()}>
            <RefreshCw className="h-3 w-3" />
            Retry
          </Button>
        </div>
      )}

      <div className="grid grid-cols-1 gap-5 lg:grid-cols-[1.4fr_1fr]">
        <AonikCard title="Token usage" subtitle="Daily totals · last 30 days">
          {loading && runs.length === 0 ? (
            <div className="flex items-center justify-center py-10">
              <RefreshCw className="h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
            </div>
          ) : (
            <DailyBarChart series={dailySeries} max={tokenChartMax} />
          )}
        </AonikCard>

        <AonikCard title="By use case" subtitle="Share of cost in the visible window">
          {byUseCase.length === 0 ? (
            <p className="py-6 text-center text-sm text-[var(--color-text-tertiary)]">
              No usage to break down yet.
            </p>
          ) : (
            <div className="flex flex-col gap-2.5">
              {byUseCase.map((entry) => {
                const colour = paletteFor(entry.useCase);
                const percent = Math.max(2, Math.round(entry.share * 100));
                return (
                  <div key={entry.useCase}>
                    <div className="mb-1 flex justify-between text-[12px]">
                      <span className="truncate text-[var(--color-text-primary)]">
                        {entry.useCase}
                      </span>
                      <span className="font-[family-name:var(--font-mono)] text-[var(--color-text-secondary)]">
                        {formatCost(entry.cost)} · {percent}%
                      </span>
                    </div>
                    <div className="h-1 overflow-hidden rounded-full bg-[var(--color-surface-inset)]">
                      <div
                        className="h-full"
                        style={{ width: `${percent}%`, background: colour }}
                      />
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </AonikCard>
      </div>
    </div>
  );
}

// ─── Daily bar chart ─────────────────────────────────────────────────────

function DailyBarChart({
  series,
  max,
}: {
  series: { date: string; tokens: number }[];
  max: number;
}) {
  const width = 600;
  const height = 200;
  const barGap = 2;
  const barWidth = Math.max(2, (width - (series.length - 1) * barGap) / series.length);

  return (
    <div>
      <svg viewBox={`0 0 ${width} ${height}`} className="h-[220px] w-full">
        {[0, 50, 100, 150, 200].map((y) => (
          <line
            key={y}
            x1={0}
            y1={y}
            x2={width}
            y2={y}
            stroke="var(--color-border-light)"
            strokeDasharray="2 4"
          />
        ))}
        {series.map((d, i) => {
          const x = i * (barWidth + barGap);
          const h = max === 0 ? 0 : (d.tokens / max) * (height - 10);
          return (
            <rect
              key={d.date}
              x={x}
              y={height - h}
              width={barWidth}
              height={h}
              fill="var(--color-brand-primary)"
              opacity={d.tokens === 0 ? 0.15 : 0.85}
            >
              <title>
                {d.date} · {formatTokens(d.tokens)} tokens
              </title>
            </rect>
          );
        })}
      </svg>
      <div className="mt-1 flex justify-between font-[family-name:var(--font-mono)] text-[10px] text-[var(--color-text-tertiary)]">
        <span>{series[0]?.date ?? '—'}</span>
        <span>{series[series.length - 1]?.date ?? '—'}</span>
      </div>
    </div>
  );
}

// ─── KPI tile ────────────────────────────────────────────────────────────

function UsageTile({
  label,
  value,
  sub,
  tone,
}: {
  label: string;
  value: string;
  sub: string;
  tone: string;
}) {
  return (
    <div className="rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] p-3.5">
      <div className="flex items-center gap-1.5 text-[11px] text-[var(--color-text-secondary)]">
        <span className="h-1.5 w-1.5 rounded-full" style={{ background: tone }} />
        {label}
      </div>
      <div className="mt-1 font-[family-name:var(--font-mono)] text-[22px] font-semibold leading-none text-[var(--color-text-primary)]">
        {value}
      </div>
      <div className="mt-1 font-[family-name:var(--font-mono)] text-[10px] text-[var(--color-text-tertiary)]">
        {sub}
      </div>
    </div>
  );
}
