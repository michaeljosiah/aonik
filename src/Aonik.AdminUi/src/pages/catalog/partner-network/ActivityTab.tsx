// Partner Network hub — Activity tab.
//
// Aggregates the real `recentTransmissions` from partnerService.get() across the
// loaded partners (shell's bounded fan-out). There is no cross-partner activity
// feed endpoint, so this is per-partner detail stitched together and sorted by
// time — not a live event stream.
//
// Honest scope notes (Spec 031): Transmission today records *payout* attempts
// only (gap X1) — collections and bill payments produce no transmission record
// yet — and its Status is a freeform string with no normalized enum (gap X3),
// so tone is inferred from keywords via transmissionTone().

import { useMemo, useState } from 'react';
import { Activity, AlertTriangle, Plug, RefreshCw } from 'lucide-react';
import { AgentAvatar, FilterBar, type FilterBarTab, Pill } from '@/components/layout/aonik';
import type { PartnerDetail, PartnerTransmissionItem } from '@/types/partners';
import { Chip, EmptyState, InfoNote, Panel, ViewToggle, type HubView } from './components';
import {
  DETAIL_FETCH_CAP,
  formatRelative,
  transmissionTone,
  type PartnerDetailsState,
} from './usePartnerNetwork';

interface TxRow {
  partner: PartnerDetail;
  tx: PartnerTransmissionItem;
}

export interface ActivityTabProps {
  details: PartnerDetailsState;
  onOpenPartner: (partnerId: string) => void;
}

function txTime(value?: string | null): number {
  if (!value) return 0;
  const t = new Date(value).getTime();
  return Number.isNaN(t) ? 0 : t;
}

export function ActivityTab({ details, onOpenPartner }: ActivityTabProps) {
  const { details: partners, loading, error, truncated } = details;
  const [statusFilter, setStatusFilter] = useState('all');
  const [search, setSearch] = useState('');
  const [view, setView] = useState<HubView>('list');

  const allRows = useMemo<TxRow[]>(() => {
    const out: TxRow[] = [];
    for (const d of partners) {
      for (const tx of d.recentTransmissions ?? []) out.push({ partner: d, tx });
    }
    out.sort((a, b) => txTime(b.tx.createdAt) - txTime(a.tx.createdAt));
    return out;
  }, [partners]);

  const statusTabs = useMemo<FilterBarTab[]>(() => {
    const counts = new Map<string, number>();
    for (const r of allRows) counts.set(r.tx.status, (counts.get(r.tx.status) ?? 0) + 1);
    const tabs: FilterBarTab[] = [{ value: 'all', label: 'All', count: allRows.length }];
    for (const [status, count] of [...counts].sort((a, b) => a[0].localeCompare(b[0]))) {
      tabs.push({ value: status, label: status, count });
    }
    return tabs;
  }, [allRows]);

  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    return allRows.filter((r) => {
      if (statusFilter !== 'all' && r.tx.status !== statusFilter) return false;
      if (q) {
        const hay = `${r.partner.name} ${r.tx.connectorType ?? ''}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });
  }, [allRows, statusFilter, search]);

  if (loading && partners.length === 0) {
    return <EmptyState icon={Activity} title="Loading recent activity…" />;
  }

  if (error && allRows.length === 0) {
    return <EmptyState icon={Activity} title="Couldn't load activity" description={error} />;
  }

  if (allRows.length === 0) {
    return (
      <EmptyState
        icon={Activity}
        title="No recent transmissions"
        description="None of the loaded partners have recent transmission records. Payout attempts appear here once partners begin moving money."
      />
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <FilterBar
        tabs={statusTabs}
        active={statusFilter}
        onTabChange={setStatusFilter}
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Search partner or connector…"
        hideFilterButton
        extra={<ViewToggle view={view} onChange={setView} />}
      />

      <InfoNote icon={Activity}>
        Transmissions currently record payout attempts only; collections and bill payments don't yet produce a
        transmission record, and there's no normalized status enum — tone is inferred from each partner's freeform
        status text.
      </InfoNote>

      {truncated && (
        <InfoNote>
          Showing activity for the first {DETAIL_FETCH_CAP} partners. Detail is fetched on demand and bounded to keep
          this tab responsive.
        </InfoNote>
      )}

      {rows.length === 0 ? (
        <EmptyState
          icon={Activity}
          title="No activity matches"
          description="No transmission matches the current filters."
        />
      ) : view === 'grid' ? (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          {rows.map((r) => (
            <ActivityCard key={r.tx.transmissionId} row={r} onOpen={() => onOpenPartner(r.partner.partnerId)} />
          ))}
        </div>
      ) : (
        <ActivityTable rows={rows} onOpenPartner={onOpenPartner} />
      )}
    </div>
  );
}

function ActivityCard({ row, onOpen }: { row: TxRow; onOpen: () => void }) {
  const { tx, partner } = row;
  return (
    <div className="flex flex-col gap-3 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-5">
      <div className="flex items-start justify-between gap-3">
        <button type="button" onClick={onOpen} className="flex min-w-0 items-center gap-2.5 text-left">
          <AgentAvatar name={partner.name} size={32} />
          <div className="min-w-0">
            <p className="truncate text-sm font-semibold text-[var(--color-text-primary)]">{partner.name}</p>
            <p className="inline-flex items-center gap-1 text-[11.5px] text-[var(--color-text-tertiary)]">
              <Plug size={11} />
              {tx.connectorType ?? 'Connector'}
            </p>
          </div>
        </button>
        <Pill tone={transmissionTone(tx.status)} dot>
          {tx.status}
        </Pill>
      </div>

      <div className="flex items-center gap-2">
        {tx.retryCount > 0 && (
          <Chip icon={RefreshCw} dense>
            <span className="text-[var(--color-text-tertiary)]">retries</span>
            <span className="font-[family-name:var(--font-mono)] text-[var(--color-text-secondary)]">
              {tx.retryCount}
            </span>
          </Chip>
        )}
        <span className="text-[11.5px] text-[var(--color-text-tertiary)]">{formatRelative(tx.createdAt)}</span>
      </div>

      {tx.lastError && (
        <div className="flex items-start gap-1.5 rounded-lg bg-[var(--color-error-light)] px-3 py-2 text-[11.5px] text-[var(--color-error)]">
          <AlertTriangle size={13} className="mt-px flex-none" />
          <span className="break-words">{tx.lastError}</span>
        </div>
      )}
    </div>
  );
}

function ActivityTable({ rows, onOpenPartner }: { rows: TxRow[]; onOpenPartner: (partnerId: string) => void }) {
  return (
    <Panel bodyClassName="overflow-x-auto">
      <table className="w-full border-collapse text-left text-[13px]">
        <thead>
          <tr className="border-b border-[var(--color-border-light)] text-[11px] uppercase tracking-wide text-[var(--color-text-tertiary)]">
            <th className="px-5 py-3 font-medium">Partner</th>
            <th className="px-3 py-3 font-medium">Connector</th>
            <th className="px-3 py-3 font-medium">Status</th>
            <th className="px-3 py-3 text-right font-medium">Retries</th>
            <th className="px-3 py-3 font-medium">Error</th>
            <th className="px-5 py-3 font-medium">When</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr
              key={r.tx.transmissionId}
              className="border-b border-[var(--color-border-light)] last:border-0 hover:bg-[var(--color-surface-inset)]"
            >
              <td className="px-5 py-3">
                <button
                  type="button"
                  onClick={() => onOpenPartner(r.partner.partnerId)}
                  className="flex items-center gap-2.5 text-left"
                >
                  <AgentAvatar name={r.partner.name} size={26} />
                  <span className="font-medium text-[var(--color-text-primary)]">{r.partner.name}</span>
                </button>
              </td>
              <td className="px-3 py-3 text-[var(--color-text-secondary)]">{r.tx.connectorType ?? '—'}</td>
              <td className="px-3 py-3">
                <Pill tone={transmissionTone(r.tx.status)} dot>
                  {r.tx.status}
                </Pill>
              </td>
              <td className="px-3 py-3 text-right font-[family-name:var(--font-mono)] text-[var(--color-text-secondary)]">
                {r.tx.retryCount}
              </td>
              <td className="max-w-[260px] px-3 py-3">
                {r.tx.lastError ? (
                  <span className="block truncate text-[12px] text-[var(--color-error)]" title={r.tx.lastError}>
                    {r.tx.lastError}
                  </span>
                ) : (
                  <span className="text-[var(--color-text-tertiary)]">—</span>
                )}
              </td>
              <td className="px-5 py-3 text-[var(--color-text-tertiary)]">{formatRelative(r.tx.createdAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </Panel>
  );
}
