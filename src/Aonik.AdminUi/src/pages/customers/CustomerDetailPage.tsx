// Customer Detail — visual port of
// templates/aonik-admin-starterkit/screens/customer-detail.jsx, kept on top
// of the existing data wiring (customerService.get / getStats / listInsights
// + documentService) and the existing Finance sub-tab components.
//
// Differences from the template (called out so they don't read as gaps):
//   • Template's ARR / MRR / LTV / Runway / Open-orders KPIs are not on the
//     backend yet; we surface fields the API actually carries — Total
//     orders, Total paid, Outstanding, Last activity, Customer since.
//   • "Recent activity" is a placeholder card — no /customers/:id/activity
//     audit feed exists yet.
//   • Template's "Orders" tab is omitted because the orders endpoint can't
//     filter by party today.

import { useCallback, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
  AlertCircle,
  Building2,
  Download,
  FileText,
  Globe,
  Lightbulb,
  Plus,
  RefreshCw,
  Sparkles,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card as AonikCard, Pill, type PillTone } from '@/components/layout/aonik';
import { customerService } from '@/services/customerService';
import type {
  CustomerActivityEntry,
  CustomerInsightsResponse,
} from '@/services/customerService';
import { documentService } from '@/services/documentService';
import { orderService } from '@/services/orderService';
import type {
  CurrencyAmount,
  CustomerDetail,
  CustomerStats,
  DocumentListItem,
  OrderListItem,
} from '@/types';

import { AccountsSubTab } from './finance/AccountsSubTab';
import { BudgetsSubTab } from './finance/BudgetsSubTab';
import { CommitmentsSubTab } from './finance/CommitmentsSubTab';
import { FinancialGraphSubTab } from './finance/FinancialGraphSubTab';
import { TransactionsSubTab } from './finance/TransactionsSubTab';

// ─── Helpers ─────────────────────────────────────────────────────────────

const STATUS_TONE: Record<string, PillTone> = {
  Active: 'success',
  Pending: 'warning',
  Suspended: 'danger',
  Inactive: 'muted',
  Deactivated: 'muted',
};

const VERIFICATION_TONE: Record<string, PillTone> = {
  Verified: 'success',
  Pending: 'warning',
  ReReview: 'pending',
  Rejected: 'danger',
};

type TabKey = 'overview' | 'finance' | 'insights' | 'documents' | 'orders' | 'activity';
type FinanceSubKey = 'accounts' | 'transactions' | 'budgets' | 'commitments' | 'graph';

const TABS: Array<{ value: TabKey; label: string }> = [
  { value: 'overview', label: 'Overview' },
  { value: 'finance', label: 'Finance' },
  { value: 'orders', label: 'Orders' },
  { value: 'insights', label: 'Insights' },
  { value: 'documents', label: 'Documents' },
  { value: 'activity', label: 'Activity' },
];

const FINANCE_SUBS: Array<{ value: FinanceSubKey; label: string }> = [
  { value: 'accounts', label: 'Accounts' },
  { value: 'transactions', label: 'Transactions' },
  { value: 'budgets', label: 'Budgets' },
  { value: 'commitments', label: 'Commitments' },
  { value: 'graph', label: 'Financial graph' },
];

function formatDate(value?: string | null): string {
  if (!value) return '—';
  return new Date(value).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function formatDateTime(value?: string | null): string {
  if (!value) return '—';
  return new Date(value).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function formatRelative(value?: string | null): string {
  if (!value) return '—';
  const diff = Date.now() - new Date(value).getTime();
  const minutes = Math.round(diff / 60_000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.round(hours / 24);
  if (days < 30) return `${days}d ago`;
  return formatDate(value);
}

function formatCurrency(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency,
      maximumFractionDigits: 0,
    }).format(amount);
  } catch {
    return `${currency} ${Math.round(amount).toLocaleString()}`;
  }
}

function summariseAmounts(amounts?: CurrencyAmount[] | null): string {
  if (!amounts || amounts.length === 0) return '—';
  if (amounts.length === 1) {
    const entry = amounts[0];
    return formatCurrency(entry.amount, entry.currency);
  }
  return `${amounts.length} currencies`;
}

function deriveInitials(name?: string | null): string {
  if (!name) return 'C';
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0])
    .join('')
    .toUpperCase();
}

// ─── Page ────────────────────────────────────────────────────────────────

export function CustomerDetailPage() {
  const navigate = useNavigate();
  const { partyId } = useParams<{ partyId: string }>();

  const [customer, setCustomer] = useState<CustomerDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [stats, setStats] = useState<CustomerStats | null>(null);
  const [statsLoading, setStatsLoading] = useState(false);

  const [activeTab, setActiveTab] = useState<TabKey>('overview');
  const [financeSub, setFinanceSub] = useState<FinanceSubKey>('accounts');

  const [documents, setDocuments] = useState<DocumentListItem[]>([]);
  const [documentsLoading, setDocumentsLoading] = useState(false);
  const [documentsError, setDocumentsError] = useState<string | null>(null);

  const [insights, setInsights] = useState<CustomerInsightsResponse | null>(null);
  const [insightsLoading, setInsightsLoading] = useState(false);
  const [insightsError, setInsightsError] = useState<string | null>(null);

  const [orders, setOrders] = useState<OrderListItem[]>([]);
  const [ordersTotal, setOrdersTotal] = useState(0);
  const [ordersLoading, setOrdersLoading] = useState(false);
  const [ordersError, setOrdersError] = useState<string | null>(null);

  const [activity, setActivity] = useState<CustomerActivityEntry[]>([]);
  const [activityLoading, setActivityLoading] = useState(false);
  const [activityError, setActivityError] = useState<string | null>(null);

  const loadCustomer = useCallback(async () => {
    if (!partyId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await customerService.get(partyId);
      setCustomer(data);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load customer. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [partyId]);

  const loadStats = useCallback(async () => {
    if (!partyId) return;
    setStatsLoading(true);
    try {
      const data = await customerService.getStats(partyId);
      setStats(data);
    } catch {
      setStats(null);
    } finally {
      setStatsLoading(false);
    }
  }, [partyId]);

  const loadDocuments = useCallback(async () => {
    if (!partyId) return;
    setDocumentsLoading(true);
    setDocumentsError(null);
    try {
      const result = await documentService.list({
        ownerPartyId: partyId,
        pageNumber: 1,
        pageSize: 10,
      });
      setDocuments(result.items);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setDocumentsError(message || 'Failed to load documents.');
    } finally {
      setDocumentsLoading(false);
    }
  }, [partyId]);

  const loadOrders = useCallback(async () => {
    if (!partyId) return;
    setOrdersLoading(true);
    setOrdersError(null);
    try {
      const result = await orderService.listOrders({
        payerPartyId: partyId,
        pageNumber: 1,
        pageSize: 25,
      });
      setOrders(result.items);
      setOrdersTotal(result.totalCount);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setOrdersError(message || 'Failed to load orders.');
    } finally {
      setOrdersLoading(false);
    }
  }, [partyId]);

  const loadActivity = useCallback(async () => {
    if (!partyId) return;
    setActivityLoading(true);
    setActivityError(null);
    try {
      const result = await customerService.getActivity(partyId, 25);
      setActivity(result.items);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setActivityError(message || 'Failed to load activity.');
    } finally {
      setActivityLoading(false);
    }
  }, [partyId]);

  const loadInsights = useCallback(async () => {
    if (!partyId) return;
    setInsightsLoading(true);
    setInsightsError(null);
    try {
      const result = await customerService.listInsights(partyId);
      setInsights(result);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setInsightsError(message || 'Failed to load insights.');
    } finally {
      setInsightsLoading(false);
    }
  }, [partyId]);

  useEffect(() => {
    void loadCustomer();
    void loadStats();
  }, [loadCustomer, loadStats]);

  useEffect(() => {
    if (activeTab === 'documents') void loadDocuments();
    if (activeTab === 'insights') void loadInsights();
    if (activeTab === 'orders') void loadOrders();
    if (activeTab === 'overview' || activeTab === 'activity') void loadActivity();
  }, [activeTab, loadDocuments, loadInsights, loadOrders, loadActivity]);

  // ─── Loading / not-found states ───────────────────────────────────────

  if (loading) {
    return (
      <div className="flex flex-1 items-center justify-center">
        <div className="text-center">
          <RefreshCw className="mx-auto mb-3 h-8 w-8 animate-spin text-[var(--color-brand-primary)]" />
          <p className="text-sm text-[var(--color-text-secondary)]">Loading customer…</p>
        </div>
      </div>
    );
  }

  if (!customer) {
    return (
      <div className="flex flex-1 items-center justify-center">
        <div className="text-center">
          <AlertCircle className="mx-auto mb-3 h-12 w-12 text-[var(--color-error)]" />
          <h2 className="mb-2 text-xl font-semibold text-[var(--color-text-primary)]">
            Customer not found
          </h2>
          <p className="mb-4 text-sm text-[var(--color-text-secondary)]">
            The customer you're looking for doesn't exist or you don't have access.
          </p>
          <Button onClick={() => navigate('/customers')}>Back to Customers</Button>
        </div>
      </div>
    );
  }

  // ─── Derived values ───────────────────────────────────────────────────

  const contacts = customer.contacts ?? [];
  const addresses = customer.addresses ?? [];
  const consents = customer.consents ?? [];
  const externalAccounts = customer.externalAccounts ?? [];

  const primaryEmail = contacts.find((c) => c.type === 'Email' && c.isPrimary)?.value;
  const primaryPhone = contacts.find((c) => c.type === 'Phone' && c.isPrimary)?.value;
  const primaryAddress = addresses[0];

  const verificationStatus =
    customer.partyType === 'Business'
      ? customer.businessProfile?.kybStatus
      : customer.personProfile?.idvStatus;

  const registrationCode =
    customer.partyType === 'Business'
      ? customer.businessProfile?.registrationNumber
      : null;

  const profileSubtitle =
    customer.partyType === 'Business'
      ? customer.businessProfile?.industry || 'Business customer'
      : customer.personProfile?.occupation || 'Individual customer';

  const totalOrders = stats?.totalOrders;
  const totalPaidSummary = summariseAmounts(stats?.totalPaidByCurrency);
  const outstandingSummary = summariseAmounts(stats?.outstandingByCurrency);
  const trailingTwelveSummary = summariseAmounts(stats?.trailingTwelveMonthsByCurrency);
  const trailingThirtySummary = summariseAmounts(stats?.trailingThirtyDaysByCurrency);
  const openOrderCount = stats?.openOrderCount;
  const lastActivityAt = stats?.lastActivityAt || customer.updatedAt || customer.createdAt;

  // ─── Render ───────────────────────────────────────────────────────────

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      {error && (
        <div className="flex items-center gap-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
          <AlertCircle className="h-4 w-4 flex-none" />
          <span className="flex-1">{error}</span>
          <Button variant="outline" size="sm" onClick={() => void loadCustomer()}>
            <RefreshCw className="h-3 w-3" />
            Retry
          </Button>
        </div>
      )}

      {/* Header card */}
      <div className="flex items-center gap-5 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-5">
        <div
          className="flex h-[68px] w-[68px] flex-none items-center justify-center font-[family-name:var(--font-brand)] font-semibold leading-none text-white"
          style={{
            borderRadius: 14,
            fontSize: 26,
            background:
              'linear-gradient(135deg, var(--color-brand-primary) 0%, var(--color-brand-primary-dark) 100%)',
            letterSpacing: '-0.02em',
          }}
        >
          {deriveInitials(customer.displayName)}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2.5">
            <div className="font-[family-name:var(--font-brand)] text-[22px] font-bold tracking-[-0.01em] text-[var(--color-text-primary)]">
              {customer.displayName}
            </div>
            <Pill tone={STATUS_TONE[customer.status] ?? 'default'} dot>
              {customer.status}
            </Pill>
            {registrationCode && (
              <span className="rounded border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-2 py-0.5 font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
                {registrationCode}
              </span>
            )}
          </div>
          <div className="mt-1.5 flex flex-wrap gap-3.5 text-xs text-[var(--color-text-secondary)]">
            <span className="inline-flex items-center gap-1.5">
              <Building2 className="h-3 w-3" />
              {customer.partyType}
              {customer.customerTierCode ? ` · Tier ${customer.customerTierCode}` : ''}
              {profileSubtitle ? ` · ${profileSubtitle}` : ''}
            </span>
            {primaryAddress?.country && (
              <span className="inline-flex items-center gap-1.5">
                <Globe className="h-3 w-3" />
                {primaryAddress.country}
              </span>
            )}
            <span className="font-[family-name:var(--font-mono)]">
              customer since · {formatDate(customer.createdAt)}
            </span>
          </div>
        </div>
        <div className="flex flex-none gap-1.5">
          <Button
            variant="outline"
            size="sm"
            onClick={async () => {
              if (!partyId) return;
              try {
                await customerService.exportData(partyId);
              } catch {
                /* swallow — error toast handled by service in future iterations */
              }
            }}
          >
            <Download className="h-3 w-3" />
            Export
          </Button>
          <Button variant="outline" size="sm" onClick={() => setActiveTab('insights')}>
            <Sparkles className="h-3 w-3" />
            Generate insight
          </Button>
          <Button size="sm" disabled>
            <Plus className="h-3 w-3" />
            New order
          </Button>
        </div>
      </div>

      {/* KPI strip — backend-grounded mappings, no faked metrics */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
        <KpiCell
          label="T12M revenue"
          value={statsLoading && !stats ? '—' : trailingTwelveSummary}
          dot="var(--color-brand-primary)"
          sub="captured · trailing 12mo"
        />
        <KpiCell
          label="T30D revenue"
          value={statsLoading && !stats ? '—' : trailingThirtySummary}
          dot="var(--color-success)"
          sub="rough monthly run rate"
        />
        <KpiCell
          label="LTV"
          value={statsLoading && !stats ? '—' : totalPaidSummary}
          dot="var(--color-accent-team)"
          sub={
            totalOrders != null
              ? `${totalOrders.toLocaleString()} order${totalOrders === 1 ? '' : 's'}`
              : 'lifetime captured'
          }
        />
        <KpiCell
          label="Open orders"
          value={
            statsLoading && openOrderCount === undefined
              ? '—'
              : openOrderCount != null
              ? openOrderCount.toLocaleString()
              : '—'
          }
          dot="var(--color-warning)"
          sub={
            stats && stats.outstandingByCurrency.length > 0
              ? `${outstandingSummary} open`
              : 'no open work'
          }
        />
        <KpiCell
          label="Last activity"
          value={formatRelative(lastActivityAt)}
          dot="var(--color-brand-secondary)"
          sub={lastActivityAt ? formatDate(lastActivityAt) : 'no activity yet'}
        />
      </div>

      {/* Tabs */}
      <div className="flex gap-0.5 border-b border-[var(--color-border-light)] px-0.5">
        {TABS.map((tab) => {
          const isActive = activeTab === tab.value;
          return (
            <button
              key={tab.value}
              type="button"
              onClick={() => setActiveTab(tab.value)}
              className={
                'h-[38px] -mb-px border-b-2 px-3.5 text-[13px] transition-colors ' +
                (isActive
                  ? 'border-[var(--color-brand-primary)] font-semibold text-[var(--color-text-primary)]'
                  : 'border-transparent text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]')
              }
            >
              {tab.label}
            </button>
          );
        })}
      </div>

      {/* Tab content */}
      {activeTab === 'overview' && (
        <OverviewTab
          customer={customer}
          consents={consents}
          contacts={{ primaryEmail, primaryPhone }}
          primaryAddress={primaryAddress}
          externalAccounts={externalAccounts}
          verificationStatus={verificationStatus}
          activity={activity.slice(0, 5)}
          activityLoading={activityLoading}
          activityError={activityError}
        />
      )}

      {activeTab === 'finance' && (
        <FinanceTab
          financeSub={financeSub}
          onSubChange={setFinanceSub}
          userId={customer.userId}
        />
      )}

      {activeTab === 'insights' && (
        <InsightsTab
          insights={insights}
          loading={insightsLoading}
          error={insightsError}
        />
      )}

      {activeTab === 'documents' && (
        <DocumentsTab
          documents={documents}
          loading={documentsLoading}
          error={documentsError}
          onView={(id) => navigate(`/compliance/documents/${id}`)}
        />
      )}

      {activeTab === 'orders' && (
        <OrdersTab
          orders={orders}
          totalCount={ordersTotal}
          loading={ordersLoading}
          error={ordersError}
          onView={(id) => navigate(`/orders/${id}`)}
          onReload={() => void loadOrders()}
        />
      )}

      {activeTab === 'activity' && (
        <ActivityTab
          entries={activity}
          loading={activityLoading}
          error={activityError}
          onView={(linkPath) => navigate(linkPath)}
        />
      )}
    </div>
  );
}

// ─── KPI cell ────────────────────────────────────────────────────────────

function KpiCell({
  label,
  value,
  sub,
  dot,
}: {
  label: string;
  value: ReactNode;
  sub?: ReactNode;
  dot: string;
}) {
  return (
    <div className="rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] p-3.5">
      <div className="flex items-center gap-1.5 text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
        <span className="h-1.5 w-1.5 rounded-full" style={{ background: dot }} />
        {label}
      </div>
      <div className="mt-1 font-[family-name:var(--font-mono)] text-[20px] font-semibold leading-none text-[var(--color-text-primary)]">
        {value}
      </div>
      {sub && (
        <div className="mt-1 text-[11px] text-[var(--color-text-secondary)]">{sub}</div>
      )}
    </div>
  );
}

// ─── Overview tab ────────────────────────────────────────────────────────

interface OverviewTabProps {
  customer: CustomerDetail;
  consents: CustomerDetail['consents'];
  contacts: { primaryEmail?: string; primaryPhone?: string };
  primaryAddress?: CustomerDetail['addresses'][number];
  externalAccounts: CustomerDetail['externalAccounts'];
  verificationStatus?: string | null;
  activity: CustomerActivityEntry[];
  activityLoading: boolean;
  activityError: string | null;
}

function OverviewTab({
  customer,
  consents,
  contacts,
  primaryAddress,
  externalAccounts,
  verificationStatus,
  activity,
  activityLoading,
  activityError,
}: OverviewTabProps) {
  const detailRows: Array<[string, ReactNode, boolean?]> = customer.partyType === 'Business'
    ? [
        ['Legal name', customer.displayName],
        ['Type', `${customer.partyType}${customer.customerTierCode ? ` · Tier ${customer.customerTierCode}` : ''}`],
        ['Industry', customer.businessProfile?.industry || '—'],
        ['Registration', customer.businessProfile?.registrationNumber || '—', true],
        ['Incorporation', customer.businessProfile?.incorporationCountry || '—'],
        ['Email', contacts.primaryEmail || '—'],
        ['Phone', contacts.primaryPhone || '—', true],
        [
          'Registered address',
          primaryAddress
            ? [primaryAddress.line1, primaryAddress.city, primaryAddress.country]
                .filter(Boolean)
                .join(', ')
            : '—',
        ],
      ]
    : [
        ['Full name', customer.displayName],
        ['Type', customer.partyType],
        ['Date of birth', formatDate(customer.personProfile?.dob ?? null)],
        ['Nationality', customer.personProfile?.nationality || '—'],
        ['Occupation', customer.personProfile?.occupation || '—'],
        ['Email', contacts.primaryEmail || '—'],
        ['Phone', contacts.primaryPhone || '—', true],
        [
          'Address',
          primaryAddress
            ? [primaryAddress.line1, primaryAddress.city, primaryAddress.country]
                .filter(Boolean)
                .join(', ')
            : '—',
        ],
      ];

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      <AonikCard title="Details">
        <div className="flex flex-col">
          {detailRows.map(([label, value, mono], idx) => (
            <div
              key={String(label)}
              className={
                'grid gap-3 py-2 ' +
                (idx < detailRows.length - 1
                  ? 'border-b border-[var(--color-border-light)]'
                  : '')
              }
              style={{ gridTemplateColumns: '140px 1fr' }}
            >
              <span className="text-[11px] tracking-[0.02em] text-[var(--color-text-tertiary)]">
                {label}
              </span>
              <span
                className={
                  'text-[12.5px] text-[var(--color-text-primary)] ' +
                  (mono ? 'font-[family-name:var(--font-mono)]' : '')
                }
              >
                {value}
              </span>
            </div>
          ))}
        </div>
      </AonikCard>

      <AonikCard
        title="Compliance"
        subtitle="Verification · consents"
        action={
          verificationStatus ? (
            <Pill tone={VERIFICATION_TONE[verificationStatus] ?? 'muted'} dot>
              {verificationStatus}
            </Pill>
          ) : (
            <Pill tone="muted">Unverified</Pill>
          )
        }
      >
        <div className="flex flex-col gap-2.5">
          {consents.length === 0 ? (
            <p className="text-xs text-[var(--color-text-tertiary)]">No consents recorded.</p>
          ) : (
            consents.map((consent) => (
              <div
                key={consent.consentId}
                className="flex items-center gap-2.5 py-1"
              >
                <span
                  className="h-2 w-2 rounded-full"
                  style={{
                    background: consent.revokedAt
                      ? 'var(--color-text-tertiary)'
                      : 'var(--color-success)',
                  }}
                />
                <span className="flex-1 text-[12.5px] text-[var(--color-text-primary)]">
                  {consent.consentType}
                </span>
                <Pill tone={consent.revokedAt ? 'muted' : 'success'} size="sm">
                  {consent.revokedAt ? 'Revoked' : 'Active'}
                </Pill>
                <span className="min-w-[60px] text-right font-[family-name:var(--font-mono)] text-[10px] text-[var(--color-text-tertiary)]">
                  {formatDate(consent.grantedAt)}
                </span>
              </div>
            ))
          )}
        </div>
      </AonikCard>

      {externalAccounts.length > 0 && (
        <AonikCard title="External accounts" subtitle={`${externalAccounts.length} linked`}>
          <div className="flex flex-col gap-2">
            {externalAccounts.map((acct) => (
              <div
                key={acct.partyAccountId}
                className="flex items-center gap-3 rounded-md border border-[var(--color-border-light)] p-3"
              >
                <Building2 className="h-4 w-4 text-[var(--color-text-tertiary)]" />
                <div className="min-w-0 flex-1">
                  <div className="text-[13px] font-medium text-[var(--color-text-primary)]">
                    {acct.accountType}
                  </div>
                  <div className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
                    {acct.maskedIdentifier}
                    {acct.providerRef ? ` · ${acct.providerRef}` : ''}
                  </div>
                </div>
                <Pill tone={VERIFICATION_TONE[acct.verificationStatus] ?? 'muted'} size="sm">
                  {acct.verificationStatus}
                </Pill>
              </div>
            ))}
          </div>
        </AonikCard>
      )}

      <AonikCard
        title="Recent activity"
        subtitle="Most recent 5 events across orders, payments, audit, and documents"
        className="lg:col-span-2"
      >
        <ActivityList
          entries={activity}
          loading={activityLoading}
          error={activityError}
        />
      </AonikCard>
    </div>
  );
}

// ─── Finance tab ─────────────────────────────────────────────────────────

interface FinanceTabProps {
  financeSub: FinanceSubKey;
  onSubChange: (sub: FinanceSubKey) => void;
  userId: string | null | undefined;
}

function FinanceTab({ financeSub, onSubChange, userId }: FinanceTabProps) {
  if (!userId) {
    return (
      <AonikCard>
        <div className="flex flex-col items-center justify-center py-10 text-center">
          <Globe className="mb-2 h-8 w-8 text-[var(--color-text-tertiary)]" />
          <p className="text-sm text-[var(--color-text-tertiary)]">
            No user account linked to this customer.
          </p>
          <p className="text-xs text-[var(--color-text-tertiary)]">
            Finance views require a linked Aonik user.
          </p>
        </div>
      </AonikCard>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex gap-0.5">
        {FINANCE_SUBS.map((sub) => {
          const isActive = financeSub === sub.value;
          return (
            <button
              key={sub.value}
              type="button"
              onClick={() => onSubChange(sub.value)}
              className={
                'inline-flex h-[30px] items-center rounded-md px-3 text-xs transition-colors ' +
                (isActive
                  ? 'bg-[var(--color-brand-primary-10)] font-semibold text-[var(--color-brand-primary)]'
                  : 'text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]')
              }
            >
              {sub.label}
            </button>
          );
        })}
      </div>

      {financeSub === 'accounts' && <AccountsSubTab key="accounts" userId={userId} />}
      {financeSub === 'transactions' && <TransactionsSubTab key="transactions" userId={userId} />}
      {financeSub === 'budgets' && <BudgetsSubTab key="budgets" userId={userId} />}
      {financeSub === 'commitments' && <CommitmentsSubTab key="commitments" userId={userId} />}
      {financeSub === 'graph' && <FinancialGraphSubTab key="graph" userId={userId} />}
    </div>
  );
}

// ─── Insights tab ────────────────────────────────────────────────────────

interface InsightsTabProps {
  insights: CustomerInsightsResponse | null;
  loading: boolean;
  error: string | null;
}

function InsightsTab({ insights, loading, error }: InsightsTabProps) {
  if (loading && !insights) {
    return (
      <AonikCard>
        <div className="flex items-center justify-center py-10">
          <RefreshCw className="h-6 w-6 animate-spin text-[var(--color-brand-primary)]" />
        </div>
      </AonikCard>
    );
  }

  if (error) {
    return (
      <AonikCard>
        <div className="flex items-center gap-2 text-sm text-[var(--color-error)]">
          <AlertCircle className="h-4 w-4" />
          {error}
        </div>
      </AonikCard>
    );
  }

  if (!insights?.aiSummary && !insights?.snapshot) {
    return (
      <AonikCard>
        <div className="flex flex-col items-center justify-center py-10 text-center">
          <Lightbulb className="mb-2 h-8 w-8 text-[var(--color-text-tertiary)]" />
          <p className="text-sm text-[var(--color-text-secondary)]">
            No insights generated yet for this customer.
          </p>
          <p className="mt-1 text-xs text-[var(--color-text-tertiary)]">
            Insights appear after the snapshot pipeline and AI summary run.
          </p>
        </div>
      </AonikCard>
    );
  }

  const summary = insights.aiSummary;

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1.4fr_1fr]">
      {summary ? (
        <AonikCard
          title="AI summary"
          subtitle={`Generated ${formatRelative(summary.createdUtc)} · v${summary.narrativeVersion}`}
          action={<Pill tone="info" dot>fresh</Pill>}
        >
          <div className="text-[15px] font-semibold leading-snug text-[var(--color-text-primary)]">
            {summary.headline}
          </div>
          <p className="mt-2.5 text-[13px] leading-relaxed text-[var(--color-text-secondary)]">
            {summary.summary}
          </p>

          {summary.keyObservations.length > 0 && (
            <>
              <SectionEyebrow>Key observations</SectionEyebrow>
              <ul className="ml-4 list-disc space-y-1.5 text-[12.5px] text-[var(--color-text-primary)]">
                {summary.keyObservations.map((obs, i) => (
                  <li key={i}>{obs}</li>
                ))}
              </ul>
            </>
          )}

          {(summary.positivePatterns.length > 0 || summary.riskPatterns.length > 0) && (
            <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2">
              {summary.positivePatterns.length > 0 && (
                <div>
                  <div className="text-[11px] font-semibold uppercase tracking-[0.06em] text-[var(--color-success)]">
                    Positive patterns
                  </div>
                  <ul className="mt-1.5 ml-4 list-disc space-y-1 text-[12px] text-[var(--color-text-primary)]">
                    {summary.positivePatterns.map((p, i) => (
                      <li key={i}>{p}</li>
                    ))}
                  </ul>
                </div>
              )}
              {summary.riskPatterns.length > 0 && (
                <div>
                  <div className="text-[11px] font-semibold uppercase tracking-[0.06em] text-[var(--color-danger)]">
                    Risk patterns
                  </div>
                  <ul className="mt-1.5 ml-4 list-disc space-y-1 text-[12px] text-[var(--color-text-primary)]">
                    {summary.riskPatterns.map((p, i) => (
                      <li key={i}>{p}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}

          {summary.recommendedFocusAreas.length > 0 && (
            <>
              <SectionEyebrow>Recommended focus</SectionEyebrow>
              <div className="flex flex-wrap gap-1.5">
                {summary.recommendedFocusAreas.map((area, i) => (
                  <Pill key={i} tone="info" size="sm">
                    {area}
                  </Pill>
                ))}
              </div>
            </>
          )}

          {summary.caveats.length > 0 && (
            <div className="mt-4 rounded-md bg-[var(--color-surface-inset)] p-3">
              <SectionEyebrow inset>Caveats</SectionEyebrow>
              <ul className="space-y-1 text-[11px] text-[var(--color-text-tertiary)]">
                {summary.caveats.map((c, i) => (
                  <li key={i}>{c}</li>
                ))}
              </ul>
            </div>
          )}
        </AonikCard>
      ) : null}

      {insights.snapshot && (
        <AonikCard
          title="Snapshot"
          subtitle={`As of ${formatDateTime(insights.snapshot.asOfUtc)}`}
          action={
            insights.snapshot.isPartial ? (
              <Pill tone="warning" size="sm">Partial</Pill>
            ) : null
          }
        >
          {insights.snapshot.topSignalTitle && (
            <>
              <div className="text-[14px] font-semibold text-[var(--color-text-primary)]">
                {insights.snapshot.topSignalTitle}
              </div>
              {insights.snapshot.topSignalDescription && (
                <p className="mt-1 text-[13px] text-[var(--color-text-secondary)]">
                  {insights.snapshot.topSignalDescription}
                </p>
              )}
            </>
          )}
          {insights.snapshot.cashflowStressLevel &&
            insights.snapshot.cashflowStressLevel !== 'Low' && (
              <div className="mt-3 text-sm text-[var(--color-text-secondary)]">
                Cashflow stress:{' '}
                <span className="font-medium">{insights.snapshot.cashflowStressLevel}</span>
              </div>
            )}
        </AonikCard>
      )}
    </div>
  );
}

function SectionEyebrow({ children, inset }: { children: ReactNode; inset?: boolean }) {
  return (
    <div
      className={
        'text-[11px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)] ' +
        (inset ? 'mb-1.5' : 'mb-1.5 mt-4')
      }
    >
      {children}
    </div>
  );
}

// ─── Documents tab ───────────────────────────────────────────────────────

interface DocumentsTabProps {
  documents: DocumentListItem[];
  loading: boolean;
  error: string | null;
  onView: (id: string) => void;
}

function DocumentsTab({ documents, loading, error, onView }: DocumentsTabProps) {
  return (
    <AonikCard
      title="Documents"
      subtitle="Recent compliance uploads"
      action={
        <Link
          to="/compliance/documents"
          className="text-xs text-[var(--color-brand-primary)] hover:underline"
        >
          View all
        </Link>
      }
    >
      {error && (
        <div className="mb-3 rounded border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
          {error}
        </div>
      )}

      {loading ? (
        <div className="flex items-center justify-center py-6">
          <RefreshCw className="h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
        </div>
      ) : documents.length === 0 ? (
        <div className="py-6 text-center">
          <FileText className="mx-auto mb-2 h-8 w-8 text-[var(--color-text-tertiary)]" />
          <p className="text-sm text-[var(--color-text-secondary)]">No documents recorded.</p>
        </div>
      ) : (
        <div className="flex flex-col">
          {documents.map((doc, idx) => {
            const tone =
              VERIFICATION_TONE[doc.status] ??
              (doc.status === 'Rejected' ? 'danger' : 'muted');
            const isLast = idx === documents.length - 1;
            return (
              <div
                key={doc.documentId}
                className={
                  'grid items-center gap-3 py-3 ' +
                  (isLast ? '' : 'border-b border-[var(--color-border-light)]')
                }
                style={{ gridTemplateColumns: '24px 1fr auto auto auto auto' }}
              >
                <FileText className="h-[18px] w-[18px] text-[var(--color-text-tertiary)]" />
                <div className="min-w-0">
                  <div className="truncate text-[13px] font-medium text-[var(--color-text-primary)]">
                    {doc.documentType}
                  </div>
                  <div className="text-[11px] text-[var(--color-text-tertiary)]">
                    Reference {doc.referenceNumber || '—'}
                  </div>
                </div>
                <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
                  uploaded {formatDate(doc.issuedOn)}
                </span>
                <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
                  expires {formatDate(doc.expiresOn)}
                </span>
                <Pill tone={tone} dot>
                  {doc.status}
                </Pill>
                <button
                  type="button"
                  onClick={() => onView(doc.documentId)}
                  className="hover-halo"
                  aria-label={`View ${doc.documentType}`}
                >
                  <Download className="h-[13px] w-[13px]" />
                </button>
              </div>
            );
          })}
        </div>
      )}
    </AonikCard>
  );
}

// ─── Orders tab ──────────────────────────────────────────────────────────

const ORDER_STATUS_TONE: Record<string, PillTone> = {
  Complete: 'success',
  Settled: 'success',
  Captured: 'success',
  Submitted: 'info',
  Active: 'info',
  Pending: 'warning',
  AwaitingFunds: 'warning',
  Cancelled: 'muted',
  Failed: 'danger',
  Expired: 'muted',
};

interface OrdersTabProps {
  orders: OrderListItem[];
  totalCount: number;
  loading: boolean;
  error: string | null;
  onView: (orderId: string) => void;
  onReload: () => void;
}

function OrdersTab({ orders, totalCount, loading, error, onView, onReload }: OrdersTabProps) {
  return (
    <AonikCard
      title="Orders"
      subtitle={
        totalCount > 0
          ? `${totalCount.toLocaleString()} total · most recent ${Math.min(orders.length, totalCount)}`
          : 'Bill payments, payouts, and collections from this customer'
      }
      action={
        <button
          type="button"
          onClick={onReload}
          className="text-xs text-[var(--color-brand-primary)] hover:underline"
        >
          Refresh
        </button>
      }
    >
      {error && (
        <div className="mb-3 rounded border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
          {error}
        </div>
      )}

      {loading ? (
        <div className="flex items-center justify-center py-6">
          <RefreshCw className="h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
        </div>
      ) : orders.length === 0 ? (
        <div className="py-6 text-center">
          <FileText className="mx-auto mb-2 h-8 w-8 text-[var(--color-text-tertiary)]" />
          <p className="text-sm text-[var(--color-text-secondary)]">
            No orders recorded for this customer yet.
          </p>
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-[var(--color-border-light)] text-left text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                <th className="px-2 py-2.5">Order</th>
                <th className="px-2 py-2.5">Date</th>
                <th className="px-2 py-2.5">Type</th>
                <th className="px-2 py-2.5">Status</th>
                <th className="px-2 py-2.5 text-right">Amount</th>
              </tr>
            </thead>
            <tbody>
              {orders.map((order, idx) => {
                const isLast = idx === orders.length - 1;
                const tone = ORDER_STATUS_TONE[order.status] ?? 'default';
                return (
                  <tr
                    key={order.orderId}
                    onClick={() => onView(order.orderId)}
                    className={
                      'cursor-pointer transition-colors hover:bg-[var(--color-surface-inset)] ' +
                      (isLast ? '' : 'border-b border-[var(--color-border-light)]')
                    }
                  >
                    <td className="px-2 py-2.5">
                      <span className="font-[family-name:var(--font-mono)] text-[11px] font-medium text-[var(--color-brand-primary)]">
                        ORD-{order.orderId.replace(/-/g, '').slice(0, 8).toUpperCase()}
                      </span>
                    </td>
                    <td className="px-2 py-2.5">
                      <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
                        {formatDate(order.createdAt)}
                      </span>
                    </td>
                    <td className="px-2 py-2.5">
                      <span className="text-[12.5px] text-[var(--color-text-primary)]">
                        {order.orderType}
                      </span>
                    </td>
                    <td className="px-2 py-2.5">
                      <Pill tone={tone} dot size="sm">
                        {order.status}
                      </Pill>
                    </td>
                    <td className="px-2 py-2.5 text-right">
                      <span className="font-[family-name:var(--font-mono)] text-[12.5px] font-medium text-[var(--color-text-primary)]">
                        {formatCurrency(order.totalAmountIn, order.originCurrency)}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </AonikCard>
  );
}

// ─── Activity feed ───────────────────────────────────────────────────────

const ACTIVITY_KIND_VISUAL: Record<
  string,
  { icon: typeof Sparkles; color: string }
> = {
  order_created: { icon: Plus, color: 'var(--color-brand-secondary)' },
  order_updated: { icon: RefreshCw, color: 'var(--color-text-secondary)' },
  payment_captured: { icon: Sparkles, color: 'var(--color-success)' },
  document_uploaded: { icon: FileText, color: 'var(--color-text-secondary)' },
  audit_log: { icon: Lightbulb, color: 'var(--color-warning)' },
};

function ActivityList({
  entries,
  loading,
  error,
  onView,
}: {
  entries: CustomerActivityEntry[];
  loading: boolean;
  error: string | null;
  onView?: (linkPath: string) => void;
}) {
  if (error) {
    return (
      <div className="flex items-center gap-2 text-sm text-[var(--color-error)]">
        <AlertCircle className="h-4 w-4" />
        {error}
      </div>
    );
  }

  if (loading && entries.length === 0) {
    return (
      <div className="flex items-center justify-center py-6">
        <RefreshCw className="h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
      </div>
    );
  }

  if (entries.length === 0) {
    return (
      <div className="py-6 text-center text-sm text-[var(--color-text-tertiary)]">
        No activity recorded for this customer yet.
      </div>
    );
  }

  return (
    <div className="flex flex-col">
      {entries.map((entry, idx) => {
        const visual =
          ACTIVITY_KIND_VISUAL[entry.kind] ?? {
            icon: Sparkles,
            color: 'var(--color-text-secondary)',
          };
        const Icon = visual.icon;
        const isLast = idx === entries.length - 1;
        const clickable = onView && entry.linkPath;
        return (
          <div
            key={`${entry.timestamp}-${idx}`}
            onClick={clickable ? () => onView!(entry.linkPath!) : undefined}
            role={clickable ? 'button' : undefined}
            tabIndex={clickable ? 0 : undefined}
            onKeyDown={
              clickable
                ? (e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      onView!(entry.linkPath!);
                    }
                  }
                : undefined
            }
            className={
              'grid items-center gap-2.5 py-2.5 ' +
              (isLast ? '' : 'border-b border-[var(--color-border-light)] ') +
              (clickable
                ? 'cursor-pointer transition-colors hover:bg-[var(--color-surface-inset)] -mx-2 px-2 rounded-md'
                : '')
            }
            style={{ gridTemplateColumns: '28px 1fr auto' }}
          >
            <div
              className="flex h-7 w-7 items-center justify-center rounded-md"
              style={{
                background: `${visual.color}1f`,
                color: visual.color,
              }}
            >
              <Icon className="h-3.5 w-3.5" />
            </div>
            <div className="min-w-0">
              <div className="truncate text-[12.5px] text-[var(--color-text-primary)]">
                {entry.title}
              </div>
              {entry.subtitle && (
                <div className="truncate text-[11px] text-[var(--color-text-tertiary)]">
                  {entry.subtitle}
                </div>
              )}
            </div>
            <span className="font-[family-name:var(--font-mono)] text-[10px] text-[var(--color-text-tertiary)]">
              {formatRelative(entry.timestamp)}
            </span>
          </div>
        );
      })}
    </div>
  );
}

function ActivityTab({
  entries,
  loading,
  error,
  onView,
}: {
  entries: CustomerActivityEntry[];
  loading: boolean;
  error: string | null;
  onView: (linkPath: string) => void;
}) {
  return (
    <AonikCard
      title="Activity feed"
      subtitle="Up to the most recent 25 events across all sources"
    >
      <ActivityList
        entries={entries}
        loading={loading}
        error={error}
        onView={onView}
      />
    </AonikCard>
  );
}
