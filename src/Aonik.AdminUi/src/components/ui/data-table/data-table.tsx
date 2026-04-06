import * as Checkbox from '@radix-ui/react-checkbox';
import { ArrowUpDown, ArrowUp, ArrowDown, Check, RefreshCw } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useMemo, useState } from 'react';

export type SortDirection = 'asc' | 'desc' | null;

export interface ColumnDef<T> {
  id: string;
  header: string;
  accessorKey?: keyof T;
  accessorFn?: (row: T) => unknown;
  cell?: (row: T) => React.ReactNode;
  sortable?: boolean;
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
  loadingMessage = 'Loading...',
  emptyIcon,
  emptyTitle = 'No data found',
  emptyDescription,
  rowActions,
  rowActionsPosition = 'end',
  className,
}: DataTableProps<T>) {
  const [sortColumn, setSortColumn] = useState<string | null>(null);
  const [sortDirection, setSortDirection] = useState<SortDirection>(null);

  // Handle sorting
  const handleSort = (columnId: string) => {
    if (sortColumn === columnId) {
      // Cycle: asc -> desc -> null
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

  // Sort data client-side
  const sortedData = useMemo(() => {
    if (!sortColumn || !sortDirection) {
      return data;
    }

    const column = columns.find((c) => c.id === sortColumn);
    if (!column) return data;

    return [...data].sort((a, b) => {
      let aValue: unknown;
      let bValue: unknown;

      if (column.accessorFn) {
        aValue = column.accessorFn(a);
        bValue = column.accessorFn(b);
      } else if (column.accessorKey) {
        aValue = a[column.accessorKey];
        bValue = b[column.accessorKey];
      } else {
        return 0;
      }

      // Handle null/undefined
      if (aValue == null && bValue == null) return 0;
      if (aValue == null) return sortDirection === 'asc' ? 1 : -1;
      if (bValue == null) return sortDirection === 'asc' ? -1 : 1;

      // Compare values
      if (typeof aValue === 'string' && typeof bValue === 'string') {
        const comparison = aValue.localeCompare(bValue);
        return sortDirection === 'asc' ? comparison : -comparison;
      }

      if (typeof aValue === 'number' && typeof bValue === 'number') {
        return sortDirection === 'asc' ? aValue - bValue : bValue - aValue;
      }

      // Date comparison
      if (aValue instanceof Date && bValue instanceof Date) {
        return sortDirection === 'asc'
          ? aValue.getTime() - bValue.getTime()
          : bValue.getTime() - aValue.getTime();
      }

      // Fallback string comparison
      const aStr = String(aValue);
      const bStr = String(bValue);
      const comparison = aStr.localeCompare(bStr);
      return sortDirection === 'asc' ? comparison : -comparison;
    });
  }, [data, columns, sortColumn, sortDirection]);

  // Selection handlers
  const allSelected = data.length > 0 && data.every((row) => selectedIds.has(getRowId(row)));
  const someSelected = data.some((row) => selectedIds.has(getRowId(row))) && !allSelected;

  const handleSelectAll = (checked: boolean) => {
    if (!onSelectionChange) return;
    
    if (checked) {
      const newSelected = new Set(selectedIds);
      data.forEach((row) => newSelected.add(getRowId(row)));
      onSelectionChange(newSelected);
    } else {
      const newSelected = new Set(selectedIds);
      data.forEach((row) => newSelected.delete(getRowId(row)));
      onSelectionChange(newSelected);
    }
  };

  const handleSelectRow = (rowId: string, checked: boolean) => {
    if (!onSelectionChange) return;
    
    const newSelected = new Set(selectedIds);
    if (checked) {
      newSelected.add(rowId);
    } else {
      newSelected.delete(rowId);
    }
    onSelectionChange(newSelected);
  };

  const getSortIcon = (columnId: string) => {
    if (sortColumn !== columnId) {
      return <ArrowUpDown className="w-3.5 h-3.5 text-[var(--color-text-tertiary)]" />;
    }
    if (sortDirection === 'asc') {
      return <ArrowUp className="w-3.5 h-3.5 text-[var(--color-brand-primary)]" />;
    }
    return <ArrowDown className="w-3.5 h-3.5 text-[var(--color-brand-primary)]" />;
  };

  // Calculate total columns for colSpan
  const totalColumns = columns.length 
    + (showCheckboxes ? 1 : 0) 
    + (rowIcon ? 1 : 0) 
    + (rowActions ? 1 : 0);

  // Render a column header
  const renderColumnHeader = (column: ColumnDef<T>, isFirstColumn: boolean) => (
    <th
      key={column.id}
      className={cn(
        "text-left py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]",
        isFirstColumn ? "pl-0 pr-4" : "px-4",
        column.sortable && "cursor-pointer select-none hover:text-[var(--color-text-primary)]",
        column.headerClassName
      )}
      onClick={column.sortable ? () => handleSort(column.id) : undefined}
    >
      <div className="flex items-center gap-1.5">
        <span>{column.header}</span>
        {column.sortable && getSortIcon(column.id)}
      </div>
    </th>
  );

  // Render a column cell
  const renderColumnCell = (column: ColumnDef<T>, row: T, isFirstColumn: boolean) => {
    let cellContent: React.ReactNode;
    
    if (column.cell) {
      cellContent = column.cell(row);
    } else if (column.accessorFn) {
      cellContent = String(column.accessorFn(row) ?? '');
    } else if (column.accessorKey) {
      cellContent = String(row[column.accessorKey] ?? '');
    } else {
      cellContent = '';
    }

    return (
      <td key={column.id} className={cn(isFirstColumn ? "pl-0 pr-4 py-3" : "px-4 py-3", column.className)}>
        {cellContent}
      </td>
    );
  };

  return (
    <div className={cn("overflow-x-auto", className)}>
      <table className="w-full">
        <thead>
          <tr className="border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]/50">
            {/* Checkbox column */}
            {showCheckboxes && (
              <th className="w-12 px-4 py-3">
                <Checkbox.Root
                  checked={allSelected ? true : someSelected ? 'indeterminate' : false}
                  onCheckedChange={(checked) => handleSelectAll(checked === true)}
                  className="w-4 h-4 rounded border border-[var(--color-border)] bg-[var(--color-surface)] flex items-center justify-center data-[state=checked]:bg-[var(--color-brand-primary)] data-[state=checked]:border-[var(--color-brand-primary)] data-[state=indeterminate]:bg-[var(--color-brand-primary)] data-[state=indeterminate]:border-[var(--color-brand-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:ring-offset-1"
                >
                  <Checkbox.Indicator>
                    {someSelected ? (
                      <div className="w-2 h-0.5 bg-white" />
                    ) : (
                      <Check className="w-3 h-3 text-white" />
                    )}
                  </Checkbox.Indicator>
                </Checkbox.Root>
              </th>
            )}
            {/* Icon column header (empty) */}
            {rowIcon && <th className="w-10 py-3" />}
            {/* First column */}
            {columns.length > 0 && renderColumnHeader(columns[0], true)}
            {/* Row actions after first column (if position is 'start') */}
            {rowActions && rowActionsPosition === 'start' && <th className="w-10 py-3" />}
            {/* Remaining columns */}
            {columns.slice(1).map((column) => renderColumnHeader(column, false))}
            {/* Row actions at end (if position is 'end') */}
            {rowActions && rowActionsPosition === 'end' && <th className="w-12 px-4 py-3" />}
          </tr>
        </thead>
        <tbody>
          {loading ? (
            <tr>
              <td colSpan={totalColumns} className="px-4 py-12 text-center">
                <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                <p className="text-sm text-[var(--color-text-secondary)]">{loadingMessage}</p>
              </td>
            </tr>
          ) : sortedData.length === 0 ? (
            <tr>
              <td colSpan={totalColumns} className="px-4 py-12 text-center">
                {emptyIcon && (
                  <div className="mb-3 flex justify-center text-[var(--color-text-tertiary)]">
                    {emptyIcon}
                  </div>
                )}
                <p className="text-[var(--color-text-primary)] font-medium mb-1">{emptyTitle}</p>
                {emptyDescription && (
                  <p className="text-sm text-[var(--color-text-secondary)]">{emptyDescription}</p>
                )}
              </td>
            </tr>
          ) : (
            sortedData.map((row) => {
              const rowId = getRowId(row);
              const isSelected = selectedIds.has(rowId);

              return (
                <tr
                  key={rowId}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                  className={cn(
                    "border-b border-[var(--color-border-light)] transition-colors hover:bg-[var(--color-surface-inset)]",
                    onRowClick && "cursor-pointer",
                    isSelected && "bg-[var(--color-brand-primary-light)]"
                  )}
                >
                  {/* Checkbox cell */}
                  {showCheckboxes && (
                    <td className="w-12 px-4 py-3">
                      <Checkbox.Root
                        checked={isSelected}
                        onClick={(event) => event.stopPropagation()}
                        onCheckedChange={(checked) => handleSelectRow(rowId, checked === true)}
                        className="w-4 h-4 rounded border border-[var(--color-border)] bg-[var(--color-surface)] flex items-center justify-center data-[state=checked]:bg-[var(--color-brand-primary)] data-[state=checked]:border-[var(--color-brand-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:ring-offset-1"
                      >
                        <Checkbox.Indicator>
                          <Check className="w-3 h-3 text-white" />
                        </Checkbox.Indicator>
                      </Checkbox.Root>
                    </td>
                  )}
                  {/* Icon cell */}
                  {rowIcon && (
                    <td className="w-10 py-3">
                      {rowIcon(row)}
                    </td>
                  )}
                  {/* First column */}
                  {columns.length > 0 && renderColumnCell(columns[0], row, true)}
                  {/* Row actions after first column (if position is 'start') */}
                  {rowActions && rowActionsPosition === 'start' && (
                    <td className="w-10 py-3" onClick={(event) => event.stopPropagation()}>
                      {rowActions(row)}
                    </td>
                  )}
                  {/* Remaining columns */}
                  {columns.slice(1).map((column) => renderColumnCell(column, row, false))}
                  {/* Row actions at end (if position is 'end') */}
                  {rowActions && rowActionsPosition === 'end' && (
                    <td className="w-12 px-4 py-3" onClick={(event) => event.stopPropagation()}>
                      {rowActions(row)}
                    </td>
                  )}
                </tr>
              );
            })
          )}
        </tbody>
      </table>
    </div>
  );
}
