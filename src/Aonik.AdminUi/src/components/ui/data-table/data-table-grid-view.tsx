import * as Checkbox from '@radix-ui/react-checkbox';
import { Check, RefreshCw } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface DataTableGridViewProps<T> {
  data: T[];
  getRowId: (row: T) => string;
  renderCard: (row: T) => React.ReactNode;
  selectedIds?: Set<string>;
  onSelectionChange?: (selectedIds: Set<string>) => void;
  showCheckboxes?: boolean;
  loading?: boolean;
  loadingMessage?: string;
  emptyIcon?: React.ReactNode;
  emptyTitle?: string;
  emptyDescription?: string;
  columns?: number;
  className?: string;
}

export function DataTableGridView<T>({
  data,
  getRowId,
  renderCard,
  selectedIds = new Set(),
  onSelectionChange,
  showCheckboxes = true,
  loading = false,
  loadingMessage = 'Loading...',
  emptyIcon,
  emptyTitle = 'No data found',
  emptyDescription,
  columns = 3,
  className,
}: DataTableGridViewProps<T>) {
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

  if (loading) {
    return (
      <div className={cn("px-4 py-12 text-center", className)}>
        <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
        <p className="text-sm text-[var(--color-text-secondary)]">{loadingMessage}</p>
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <div className={cn("px-4 py-12 text-center", className)}>
        {emptyIcon && <div className="mx-auto mb-3 text-[var(--color-text-tertiary)]">{emptyIcon}</div>}
        <p className="text-[var(--color-text-primary)] font-medium mb-1">{emptyTitle}</p>
        {emptyDescription && (
          <p className="text-sm text-[var(--color-text-secondary)]">{emptyDescription}</p>
        )}
      </div>
    );
  }

  return (
    <div
      className={cn(
        "grid gap-4 p-4",
        columns === 2 && "grid-cols-1 sm:grid-cols-2",
        columns === 3 && "grid-cols-1 sm:grid-cols-2 lg:grid-cols-3",
        columns === 4 && "grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4",
        className
      )}
    >
      {data.map((row) => {
        const rowId = getRowId(row);
        const isSelected = selectedIds.has(rowId);

        return (
          <div
            key={rowId}
            className={cn(
              "relative rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4 transition-all hover:border-[var(--color-border)] hover:shadow-sm",
              isSelected && "border-[var(--color-brand-primary)] bg-[var(--color-brand-primary-light)]"
            )}
          >
            {showCheckboxes && onSelectionChange && (
              <div className="absolute top-3 left-3">
                <Checkbox.Root
                  checked={isSelected}
                  onCheckedChange={(checked) => handleSelectRow(rowId, checked === true)}
                  className="w-4 h-4 rounded border border-[var(--color-border)] bg-[var(--color-surface)] flex items-center justify-center data-[state=checked]:bg-[var(--color-brand-primary)] data-[state=checked]:border-[var(--color-brand-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:ring-offset-1"
                >
                  <Checkbox.Indicator>
                    <Check className="w-3 h-3 text-white" />
                  </Checkbox.Indicator>
                </Checkbox.Root>
              </div>
            )}
            <div className={showCheckboxes ? "pl-6" : ""}>
              {renderCard(row)}
            </div>
          </div>
        );
      })}
    </div>
  );
}
