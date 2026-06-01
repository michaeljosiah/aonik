// Partner Network hub — operator surface for Spec 031 (partners: B2B /
// cross-border money plumbing). Upgrades the former simple /catalog/partners
// list into a six-tab hub with an internal left sub-nav, modelled on
// Templates/aonik-admin-starterkit/screens/partner-hub.jsx.
//
// Data fidelity = "real data, honest gaps":
//   • Overview / Partners / Coverage aggregate partnerService.list (one page,
//     up to PARTNER_LOAD_CAP) client-side.
//   • Routing / Activity stitch together per-partner partnerService.get detail
//     via a single bounded fan-out (usePartnerDetails), latched on first use.
//   • Updates is an honest "awaiting backend" surface — there is no webhook
//     inbox endpoint yet (gap C4).
// No throughput / settlement / fee telemetry is invented; where a metric has no
// backing field we either omit it or say so.
//
// The export name `CatalogPartnersPage` is preserved so the finance module's
// route + workspace-panel registration need no changes. The standalone partner
// detail page (/catalog/partners/:partnerId) is unchanged.

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import {
  Activity as ActivityIcon,
  AlertCircle,
  Globe,
  Inbox,
  LayoutDashboard,
  Network,
  Plus,
  RefreshCw,
  Route as RouteIcon,
  type LucideIcon,
} from 'lucide-react';

import { PageHeader } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { CreatePartnerDialog } from '@/components/dialogs/CreatePartnerDialog';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { cn } from '@/lib/utils';
import { partnerService } from '@/services/partnerService';
import type { CreatePartnerRequest } from '@/types/partners';

import { usePartnerDetails, usePartnerNetwork } from './partner-network/usePartnerNetwork';
import { OverviewTab } from './partner-network/OverviewTab';
import { PartnersTab } from './partner-network/PartnersTab';
import { CoverageTab } from './partner-network/CoverageTab';
import { RoutingTab } from './partner-network/RoutingTab';
import { ActivityTab } from './partner-network/ActivityTab';
import { UpdatesTab } from './partner-network/UpdatesTab';

type HubTabId = 'overview' | 'partners' | 'coverage' | 'routing' | 'activity' | 'updates';

interface HubNavItem {
  id: HubTabId;
  label: string;
  icon: LucideIcon;
}

const NAV_GROUPS: { label: string; items: HubNavItem[] }[] = [
  {
    label: 'Network',
    items: [
      { id: 'overview', label: 'Overview', icon: LayoutDashboard },
      { id: 'partners', label: 'Partners', icon: Network },
      { id: 'coverage', label: 'Coverage', icon: Globe },
    ],
  },
  {
    label: 'Money movement',
    items: [
      { id: 'routing', label: 'Routing', icon: RouteIcon },
      { id: 'activity', label: 'Activity', icon: ActivityIcon },
      { id: 'updates', label: 'Updates', icon: Inbox },
    ],
  },
];

const TAB_TITLE: Record<HubTabId, { title: string; subtitle: string }> = {
  overview: { title: 'Partner Network', subtitle: '' },
  partners: { title: 'Partners', subtitle: 'Every connected partner and its configuration' },
  coverage: { title: 'Coverage', subtitle: 'Which markets each partner serves' },
  routing: { title: 'Routing', subtitle: 'How money-movement instructions are routed to connectors' },
  activity: { title: 'Activity', subtitle: 'Recent partner transmission attempts' },
  updates: { title: 'Updates', subtitle: 'Inbound partner webhook events' },
};

export function CatalogPartnersPage() {
  const navigate = useNavigate();
  const data = usePartnerNetwork();
  const [activeTab, setActiveTab] = useState<HubTabId>('overview');
  const [createOpen, setCreateOpen] = useState(false);

  // The Routing/Activity per-partner fan-out is lazy and latched: it is not paid
  // until the operator first opens one of those tabs, and stays armed afterwards
  // so toggling between them (or away and back) doesn't refetch.
  const [detailsArmed, setDetailsArmed] = useState(false);
  useEffect(() => {
    if (activeTab === 'routing' || activeTab === 'activity') setDetailsArmed(true);
  }, [activeTab]);
  const details = usePartnerDetails(data.partners, detailsArmed);

  const onOpenPartner = useCallback(
    (partnerId: string) => navigate(`/catalog/partners/${partnerId}`),
    [navigate],
  );

  const handleCreate = useCallback(
    async (req: CreatePartnerRequest) => {
      try {
        await partnerService.create(req);
        toast.success('Partner created');
        data.reload();
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Failed to create partner';
        toast.error(message);
        throw err;
      }
    },
    [data],
  );

  const overviewSubtitle = useMemo(
    () =>
      data.totalCount === 1
        ? '1 partner moving money on your behalf'
        : `${data.totalCount} partners moving money on your behalf`,
    [data.totalCount],
  );

  // Full-screen loader only on the very first load, matching prior behaviour.
  if (data.loading && data.partners.length === 0 && !data.error) {
    return <PageLoadingScreen message="Loading partner network" />;
  }

  const meta = TAB_TITLE[activeTab];
  const subtitle = activeTab === 'overview' ? overviewSubtitle : meta.subtitle;

  return (
    <div className="flex min-h-full flex-col">
      {/* Mobile sub-nav (the left rail is hidden below md). */}
      <div className="flex gap-1 overflow-x-auto border-b border-[var(--color-border-light)] px-4 py-2 md:hidden">
        {NAV_GROUPS.flatMap((g) => g.items).map((item) => (
          <HubNavButton
            key={item.id}
            item={item}
            active={activeTab === item.id}
            onClick={() => setActiveTab(item.id)}
            compact
          />
        ))}
      </div>

      <div className="flex min-h-full flex-1">
        <aside className="hidden w-52 flex-none border-r border-[var(--color-border-light)] md:block">
          <nav className="sticky top-0 flex flex-col gap-5 p-4">
            {NAV_GROUPS.map((group) => (
              <div key={group.label} className="flex flex-col gap-1">
                <p className="px-3 pb-1 text-[10.5px] font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">
                  {group.label}
                </p>
                {group.items.map((item) => (
                  <HubNavButton
                    key={item.id}
                    item={item}
                    active={activeTab === item.id}
                    onClick={() => setActiveTab(item.id)}
                  />
                ))}
              </div>
            ))}
          </nav>
        </aside>

        <main className="min-w-0 flex-1">
          <div className="flex flex-col gap-5 p-6 md:px-8">
            <PageHeader
              eyebrow="Finance · Network"
              title={meta.title}
              subtitle={subtitle}
              actions={
                <>
                  <Button variant="outline" size="sm" onClick={data.reload} disabled={data.loading}>
                    <RefreshCw className={cn('h-3 w-3', data.loading && 'animate-spin')} />
                    Re-sync
                  </Button>
                  <Button size="sm" onClick={() => setCreateOpen(true)}>
                    <Plus className="h-3 w-3" />
                    Add partner
                  </Button>
                </>
              }
            />

            {data.error && (
              <div className="flex items-center gap-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
                <AlertCircle className="h-4 w-4 flex-none" />
                <span className="flex-1">{data.error}</span>
                <Button variant="outline" size="sm" onClick={data.reload}>
                  <RefreshCw className="h-3 w-3" />
                  Retry
                </Button>
              </div>
            )}

            {activeTab === 'overview' && (
              <OverviewTab
                data={data}
                onOpenPartner={onOpenPartner}
                onViewAllPartners={() => setActiveTab('partners')}
              />
            )}
            {activeTab === 'partners' && <PartnersTab data={data} onOpenPartner={onOpenPartner} />}
            {activeTab === 'coverage' && <CoverageTab data={data} onOpenPartner={onOpenPartner} />}
            {activeTab === 'routing' && <RoutingTab details={details} onOpenPartner={onOpenPartner} />}
            {activeTab === 'activity' && <ActivityTab details={details} onOpenPartner={onOpenPartner} />}
            {activeTab === 'updates' && <UpdatesTab />}
          </div>
        </main>
      </div>

      <CreatePartnerDialog open={createOpen} onOpenChange={setCreateOpen} onSave={handleCreate} />
    </div>
  );
}

function HubNavButton({
  item,
  active,
  onClick,
  compact = false,
}: {
  item: HubNavItem;
  active: boolean;
  onClick: () => void;
  compact?: boolean;
}) {
  const Icon = item.icon;
  return (
    <button
      type="button"
      onClick={onClick}
      aria-current={active ? 'page' : undefined}
      className={cn(
        'flex items-center gap-2.5 rounded-lg text-[13px] font-medium transition-colors',
        compact ? 'flex-none px-3 py-1.5' : 'w-full px-3 py-2',
        active
          ? 'bg-[var(--color-brand-primary-10)] text-[var(--color-brand-primary)]'
          : 'text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-inset)] hover:text-[var(--color-text-primary)]',
      )}
    >
      <Icon size={15} className={cn('flex-none', !active && 'text-[var(--color-text-tertiary)]')} />
      <span className="whitespace-nowrap">{item.label}</span>
    </button>
  );
}
