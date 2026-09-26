import { useMemo, useState } from 'react';
import { DownloadIcon, PencilIcon, PlusIcon, ReceiptIcon, Trash2Icon } from 'lucide-react';

import { AonikSidebar } from '@/components/layout/aonik/AonikSidebar';
import { AonikTopBar } from '@/components/layout/aonik/AonikTopBar';
import { FilterBar, KpiTile, PageHeader, Pill, ProposalCard, type PillTone } from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import {
  DataTable,
  DataTablePagination,
  DataTableRowActions,
  type ColumnDef,
} from '@/components/ui/data-table';

/**
 * Dev-only preview of the app shell (sidebar + top bar) around a sample list
 * page, so Spec 098 P2 composites can be reviewed without a signed-in
 * backend. Mounted at /dev/shell by App.tsx in dev builds only.
 */

interface SampleOrder {
  id: string;
  customer: string;
  type: string;
  status: 'Settled' | 'Pending' | 'Failed' | 'Quoted';
  amount: number;
  createdAt: string;
}

const STATUS_TONE: Record<SampleOrder['status'], PillTone> = {
  Settled: 'success',
  Pending: 'warning',
  Failed: 'danger',
  Quoted: 'info',
};

const ORDERS: SampleOrder[] = [
  { id: 'ORD-10442', customer: 'Adaeze Okafor', type: 'Remittance', status: 'Settled', amount: 1250, createdAt: '25 Sep 2026' },
  { id: 'ORD-10441', customer: 'Kwame Mensah', type: 'Bill payment', status: 'Pending', amount: 310.5, createdAt: '25 Sep 2026' },
  { id: 'ORD-10440', customer: 'Mensah Logistics', type: 'Payout', status: 'Settled', amount: 18420, createdAt: '24 Sep 2026' },
  { id: 'ORD-10439', customer: 'Tunde Bakare', type: 'Remittance', status: 'Failed', amount: 75, createdAt: '24 Sep 2026' },
  { id: 'ORD-10438', customer: 'Efua Asante', type: 'Remittance', status: 'Quoted', amount: 640, createdAt: '23 Sep 2026' },
  { id: 'ORD-10437', customer: 'Chidi Eze', type: 'Product purchase', status: 'Settled', amount: 42.99, createdAt: '23 Sep 2026' },
];

const gbp = new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'GBP' });

export default function DevShellPage() {
  const [collapsed, setCollapsed] = useState(false);
  const [tab, setTab] = useState('all');
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const rows = useMemo(
    () =>
      ORDERS.filter((o) => (tab === 'all' ? true : o.status.toLowerCase() === tab)).filter((o) =>
        `${o.id} ${o.customer}`.toLowerCase().includes(search.toLowerCase()),
      ),
    [tab, search],
  );

  const columns: ColumnDef<SampleOrder>[] = [
    { id: 'id', header: 'Order', accessorKey: 'id', sortable: true, cell: (o) => <span className="font-mono text-xs">{o.id}</span> },
    { id: 'customer', header: 'Customer', accessorKey: 'customer', sortable: true },
    { id: 'type', header: 'Type', accessorKey: 'type' },
    { id: 'status', header: 'Status', accessorKey: 'status', cell: (o) => <Pill tone={STATUS_TONE[o.status]} dot>{o.status}</Pill> },
    { id: 'amount', header: 'Amount', accessorKey: 'amount', sortable: true, numeric: true, cell: (o) => gbp.format(o.amount) },
    { id: 'createdAt', header: 'Created', accessorKey: 'createdAt' },
  ];

  return (
    <div className="flex h-full min-h-screen bg-background">
      <AonikSidebar collapsed={collapsed} onToggle={() => setCollapsed((c) => !c)} />
      <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
        <AonikTopBar
          breadcrumb={[{ label: 'Transact', href: '/dev/shell' }, 'Orders']}
          onToggleSidebar={() => setCollapsed((c) => !c)}
          onAskAonik={() => {}}
        />
        <main className="flex-1 overflow-auto">
          <div className="mx-auto flex max-w-screen-2xl flex-col gap-6 p-6">
            <PageHeader
              title="Orders"
              subtitle="Every requested transaction and where it is now."
              actions={
                <>
                  <Button variant="outline">
                    <DownloadIcon /> Export
                  </Button>
                  <Button>
                    <PlusIcon /> New order
                  </Button>
                </>
              }
            />

            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              <KpiTile label="Settled this month" value="£48,210.00" delta="+12.4%" deltaTone="up" sparkline={[4, 6, 5, 8, 7, 9, 12]} />
              <KpiTile label="Open orders" value="38" delta="-3" deltaTone="down" />
              <KpiTile label="Failed today" value="2" delta="No change" deltaTone="neutral" />
              <KpiTile label="Average order" value="£312.40" />
            </div>

            <ProposalCard
              agent="Finance"
              confidence={0.94}
              summary="Retry ORD-10439 through the secondary partner; the primary rejected it for a KYC mismatch."
              diff={[
                { type: 'ctx', text: 'order: ORD-10439' },
                { type: 'rm', text: 'partner: primary-ng' },
                { type: 'add', text: 'partner: secondary-ng' },
              ]}
              reason="The secondary partner accepted 41 of the last 42 orders for this corridor."
              onApply={() => {}}
              onReview={() => {}}
              onDismiss={() => {}}
            />

            <FilterBar
              tabs={[
                { value: 'all', label: 'All', count: ORDERS.length },
                { value: 'pending', label: 'Pending', count: 1 },
                { value: 'settled', label: 'Settled', count: 3 },
                { value: 'failed', label: 'Failed', count: 1 },
              ]}
              active={tab}
              onTabChange={setTab}
              search={search}
              onSearchChange={setSearch}
              searchPlaceholder="Search orders"
            />

            <Card className="overflow-hidden p-0">
              <DataTable
                data={rows}
                columns={columns}
                getRowId={(o) => o.id}
                selectedIds={selected}
                onSelectionChange={setSelected}
                emptyIcon={<ReceiptIcon />}
                emptyTitle="No orders match"
                emptyDescription="Clear the search or pick another status."
                rowActions={() => (
                  <DataTableRowActions
                    actions={[
                      { label: 'Edit', icon: <PencilIcon />, onClick: () => {} },
                      { label: 'Cancel order', icon: <Trash2Icon />, onClick: () => {}, variant: 'danger' },
                    ]}
                  />
                )}
              />
              <DataTablePagination
                pageNumber={1}
                pageSize={25}
                totalCount={1204}
                onPageChange={() => {}}
                onPageSizeChange={() => {}}
              />
            </Card>
          </div>
        </main>
      </div>
    </div>
  );
}
