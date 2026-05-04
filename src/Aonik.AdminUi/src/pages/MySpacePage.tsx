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
import { toast } from 'sonner';

import { Card, KpiTile, ProposalCard } from '@/components/layout/aonik';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { mySpaceService } from '@/services/mySpaceService';
import { agentProposalsService } from '@/services/agentProposalsService';
import { useAuth } from '@/auth';
import type {
  AgentProposalDto,
  CashTimelineDto,
  CashTimelineEventDto,
  CashTimelinePointDto,
  FinancialMetricDto,
  MySpaceSummaryResponse,
  ProposalDetailResponse,
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
  // Selected timeline currency — null means "use tenant primary on first load".
  const [selectedCurrency, setSelectedCurrency] = useState<string | null>(null);

  const loadData = useCallback(async (currency?: string | null) => {
    try {
      setLoading(true);
      setError(null);
      const summary = await mySpaceService.getSummary(currency ?? undefined);
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
    loadData(selectedCurrency);
  }, [loadData, selectedCurrency]);

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

  // Local proposals copy so Apply / Dismiss can optimistically remove the
  // card before the server round-trip completes; on failure we restore.
  const [proposals, setProposals] = useState<AgentProposalDto[]>([]);
  useEffect(() => {
    setProposals(data?.agentProposals ?? []);
  }, [data]);

  const proposalsWaiting = proposals.length;
  const unpaidInvoiceCount = outstanding?.count ?? 0;
  const cashFreshness = formatRelative(data?.cashPositionUpdatedAt);

  const handleApproveProposal = useCallback(async (id: string) => {
    const previous = proposals;
    setProposals((current) => current.filter((p) => p.id !== id));
    try {
      await agentProposalsService.approve(id);
      toast.success('Proposal applied');
    } catch (err) {
      const message = (err as { userMessage?: string })?.userMessage
        ?? (err instanceof Error ? err.message : 'Could not apply proposal.');
      toast.error(message);
      setProposals(previous);
    }
  }, [proposals]);

  const handleDismissProposal = useCallback(async (id: string) => {
    const previous = proposals;
    setProposals((current) => current.filter((p) => p.id !== id));
    try {
      await agentProposalsService.dismiss(id);
      toast.success('Proposal dismissed');
    } catch (err) {
      const message = (err as { userMessage?: string })?.userMessage
        ?? (err instanceof Error ? err.message : 'Could not dismiss proposal.');
      toast.error(message);
      setProposals(previous);
    }
  }, [proposals]);

  const [reviewProposalId, setReviewProposalId] = useState<string | null>(null);
  const handleReviewProposal = useCallback((id: string) => setReviewProposalId(id), []);

  if (loading) {
    return <PageLoadingScreen message="Loading dashboard" />;
  }

  if (error) {
    return (
      <div className="flex flex-1 items-center justify-center">
        <div className="flex flex-col items-center gap-3 text-center">
          <AlertCircle className="h-8 w-8 text-[var(--color-error)]" />
          <p className="text-sm text-[var(--color-text-secondary)]">{error}</p>
          <button
            type="button"
            onClick={() => void loadData()}
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
        <CashTimelineCard data={cashTimeline} onCurrencyChange={setSelectedCurrency} />
        <AgentProposalsCard
          proposals={proposals}
          onApprove={handleApproveProposal}
          onReview={handleReviewProposal}
          onDismiss={handleDismissProposal}
        />
      </div>

      <ProposalReviewDialog
        proposalId={reviewProposalId}
        onClose={() => setReviewProposalId(null)}
        onApprove={async (id) => {
          setReviewProposalId(null);
          await handleApproveProposal(id);
        }}
        onDismiss={async (id) => {
          setReviewProposalId(null);
          await handleDismissProposal(id);
        }}
      />

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

interface CashTimelineCardProps {
  data: CashTimelineDto | undefined;
  onCurrencyChange: (currency: string) => void;
}

function CashTimelineCard({ data, onCurrencyChange }: CashTimelineCardProps) {
  const historical = data?.historical ?? [];
  const projected = data?.projected ?? [];
  const events = data?.events ?? [];
  const currency = data?.currency ?? 'USD';
  const codes = data?.availableCurrencies ?? [];

  return (
    <Card
      title="Cash timeline · 30 days back · 30 forward"
      subtitle="Daily running balance plus a naive forward projection"
      action={
        codes.length > 0 ? (
          <div className="flex gap-1 text-[12px]">
            {codes.map((code) => {
              const isActive = currency === code;
              return (
                <button
                  key={code}
                  type="button"
                  onClick={() => {
                    if (!isActive) onCurrencyChange(code);
                  }}
                  className="h-7 rounded-md px-2 font-medium transition-colors hover:bg-[var(--color-surface-inset)]"
                  style={{
                    color: isActive
                      ? 'var(--color-brand-primary)'
                      : 'var(--color-text-secondary)',
                  }}
                  title={isActive ? `${code} (active)` : `Switch to ${code}`}
                  aria-pressed={isActive}
                >
                  {code}
                </button>
              );
            })}
          </div>
        ) : null
      }
      padding={20}
    >
      <CashTimelineChart
        historical={historical}
        projected={projected}
        events={events}
        currency={currency}
      />
      <div
        className="mt-3.5 flex flex-wrap gap-4 rounded-lg bg-[var(--color-surface-inset)] px-3 py-2.5 text-[11px] text-[var(--color-text-secondary)]"
        style={{ fontFamily: 'var(--font-mono)' }}
      >
        <CashTimelineSummary
          historical={historical}
          projectedLow={data?.projectedLow ?? null}
          projectedLowAt={data?.projectedLowAt ?? null}
          eventCount={events.length}
          currency={currency}
        />
      </div>
    </Card>
  );
}

interface CashTimelineChartProps {
  historical: CashTimelinePointDto[];
  projected: CashTimelinePointDto[];
  events: CashTimelineEventDto[];
  currency: string;
}

function CashTimelineChart({ historical, projected, events, currency }: CashTimelineChartProps) {
  // Combined series: historical occupies the left half, projected the right.
  // The "TODAY" boundary sits at the last historical point, which is also
  // the last point used for trend extrapolation by the backend.
  const combined = [...historical, ...projected];

  if (combined.length === 0) {
    return (
      <div
        className="flex h-[220px] items-center justify-center text-[12px] text-[var(--color-text-tertiary)]"
        style={{ fontFamily: 'var(--font-mono)' }}
      >
        No cash entries in the last 30 days.
      </div>
    );
  }

  const balances = combined.map((p) => p.balance);
  const max = Math.max(...balances);
  const min = Math.min(...balances);
  const range = max - min || 1;

  const xFor = (i: number) =>
    CASH_CHART_PADDING +
    (i / Math.max(1, combined.length - 1)) * (CASH_CHART_WIDTH - 2 * CASH_CHART_PADDING);
  const yFor = (balance: number) =>
    CASH_CHART_PADDING +
    (1 - (balance - min) / range) * (CASH_CHART_HEIGHT - 2 * CASH_CHART_PADDING);

  const histPolyline = historical
    .map((p, i) => `${xFor(i).toFixed(1)},${yFor(p.balance).toFixed(1)}`)
    .join(' ');
  const histPolygon = histPolyline
    ? `${histPolyline} ${xFor(historical.length - 1).toFixed(1)},${CASH_CHART_HEIGHT} ${xFor(0).toFixed(1)},${CASH_CHART_HEIGHT}`
    : '';

  // Projected polyline starts at the last historical point so the line is
  // visually continuous from solid into dashed at the TODAY boundary.
  const projPoints =
    historical.length > 0 && projected.length > 0
      ? [historical[historical.length - 1], ...projected]
      : projected;
  const projStartIndex = historical.length > 0 ? historical.length - 1 : 0;
  const projPolyline = projPoints
    .map((p, i) => `${xFor(projStartIndex + i).toFixed(1)},${yFor(p.balance).toFixed(1)}`)
    .join(' ');

  // Map event dates to projected indices so each marker lands on the dashed line.
  const projectedIndexByDate = new Map<string, number>();
  projected.forEach((p, i) => projectedIndexByDate.set(p.date.slice(0, 10), historical.length + i));
  const eventDots = events
    .map((event) => {
      const key = event.date.slice(0, 10);
      const idx = projectedIndexByDate.get(key);
      if (idx === undefined) return null;
      const point = combined[idx];
      return { x: xFor(idx), y: yFor(point.balance), event };
    })
    .filter((e): e is { x: number; y: number; event: CashTimelineEventDto } => e !== null);

  const todayIndex = historical.length > 0 ? historical.length - 1 : 0;
  const todayX = xFor(todayIndex);

  // 7 evenly-spaced date labels across the bottom (start, +10d, +20d, today, +10d, +20d, end).
  const labelIndices =
    combined.length >= 7
      ? [0, 10, 20, todayIndex, todayIndex + 10, todayIndex + 20, combined.length - 1]
      : combined.map((_, i) => i);
  const labelDates = labelIndices
    .filter((i) => i >= 0 && i < combined.length)
    .map((i) => combined[i].date);

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
        {histPolygon && <polygon fill="url(#ms-cashGrad)" points={histPolygon} />}
        {histPolyline && (
          <polyline
            fill="none"
            stroke="var(--color-brand-primary)"
            strokeWidth="2"
            points={histPolyline}
          />
        )}
        {projPolyline && (
          <polyline
            fill="none"
            stroke="var(--color-brand-primary)"
            strokeWidth="2"
            strokeDasharray="4 4"
            points={projPolyline}
          />
        )}
        {/* TODAY marker at the historical/projected boundary */}
        <line
          x1={todayX}
          y1={CASH_CHART_PADDING}
          x2={todayX}
          y2={CASH_CHART_HEIGHT - CASH_CHART_PADDING}
          stroke="var(--color-brand-secondary)"
          strokeWidth="1.5"
          strokeDasharray="3 3"
        />
        <rect
          x={todayX - 32}
          y={4}
          width="64"
          height="18"
          rx="3"
          fill="var(--color-brand-secondary-10)"
        />
        <text
          x={todayX}
          y={17}
          fill="var(--color-brand-secondary)"
          fontSize="10"
          fontFamily="var(--font-mono)"
          textAnchor="middle"
          fontWeight="600"
        >
          TODAY
        </text>
        {/* Revenue / payroll / payout event markers — coral revenue dots for now */}
        {eventDots.map(({ x, y, event }, i) => (
          <circle
            key={`${event.date}-${i}`}
            cx={x}
            cy={y}
            r={4}
            fill="var(--color-accent-ent)"
            stroke="var(--color-surface)"
            strokeWidth="1.5"
          >
            <title>{event.label}</title>
          </circle>
        ))}
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
  historical,
  projectedLow,
  projectedLowAt,
  eventCount,
  currency,
}: {
  historical: CashTimelinePointDto[];
  projectedLow: number | null;
  projectedLowAt: string | null;
  eventCount: number;
  currency: string;
}) {
  if (historical.length === 0) {
    return <span>◆ No activity in the window</span>;
  }
  const latest = historical[historical.length - 1]?.balance ?? 0;
  const earliest = historical[0]?.balance ?? 0;
  const delta = latest - earliest;

  return (
    <>
      <span>◆ Latest: {formatCurrency(latest, currency)}</span>
      {projectedLow !== null && (
        <span>
          ◆ Projected low: {formatCurrency(projectedLow, currency)}
          {projectedLowAt
            ? ` · ${new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' }).format(new Date(projectedLowAt))}`
            : ''}
        </span>
      )}
      <span>
        ◆ 30-day net: {delta >= 0 ? '+' : ''}
        {formatCurrency(delta, currency)}
      </span>
      {eventCount > 0 && (
        <span>
          ◆ {eventCount} revenue event{eventCount === 1 ? '' : 's'} ahead
        </span>
      )}
    </>
  );
}

// ─── Agent proposals ─────────────────────────────────────────────────────

interface AgentProposalsCardProps {
  proposals: AgentProposalDto[];
  onApprove: (id: string) => void;
  onReview: (id: string) => void;
  onDismiss: (id: string) => void;
}

function AgentProposalsCard({
  proposals,
  onApprove,
  onReview,
  onDismiss,
}: AgentProposalsCardProps) {
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
              onApply={() => onApprove(p.id)}
              onReview={() => onReview(p.id)}
              onDismiss={() => onDismiss(p.id)}
            />
          ))}
        </div>
      )}
    </Card>
  );
}

// ─── Review dialog ───────────────────────────────────────────────────────
// Lightweight inspector for a single proposal — fetches the full detail
// (including PayloadJson) on open. Per-ProposalType custom views can land
// later; for now we render a generic key-value layout plus a JSON dump.

function ProposalReviewDialog({
  proposalId,
  onClose,
  onApprove,
  onDismiss,
}: {
  proposalId: string | null;
  onClose: () => void;
  onApprove: (id: string) => Promise<void>;
  onDismiss: (id: string) => Promise<void>;
}) {
  const open = proposalId !== null;
  const [detail, setDetail] = useState<ProposalDetailResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!proposalId) {
      setDetail(null);
      setError(null);
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError(null);
    agentProposalsService
      .get(proposalId)
      .then((d) => {
        if (cancelled) return;
        setDetail(d);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(
          (err as { userMessage?: string })?.userMessage ??
            (err instanceof Error ? err.message : 'Could not load proposal.'),
        );
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [proposalId]);

  const prettyPayload = useMemo(() => {
    if (!detail?.payloadJson) return '';
    try {
      return JSON.stringify(JSON.parse(detail.payloadJson), null, 2);
    } catch {
      return detail.payloadJson;
    }
  }, [detail]);

  return (
    <Dialog
      open={open}
      onOpenChange={(isOpen) => {
        if (!isOpen) onClose();
      }}
    >
      <DialogContent className="max-w-[640px]">
        <DialogHeader>
          <DialogTitle>Review proposal</DialogTitle>
          <DialogDescription>
            Inspect the full agent payload before applying or dismissing.
          </DialogDescription>
        </DialogHeader>

        {loading && (
          <div className="flex items-center justify-center py-8 text-sm text-[var(--color-text-secondary)]">
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            Loading proposal…
          </div>
        )}

        {error && !loading && (
          <div className="flex items-center gap-2 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-sm text-[var(--color-error)]">
            <AlertCircle className="h-4 w-4" />
            {error}
          </div>
        )}

        {detail && !loading && !error && (
          <div className="flex flex-col gap-4">
            <div className="grid grid-cols-2 gap-x-4 gap-y-2 text-[13px]">
              <ReviewField label="Agent" value={`${detail.agentName} · ${detail.agentDomain}`} />
              <ReviewField label="Type" value={detail.proposalType} />
              <ReviewField
                label="Confidence"
                value={
                  <span className="font-mono">{detail.confidence.toFixed(2)}</span>
                }
              />
              <ReviewField
                label="Risk"
                value={
                  <Badge variant="outline" className="text-[11px]">
                    {detail.riskTier || 'unknown'}
                  </Badge>
                }
              />
              <ReviewField label="Status" value={detail.status} />
              <ReviewField
                label="Created"
                value={new Date(detail.createdAt).toLocaleString()}
              />
            </div>

            <div>
              <div className="mb-1 text-[11px] font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)]">
                Summary
              </div>
              <div className="rounded-md bg-[var(--color-surface-inset)] px-3 py-2 text-[13px] text-[var(--color-text-primary)]">
                {detail.summary}
              </div>
            </div>

            <div>
              <div className="mb-1 text-[11px] font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)]">
                Payload
              </div>
              <pre className="max-h-[260px] overflow-auto rounded-md bg-[var(--color-surface-inset)] px-3 py-2 font-mono text-[11px] leading-relaxed text-[var(--color-text-primary)]">
                {prettyPayload || '(empty)'}
              </pre>
            </div>
          </div>
        )}

        <DialogFooter>
          {detail && detail.status === 'Proposed' ? (
            <>
              <Button variant="ghost" onClick={() => detail && onDismiss(detail.id)}>
                Dismiss
              </Button>
              <Button onClick={() => detail && onApprove(detail.id)}>Apply</Button>
            </>
          ) : (
            <Button variant="ghost" onClick={onClose}>
              Close
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function ReviewField({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <div className="text-[11px] font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)]">
        {label}
      </div>
      <div className="mt-0.5 text-[13px] text-[var(--color-text-primary)]">{value}</div>
    </div>
  );
}
