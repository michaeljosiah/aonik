// Approvals queue — visual port of
// templates/aonik-admin-starterkit/screens/approvals.jsx, scoped to the
// existing agent proposal pipeline.
//
// Differences from the template, called out so they don't read as gaps:
//   • Single-approver flow only — Proposal has no approval-chain entity.
//     Multi-approver, SLAs, and approval progress bars are not rendered.
//   • Type rail groups by AgentDomain (the field we have) rather than the
//     template's cross-product types (Orders/Refunds/KYB/Payouts/Policy).
//     When other product domains start emitting proposals their domains
//     will appear here automatically.
//   • Comment composer is omitted — there's no thread model on Proposal.
//   • "Triggering policy" / "expires in N min" come straight from the
//     proposal's RiskTier and CreatedAt; no real policy registry yet.

import { useCallback, useEffect, useMemo, useState } from 'react';
import { toast } from 'sonner';
import {
  AlertCircle,
  Check,
  Clock,
  RefreshCw,
  Sparkles,
  X,
} from 'lucide-react';

import {
  AgentAvatar,
  Pill,
  type PillTone,
} from '@/components/layout/aonik';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { agentProposalsService } from '@/services/agentProposalsService';
import type {
  ProposalDetailResponse,
  ProposalListItem,
} from '@/types';

// ─── Helpers ─────────────────────────────────────────────────────────────

function formatRelative(value: string): string {
  const diff = Date.now() - new Date(value).getTime();
  const minutes = Math.round(diff / 60_000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} hr ago`;
  const days = Math.round(hours / 24);
  return `${days} day${days === 1 ? '' : 's'} ago`;
}

function shortProposalId(id: string): string {
  const compact = id.replace(/-/g, '').slice(0, 8).toUpperCase();
  return `APR-${compact}`;
}

const RISK_TONE: Record<string, PillTone> = {
  Low: 'success',
  Medium: 'warning',
  High: 'danger',
};

const URGENCY_LABEL: Record<string, string> = {
  Low: 'low urgency',
  Medium: 'medium urgency',
  High: 'high urgency',
};

function tryFormatPayload(payloadJson: string): string {
  if (!payloadJson) return '';
  try {
    return JSON.stringify(JSON.parse(payloadJson), null, 2);
  } catch {
    return payloadJson;
  }
}

// Flatten a JSON object into [label, value] rows for the typed-payload
// section. Nested values render as compact JSON; arrays show length.
function payloadRows(payloadJson: string): Array<[string, string]> {
  if (!payloadJson) return [];
  try {
    const parsed = JSON.parse(payloadJson);
    if (parsed == null || typeof parsed !== 'object') {
      return [['Payload', String(parsed)]];
    }
    return Object.entries(parsed).map(([key, value]) => {
      if (value == null) return [key, '—'];
      if (Array.isArray(value)) return [key, `${value.length} items`];
      if (typeof value === 'object') return [key, JSON.stringify(value)];
      return [key, String(value)];
    });
  } catch {
    return [['Payload', payloadJson]];
  }
}

// ─── Page ────────────────────────────────────────────────────────────────

export function ApprovalsPage() {
  const [items, setItems] = useState<ProposalListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [activeDomain, setActiveDomain] = useState<string>('all');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<ProposalDetailResponse | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [actioning, setActioning] = useState<'approve' | 'dismiss' | null>(null);

  const loadList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await agentProposalsService.list({ take: 100 });
      setItems(result.items);
      setTotal(result.total);
      // Pre-select the first row when the list lands so the detail panel
      // is never empty.
      if (result.items.length > 0) {
        setSelectedId((current) => current ?? result.items[0].id);
      } else {
        setSelectedId(null);
      }
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load proposals.');
    } finally {
      setLoading(false);
      setInitialLoad(false);
    }
  }, []);

  useEffect(() => {
    void loadList();
  }, [loadList]);

  useEffect(() => {
    if (!selectedId) {
      setDetail(null);
      return;
    }
    let cancelled = false;
    setDetailLoading(true);
    setDetailError(null);
    agentProposalsService
      .get(selectedId)
      .then((result) => {
        if (cancelled) return;
        setDetail(result);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        const message =
          err && typeof err === 'object' && 'userMessage' in err
            ? String((err as { userMessage?: string }).userMessage ?? '')
            : '';
        setDetailError(message || 'Failed to load proposal detail.');
      })
      .finally(() => {
        if (!cancelled) setDetailLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [selectedId]);

  // ─── Domains rail ─────────────────────────────────────────────────────

  const domainCounts = useMemo(() => {
    const counts = new Map<string, number>();
    for (const item of items) {
      const key = item.agentDomain || 'Other';
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }
    return Array.from(counts.entries())
      .sort((a, b) => b[1] - a[1])
      .map(([domain, count]) => ({ domain, count }));
  }, [items]);

  const filtered = useMemo(() => {
    if (activeDomain === 'all') return items;
    return items.filter((item) => (item.agentDomain || 'Other') === activeDomain);
  }, [items, activeDomain]);

  // Keep the selection valid when the filter narrows the list.
  useEffect(() => {
    if (selectedId && !filtered.some((item) => item.id === selectedId)) {
      setSelectedId(filtered[0]?.id ?? null);
    }
  }, [filtered, selectedId]);

  const current = filtered.find((item) => item.id === selectedId) ?? filtered[0] ?? null;

  // ─── Actions ──────────────────────────────────────────────────────────

  const handleApprove = useCallback(async () => {
    if (!detail || actioning) return;
    setActioning('approve');
    // Optimistically remove the row from the queue.
    const removed = items.find((i) => i.id === detail.id);
    setItems((prev) => prev.filter((i) => i.id !== detail.id));
    setTotal((prev) => Math.max(0, prev - 1));
    try {
      await agentProposalsService.approve(detail.id);
      toast.success(`Approved ${shortProposalId(detail.id)}`);
    } catch (err) {
      // Roll back.
      if (removed) {
        setItems((prev) => [removed, ...prev]);
        setTotal((prev) => prev + 1);
      }
      const message = err instanceof Error ? err.message : 'Approve failed';
      toast.error(message);
    } finally {
      setActioning(null);
    }
  }, [detail, items, actioning]);

  const handleDismiss = useCallback(async () => {
    if (!detail || actioning) return;
    setActioning('dismiss');
    const removed = items.find((i) => i.id === detail.id);
    setItems((prev) => prev.filter((i) => i.id !== detail.id));
    setTotal((prev) => Math.max(0, prev - 1));
    try {
      await agentProposalsService.dismiss(detail.id);
      toast.success(`Dismissed ${shortProposalId(detail.id)}`);
    } catch (err) {
      if (removed) {
        setItems((prev) => [removed, ...prev]);
        setTotal((prev) => prev + 1);
      }
      const message = err instanceof Error ? err.message : 'Dismiss failed';
      toast.error(message);
    } finally {
      setActioning(null);
    }
  }, [detail, items, actioning]);

  // ─── Render ───────────────────────────────────────────────────────────

  if (initialLoad) {
    return <PageLoadingScreen message="Loading approvals" />;
  }

  return (
    <div
      className="grid h-full overflow-hidden"
      style={{ gridTemplateColumns: '220px 380px 1fr' }}
    >
      <DomainRail
        domainCounts={domainCounts}
        activeDomain={activeDomain}
        onSelect={setActiveDomain}
        totalCount={total}
      />

      <ListColumn
        items={filtered}
        selectedId={current?.id ?? null}
        loading={loading}
        error={error}
        onSelect={setSelectedId}
        onRefresh={() => void loadList()}
      />

      <DetailColumn
        list={current}
        detail={detail}
        loading={detailLoading}
        error={detailError}
        actioning={actioning}
        onApprove={handleApprove}
        onDismiss={handleDismiss}
      />
    </div>
  );
}

// ─── Domain rail ─────────────────────────────────────────────────────────

interface DomainRailProps {
  domainCounts: { domain: string; count: number }[];
  activeDomain: string;
  onSelect: (domain: string) => void;
  totalCount: number;
}

function DomainRail({ domainCounts, activeDomain, onSelect, totalCount }: DomainRailProps) {
  return (
    <div className="flex flex-col gap-0.5 overflow-auto border-r border-border bg-muted p-3">
      <div className="px-2 py-1 text-xs font-medium text-muted-foreground">
        By domain
      </div>
      <RailButton
        label="All"
        count={totalCount}
        active={activeDomain === 'all'}
        onClick={() => onSelect('all')}
      />
      {domainCounts.map(({ domain, count }) => (
        <RailButton
          key={domain}
          label={domain}
          count={count}
          active={activeDomain === domain}
          onClick={() => onSelect(domain)}
        />
      ))}

      <div className="my-3 h-px bg-border" />
      <div className="px-2 py-1 text-xs font-medium text-muted-foreground">
        Filters
      </div>
      <button
        type="button"
        disabled
        className="flex items-center gap-2 rounded-md px-2.5 py-1.5 text-left text-xs text-muted-foreground opacity-60"
        title="Awaiting-me filters require user assignments — coming with the approval-chain milestone."
      >
        Awaiting me
      </button>
      <button
        type="button"
        disabled
        className="flex items-center gap-2 rounded-md px-2.5 py-1.5 text-left text-xs text-muted-foreground opacity-60"
        title="SLAs require deadline metadata on Proposal — not yet wired."
      >
        SLA breaching
      </button>
    </div>
  );
}

function RailButton({
  label,
  count,
  active,
  onClick,
}: {
  label: string;
  count: number;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={
        'flex items-center gap-2 rounded-md px-2.5 py-1.5 text-left text-xs transition-colors ' +
        (active
          ? 'bg-primary/10 font-semibold text-primary'
          : 'text-muted-foreground hover:bg-card hover:text-foreground')
      }
    >
      <span className="flex-1">{label}</span>
      <span
        className={
          'min-w-[20px] rounded px-1.5 py-0.5 text-center font-[family-name:var(--font-mono)] text-[10px] ' +
          (active
            ? 'bg-primary text-primary-foreground'
            : 'bg-card text-muted-foreground')
        }
      >
        {count}
      </span>
    </button>
  );
}

// ─── List column ─────────────────────────────────────────────────────────

interface ListColumnProps {
  items: ProposalListItem[];
  selectedId: string | null;
  loading: boolean;
  error: string | null;
  onSelect: (id: string) => void;
  onRefresh: () => void;
}

function ListColumn({ items, selectedId, loading, error, onSelect, onRefresh }: ListColumnProps) {
  return (
    <div className="flex flex-col overflow-hidden border-r border-border">
      <div className="flex items-center justify-between border-b border-border px-4 py-3.5">
        <div>
          <div className="text-[16px] font-bold text-foreground">Approvals</div>
          <div className="mt-0.5 text-[11.5px] text-muted-foreground">
            {loading
              ? 'loading…'
              : `${items.length} ${items.length === 1 ? 'pending' : 'pending'}`}
          </div>
        </div>
        <button
          type="button"
          onClick={onRefresh}
          disabled={loading}
          className="rounded-md border border-border bg-card p-1.5 text-muted-foreground hover:text-foreground disabled:opacity-50"
          aria-label="Refresh"
        >
          <RefreshCw className={'h-3 w-3 ' + (loading ? 'animate-spin' : '')} />
        </button>
      </div>

      {error ? (
        <div className="m-4 flex items-center gap-2 rounded-md border border-destructive bg-destructive/10 p-3 text-xs text-destructive">
          <AlertCircle className="h-4 w-4 flex-none" />
          <span className="flex-1">{error}</span>
        </div>
      ) : null}

      <div className="flex flex-1 flex-col overflow-auto">
        {!loading && items.length === 0 && !error && (
          <div className="flex flex-col items-center justify-center py-10 text-center">
            <Sparkles className="mb-2 h-8 w-8 text-muted-foreground" />
            <p className="text-sm font-medium text-foreground">
              Queue is empty
            </p>
            <p className="mt-1 max-w-[260px] text-xs text-muted-foreground">
              No pending proposals in this view. Agents will queue work here as they
              propose changes.
            </p>
          </div>
        )}

        {items.map((item) => {
          const isSelected = item.id === selectedId;
          return (
            <button
              key={item.id}
              type="button"
              onClick={() => onSelect(item.id)}
              className={
                'flex w-full flex-col gap-1.5 border-b border-border px-4 py-3.5 text-left transition-colors ' +
                (isSelected
                  ? 'border-l-[3px] border-l-primary bg-primary/10 pl-[13px]'
                  : 'border-l-[3px] border-l-transparent hover:bg-muted')
              }
            >
              <div className="flex items-center gap-2">
                <Pill tone="muted">
                  {item.proposalType || 'Proposal'}
                </Pill>
                {item.riskTier === 'High' && (
                  <Badge variant="destructive">High</Badge>
                )}
                <span className="flex-1" />
                <span className="font-[family-name:var(--font-mono)] text-[10.5px] text-muted-foreground">
                  {shortProposalId(item.id)}
                </span>
              </div>
              <div className="text-[13px] font-semibold leading-snug text-foreground">
                {item.summary}
              </div>
              <div className="flex items-center gap-2 text-[11px] text-muted-foreground">
                <AgentAvatar name={item.agentName} size={18} />
                <span>{item.agentName}</span>
                <span className="text-primary">· agent</span>
                <span className="flex-1" />
                <span className="font-[family-name:var(--font-mono)] text-[10.5px] text-muted-foreground">
                  {formatRelative(item.createdAt)}
                </span>
              </div>
              <div className="flex items-center gap-2 pt-0.5 text-[11px]">
                <Pill tone={RISK_TONE[item.riskTier] ?? 'default'} dot>
                  {item.riskTier || 'Unknown'}
                </Pill>
                <span className="font-[family-name:var(--font-mono)] text-[10.5px] text-muted-foreground">
                  conf {item.confidence.toFixed(2)}
                </span>
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
}

// ─── Detail column ───────────────────────────────────────────────────────

interface DetailColumnProps {
  list: ProposalListItem | null;
  detail: ProposalDetailResponse | null;
  loading: boolean;
  error: string | null;
  actioning: 'approve' | 'dismiss' | null;
  onApprove: () => void;
  onDismiss: () => void;
}

function DetailColumn({
  list,
  detail,
  loading,
  error,
  actioning,
  onApprove,
  onDismiss,
}: DetailColumnProps) {
  if (!list) {
    return (
      <div className="flex flex-col items-center justify-center text-center">
        <Sparkles className="mb-2 h-8 w-8 text-muted-foreground" />
        <p className="text-sm font-medium text-foreground">
          No proposal selected
        </p>
        <p className="mt-1 max-w-[300px] text-xs text-muted-foreground">
          Select a row from the queue to review the agent's payload, the
          policy that triggered it, and approve or dismiss.
        </p>
      </div>
    );
  }

  return (
    <div className="flex flex-col overflow-auto">
      <div className="flex items-start justify-between gap-4 border-b border-border px-6 py-4">
        <div className="min-w-0">
          <div className="mb-1.5 flex items-center gap-2">
            <span className="font-[family-name:var(--font-mono)] text-[11.5px] text-muted-foreground">
              {shortProposalId(list.id)}
            </span>
            <Pill tone={RISK_TONE[list.riskTier] ?? 'default'} dot>
              {URGENCY_LABEL[list.riskTier] ?? list.riskTier.toLowerCase()}
            </Pill>
            <Pill tone="info">
              {list.proposalType || 'Proposal'}
            </Pill>
          </div>
          <div className="text-[18px] font-bold tracking-[-0.01em] text-foreground">
            {list.summary}
          </div>
        </div>
        <div className="flex flex-none gap-1.5">
          <Button
            variant="outline"
            size="sm"
            onClick={onDismiss}
            disabled={actioning !== null || loading}
            className="border-destructive/30 text-destructive"
          >
            <X className="h-3 w-3" />
            Reject
          </Button>
          <Button
            size="sm"
            onClick={onApprove}
            disabled={actioning !== null || loading}
          >
            <Check className="h-3 w-3" />
            {actioning === 'approve' ? 'Approving…' : 'Approve'}
          </Button>
        </div>
      </div>

      {error && (
        <Alert variant="destructive" className="m-6 w-auto">
          <AlertCircle />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <div className="flex flex-col gap-4 p-6">
        <Section title="Why this proposal landed here" icon={<Clock className="h-3 w-3" />}>
          <div className="text-[13px] leading-snug text-foreground">
            {list.proposalType ? `Risk tier ${list.riskTier} on ${list.proposalType.toLowerCase()} actions` : `Risk tier ${list.riskTier}`}{' '}
            triggers a manual approval. Confidence on this proposal is{' '}
            <b>{list.confidence.toFixed(2)}</b>.
          </div>
          <div className="mt-1.5 text-[12px] text-muted-foreground">
            Created <b>{formatRelative(list.createdAt)}</b> by{' '}
            <b>{list.agentName}</b>{list.agentDomain ? ` (${list.agentDomain})` : ''}.
          </div>
        </Section>

        <Section title="Decision context">
          {loading && !detail ? (
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <RefreshCw className="h-3.5 w-3.5 animate-spin" />
              Loading payload…
            </div>
          ) : detail ? (
            <div className="flex flex-col">
              {payloadRows(detail.payloadJson).map(([label, value], idx) => (
                <div
                  key={`${label}-${idx}`}
                  className={
                    'grid items-baseline gap-3 py-2 ' +
                    (idx === 0 ? '' : 'border-t border-dashed border-border')
                  }
                  style={{ gridTemplateColumns: '160px 1fr' }}
                >
                  <span className="text-[12px] text-muted-foreground">{label}</span>
                  <span
                    className={
                      'text-[12.5px] text-foreground ' +
                      (label.toLowerCase().includes('amount') ||
                      label.toLowerCase().includes('id') ||
                      label.toLowerCase().includes('ref')
                        ? 'font-[family-name:var(--font-mono)]'
                        : '')
                    }
                  >
                    {value}
                  </span>
                </div>
              ))}
              {payloadRows(detail.payloadJson).length === 0 && (
                <p className="text-xs text-muted-foreground">
                  No payload was attached to this proposal.
                </p>
              )}
            </div>
          ) : (
            <p className="text-xs text-muted-foreground">No detail available.</p>
          )}
        </Section>

        {detail?.payloadJson && (
          <Section title="Raw payload">
            <pre className="max-h-[260px] overflow-auto rounded-md bg-muted p-3 font-[family-name:var(--font-mono)] text-[11px] leading-relaxed text-foreground">
              {tryFormatPayload(detail.payloadJson)}
            </pre>
          </Section>
        )}
      </div>
    </div>
  );
}

function Section({
  title,
  icon,
  children,
}: {
  title: string;
  icon?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <div className="rounded-lg border border-border bg-card p-3.5">
      <div className="mb-2 flex items-center gap-1.5 text-xs font-medium text-muted-foreground">
        {icon}
        {title}
      </div>
      {children}
    </div>
  );
}
