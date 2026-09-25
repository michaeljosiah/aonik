import { ArrowDownIcon, ArrowUpDownIcon, ArrowUpIcon } from 'lucide-react';
import { useMemo, useState } from 'react';

import { cn } from '@/lib/utils';
import { Checkbox } from '@/components/ui/checkbox';
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';

export type SortDirection = 'asc' | 'desc' | null;

export interface ColumnDef<T> {
  id: string;
  header: string;
  accessorKey?: keyof T;
  accessorFn?: (row: T) => unknown;
  cell?: (row: T) => React.ReactNode;
  sortable?: boolean;
  /** Right-aligned mono tabular figures (amounts, counts). */
  numeric?: boolean;
  className?: string;
  headerClassName?: string;
}

export interface DataTableProps<T> {
  data: T[];
  columns: ColumnDef<T>[];
  getRowId: (row: T) => string;
  onRowClick?: (row: T) => void;
  selectedIds?: Set<string>;
  onSelectionChange?: (selectedIds: Set<string>) => void;
  showCheckboxes?: boolean;
  /** Render an icon for each row (displayed between checkbox and first column) */
  rowIcon?: (row: T) => React.ReactNode;
  loading?: boolean;
  loadingMessage?: string;
  emptyIcon?: React.ReactNode;
  emptyTitle?: string;
  emptyDescription?: string;
  /** Render row actions (3-dot menu) */
  rowActions?: (row: T) => React.ReactNode;
  /** Position of row actions: 'start' (after first column) or 'end' (last column). Default: 'end' */
  rowActionsPosition?: 'start' | 'end';
  className?: string;
}

const LOADING_ROWS = 5;

function compareValues(aValue: unknown, bValue: unknown, direction: 'asc' | 'desc'): number {
  if (aValue == null && bValue == null) return 0;
  if (aValue == null) return direction === 'asc' ? 1 : -1;
  if (bValue == null) return direction === 'asc' ? -1 : 1;

  let comparison: number;
  if (typeof aValue === 'number' && typeof bValue === 'number') {
    comparison = aValue - bValue;
  } else if (aValue instanceof Date && bValue instanceof Date) {
    comparison = aValue.getTime() - bValue.getTime();
  } else {
    comparison = String(aValue).localeCompare(String(bValue));
  }
  return direction === 'asc' ? comparison : -comparison;
}

export function DataTable<T>({
  data,
  columns,
  getRowId,
  onRowClick,
  selectedIds = new Set(),
  onSelectionChange,
  showCheckboxes = true,
  rowIcon,
  loading = false,
  loadingMessage = 'Loading…',
  emptyIcon,
  emptyTitle = 'Nothing here yet',
  emptyDescription,
  rowActions,
  rowActionsPosition = 'end',
  className,
}: DataTableProps<T>) {
  const [sortColumn, setSortColumn] = useState<string | null>(null);
  const [sortDirection, setSortDirection] = useState<SortDirection>(null);

  // Cycle: asc -> desc -> none
  const handleSort = (columnId: string) => {
    if (sortColumn === columnId) {
      if (sortDirection === 'asc') {
        setSortDirection('desc');
      } else if (sortDirection === 'desc') {
        setSortColumn(null);
        setSortDirection(null);
      }
    } else {
      setSortColumn(columnId);
      setSortDirection('asc');
    }
  };

  const sortedData = useMemo(() => {
    if (!sortColumn || !sortDirection) return data;
    const column = columns.find((c) => c.id === sortColumn);
    if (!column || (!column.accessorFn && !column.accessorKey)) return data;
    const read = (row: T) => (column.accessorFn ? column.accessorFn(row) : row[column.accessorKey as keyof T]);
    return [...data].sort((a, b) => compareValues(read(a), read(b), sortDirection));
  }, [data, columns, sortColumn, sortDirection]);

  const allSelected = data.length > 0 && data.every((row) => selectedIds.has(getRowId(row)));
  const someSelected = data.some((row) => selectedIds.has(getRowId(row))) && !allSelected;

  const handleSelectAll = (checked: boolean) => {
    if (!onSelectionChange) return;
    const next = new Set(selectedIds);
    data.forEach((row) => (checked ? next.add(getRowId(row)) : next.delete(getRowId(row))));
    onSelectionChange(next);
  };

  const handleSelectRow = (rowId: string, checked: boolean) => {
    if (!onSelectionChange) return;
    const next = new Set(selectedIds);
    if (checked) next.add(rowId);
    else next.delete(rowId);
    onSelectionChange(next);
  };

  const totalColumns =
    columns.length + (showCheckboxes ? 1 : 0) + (rowIcon ? 1 : 0) + (rowActions ? 1 : 0);

  const renderColumnHeader = (column: ColumnDef<T>) => {
    const isSorted = sortColumn === column.id;
    const ariaSort = isSorted ? (sortDirection === 'asc' ? 'ascending' : 'descending') : undefined;
    return (
      <TableHead
        key={column.id}
        numeric={column.numeric}
        aria-sort={ariaSort}
        className={cn('px-3', column.headerClassName)}
      >
        {column.sortable ? (
          <button
            type="button"
            onClick={() => handleSort(column.id)}
            className={cn(
              '-mx-2 inline-flex h-8 items-center gap-1.5 rounded-md px-2 outline-none transition-colors hover:bg-accent hover:text-accent-foreground focus-visible:ring-[3px] focus-visible:ring-ring/50',
              column.numeric && 'flex-row-reverse',
            )}
          >
            {column.header}
            {isSorted && sortDirection === 'asc' ? (
              <ArrowUpIcon className="size-3.5" />
            ) : isSorted && sortDirection === 'desc' ? (
              <ArrowDownIcon className="size-3.5" />
            ) : (
              <ArrowUpDownIcon className="size-3.5 text-muted-foreground" />
            )}
          </button>
        ) : (
          column.header
        )}
      </TableHead>
    );
  };

  const renderColumnCell = (column: ColumnDef<T>, row: T) => {
    let content: React.ReactNode;
    if (column.cell) content = column.cell(row);
    else if (column.accessorFn) content = String(column.accessorFn(row) ?? '');
    else if (column.accessorKey) content = String(row[column.accessorKey] ?? '');
    else content = '';

    return (
      <TableCell key={column.id} numeric={column.numeric} className={cn('px-3 py-2.5', column.className)}>
        {content}
      </TableCell>
    );
  };

  const actionsCell = (row: T, width: string) => (
    <TableCell className={cn(width, 'px-2 py-2.5')} onClick={(event) => event.stopPropagation()}>
      {rowActions?.(row)}
    </TableCell>
  );

  return (
    <div className={className}>
      <Table>
        <TableHeader>
          <TableRow className="hover:bg-transparent">
            {showCheckboxes && (
              <TableHead className="w-10 px-3">
                <Checkbox
                  aria-label="Select all rows"
                  checked={allSelected ? true : someSelected ? 'indeterminate' : false}
                  onCheckedChange={(checked) => handleSelectAll(checked === true)}
                  disabled={!onSelectionChange || data.length === 0}
                />
              </TableHead>
            )}
            {rowIcon && <TableHead className="w-10" />}
            {columns.length > 0 && renderColumnHeader(columns[0])}
            {rowActions && rowActionsPosition === 'start' && <TableHead className="w-10" />}
            {columns.slice(1).map(renderColumnHeader)}
            {rowActions && rowActionsPosition === 'end' && <TableHead className="w-12" />}
          </TableRow>
        </TableHeader>
        <TableBody>
          {loading ? (
            Array.from({ length: LOADING_ROWS }, (_, i) => (
              <TableRow key={`loading-${i}`} className="hover:bg-transparent">
                <TableCell colSpan={totalColumns} className="px-3 py-3">
                  <Skeleton className="h-4 w-full" />
                  {i === 0 && <span className="sr-only">{loadingMessage}</span>}
                </TableCell>
              </TableRow>
            ))
          ) : sortedData.length === 0 ? (
            <TableRow className="hover:bg-transparent">
              <TableCell colSpan={totalColumns} className="whitespace-normal p-0">
                <Empty className="py-10 md:py-12">
                  <EmptyHeader>
                    {emptyIcon && <EmptyMedia variant="icon">{emptyIcon}</EmptyMedia>}
                    <EmptyTitle className="text-base">{emptyTitle}</EmptyTitle>
                    {emptyDescription && <EmptyDescription>{emptyDescription}</EmptyDescription>}
                  </EmptyHeader>
                </Empty>
              </TableCell>
            </TableRow>
          ) : (
            sortedData.map((row) => {
              const rowId = getRowId(row);
              const isSelected = selectedIds.has(rowId);
              return (
                <TableRow
                  key={rowId}
                  data-state={isSelected ? 'selected' : undefined}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                  className={cn(onRowClick && 'cursor-pointer')}
                >
                  {showCheckboxes && (
                    <TableCell className="w-10 px-3 py-2.5" onClick={(event) => event.stopPropagation()}>
                      <Checkbox
                        aria-label="Select row"
                        checked={isSelected}
                        onCheckedChange={(checked) => handleSelectRow(rowId, checked === true)}
                        disabled={!onSelectionChange}
                      />
                    </TableCell>
                  )}
                  {rowIcon && <TableCell className="w-10 px-3 py-2.5">{rowIcon(row)}</TableCell>}
                  {columns.length > 0 && renderColumnCell(columns[0], row)}
                  {rowActions && rowActionsPosition === 'start' && actionsCell(row, 'w-10')}
                  {columns.slice(1).map((column) => renderColumnCell(column, row))}
                  {rowActions && rowActionsPosition === 'end' && actionsCell(row, 'w-12')}
                </TableRow>
              );
            })
          )}
        </TableBody>
      </Table>
    </div>
  );
}
