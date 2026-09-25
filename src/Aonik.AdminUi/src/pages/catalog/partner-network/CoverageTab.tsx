// Partner Network hub — Coverage tab.
//
// A partner × market grid built purely from each partner's `coverageCountries`.
// The prototype renders service-level cells (payout/collection/bill-pay per
// country); the list endpoint only tells us *which countries* a partner covers,
// not which services per country, so this is an honest binary coverage map —
// no invented per-service dimension. The matrix shows coverage; the list view
// answers "who covers this market?".

import { useMemo, useState } from 'react';
import { Globe } from 'lucide-react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { AgentAvatar, FilterBar } from '@/components/layout/aonik';
import type { PartnerListItem } from '@/types/partners';
import { Chip, EmptyState, InfoNote, Panel, ViewToggle, type HubView } from './components';
import type { PartnerNetworkData } from './usePartnerNetwork';

export interface CoverageTabProps {
  data: PartnerNetworkData;
  onOpenPartner: (partnerId: string) => void;
}

export function CoverageTab({ data, onOpenPartner }: CoverageTabProps) {
  const { partners, loading } = data;
  const [search, setSearch] = useState('');
  const [view, setView] = useState<HubView>('grid');

  const allCountries = useMemo(() => {
    const set = new Set<string>();
    for (const p of partners) p.coverageCountries.forEach((c) => set.add(c));
    return [...set].sort((a, b) => a.localeCompare(b));
  }, [partners]);

  const countries = useMemo(() => {
    const q = search.trim().toLowerCase();
    return q ? allCountries.filter((c) => c.toLowerCase().includes(q)) : allCountries;
  }, [allCountries, search]);

  const byCountry = useMemo(() => {
    const map = new Map<string, PartnerListItem[]>();
    for (const c of allCountries) map.set(c, []);
    for (const p of partners) {
      for (const c of p.coverageCountries) map.get(c)?.push(p);
    }
    return map;
  }, [partners, allCountries]);

  // Partners that actually declare at least one market — only these belong in
  // the matrix rows.
  const coveredPartners = useMemo(() => partners.filter((p) => p.coverageCountries.length > 0), [partners]);

  if (!loading && allCountries.length === 0) {
    return (
      <EmptyState
        icon={Globe}
        title="No market coverage configured"
        description="No partner has declared any coverage countries yet. Coverage appears here once partners list the markets they operate in."
      />
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Search markets…"
        hideFilterButton
        extra={<ViewToggle view={view} onChange={setView} />}
      />

      <InfoNote>
        Coverage is shown at the market level. Per-service coverage (payout vs collection vs bill payment by
        country) isn't exposed by the partner list endpoint yet, so this grid reflects declared markets only.
      </InfoNote>

      {countries.length === 0 ? (
        <EmptyState icon={Globe} title="No markets match" description="No market matches your search." />
      ) : view === 'grid' ? (
        <CoverageMatrix partners={coveredPartners} countries={countries} onOpenPartner={onOpenPartner} />
      ) : (
        <CoverageList byCountry={byCountry} countries={countries} onOpenPartner={onOpenPartner} />
      )}
    </div>
  );
}

function CoverageMatrix({
  partners,
  countries,
  onOpenPartner,
}: {
  partners: PartnerListItem[];
  countries: string[];
  onOpenPartner: (partnerId: string) => void;
}) {
  return (
    <Panel bodyClassName="overflow-x-auto">
      <Table className="border-collapse text-[13px]">
        <TableHeader>
          <TableRow className="border-b border-border hover:bg-transparent">
            <TableHead className="h-auto text-xs sticky left-0 z-10 bg-card px-5 py-3 text-left font-medium text-muted-foreground">
              Partner
            </TableHead>
            {countries.map((c) => (
              <TableHead
                key={c}
                className="px-3 py-3 text-center font-[family-name:var(--font-mono)] text-[11px] font-medium text-muted-foreground"
              >
                {c}
              </TableHead>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody>
          {partners.map((p) => {
            const covers = new Set(p.coverageCountries);
            return (
              <TableRow
                key={p.partnerId}
                onClick={() => onOpenPartner(p.partnerId)}
                className="cursor-pointer border-b border-border transition-colors last:border-0 hover:bg-muted"
              >
                <TableCell className="sticky left-0 z-10 bg-card px-5 py-3">
                  <div className="flex items-center gap-2.5">
                    <AgentAvatar name={p.name} size={26} />
                    <span className="whitespace-nowrap font-medium text-foreground">{p.name}</span>
                  </div>
                </TableCell>
                {countries.map((c) => (
                  <TableCell key={c} className="px-3 py-3 text-center">
                    {covers.has(c) ? (
                      <span
                        className="inline-block h-2 w-2 rounded-full bg-primary"
                        aria-label="Covered"
                      />
                    ) : (
                      <span className="text-muted-foreground" aria-label="Not covered">
                        ·
                      </span>
                    )}
                  </TableCell>
                ))}
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </Panel>
  );
}

function CoverageList({
  byCountry,
  countries,
  onOpenPartner,
}: {
  byCountry: Map<string, PartnerListItem[]>;
  countries: string[];
  onOpenPartner: (partnerId: string) => void;
}) {
  return (
    <Panel bodyClassName="divide-y divide-border">
      {countries.map((c) => {
        const ps = byCountry.get(c) ?? [];
        return (
          <div key={c} className="flex flex-col gap-2.5 px-5 py-4 sm:flex-row sm:items-center">
            <div className="flex w-40 flex-none items-center gap-2">
              <Chip icon={Globe}>{c}</Chip>
              <span className="text-[11.5px] text-muted-foreground">
                {ps.length === 1 ? '1 partner' : `${ps.length} partners`}
              </span>
            </div>
            <div className="flex flex-wrap gap-1.5">
              {ps.map((p) => (
                <button
                  key={p.partnerId}
                  type="button"
                  onClick={() => onOpenPartner(p.partnerId)}
                  className="inline-flex items-center gap-1.5 rounded-full border border-border bg-card py-0.5 pl-0.5 pr-2.5 transition-colors hover:border-border"
                >
                  <AgentAvatar name={p.name} size={20} />
                  <span className="text-[12px] font-medium text-foreground">{p.name}</span>
                </button>
              ))}
            </div>
          </div>
        );
      })}
    </Panel>
  );
}
