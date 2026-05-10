// Partners — visual port of ScreenPartners in
// templates/aonik-admin-starterkit/screens/journal-partners.jsx, wired to
// the existing /admin/partners endpoint.
//
// Differences from the template, called out so they don't read as gaps:
//   • Throughput / error rate / fee / latency / heartbeat are runtime
//     metrics that aren't on PartnerListItem. We surface real DTO
//     fields instead — branch / connector / routing-rule / linked-biller
//     counts. Same "scannable card with four numbers" shape.
//   • Coverage countries map to the template's "rails" pill row (mono,
//     teal-tinted), capped at first 6 with overflow indicator.
//   • "Trace" action button is dropped — needs a partner-level trace
//     viewer that doesn't exist yet.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import { AlertCircle, Plus, RefreshCw } from 'lucide-react';

import {
  AgentAvatar,
  Card as AonikCard,
  FilterBar,
  type FilterBarTab,
  PageHeader,
  Pill,
  type PillTone,
} from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { CreatePartnerDialog } from '@/components/dialogs/CreatePartnerDialog';
import { DataTablePagination } from '@/components/ui/data-table';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { partnerService } from '@/services/partnerService';
import type { PagedResult } from '@/types';
import type { CreatePartnerRequest, PartnerListItem } from '@/types/partners';

// ─── Helpers ─────────────────────────────────────────────────────────────

const STATUS_TONE: Record<string, PillTone> = {
  Active: 'success',
  Pending: 'warning',
  Suspended: 'danger',
  Inactive: 'muted',
  Healthy: 'success',
  Degraded: 'warning',
  Incident: 'danger',
};

const FILTER_TABS: FilterBarTab[] = [
  { value: '', label: 'All' },
  { value: 'Active', label: 'Active' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Suspended', label: 'Suspended' },
  { value: 'Inactive', label: 'Inactive' },
];

function formatDate(value?: string | null): string {
  if (!value) return '—';
  return new Date(value).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
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
  return `${days}d ago`;
}

// ─── Page ────────────────────────────────────────────────────────────────

export function CatalogPartnersPage() {
  const navigate = useNavigate();

  const [partners, setPartners] = useState<PartnerListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(24);
  const [totalCount, setTotalCount] = useState(0);
  const [createOpen, setCreateOpen] = useState(false);
  const requestIdRef = useRef(0);

  const loadPartners = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);
    try {
      const result: PagedResult<PartnerListItem> = await partnerService.list({
        pageNumber,
        pageSize,
        status: statusFilter || undefined,
        search: searchQuery || undefined,
      });
      if (requestIdRef.current !== requestId) return;
      setPartners(result.items);
      setTotalCount(result.totalCount);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load partners.');
    } finally {
      if (requestIdRef.current === requestId) {
        setLoading(false);
        setInitialLoad(false);
      }
    }
  }, [pageNumber, pageSize, statusFilter, searchQuery]);

  useEffect(() => {
    void loadPartners();
  }, [loadPartners]);

  useEffect(() => {
    setPageNumber(1);
  }, [searchQuery, statusFilter]);

  const handleCreate = useCallback(
    async (data: CreatePartnerRequest) => {
      try {
        await partnerService.create(data);
        toast.success('Partner created');
        await loadPartners();
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Failed to create partner';
        toast.error(message);
        throw err;
      }
    },
    [loadPartners],
  );

  // ─── Header counts ────────────────────────────────────────────────────

  const statusCounts = useMemo(() => {
    const counts = new Map<string, number>();
    for (const p of partners) counts.set(p.status, (counts.get(p.status) ?? 0) + 1);
    return counts;
  }, [partners]);

  const subtitle = (() => {
    if (totalCount === 0) {
      return 'Payment-rail and processor partners';
    }
    const fragments: string[] = [`${totalCount.toLocaleString()} total`];
    if (statusCounts.has('Suspended')) {
      fragments.push(`${statusCounts.get('Suspended')} suspended`);
    }
    if (statusCounts.has('Pending')) {
      fragments.push(`${statusCounts.get('Pending')} pending`);
    }
    return fragments.join(' · ');
  })();

  if (initialLoad) {
    return <PageLoadingScreen message="Loading partners" />;
  }

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="Finance · Network"
        title="Partners"
        subtitle={subtitle}
        actions={
          <>
            <Button variant="outline" size="sm" onClick={() => void loadPartners()} disabled={loading}>
              <RefreshCw className={'h-3 w-3 ' + (loading ? 'animate-spin' : '')} />
              Re-sync
            </Button>
            <Button size="sm" onClick={() => setCreateOpen(true)}>
              <Plus className="h-3 w-3" />
              Add partner
            </Button>
          </>
        }
      />

      {error && (
        <div className="flex items-center gap-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
          <AlertCircle className="h-4 w-4 flex-none" />
          <span className="flex-1">{error}</span>
          <Button variant="outline" size="sm" onClick={() => void loadPartners()}>
            <RefreshCw className="h-3 w-3" />
            Retry
          </Button>
        </div>
      )}

      <FilterBar
        tabs={FILTER_TABS}
        active={statusFilter}
        onTabChange={setStatusFilter}
        search={searchQuery}
        onSearchChange={setSearchQuery}
        searchPlaceholder="Filter partners by name, country…"
        hideFilterButton
      />

      {loading && partners.length === 0 ? (
        <AonikCard>
          <div className="flex items-center justify-center py-10">
            <RefreshCw className="h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
          </div>
        </AonikCard>
      ) : partners.length === 0 ? (
        <AonikCard>
          <div className="flex flex-col items-center justify-center py-10 text-center">
            <p className="text-sm font-medium text-[var(--color-text-primary)]">
              No partners found
            </p>
            <p className="mt-1 text-xs text-[var(--color-text-tertiary)]">
              {searchQuery || statusFilter
                ? 'Try adjusting the active tab or search.'
                : 'Add the first partner to start routing payments.'}
            </p>
          </div>
        </AonikCard>
      ) : (
        <div className="grid grid-cols-1 gap-3.5 md:grid-cols-2 xl:grid-cols-3">
          {partners.map((partner) => (
            <PartnerCard
              key={partner.partnerId}
              partner={partner}
              onClick={() => navigate(`/catalog/partners/${partner.partnerId}`)}
            />
          ))}
        </div>
      )}

      {totalCount > pageSize && (
        <AonikCard padding={0}>
          <DataTablePagination
            pageNumber={pageNumber}
            pageSize={pageSize}
            totalCount={totalCount}
            onPageChange={setPageNumber}
            onPageSizeChange={(n) => {
              setPageSize(n);
              setPageNumber(1);
            }}
            pageSizeOptions={[12, 24, 48, 96]}
          />
        </AonikCard>
      )}

      <CreatePartnerDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        onSave={handleCreate}
      />
    </div>
  );
}

// ─── Partner card ────────────────────────────────────────────────────────

interface PartnerCardProps {
  partner: PartnerListItem;
  onClick: () => void;
}

function PartnerCard({ partner, onClick }: PartnerCardProps) {
  const tone = STATUS_TONE[partner.status] ?? 'default';
  const visibleCountries = partner.coverageCountries.slice(0, 6);
  const overflowCount = partner.coverageCountries.length - visibleCountries.length;

  const stats: Array<[string, string | number]> = [
    ['Branches', partner.branchCount],
    ['Connectors', partner.connectorCount],
    ['Routing rules', partner.activeRoutingRuleCount],
    ['Linked billers', partner.linkedBillerCount],
  ];

  return (
    <button
      type="button"
      onClick={onClick}
      className="flex flex-col gap-3.5 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-[18px] text-left transition-colors hover:border-[var(--color-brand-primary-20)] hover:bg-[var(--color-surface-inset)]"
    >
      <div className="flex items-center gap-3">
        <AgentAvatar name={partner.name} size={36} />
        <div className="min-w-0 flex-1">
          <div className="truncate text-[14px] font-semibold text-[var(--color-text-primary)]">
            {partner.name}
          </div>
          <div className="truncate font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
            {partner.partnerId.slice(0, 8).toUpperCase()}
          </div>
        </div>
        <Pill tone={tone} dot>
          {partner.status}
        </Pill>
      </div>

      {partner.coverageCountries.length > 0 && (
        <div className="flex flex-wrap gap-1">
          {visibleCountries.map((country) => (
            <span
              key={country}
              className="rounded bg-[var(--color-brand-primary-10)] px-1.5 py-0.5 font-[family-name:var(--font-mono)] text-[10px] font-semibold text-[var(--color-brand-primary)]"
            >
              {country}
            </span>
          ))}
          {overflowCount > 0 && (
            <span className="rounded bg-[var(--color-surface-inset)] px-1.5 py-0.5 font-[family-name:var(--font-mono)] text-[10px] font-semibold text-[var(--color-text-tertiary)]">
              +{overflowCount}
            </span>
          )}
        </div>
      )}

      <div className="grid grid-cols-2 gap-2.5 border-t border-[var(--color-border-light)] pt-2.5">
        {stats.map(([label, value]) => (
          <div key={label}>
            <div className="text-[10px] uppercase tracking-[0.04em] text-[var(--color-text-tertiary)]">
              {label}
            </div>
            <div className="font-[family-name:var(--font-mono)] text-[13px] font-medium text-[var(--color-text-primary)]">
              {value}
            </div>
          </div>
        ))}
      </div>

      <div className="flex items-center justify-between font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
        <span>added · {formatDate(partner.createdAt)}</span>
        {partner.updatedAt && <span>updated · {formatRelative(partner.updatedAt)}</span>}
      </div>
    </button>
  );
}
