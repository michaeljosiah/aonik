import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { AlertCircle, ClipboardList, Eye, Plus } from 'lucide-react';

import type { OrderListItem, PagedResult } from '@/types';
import {
  DataTable,
  DataTableHeader,
  DataTablePagination,
  DataTableRowActions,
  type ColumnDef,
  type DataTableAction,
  type FilterOption,
} from '@/components/ui/data-table';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { orderService, type ListOrdersParams } from '@/services/orderService';

const statusStyles: Record<string, { text: string; bg: string }> = {
  Draft: { text: 'text-[var(--color-text-secondary)]', bg: 'bg-[var(--color-surface-inset)]' },
  PendingCompliance: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  Submitted: { text: 'text-[var(--color-brand-primary)]', bg: 'bg-[var(--color-brand-primary-light)]' },
  Complete: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
  Completed: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
  Cancelled: { text: 'text-[var(--color-text-tertiary)]', bg: 'bg-[var(--color-surface-inset)]' },
  Failed: { text: 'text-[var(--color-error)]', bg: 'bg-[var(--color-error-light)]' },
  Expired: { text: 'text-[var(--color-text-tertiary)]', bg: 'bg-[var(--color-surface-inset)]' },
};

const statusFilterOptions: FilterOption[] = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Submitted', label: 'Submitted' },
  { value: 'PendingCompliance', label: 'Pending compliance' },
  { value: 'Complete', label: 'Complete' },
  { value: 'Cancelled', label: 'Cancelled' },
  { value: 'Failed', label: 'Failed' },
  { value: 'Expired', label: 'Expired' },
];

const orderTypeFilterOptions: FilterOption[] = [
  { value: 'BillPayment', label: 'Bill payment' },
];

const formatDate = (dateString?: string | null) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
};

const formatMoney = (amount: number | null | undefined, currency?: string | null) => {
  if (amount == null) return '—';
  const cur = (currency ?? '').trim();
  if (!cur) return amount.toLocaleString('en-US');
  return `${cur} ${amount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
};

const shortId = (id: string) => {
  if (!id) return '';
  return id.length <= 10 ? id : `${id.slice(0, 8)}…${id.slice(-4)}`;
};

export function OrdersListPage() {
  const navigate = useNavigate();
  const [orders, setOrders] = useState<OrderListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [orderTypeFilter, setOrderTypeFilter] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const requestIdRef = useRef(0);

  const loadOrders = useCallback(async () => {
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setLoading(true);
    setError(null);

    try {
      const params: ListOrdersParams = {
        pageNumber,
        pageSize,
        status: statusFilter || undefined,
        orderType: orderTypeFilter || undefined,
        search: searchQuery || undefined,
      };

      const result: PagedResult<OrderListItem> = await orderService.listOrders(params);
      if (requestIdRef.current !== requestId) return;

      setOrders(result.items);
      setTotalCount(result.totalCount);
      setLoading(false);
    } catch (err: unknown) {
      if (requestIdRef.current !== requestId) return;
      console.error('Failed to load orders:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load orders. Please try again.');
      setLoading(false);
    }
  }, [orderTypeFilter, pageNumber, pageSize, searchQuery, statusFilter]);

  useEffect(() => {
    loadOrders();
  }, [loadOrders]);

  useEffect(() => {
    setPageNumber(1);
  }, [searchQuery, statusFilter, orderTypeFilter]);

  const getRowActions = (order: OrderListItem): DataTableAction[] => {
    const canView = order.orderType === 'BillPayment';
    return [
      {
        label: 'View',
        icon: <Eye className="w-4 h-4" />,
        onClick: () => {
          if (canView) {
            navigate(`/orders/bill-payments/${order.orderId}`);
          }
        },
        disabled: !canView,
      },
    ];
  };

  const columns: ColumnDef<OrderListItem>[] = [
    {
      id: 'order',
      header: 'Order',
      accessorFn: (row) => row.orderId,
      sortable: true,
      cell: (order) => (
        <div>
          <p className="font-medium text-[var(--color-text-primary)]">{shortId(order.orderId)}</p>
          <p className="text-xs text-[var(--color-text-tertiary)]">{order.orderType}</p>
        </div>
      ),
    },
    {
      id: 'status',
      header: 'Status',
      accessorKey: 'status',
      sortable: true,
      cell: (order) => {
        const style = statusStyles[order.status] ?? {
          text: 'text-[var(--color-text-secondary)]',
          bg: 'bg-[var(--color-surface-inset)]',
        };
        return (
          <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${style.bg} ${style.text}`}>
            {order.status}
          </span>
        );
      },
    },
    {
      id: 'payer',
      header: 'Payer',
      accessorFn: (row) => row.payerName || '',
      sortable: true,
      cell: (order) => (
        <div>
          <p className="text-sm text-[var(--color-text-primary)]">{order.payerName || '—'}</p>
          <p className="text-xs text-[var(--color-text-tertiary)]">{order.originCountry || '—'}</p>
        </div>
      ),
    },
    {
      id: 'amountIn',
      header: 'Total In',
      accessorFn: (row) => row.totalAmountIn,
      sortable: true,
      className: 'text-right',
      headerClassName: 'text-right',
      cell: (order) => (
        <span className="text-sm text-[var(--color-text-secondary)]">
          {formatMoney(order.totalAmountIn, order.originCurrency)}
        </span>
      ),
    },
    {
      id: 'amountOut',
      header: 'Total Out',
      accessorFn: (row) => row.totalAmountOut ?? null,
      sortable: true,
      className: 'text-right',
      headerClassName: 'text-right',
      cell: (order) => (
        <span className="text-sm text-[var(--color-text-secondary)]">
          {order.totalAmountOut == null ? '—' : formatMoney(order.totalAmountOut, order.destinationCurrency)}
        </span>
      ),
    },
    {
      id: 'createdAt',
      header: 'Created',
      accessorFn: (row) => (row.createdAt ? new Date(row.createdAt) : null),
      sortable: true,
      cell: (order) => (
        <span className="text-sm text-[var(--color-text-secondary)]">{formatDate(order.createdAt)}</span>
      ),
    },
  ];

  const breadcrumbItems = [{ label: 'Orders', icon: <ClipboardList className="w-3.5 h-3.5" /> }];

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Orders</h1>
          <p className="text-[var(--color-text-secondary)]">Track business intent across products and fulfilment.</p>
        </div>
        <Button onClick={() => navigate('/orders/bill-payments/new')} className="rounded-sm">
          <Plus className="w-4 h-4 mr-2" />
          New bill payment
        </Button>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span>{error}</span>
            <Button variant="outline" size="sm" onClick={loadOrders} className="ml-auto">
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-4">
          <DataTableHeader
            searchValue={searchQuery}
            onSearchChange={setSearchQuery}
            searchPlaceholder="Search orders"
            filterValue={statusFilter}
            onFilterChange={setStatusFilter}
            filterOptions={statusFilterOptions}
            filterPlaceholder="Status"
            showViewToggle={false}
            actions={
              <div className="flex items-center gap-2">
                <Select
                  value={orderTypeFilter || undefined}
                  onValueChange={(value) => setOrderTypeFilter(value === '__all__' ? '' : value)}
                >
                  <SelectTrigger aria-label="Order type" className="h-9 w-[180px] rounded-sm">
                    <SelectValue placeholder="Type" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__all__">Type</SelectItem>
                    {orderTypeFilterOptions.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            }
            className="px-0 border-b-0"
          />

          <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
            <DataTable
              data={orders}
              columns={columns}
              getRowId={(o) => o.orderId}
              selectedIds={selectedIds}
              onSelectionChange={setSelectedIds}
              showCheckboxes={true}
              loading={loading}
              loadingMessage="Loading orders..."
              emptyIcon={<ClipboardList className="w-12 h-12" />}
              emptyTitle="No orders found"
              emptyDescription={
                searchQuery || statusFilter || orderTypeFilter
                  ? 'Try adjusting your filters.'
                  : 'Orders will appear here as they are created.'
              }
              rowActions={(order) => <DataTableRowActions actions={getRowActions(order)} />}
              rowActionsPosition="start"
            />
          </div>

          <div className="pt-4">
            <DataTablePagination
              pageNumber={pageNumber}
              pageSize={pageSize}
              totalCount={totalCount}
              onPageChange={setPageNumber}
              onPageSizeChange={(newSize) => {
                setPageSize(newSize);
                setPageNumber(1);
              }}
              className="px-0 border-t-0"
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
