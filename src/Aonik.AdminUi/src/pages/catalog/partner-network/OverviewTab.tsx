// Partner Network hub — Overview tab (body only; the shell owns the page
// header, refresh, add-partner action and the shared load-error banner).
//
// Every figure here is a real aggregate of the loaded partner list
// (partnerService.list). The prototype's Overview leads with money-moved /
// settlement-rate KPIs; we have no settlement telemetry endpoint, so those are
// deliberately replaced with counts we can actually stand behind: partners,
// healthy partners, distinct markets, and connector endpoints.

import { useMemo } from 'react';
import { Building2, Globe, Network, Plug } from 'lucide-react';
import { AgentAvatar, KpiTile, Pill } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import type { PartnerListItem } from '@/types/partners';
import { InfoNote, Panel } from './components';
import { formatRelative, partnerStatusTone, PARTNER_LOAD_CAP, type PartnerNetworkData } from './usePartnerNetwork';

const HEALTHY_STATUSES = new Set(['Active', 'Healthy']);

export interface OverviewTabProps {
  data: PartnerNetworkData;
  onOpenPartner: (partnerId: string) => void;
  onViewAllPartners: () => void;
}

export function OverviewTab({ data, onOpenPartner, onViewAllPartners }: OverviewTabProps) {
  const { partners, totalCount, loading } = data;

  const stats = useMemo(() => {
    const markets = new Set<string>();
    let connectors = 0;
    let healthy = 0;
    for (const p of partners) {
      p.coverageCountries.forEach((c) => markets.add(c));
      connectors += p.connectorCount;
      if (HEALTHY_STATUSES.has(p.status)) healthy += 1;
    }
    return { markets: markets.size, connectors, healthy };
  }, [partners]);

  const recentlyUpdated = useMemo(
    () =>
      [...partners]
        .filter((p) => p.updatedAt)
        .sort((a, b) => new Date(b.updatedAt!).getTime() - new Date(a.updatedAt!).getTime())
        .slice(0, 6),
    [partners],
  );

  const blank = loading && !partners.length;

  return (
    <div className="flex flex-col gap-6">
      <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2 xl:grid-cols-4">
        <KpiTile label="Partners" value={blank ? '—' : String(totalCount)} />
        <KpiTile label="Healthy partners" value={blank ? '—' : `${stats.healthy} / ${partners.length}`} />
        <KpiTile label="Markets covered" value={blank ? '—' : String(stats.markets)} />
        <KpiTile label="Connector endpoints" value={blank ? '—' : String(stats.connectors)} />
      </div>

      {totalCount > partners.length && (
        <InfoNote>
          Aggregates reflect the first {partners.length} of {totalCount} partners. The list endpoint caps a page at{' '}
          {PARTNER_LOAD_CAP}; refine with filters on the Partners tab to inspect the remainder.
        </InfoNote>
      )}

      <div className="grid grid-cols-1 gap-5 lg:grid-cols-3">
        <Panel
          title="Network health"
          subtitle="Operational status across every connected partner"
          action={
            <Button variant="ghost" size="sm" onClick={onViewAllPartners}>
              View all
            </Button>
          }
          className="lg:col-span-2"
          bodyClassName="divide-y divide-[var(--color-border-light)]"
        >
          {blank ? (
            <LoadingRows />
          ) : partners.length === 0 ? (
            <p className="px-5 py-10 text-center text-[13px] text-[var(--color-text-tertiary)]">
              No partners connected yet.
            </p>
          ) : (
            partners
              .slice(0, 8)
              .map((p) => <HealthRow key={p.partnerId} partner={p} onOpen={() => onOpenPartner(p.partnerId)} />)
          )}
        </Panel>

        <Panel
          title="Recently updated"
          subtitle="Latest configuration changes"
          bodyClassName="divide-y divide-[var(--color-border-light)]"
        >
          {blank ? (
            <LoadingRows rows={4} />
          ) : recentlyUpdated.length === 0 ? (
            <p className="px-5 py-10 text-center text-[13px] text-[var(--color-text-tertiary)]">
              No recent changes.
            </p>
          ) : (
            recentlyUpdated.map((p) => (
              <button
                key={p.partnerId}
                type="button"
                onClick={() => onOpenPartner(p.partnerId)}
                className="flex w-full items-center gap-3 px-5 py-3 text-left transition-colors hover:bg-[var(--color-surface-inset)]"
              >
                <AgentAvatar name={p.name} size={28} />
                <div className="min-w-0 flex-1">
                  <p className="truncate text-[13px] font-medium text-[var(--color-text-primary)]">{p.name}</p>
                  <p className="text-[11px] text-[var(--color-text-tertiary)]">{formatRelative(p.updatedAt)}</p>
                </div>
                <Pill tone={partnerStatusTone(p.status)} dot>
                  {p.status}
                </Pill>
              </button>
            ))
          )}
        </Panel>
      </div>
    </div>
  );
}

function HealthRow({ partner, onOpen }: { partner: PartnerListItem; onOpen: () => void }) {
  return (
    <button
      type="button"
      onClick={onOpen}
      className="flex w-full items-center gap-3.5 px-5 py-3.5 text-left transition-colors hover:bg-[var(--color-surface-inset)]"
    >
      <AgentAvatar name={partner.name} size={36} />
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium text-[var(--color-text-primary)]">{partner.name}</p>
        <div className="mt-0.5 flex items-center gap-3 text-[11.5px] text-[var(--color-text-tertiary)]">
          <span className="inline-flex items-center gap-1">
            <Building2 size={11} />
            {partner.branchCount}
          </span>
          <span className="inline-flex items-center gap-1">
            <Plug size={11} />
            {partner.connectorCount}
          </span>
          <span className="inline-flex items-center gap-1">
            <Network size={11} />
            {partner.activeRoutingRuleCount}
          </span>
          <span className="inline-flex items-center gap-1">
            <Globe size={11} />
            {partner.coverageCountries.length}
          </span>
        </div>
      </div>
      <Pill tone={partnerStatusTone(partner.status)} dot>
        {partner.status}
      </Pill>
    </button>
  );
}

function LoadingRows({ rows = 6 }: { rows?: number }) {
  return (
    <>
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="flex items-center gap-3.5 px-5 py-3.5">
          <div className="h-9 w-9 animate-pulse rounded-lg bg-[var(--color-surface-inset)]" />
          <div className="flex-1 space-y-2">
            <div className="h-3 w-1/3 animate-pulse rounded bg-[var(--color-surface-inset)]" />
            <div className="h-2.5 w-1/4 animate-pulse rounded bg-[var(--color-surface-inset)]" />
          </div>
        </div>
      ))}
    </>
  );
}
