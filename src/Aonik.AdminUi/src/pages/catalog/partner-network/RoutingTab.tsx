// Partner Network hub — Routing tab.
//
// Aggregates the real `routingRules` returned by partnerService.get() across the
// loaded partners (via the shell's bounded usePartnerDetails fan-out). Each rule
// is a per-partner record: priority, active flag, freeform conditionsJson, and a
// target connector resolved against that partner's own connector list.
//
// Honest scope note: today's rules select a connector *within a partner*. The
// capability-driven, cross-partner lane routing the abstraction models
// (IPartnerConnectorResolver.TryResolve*) is not yet wired — surfaced as an
// InfoNote rather than implied by the UI.

import { useMemo, useState } from 'react';
import { ArrowRight, Route } from 'lucide-react';
import { AgentAvatar, FilterBar, type FilterBarTab, Pill } from '@/components/layout/aonik';
import type { PartnerConnectorItem, PartnerDetail, PartnerRoutingRuleItem } from '@/types/partners';
import { Chip, EmptyState, InfoNote, Panel, ViewToggle, type HubView } from './components';
import { DETAIL_FETCH_CAP, type PartnerDetailsState } from './usePartnerNetwork';

type ActiveFilter = 'all' | 'active' | 'inactive';

interface RuleRow {
  partner: PartnerDetail;
  rule: PartnerRoutingRuleItem;
  target?: PartnerConnectorItem;
}

export interface RoutingTabProps {
  details: PartnerDetailsState;
  onOpenPartner: (partnerId: string) => void;
}

/** Parse the schema-less conditionsJson into key/value pairs, or the raw string. */
function parseConditions(json?: string | null): [string, string][] | string | null {
  if (!json) return null;
  try {
    const obj = JSON.parse(json);
    if (obj && typeof obj === 'object' && !Array.isArray(obj)) {
      return Object.entries(obj as Record<string, unknown>).map(([k, v]) => [k, String(v)]);
    }
    return json;
  } catch {
    return json;
  }
}

export function RoutingTab({ details, onOpenPartner }: RoutingTabProps) {
  const { details: partners, loading, error, truncated } = details;
  const [filter, setFilter] = useState<ActiveFilter>('all');
  const [search, setSearch] = useState('');
  const [view, setView] = useState<HubView>('grid');

  const allRows = useMemo<RuleRow[]>(() => {
    const out: RuleRow[] = [];
    for (const d of partners) {
      const byId = new Map((d.connectors ?? []).map((c) => [c.connectorId, c]));
      for (const rule of d.routingRules ?? []) {
        out.push({
          partner: d,
          rule,
          target: rule.targetConnectorId ? byId.get(rule.targetConnectorId) : undefined,
        });
      }
    }
    out.sort((a, b) => a.partner.name.localeCompare(b.partner.name) || a.rule.priority - b.rule.priority);
    return out;
  }, [partners]);

  const counts = useMemo(() => {
    let active = 0;
    for (const r of allRows) if (r.rule.isActive) active += 1;
    return { all: allRows.length, active, inactive: allRows.length - active };
  }, [allRows]);

  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    return allRows.filter((r) => {
      if (filter === 'active' && !r.rule.isActive) return false;
      if (filter === 'inactive' && r.rule.isActive) return false;
      if (q) {
        const hay = `${r.partner.name} ${r.target?.connectorType ?? ''}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });
  }, [allRows, filter, search]);

  const tabs: FilterBarTab[] = [
    { value: 'all', label: 'All', count: counts.all },
    { value: 'active', label: 'Active', count: counts.active },
    { value: 'inactive', label: 'Inactive', count: counts.inactive },
  ];

  if (loading && partners.length === 0) {
    return <EmptyState icon={Route} title="Loading routing rules…" />;
  }

  if (error && allRows.length === 0) {
    return <EmptyState icon={Route} title="Couldn't load routing rules" description={error} />;
  }

  if (allRows.length === 0) {
    return (
      <EmptyState
        icon={Route}
        title="No routing rules configured"
        description="None of the loaded partners declare routing rules yet. Rules pick which connector handles a given instruction for a partner."
      />
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <FilterBar
        tabs={tabs}
        active={filter}
        onTabChange={(v) => setFilter(v as ActiveFilter)}
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Search partner or connector…"
        hideFilterButton
        extra={<ViewToggle view={view} onChange={setView} />}
      />

      <InfoNote icon={Route}>
        Routing rules select a connector within a partner, ordered by priority. Capability-driven cross-partner
        selection (by market, currency and method) is modelled in the connector abstraction but not yet wired into
        this view.
      </InfoNote>

      {truncated && (
        <InfoNote>
          Showing rules for the first {DETAIL_FETCH_CAP} partners. Per-partner detail is fetched on demand and bounded
          to keep this tab responsive.
        </InfoNote>
      )}

      {rows.length === 0 ? (
        <EmptyState icon={Route} title="No rules match" description="No routing rule matches the current filters." />
      ) : view === 'grid' ? (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          {rows.map((r) => (
            <RouteCard key={r.rule.routingRuleId} row={r} onOpen={() => onOpenPartner(r.partner.partnerId)} />
          ))}
        </div>
      ) : (
        <RouteTable rows={rows} onOpenPartner={onOpenPartner} />
      )}
    </div>
  );
}

function ConditionsView({ conditions }: { conditions: ReturnType<typeof parseConditions> }) {
  if (!conditions) {
    return <span className="text-[11.5px] text-[var(--color-text-tertiary)]">Any instruction</span>;
  }
  if (typeof conditions === 'string') {
    return (
      <code className="block truncate font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
        {conditions}
      </code>
    );
  }
  return (
    <div className="flex flex-wrap gap-1.5">
      {conditions.map(([k, v]) => (
        <Chip key={k} dense>
          <span className="text-[var(--color-text-tertiary)]">{k}</span>
          <span className="text-[var(--color-text-secondary)]">{v}</span>
        </Chip>
      ))}
    </div>
  );
}

function TargetView({ target, targetId }: { target?: PartnerConnectorItem; targetId?: string | null }) {
  if (target) {
    return (
      <span className="inline-flex items-center gap-1.5">
        <span className="font-medium text-[var(--color-text-primary)]">{target.connectorType}</span>
        <span className="font-[family-name:var(--font-mono)] text-[10.5px] text-[var(--color-text-tertiary)]">
          {target.connectorId.slice(0, 8)}
        </span>
      </span>
    );
  }
  if (targetId) {
    return (
      <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
        {targetId.slice(0, 8)}
      </span>
    );
  }
  return <span className="text-[var(--color-text-tertiary)]">—</span>;
}

function RouteCard({ row, onOpen }: { row: RuleRow; onOpen: () => void }) {
  const conditions = parseConditions(row.rule.conditionsJson);
  return (
    <div className="flex flex-col gap-3.5 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-5">
      <div className="flex items-start justify-between gap-3">
        <button type="button" onClick={onOpen} className="flex min-w-0 items-center gap-2.5 text-left">
          <AgentAvatar name={row.partner.name} size={32} />
          <span className="truncate text-sm font-semibold text-[var(--color-text-primary)]">{row.partner.name}</span>
        </button>
        <div className="flex flex-none items-center gap-2">
          <Chip dense>
            <span className="text-[var(--color-text-tertiary)]">priority</span>
            <span className="font-[family-name:var(--font-mono)] text-[var(--color-text-secondary)]">
              {row.rule.priority}
            </span>
          </Chip>
          <Pill tone={row.rule.isActive ? 'success' : 'muted'} dot>
            {row.rule.isActive ? 'Active' : 'Inactive'}
          </Pill>
        </div>
      </div>

      <div>
        <p className="mb-1.5 text-[10.5px] uppercase tracking-wide text-[var(--color-text-tertiary)]">When</p>
        <ConditionsView conditions={conditions} />
      </div>

      <div className="flex items-center gap-2 border-t border-[var(--color-border-light)] pt-3 text-[13px]">
        <span className="text-[10.5px] uppercase tracking-wide text-[var(--color-text-tertiary)]">Route to</span>
        <ArrowRight size={13} className="text-[var(--color-text-tertiary)]" />
        <TargetView target={row.target} targetId={row.rule.targetConnectorId} />
      </div>
    </div>
  );
}

function RouteTable({ rows, onOpenPartner }: { rows: RuleRow[]; onOpenPartner: (partnerId: string) => void }) {
  return (
    <Panel bodyClassName="overflow-x-auto">
      <table className="w-full border-collapse text-left text-[13px]">
        <thead>
          <tr className="border-b border-[var(--color-border-light)] text-[11px] uppercase tracking-wide text-[var(--color-text-tertiary)]">
            <th className="px-5 py-3 font-medium">Partner</th>
            <th className="px-3 py-3 text-right font-medium">Priority</th>
            <th className="px-3 py-3 font-medium">Status</th>
            <th className="px-3 py-3 font-medium">When</th>
            <th className="px-5 py-3 font-medium">Route to</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr
              key={r.rule.routingRuleId}
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
              <td className="px-3 py-3 text-right font-[family-name:var(--font-mono)] text-[var(--color-text-secondary)]">
                {r.rule.priority}
              </td>
              <td className="px-3 py-3">
                <Pill tone={r.rule.isActive ? 'success' : 'muted'} dot>
                  {r.rule.isActive ? 'Active' : 'Inactive'}
                </Pill>
              </td>
              <td className="max-w-[280px] px-3 py-3">
                <ConditionsView conditions={parseConditions(r.rule.conditionsJson)} />
              </td>
              <td className="px-5 py-3">
                <TargetView target={r.target} targetId={r.rule.targetConnectorId} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </Panel>
  );
}
