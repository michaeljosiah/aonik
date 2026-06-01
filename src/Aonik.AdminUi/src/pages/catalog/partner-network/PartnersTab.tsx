// Partner Network hub — Partners tab.
//
// The full partner roster with grid/list views. Status tabs and search filter
// the already-loaded set client-side (the hub loads one page up to
// PARTNER_LOAD_CAP); a row/card opens the existing, unchanged detail page.

import { useMemo, useState } from 'react';
import { Globe, Network } from 'lucide-react';
import { AgentAvatar, FilterBar, type FilterBarTab, Pill } from '@/components/layout/aonik';
import type { PartnerListItem } from '@/types/partners';
import { Chip, EmptyState, Panel, ViewToggle, type HubView } from './components';
import { formatDate, formatRelative, partnerStatusTone, type PartnerNetworkData } from './usePartnerNetwork';

const COVERAGE_CHIP_CAP = 6;

export interface PartnersTabProps {
  data: PartnerNetworkData;
  onOpenPartner: (partnerId: string) => void;
}

export function PartnersTab({ data, onOpenPartner }: PartnersTabProps) {
  const { partners, loading } = data;
  const [statusFilter, setStatusFilter] = useState('all');
  const [search, setSearch] = useState('');
  const [view, setView] = useState<HubView>('grid');

  const statusTabs = useMemo<FilterBarTab[]>(() => {
    const counts = new Map<string, number>();
    for (const p of partners) counts.set(p.status, (counts.get(p.status) ?? 0) + 1);
    const tabs: FilterBarTab[] = [{ value: 'all', label: 'All', count: partners.length }];
    for (const [status, count] of [...counts].sort((a, b) => a[0].localeCompare(b[0]))) {
      tabs.push({ value: status, label: status, count });
    }
    return tabs;
  }, [partners]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return partners.filter((p) => {
      if (statusFilter !== 'all' && p.status !== statusFilter) return false;
      if (q && !p.name.toLowerCase().includes(q)) return false;
      return true;
    });
  }, [partners, statusFilter, search]);

  return (
    <div className="flex flex-col gap-4">
      <FilterBar
        tabs={statusTabs}
        active={statusFilter}
        onTabChange={setStatusFilter}
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Search partners…"
        hideFilterButton
        extra={<ViewToggle view={view} onChange={setView} />}
      />

      {filtered.length === 0 ? (
        <EmptyState
          icon={Network}
          title={loading ? 'Loading partners…' : 'No partners match'}
          description={
            loading
              ? undefined
              : 'No partner matches the current status and search filters. Clear them to see the full roster.'
          }
        />
      ) : view === 'grid' ? (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {filtered.map((p) => (
            <PartnerCard key={p.partnerId} partner={p} onOpen={() => onOpenPartner(p.partnerId)} />
          ))}
        </div>
      ) : (
        <PartnerTable partners={filtered} onOpenPartner={onOpenPartner} />
      )}
    </div>
  );
}

function PartnerCard({ partner, onOpen }: { partner: PartnerListItem; onOpen: () => void }) {
  const extra = partner.coverageCountries.length - COVERAGE_CHIP_CAP;
  return (
    <button
      type="button"
      onClick={onOpen}
      className="flex flex-col gap-4 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-5 text-left transition-colors hover:border-[var(--color-border)]"
    >
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 items-center gap-3">
          <AgentAvatar name={partner.name} size={40} />
          <p className="truncate text-sm font-semibold text-[var(--color-text-primary)]">{partner.name}</p>
        </div>
        <Pill tone={partnerStatusTone(partner.status)} dot>
          {partner.status}
        </Pill>
      </div>

      <div className="flex flex-wrap gap-1.5">
        {partner.coverageCountries.length === 0 ? (
          <span className="text-[11.5px] text-[var(--color-text-tertiary)]">No markets configured</span>
        ) : (
          <>
            {partner.coverageCountries.slice(0, COVERAGE_CHIP_CAP).map((c) => (
              <Chip key={c} icon={Globe} dense>
                {c}
              </Chip>
            ))}
            {extra > 0 && (
              <Chip dense>
                +{extra}
              </Chip>
            )}
          </>
        )}
      </div>

      <div className="grid grid-cols-4 gap-2 border-t border-[var(--color-border-light)] pt-3">
        <Stat label="Branches" value={partner.branchCount} />
        <Stat label="Connectors" value={partner.connectorCount} />
        <Stat label="Routing" value={partner.activeRoutingRuleCount} />
        <Stat label="Billers" value={partner.linkedBillerCount} />
      </div>

      <p className="text-[11px] text-[var(--color-text-tertiary)]">
        Added {formatDate(partner.createdAt)} · Updated {formatRelative(partner.updatedAt)}
      </p>
    </button>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="min-w-0">
      <p className="font-[family-name:var(--font-mono)] text-[15px] font-semibold text-[var(--color-text-primary)]">
        {value}
      </p>
      <p className="truncate text-[10px] uppercase tracking-wide text-[var(--color-text-tertiary)]">{label}</p>
    </div>
  );
}

function PartnerTable({
  partners,
  onOpenPartner,
}: {
  partners: PartnerListItem[];
  onOpenPartner: (partnerId: string) => void;
}) {
  return (
    <Panel bodyClassName="overflow-x-auto">
      <table className="w-full border-collapse text-left text-[13px]">
        <thead>
          <tr className="border-b border-[var(--color-border-light)] text-[11px] uppercase tracking-wide text-[var(--color-text-tertiary)]">
            <th className="px-5 py-3 font-medium">Partner</th>
            <th className="px-3 py-3 font-medium">Status</th>
            <th className="px-3 py-3 text-right font-medium">Markets</th>
            <th className="px-3 py-3 text-right font-medium">Branches</th>
            <th className="px-3 py-3 text-right font-medium">Connectors</th>
            <th className="px-3 py-3 text-right font-medium">Routing</th>
            <th className="px-3 py-3 text-right font-medium">Billers</th>
            <th className="px-5 py-3 font-medium">Updated</th>
          </tr>
        </thead>
        <tbody>
          {partners.map((p) => (
            <tr
              key={p.partnerId}
              onClick={() => onOpenPartner(p.partnerId)}
              className="cursor-pointer border-b border-[var(--color-border-light)] transition-colors last:border-0 hover:bg-[var(--color-surface-inset)]"
            >
              <td className="px-5 py-3">
                <div className="flex items-center gap-2.5">
                  <AgentAvatar name={p.name} size={28} />
                  <span className="font-medium text-[var(--color-text-primary)]">{p.name}</span>
                </div>
              </td>
              <td className="px-3 py-3">
                <Pill tone={partnerStatusTone(p.status)} dot>
                  {p.status}
                </Pill>
              </td>
              <td className="px-3 py-3 text-right font-[family-name:var(--font-mono)] text-[var(--color-text-secondary)]">
                {p.coverageCountries.length}
              </td>
              <td className="px-3 py-3 text-right font-[family-name:var(--font-mono)] text-[var(--color-text-secondary)]">
                {p.branchCount}
              </td>
              <td className="px-3 py-3 text-right font-[family-name:var(--font-mono)] text-[var(--color-text-secondary)]">
                {p.connectorCount}
              </td>
              <td className="px-3 py-3 text-right font-[family-name:var(--font-mono)] text-[var(--color-text-secondary)]">
                {p.activeRoutingRuleCount}
              </td>
              <td className="px-3 py-3 text-right font-[family-name:var(--font-mono)] text-[var(--color-text-secondary)]">
                {p.linkedBillerCount}
              </td>
              <td className="px-5 py-3 text-[var(--color-text-tertiary)]">{formatRelative(p.updatedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </Panel>
  );
}
