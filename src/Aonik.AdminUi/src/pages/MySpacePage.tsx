// MySpace dashboard — 1:1 visual port of
// templates/aonik-admin-starterkit/screens/myspace.jsx.
//
// Header (eyebrow date + greeting + subtitle stats + CTAs), 4-column KPI
// grid, two-column row (cash timeline + agent proposals), full-width
// recent activity. Carousel / quick links / agent grid / databoxes from
// the prior page are intentionally dropped so the layout matches the
// template canvas exactly.
//
// Data is wired from `mySpaceService.getSummary()` where it exists.
// Three placeholder regions are tagged for follow-up backend work:
//   • Agent ops today  — fixed 0 until a backend metric exists.
//   • Cash timeline    — placeholder SVG (no projection endpoint yet).
//   • Agent proposals  — empty state (no proposals feed yet).

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, Calendar, Filter, Loader2, Plus, RefreshCw, Sparkles } from 'lucide-react';

import { Card, KpiTile } from '@/components/layout/aonik';
import { mySpaceService } from '@/services/mySpaceService';
import { useAuth } from '@/auth';
import type { FinancialMetricDto, MySpaceSummaryResponse } from '@/types';

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

function formatRelative(timestamp: string): string {
  const then = new Date(timestamp).getTime();
  if (Number.isNaN(then)) return '—';
  const diffMs = Date.now() - then;
  const m = Math.round(diffMs / 60_000);
  if (m < 1) return 'just now';
  if (m < 60) return `${m}m ago`;
  const h = Math.round(m / 60);
  if (h < 24) return `${h}h ago`;
  const d = Math.round(h / 24);
  if (d < 7) return `${d}d ago`;
  return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' }).format(new Date(timestamp));
}

// Map an activity icon hint to one of the template's dot tones. Falls back
// to a neutral muted dot when the icon doesn't match a known pattern.
function activityDotColor(iconHint: string | undefined): string {
  const hint = (iconHint ?? '').toLowerCase();
  if (/check|success|complete|posted|settled/.test(hint)) return 'var(--color-success)';
  if (/sparkles|agent|proposal|match/.test(hint)) return 'var(--color-brand-secondary)';
  if (/alert|warn|drift|error/.test(hint)) return 'var(--color-warning)';
  return 'var(--color-gray-400)';
}

// Sparkline accent per metric — mirrors the template's KPI palette so the
// dashboard reads as four distinct streams at a glance.
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

  // Subtitle stats — proposals count is 0 until the backend feed exists,
  // unpaid invoice count comes from the outstanding-invoices metric, and
  // freshness is "just now" placeholder until the summary returns a sync
  // timestamp. Tagged for the Wave 4b backend pass.
  const proposalsWaiting = 0; // TODO(Wave 4b): wire to agent proposals feed
  const unpaidInvoiceCount = outstanding?.count ?? 0;
  const cashFreshness = 'just now'; // TODO(Wave 4b): cashPositionUpdatedAt

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

      {/* KPI row — always exactly four, same width (per design system) */}
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
        {/* Placeholder until a backend agent-ops metric ships. */}
        <KpiTile
          label="Agent ops today"
          value="0"
          delta="—"
          deltaTone="neutral"
          sparkColor={KPI_SPARK_COLOR['agent-ops-today']}
        />
      </div>

      {/* Cash timeline + Agent proposals */}
      <div className="grid grid-cols-1 gap-5 lg:grid-cols-[1.4fr_1fr]">
        <CashTimelineCard />
        <AgentProposalsCard />
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
                  {formatRelative(row.timestamp)}
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>
    </div>
  );
}

// ─── Cash timeline placeholder ───────────────────────────────────────────
// Renders the template's static demo polyline + projection so the page
// reads correctly before a real cashTimeline endpoint exists. The currency
// switcher is visual-only for now. Replace with a charted dataset (Wave 4b).

function CashTimelineCard() {
  return (
    <Card
      title="Cash timeline · next 30 days"
      subtitle="Projected from scheduled invoices, payouts, and recurring entries"
      action={
        <div className="flex gap-1 text-[12px]">
          <button
            type="button"
            className="h-7 rounded-md px-2 font-medium text-[var(--color-brand-primary)] hover:bg-[var(--color-surface-inset)]"
          >
            NGN
          </button>
          <button
            type="button"
            className="h-7 rounded-md px-2 font-medium text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-inset)]"
          >
            USD
          </button>
          <button
            type="button"
            className="h-7 rounded-md px-2 font-medium text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-inset)]"
          >
            GBP
          </button>
        </div>
      }
      padding={20}
    >
      <div className="relative h-[220px]">
        <svg viewBox="0 0 600 220" preserveAspectRatio="none" className="h-full w-full">
          <defs>
            <linearGradient id="ms-cashGrad" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="var(--color-brand-primary)" stopOpacity="0.22" />
              <stop offset="100%" stopColor="var(--color-brand-primary)" stopOpacity="0" />
            </linearGradient>
            <pattern id="ms-grid" width="60" height="44" patternUnits="userSpaceOnUse">
              <path d="M 60 0 L 0 0 0 44" fill="none" stroke="var(--color-border-light)" strokeWidth="1" />
            </pattern>
          </defs>
          <rect width="600" height="220" fill="url(#ms-grid)" />
          <polyline
            fill="none"
            stroke="var(--color-brand-primary)"
            strokeWidth="2"
            points="0,120 40,112 80,115 120,100 160,105 200,92 240,94 280,80"
          />
          <polygon
            fill="url(#ms-cashGrad)"
            points="0,120 40,112 80,115 120,100 160,105 200,92 240,94 280,80 280,220 0,220"
          />
          <polyline
            fill="none"
            stroke="var(--color-brand-primary)"
            strokeWidth="2"
            strokeDasharray="4 4"
            points="280,80 320,78 360,70 400,85 440,68 480,60 520,66 560,50 600,55"
          />
          <line
            x1="280"
            y1="20"
            x2="280"
            y2="200"
            stroke="var(--color-brand-secondary)"
            strokeWidth="1.5"
            strokeDasharray="3 3"
          />
          <rect x="240" y="8" width="80" height="18" rx="3" fill="var(--color-brand-secondary-10)" />
          <text
            x="280"
            y="21"
            fill="var(--color-brand-secondary)"
            fontSize="10"
            fontFamily="var(--font-mono)"
            textAnchor="middle"
            fontWeight="600"
          >
            TODAY
          </text>
          <circle cx="340" cy="72" r="4" fill="var(--color-accent-ent)" />
          <circle cx="420" cy="82" r="4" fill="var(--color-brand-secondary)" />
          <circle cx="500" cy="62" r="4" fill="var(--color-accent-ent)" />
        </svg>
        <div
          className="absolute bottom-0 left-0 right-0 flex justify-between text-[10px] text-[var(--color-text-tertiary)]"
          style={{ fontFamily: 'var(--font-mono)' }}
        >
          <span>Mar 27</span>
          <span>Apr 3</span>
          <span>Apr 10</span>
          <span>Apr 17</span>
          <span>Apr 24</span>
          <span>May 1</span>
          <span>May 8</span>
        </div>
      </div>
      <div
        className="mt-3.5 flex flex-wrap gap-4 rounded-lg bg-[var(--color-surface-inset)] px-3 py-2.5 text-[11px] text-[var(--color-text-secondary)]"
        style={{ fontFamily: 'var(--font-mono)' }}
      >
        <span>◆ Projected low: $58,100 · May 6</span>
        <span>◆ 3 revenue events</span>
        <span>◆ 1 payroll · Apr 30</span>
      </div>
    </Card>
  );
}

// ─── Agent proposals empty state ─────────────────────────────────────────
// Placeholder until a pending-proposals feed exists on the backend. Once
// agentProposals[] lands on MySpaceSummaryResponse, render <ProposalCard>
// rows here in compact mode.

function AgentProposalsCard() {
  return (
    <Card
      title="Agent proposals"
      subtitle="Pending your review"
      action={
        <span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--color-pending-light)] px-2.5 py-0.5 text-[11px] font-medium text-[var(--color-pending)]">
          <span className="h-1.5 w-1.5 rounded-full bg-[var(--color-pending)]" aria-hidden />
          0 pending
        </span>
      }
      padding={20}
    >
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
    </Card>
  );
}
