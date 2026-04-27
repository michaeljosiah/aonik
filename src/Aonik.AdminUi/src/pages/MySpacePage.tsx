// MySpace dashboard — 1:1 visual port of
// templates/aonik-admin-starterkit/screens/myspace.jsx, fully wired to the
// extended /insights/myspace-summary backend (Wave 4b):
//   • header subtitle stats now come from agentProposals.length, the
//     outstanding-invoices count, and cashPositionUpdatedAt freshness
//   • KPI 4 reads agentOpsToday
//   • cash timeline plots the historical[] series in the tenant's primary
//     settlement currency (currency switcher remains visual-only for now)
//   • agent proposals card renders ProposalCard rows when proposals exist,
//     otherwise an empty state
//
// Carousel / quick links / agent grid / databoxes from the prior page are
// intentionally dropped so the layout matches the template canvas exactly.

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, Calendar, Filter, Loader2, Plus, RefreshCw, Sparkles } from 'lucide-react';

import { Card, KpiTile, ProposalCard } from '@/components/layout/aonik';
import { mySpaceService } from '@/services/mySpaceService';
import { useAuth } from '@/auth';
import type {
  AgentProposalDto,
  CashTimelineDto,
  CashTimelinePointDto,
  FinancialMetricDto,
  MySpaceSummaryResponse,
} from '@/types';

// ─── Helpers ─────────────────────────────────────────────────────────────

function formatEyebrowDate(now: Date): string {
  return new Intl.DateTimeFormat(undefined, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  })
    .format(now)
    .replace(',', ' ·');
}

function greetingForHour(hour: number): string {
  if (hour < 12) return 'Morning';
  if (hour < 18) return 'Afternoon';
  return 'Evening';
}

function firstName(fullName: string | undefined | null): string {
  if (!fullName) return 'there';
  const trimmed = fullName.trim();
  if (!trimmed) return 'there';
  return trimmed.split(/\s+/)[0];
}

function formatRelative(timestamp: string | null | undefined): string {
  if (!timestamp) return 'never';
  const then = new Date(timestamp).getTime();
  if (Number.isNaN(then)) return 'never';
  const diffMs = Date.now() - then;
  if (diffMs < 0) return 'just now';
  const m = Math.round(diffMs / 60_000);
  if (m < 1) return 'just now';
  if (m < 60) return `${m}m ago`;
  const h = Math.round(m / 60);
  if (h < 24) return `${h}h ago`;
  const d = Math.round(h / 24);
  if (d < 7) return `${d}d ago`;
  return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' }).format(new Date(timestamp));
}

function activityDotColor(iconHint: string | undefined): string {
  const hint = (iconHint ?? '').toLowerCase();
  if (/check|success|complete|posted|settled/.test(hint)) return 'var(--color-success)';
  if (/sparkles|agent|proposal|match/.test(hint)) return 'var(--color-brand-secondary)';
  if (/alert|warn|drift|error/.test(hint)) return 'var(--color-warning)';
  return 'var(--color-gray-400)';
}

function formatCurrency(value: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency,
      maximumFractionDigits: 0,
    }).format(value);
  } catch {
    return `${currency} ${Math.round(value).toLocaleString()}`;
  }
}

const KPI_SPARK_COLOR: Record<string, string> = {
  'cash-position': 'var(--color-brand-primary)',
  revenue: 'var(--color-accent-ent)',
  'outstanding-invoices': 'var(--color-brand-secondary)',
  'agent-ops-today': 'var(--color-violet)',
};

// ─── Page ────────────────────────────────────────────────────────────────

export function MySpacePage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [data, setData] = useState<MySpaceSummaryResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const summary = await mySpaceService.getSummary();
      setData(summary);
    } catch (err: unknown) {
      const message =
        (err as { userMessage?: string })?.userMessage ??
        (err instanceof Error ? err.message : 'Failed to load dashboard data.');
      setError(message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const metricByKey = useMemo(() => {
    const map = new Map<string, FinancialMetricDto>();
    data?.financialMetrics.forEach((m) => map.set(m.metricKey, m));
    return map;
  }, [data]);

  const cashPosition = metricByKey.get('cash-position');
  const revenue = metricByKey.get('revenue');
  const outstanding = metricByKey.get('outstanding-invoices');

  const now = new Date();
  const greeting = `${greetingForHour(now.getHours())}, ${firstName(user?.name)}.`;
  const eyebrow = formatEyebrowDate(now);

  const agentProposals: AgentProposalDto[] = data?.agentProposals ?? [];
  const proposalsWaiting = agentProposals.length;
  const unpaidInvoiceCount = outstanding?.count ?? 0;
  const cashFreshness = formatRelative(data?.cashPositionUpdatedAt);

  if (loading) {
    return (
      <div className="flex flex-1 items-center justify-center">
        <div className="flex flex-col items-center gap-3 text-[var(--color-text-secondary)]">
          <Loader2 className="h-8 w-8 animate-spin" />
          <p className="text-sm">Loading dashboard…</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex flex-1 items-center justify-center">
        <div className="flex flex-col items-center gap-3 text-center">
          <AlertCircle className="h-8 w-8 text-[var(--color-error)]" />
          <p className="text-sm text-[var(--color-text-secondary)]">{error}</p>
          <button
            type="button"
            onClick={loadData}
            className="inline-flex h-8 items-center gap-2 rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-sm text-[var(--color-text-primary)] hover:bg-[var(--color-surface-inset)]"
          >
            <RefreshCw className="h-3.5 w-3.5" />
            Retry
          </button>
        </div>
      </div>
    );
  }

  const activity = data?.recentActivity ?? [];
  const cashTimeline = data?.cashTimeline;

  return (
    <div className="flex flex-col gap-6 p-7 md:px-8">
      {/* Header row */}
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <span className="eyebrow">{eyebrow}</span>
          <h1
            className="mt-1.5 text-[26px] font-bold tracking-tight text-[var(--color-text-primary)]"
            style={{ fontFamily: 'var(--font-brand)', letterSpacing: '-0.01em' }}
          >
            {greeting}
          </h1>
          <p className="mt-1 text-[13px] text-[var(--color-text-secondary)]">
            {proposalsWaiting} proposal{proposalsWaiting === 1 ? '' : 's'} waiting · {unpaidInvoiceCount} invoice
            {unpaidInvoiceCount === 1 ? '' : 's'} unpaid · cash position updated {cashFreshness}
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <button
            type="button"
            className="inline-flex h-8 items-center gap-1.5 rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-[13px] font-medium text-[var(--color-text-primary)] transition-colors hover:bg-[var(--color-surface-inset)]"
            title="Filter by date range"
          >
            <Calendar className="h-3.5 w-3.5" />
            This month
          </button>
          <button
            type="button"
            onClick={() => navigate('/billing/invoices')}
            className="inline-flex h-8 items-center gap-1.5 rounded-md bg-[var(--color-brand-primary)] px-3 text-[13px] font-medium text-white transition-colors hover:bg-[var(--color-brand-primary-dark)]"
          >
            <Plus className="h-3.5 w-3.5" />
            New bill payment
          </button>
        </div>
      </div>

      {/* KPI row — always exactly four, same width */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <KpiTile
          label="Cash position"
          value={cashPosition?.formattedValue ?? '—'}
          delta={cashPosition ? `${cashPosition.trendPercent >= 0 ? '+' : ''}${cashPosition.trendPercent}%` : undefined}
          deltaTone={cashPosition?.trendDirection === 'down' ? 'down' : cashPosition?.trendDirection === 'neutral' ? 'neutral' : 'up'}
          sparkline={cashPosition?.sparkline}
          sparkColor={KPI_SPARK_COLOR['cash-position']}
        />
        <KpiTile
          label="Revenue"
          value={revenue?.formattedValue ?? '—'}
          delta={revenue ? `${revenue.trendPercent >= 0 ? '+' : ''}${revenue.trendPercent}%` : undefined}
          deltaTone={revenue?.trendDirection === 'down' ? 'down' : revenue?.trendDirection === 'neutral' ? 'neutral' : 'up'}
          sparkline={revenue?.sparkline}
          sparkColor={KPI_SPARK_COLOR.revenue}
        />
        <KpiTile
          label="Outstanding invoices"
          value={outstanding?.formattedValue ?? '—'}
          delta={
            outstanding
              ? outstanding.count != null && outstanding.count > 0
                ? `${outstanding.count} overdue`
                : `${outstanding.trendPercent >= 0 ? '+' : ''}${outstanding.trendPercent}%`
              : undefined
          }
          deltaTone="down"
          sparkline={outstanding?.sparkline}
          sparkColor={KPI_SPARK_COLOR['outstanding-invoices']}
        />
        <KpiTile
          label="Agent ops today"
          value={String(data?.agentOpsToday ?? 0)}
          delta="today"
          deltaTone="neutral"
          sparkColor={KPI_SPARK_COLOR['agent-ops-today']}
        />
      </div>

      {/* Cash timeline + Agent proposals */}
      <div className="grid grid-cols-1 gap-5 lg:grid-cols-[1.4fr_1fr]">
        <CashTimelineCard data={cashTimeline} />
        <AgentProposalsCard proposals={agentProposals} />
      </div>

      {/* Recent activity */}
      <Card
        title="Recent activity"
        subtitle="All agents · last 24 hours"
        action={
          <button
            type="button"
            className="inline-flex h-8 items-center gap-1.5 rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-[12px] font-medium text-[var(--color-text-primary)] transition-colors hover:bg-[var(--color-surface-inset)]"
          >
            <Filter className="h-3 w-3" />
            Filter
          </button>
        }
        padding={20}
      >
        {activity.length === 0 ? (
          <div className="py-6 text-center text-[13px] text-[var(--color-text-secondary)]">
            No recent activity yet. Agents will surface events here as they run.
          </div>
        ) : (
          <div className="flex flex-col">
            {activity.map((row, i) => (
              <div
                key={row.id}
                className="grid items-center gap-3.5 py-3"
                style={{
                  gridTemplateColumns: '20px 1fr auto',
                  borderBottom:
                    i < activity.length - 1 ? '1px solid var(--color-border-light)' : 'none',
                }}
              >
                <span
                  className="mx-auto h-2 w-2 rounded-full"
                  style={{ background: activityDotColor(row.icon) }}
                  aria-hidden
                />
                <div className="min-w-0">
                  <div className="truncate text-[13px] font-medium text-[var(--color-text-primary)]">
                    {row.title}
                  </div>
                  {row.description && (
                    <div className="mt-0.5 truncate text-[11px] text-[var(--color-text-secondary)]">
                      {row.description}
                    </div>
                  )}
                </div>
                <div
                  className="text-[11px] text-[var(--color-text-tertiary)]"
                  style={{ fontFamily: 'var(--font-mono)' }}
                >
                  {row.timestamp}
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>
    </div>
  );
}

// ─── Cash timeline ───────────────────────────────────────────────────────
// Renders a 30-day historical balance polyline + filled gradient area.
// Wave 4b ships historical only; projection (dashed forward extension and
// event markers) lands in Wave 4c. Currency switcher buttons highlight the
// tenant's primary currency but don't yet fetch alternatives.

const CASH_CHART_WIDTH = 600;
const CASH_CHART_HEIGHT = 200;
const CASH_CHART_PADDING = 12;
const CASH_SWITCHER_CODES = ['NGN', 'USD', 'GBP'] as const;

interface CashTimelineCardProps {
  data: CashTimelineDto | undefined;
}

function CashTimelineCard({ data }: CashTimelineCardProps) {
  const points = data?.historical ?? [];
  const currency = data?.currency ?? 'USD';

  return (
    <Card
      title="Cash timeline · last 30 days"
      subtitle="Daily running balance across all asset accounts"
      action={
        <div className="flex gap-1 text-[12px]">
          {CASH_SWITCHER_CODES.map((code) => {
            const isActive = currency === code;
            return (
              <button
                key={code}
                type="button"
                className="h-7 rounded-md px-2 font-medium hover:bg-[var(--color-surface-inset)]"
                style={{
                  color: isActive
                    ? 'var(--color-brand-primary)'
                    : 'var(--color-text-secondary)',
                }}
                title={isActive ? `${code} (active)` : `${code} (coming soon)`}
              >
                {code}
              </button>
            );
          })}
        </div>
      }
      padding={20}
    >
      <CashTimelineChart points={points} currency={currency} />
      <div
        className="mt-3.5 flex flex-wrap gap-4 rounded-lg bg-[var(--color-surface-inset)] px-3 py-2.5 text-[11px] text-[var(--color-text-secondary)]"
        style={{ fontFamily: 'var(--font-mono)' }}
      >
        <CashTimelineSummary points={points} currency={currency} />
      </div>
    </Card>
  );
}

function CashTimelineChart({
  points,
  currency,
}: {
  points: CashTimelinePointDto[];
  currency: string;
}) {
  if (points.length === 0) {
    return (
      <div
        className="flex h-[220px] items-center justify-center text-[12px] text-[var(--color-text-tertiary)]"
        style={{ fontFamily: 'var(--font-mono)' }}
      >
        No cash entries in the last 30 days.
      </div>
    );
  }

  const balances = points.map((p) => p.balance);
  const max = Math.max(...balances);
  const min = Math.min(...balances);
  const range = max - min || 1;

  const xFor = (i: number) =>
    CASH_CHART_PADDING +
    (i / Math.max(1, points.length - 1)) * (CASH_CHART_WIDTH - 2 * CASH_CHART_PADDING);
  const yFor = (balance: number) =>
    CASH_CHART_PADDING +
    (1 - (balance - min) / range) * (CASH_CHART_HEIGHT - 2 * CASH_CHART_PADDING);

  const polyline = points.map((p, i) => `${xFor(i).toFixed(1)},${yFor(p.balance).toFixed(1)}`).join(' ');
  const polygon = `${polyline} ${xFor(points.length - 1).toFixed(1)},${CASH_CHART_HEIGHT} ${xFor(0).toFixed(1)},${CASH_CHART_HEIGHT}`;

  // 6 evenly-spaced date labels across the bottom.
  const labelIndices = points.length >= 6 ? [0, 6, 12, 18, 24, points.length - 1] : points.map((_, i) => i);
  const labelDates = labelIndices.map((i) => points[i]?.date).filter(Boolean) as string[];

  return (
    <div className="relative h-[220px]">
      <svg
        viewBox={`0 0 ${CASH_CHART_WIDTH} ${CASH_CHART_HEIGHT}`}
        preserveAspectRatio="none"
        className="block h-full w-full"
        aria-label={`Cash timeline in ${currency}`}
      >
        <defs>
          <linearGradient id="ms-cashGrad" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--color-brand-primary)" stopOpacity="0.22" />
            <stop offset="100%" stopColor="var(--color-brand-primary)" stopOpacity="0" />
          </linearGradient>
          <pattern id="ms-grid" width="60" height="44" patternUnits="userSpaceOnUse">
            <path d="M 60 0 L 0 0 0 44" fill="none" stroke="var(--color-border-light)" strokeWidth="1" />
          </pattern>
        </defs>
        <rect width={CASH_CHART_WIDTH} height={CASH_CHART_HEIGHT} fill="url(#ms-grid)" />
        <polygon fill="url(#ms-cashGrad)" points={polygon} />
        <polyline
          fill="none"
          stroke="var(--color-brand-primary)"
          strokeWidth="2"
          points={polyline}
        />
        {/* TODAY marker at the right edge of the historical series */}
        <line
          x1={xFor(points.length - 1)}
          y1={CASH_CHART_PADDING}
          x2={xFor(points.length - 1)}
          y2={CASH_CHART_HEIGHT - CASH_CHART_PADDING}
          stroke="var(--color-brand-secondary)"
          strokeWidth="1.5"
          strokeDasharray="3 3"
        />
        <rect
          x={xFor(points.length - 1) - 32}
          y={4}
          width="64"
          height="18"
          rx="3"
          fill="var(--color-brand-secondary-10)"
        />
        <text
          x={xFor(points.length - 1)}
          y={17}
          fill="var(--color-brand-secondary)"
          fontSize="10"
          fontFamily="var(--font-mono)"
          textAnchor="middle"
          fontWeight="600"
        >
          TODAY
        </text>
      </svg>
      <div
        className="absolute bottom-0 left-0 right-0 flex justify-between px-3 text-[10px] text-[var(--color-text-tertiary)]"
        style={{ fontFamily: 'var(--font-mono)' }}
      >
        {labelDates.map((iso) => (
          <span key={iso}>
            {new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' }).format(new Date(iso))}
          </span>
        ))}
      </div>
    </div>
  );
}

function CashTimelineSummary({
  points,
  currency,
}: {
  points: CashTimelinePointDto[];
  currency: string;
}) {
  if (points.length === 0) {
    return <span>◆ No activity in the window</span>;
  }
  const balances = points.map((p) => p.balance);
  const minBalance = Math.min(...balances);
  const minIndex = balances.indexOf(minBalance);
  const minDate = points[minIndex]?.date;
  const latest = points[points.length - 1]?.balance ?? 0;
  const earliest = points[0]?.balance ?? 0;
  const delta = latest - earliest;

  return (
    <>
      <span>
        ◆ Latest: {formatCurrency(latest, currency)}
      </span>
      <span>
        ◆ 30-day low: {formatCurrency(minBalance, currency)}
        {minDate
          ? ` · ${new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' }).format(new Date(minDate))}`
          : ''}
      </span>
      <span>
        ◆ Net change: {delta >= 0 ? '+' : ''}
        {formatCurrency(delta, currency)}
      </span>
    </>
  );
}

// ─── Agent proposals ─────────────────────────────────────────────────────

function AgentProposalsCard({ proposals }: { proposals: AgentProposalDto[] }) {
  return (
    <Card
      title="Agent proposals"
      subtitle="Pending your review"
      action={
        <span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--color-pending-light)] px-2.5 py-0.5 text-[11px] font-medium text-[var(--color-pending)]">
          <span className="h-1.5 w-1.5 rounded-full bg-[var(--color-pending)]" aria-hidden />
          {proposals.length} pending
        </span>
      }
      padding={20}
    >
      {proposals.length === 0 ? (
        <div className="flex h-[220px] flex-col items-center justify-center gap-3 text-center">
          <div
            className="grid h-10 w-10 place-items-center rounded-full"
            style={{ background: 'var(--color-brand-primary-10)' }}
          >
            <Sparkles className="h-5 w-5 text-[var(--color-brand-primary)]" />
          </div>
          <div>
            <div className="text-[13px] font-medium text-[var(--color-text-primary)]">
              No pending proposals
            </div>
            <p className="mt-0.5 text-[11px] text-[var(--color-text-secondary)]">
              Agents will surface proposals here when they need a human decision.
            </p>
          </div>
        </div>
      ) : (
        <div className="flex flex-col gap-2.5">
          {proposals.map((p) => (
            <ProposalCard
              key={p.id}
              agent={p.agentDomain || p.agentName}
              confidence={p.confidence}
              summary={p.summary}
              reason={p.reason ?? undefined}
              compact
            />
          ))}
        </div>
      )}
    </Card>
  );
}
